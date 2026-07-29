using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HotelPOS.Common.Models;

namespace HotelPOS.Core.Services;

public class ExportImportService : IExportImportService
{
    private readonly ICustomerService _customerService;
    private readonly IRoomService _roomService;
    private readonly IAuditService _auditService;
    private readonly IPOSService? _posService;

    public ExportImportService(ICustomerService customerService, IRoomService roomService, IAuditService auditService, IPOSService? posService = null)
    {
        _customerService = customerService;
        _roomService = roomService;
        _auditService = auditService;
        _posService = posService;
    }

    public async Task ExportCustomersToCsvAsync(string filePath)
    {
        var customers = await _customerService.GetCustomersAsync(null);
        var sb = new StringBuilder();
        sb.AppendLine("ID,FullName,Phone,Email,IdCardOrPassport,Address,Notes");

        foreach (var c in customers)
        {
            sb.AppendLine($"{c.Id},\"{Escape(c.FullName)}\",\"=\"\"{Escape(c.Phone)}\"\"\",\"{Escape(c.Email)}\",\"=\"\"{Escape(c.IdCardOrPassport)}\"\"\",\"{Escape(c.Address)}\",\"{Escape(c.Notes)}\"");
        }

        await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8);
        await _auditService.LogAsync("ส่งออกข้อมูลลูกค้า (Export CSV)", "Customer", filePath, $"จำนวน {customers.Count()} รายการ");
    }

    public async Task<int> ImportCustomersFromCsvAsync(string filePath)
    {
        if (!File.Exists(filePath)) throw new FileNotFoundException("ไม่พบไฟล์ CSV", filePath);

        var lines = await File.ReadAllLinesAsync(filePath, Encoding.UTF8);
        if (lines.Length <= 1) return 0;

        int importedCount = 0;
        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;

            var parts = ParseCsvLine(line);
            if (parts.Count >= 2 && !string.IsNullOrWhiteSpace(parts[1]))
            {
                var customer = new Customer
                {
                    FullName = CleanValue(parts[1]),
                    Phone = parts.Count > 2 ? CleanValue(parts[2]) : null,
                    Email = parts.Count > 3 ? CleanValue(parts[3]) : null,
                    IdCardOrPassport = parts.Count > 4 ? CleanValue(parts[4]) : null,
                    Address = parts.Count > 5 ? CleanValue(parts[5]) : null,
                    Notes = parts.Count > 6 ? CleanValue(parts[6]) : null
                };

                await _customerService.SaveCustomerAsync(customer);
                importedCount++;
            }
        }

        await _auditService.LogAsync("นำเข้าข้อมูลลูกค้า (Import CSV)", "Customer", filePath, $"นำเข้าสำเร็จ {importedCount} รายการ");
        return importedCount;
    }

    public async Task ExportRoomsToCsvAsync(string filePath)
    {
        var rooms = await _roomService.GetRoomsAsync();
        var types = await _roomService.GetRoomTypesAsync();
        var sb = new StringBuilder();
        sb.AppendLine("ID,RoomNumber,Floor,RoomTypeName,Status,Notes");

        foreach (var r in rooms)
        {
            var typeName = types.FirstOrDefault(t => t.Id == r.RoomTypeId)?.Name ?? "";
            sb.AppendLine($"{r.Id},\"=\"\"{Escape(r.RoomNumber)}\"\"\",\"=\"\"{Escape(r.Floor)}\"\"\",\"{Escape(typeName)}\",{(int)r.Status},\"{Escape(r.Notes)}\"");
        }

        await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8);
        await _auditService.LogAsync("ส่งออกข้อมูลห้องพัก (Export CSV)", "Room", filePath, $"จำนวน {rooms.Count()} รายการ");
    }

    public async Task<int> ImportRoomsFromCsvAsync(string filePath)
    {
        if (!File.Exists(filePath)) throw new FileNotFoundException("ไม่พบไฟล์ CSV", filePath);

        var lines = await File.ReadAllLinesAsync(filePath, Encoding.UTF8);
        if (lines.Length <= 1) return 0;

        var types = (await _roomService.GetRoomTypesAsync()).ToList();
        if (!types.Any()) throw new InvalidOperationException("ต้องสร้างประเภทห้องพักอย่างน้อย 1 ประเภทในระบบก่อนนำเข้าห้องพัก");

        int defaultTypeId = types.First().Id;
        int importedCount = 0;

        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;

            var parts = ParseCsvLine(line);
            if (parts.Count >= 2 && !string.IsNullOrWhiteSpace(parts[1]))
            {
                var roomNum = CleanValue(parts[1]);
                var floor = parts.Count > 2 ? CleanValue(parts[2]) : null;
                var typeName = parts.Count > 3 ? CleanValue(parts[3]) : null;

                int typeId = defaultTypeId;
                if (!string.IsNullOrWhiteSpace(typeName))
                {
                    var matchedType = types.FirstOrDefault(t => t.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase));
                    if (matchedType != null) typeId = matchedType.Id;
                }

                var room = new Room
                {
                    RoomNumber = roomNum,
                    Floor = floor,
                    RoomTypeId = typeId,
                    IsActive = true
                };

                try
                {
                    await _roomService.SaveRoomAsync(room);
                    importedCount++;
                }
                catch (Exception)
                {
                    // ข้ามกรณีเลขห้องซ้ำ
                }
            }
        }

        await _auditService.LogAsync("นำเข้าข้อมูลห้องพัก (Import CSV)", "Room", filePath, $"นำเข้าสำเร็จ {importedCount} รายการ");
        return importedCount;
    }

    public async Task ExportProductsToCsvAsync(string filePath)
    {
        if (_posService == null) throw new InvalidOperationException("POSService ไม่พร้อมใช้งาน");

        var products = await _posService.GetProductsAsync();
        var categories = await _posService.GetCategoriesAsync();
        var sb = new StringBuilder();
        sb.AppendLine("ID,Name,Category,SKU,Price,Cost,StockQty,TrackStock");

        foreach (var p in products)
        {
            var catName = categories.FirstOrDefault(c => c.Id == p.CategoryId)?.Name ?? "";
            sb.AppendLine($"{p.Id},\"{Escape(p.Name)}\",\"{Escape(catName)}\",\"=\"\"{Escape(p.Sku)}\"\"\",{p.Price},{p.Cost},{p.StockQty},{(p.TrackStock ? 1 : 0)}");
        }

        await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8);
        await _auditService.LogAsync("ส่งออกข้อมูลสินค้า/สต็อก (Export CSV)", "Product", filePath, $"จำนวน {products.Count()} รายการ");
    }

    public async Task<int> ImportProductsFromCsvAsync(string filePath)
    {
        if (!File.Exists(filePath)) throw new FileNotFoundException("ไม่พบไฟล์ CSV", filePath);
        if (_posService == null) throw new InvalidOperationException("POSService ไม่พร้อมใช้งาน");

        var lines = await File.ReadAllLinesAsync(filePath, Encoding.UTF8);
        if (lines.Length <= 1) return 0;

        var categories = (await _posService.GetCategoriesAsync()).ToList();
        var existingProducts = (await _posService.GetProductsAsync()).ToList();
        int importedCount = 0;

        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;

            var parts = ParseCsvLine(line);
            if (parts.Count >= 2 && !string.IsNullOrWhiteSpace(parts[1]))
            {
                var prodName = CleanValue(parts[1]);
                var catName = parts.Count > 2 ? CleanValue(parts[2]) : "ทั่วไป";
                var sku = parts.Count > 3 ? CleanValue(parts[3]) : null;
                
                decimal.TryParse(parts.Count > 4 ? parts[4] : "0", out var price);
                decimal.TryParse(parts.Count > 5 ? parts[5] : "0", out var cost);
                int.TryParse(parts.Count > 6 ? parts[6] : "0", out var stockQty);
                bool trackStock = (parts.Count > 7 && (parts[7] == "1" || parts[7].Equals("true", StringComparison.OrdinalIgnoreCase)));

                // Find or create Category
                int catId;
                var matchedCat = categories.FirstOrDefault(c => c.Name.Equals(catName, StringComparison.OrdinalIgnoreCase));
                if (matchedCat != null)
                {
                    catId = matchedCat.Id;
                }
                else
                {
                    var newCat = new ProductCategory { Name = string.IsNullOrWhiteSpace(catName) ? "ทั่วไป" : catName, IsActive = true };
                    catId = await _posService.SaveCategoryAsync(newCat);
                    newCat.Id = catId;
                    categories.Add(newCat);
                }

                // Find existing product by SKU or Name
                var existingProd = existingProducts.FirstOrDefault(p => 
                    (!string.IsNullOrEmpty(sku) && p.Sku == sku) || 
                    p.Name.Equals(prodName, StringComparison.OrdinalIgnoreCase));

                var prod = existingProd ?? new Product();
                prod.Name = prodName;
                prod.CategoryId = catId;
                prod.Sku = string.IsNullOrWhiteSpace(sku) ? null : sku;
                prod.Price = price;
                prod.Cost = cost;
                prod.StockQty = stockQty;
                prod.TrackStock = trackStock;
                prod.IsActive = true;

                await _posService.SaveProductAsync(prod);
                importedCount++;
            }
        }

        await _auditService.LogAsync("นำเข้าข้อมูลสินค้า/สต็อก (Import CSV)", "Product", filePath, $"นำเข้าสำเร็จ {importedCount} รายการ");
        return importedCount;
    }

    private static string CleanValue(string val)
    {
        if (string.IsNullOrEmpty(val)) return "";
        val = val.Trim();

        // Strip Excel formula format: ="value"
        if (val.StartsWith("=") && val.Length > 1)
        {
            val = val.Substring(1);
        }

        // Strip wrapping double quotes
        if (val.StartsWith("\"") && val.EndsWith("\"") && val.Length >= 2)
        {
            val = val.Substring(1, val.Length - 2);
        }

        return val.Replace("\"\"", "\"").Trim();
    }

    private static string Escape(string? input)
    {
        if (string.IsNullOrEmpty(input)) return "";
        return input.Replace("\"", "\"\"");
    }

    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        bool inQuotes = false;
        var current = new StringBuilder();

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }
        result.Add(current.ToString());
        return result;
    }
}
