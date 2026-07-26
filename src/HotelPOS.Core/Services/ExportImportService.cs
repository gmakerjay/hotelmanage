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

    public ExportImportService(ICustomerService customerService, IRoomService roomService, IAuditService auditService)
    {
        _customerService = customerService;
        _roomService = roomService;
        _auditService = auditService;
    }

    public async Task ExportCustomersToCsvAsync(string filePath)
    {
        var customers = await _customerService.GetCustomersAsync(null);
        var sb = new StringBuilder();
        sb.AppendLine("ID,FullName,Phone,Email,IdCardOrPassport,Address,Notes");

        foreach (var c in customers)
        {
            sb.AppendLine($"{c.Id},\"{Escape(c.FullName)}\",\"{Escape(c.Phone)}\",\"{Escape(c.Email)}\",\"{Escape(c.IdCardOrPassport)}\",\"{Escape(c.Address)}\",\"{Escape(c.Notes)}\"");
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
                    FullName = parts[1].Trim(),
                    Phone = parts.Count > 2 ? parts[2].Trim() : null,
                    Email = parts.Count > 3 ? parts[3].Trim() : null,
                    IdCardOrPassport = parts.Count > 4 ? parts[4].Trim() : null,
                    Address = parts.Count > 5 ? parts[5].Trim() : null,
                    Notes = parts.Count > 6 ? parts[6].Trim() : null
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
            sb.AppendLine($"{r.Id},\"{Escape(r.RoomNumber)}\",\"{Escape(r.Floor)}\",\"{Escape(typeName)}\",{(int)r.Status},\"{Escape(r.Notes)}\"");
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
                var roomNum = parts[1].Trim();
                var floor = parts.Count > 2 ? parts[2].Trim() : null;
                var typeName = parts.Count > 3 ? parts[3].Trim() : null;

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
