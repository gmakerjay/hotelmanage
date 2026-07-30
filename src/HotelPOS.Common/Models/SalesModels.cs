namespace HotelPOS.Common.Models;

public class ProductCategory
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;   // เช่น "มินิบาร์", "อาหาร-เครื่องดื่ม", "ซักรีด"
    public bool IsActive { get; set; } = true;
}

public class Product
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public decimal Price { get; set; }
    public decimal Cost { get; set; }
    public int StockQty { get; set; }
    public bool TrackStock { get; set; } = false;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

/// <summary>รายการขาย (อาจผูกกับ Folio ห้องพัก หรือขายหน้าร้านเดี่ยวๆ ก็ได้)</summary>
public class Sale
{
    public int Id { get; set; }
    public string SaleCode { get; set; } = string.Empty;  // เลขที่บิลขาย
    public int? FolioId { get; set; }                      // null = ขายหน้าร้านเดี่ยวๆ ไม่ผูกห้อง
    public int? CustomerId { get; set; }
    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public int? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public bool IsDeleted { get; set; } = false;

    // Extended properties for UI joined display
    public string? RoomNumber { get; set; }
    public string? CustomerName { get; set; }
}

public class SaleItem
{
    public int Id { get; set; }
    public int SaleId { get; set; }
    public int ProductId { get; set; }
    public string ProductNameSnapshot { get; set; } = string.Empty; // เก็บชื่อ ณ ตอนขาย เผื่อสินค้าถูกแก้ไขทีหลัง
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal { get; set; }
}

public class Payment
{
    public int Id { get; set; }
    public int SaleId { get; set; }
    public PaymentMethod Method { get; set; }
    public decimal Amount { get; set; }
    public string? ReferenceNo { get; set; }   // เลขอ้างอิงการโอน/สลิป
    public DateTime PaidAt { get; set; } = DateTime.Now;
    public int? ReceivedBy { get; set; }
}

/// <summary>เอกสารที่พิมพ์ออกจริง เก็บเลขที่เอกสารรันต่อเนื่อง</summary>
public class InvoiceDocument
{
    public int Id { get; set; }
    public int SaleId { get; set; }
    public DocumentType DocType { get; set; }
    public string DocumentNumber { get; set; } = string.Empty;
    public PaperSize PrintedPaperSize { get; set; }
    public DateTime PrintedAt { get; set; } = DateTime.Now;
    public int? PrintedBy { get; set; }
    public int PrintCount { get; set; } = 1;    // นับจำนวนครั้งที่พิมพ์ซ้ำ (ใบแทน)
}
