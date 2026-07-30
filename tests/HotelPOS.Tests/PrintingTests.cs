using System;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using HotelPOS.Common;
using HotelPOS.Common.Models;
using HotelPOS.Printing;
using Xunit;

namespace HotelPOS.Tests;

public class PrintingTests
{
    [Fact]
    public void LoadImageSafe_ไฟล์ไม่มีอยู่จริง_คืนค่า_null_ไม่เกิด_Exception()
    {
        var img = PrintLayoutHelper.LoadImageSafe(@"C:\non_existent_image_path_9999.png");
        Assert.Null(img);
    }

    [Fact]
    public void LoadImageSafe_กรณีเป็นพาธว่าง_คืนค่า_null()
    {
        Assert.Null(PrintLayoutHelper.LoadImageSafe(null));
        Assert.Null(PrintLayoutHelper.LoadImageSafe(""));
    }

    [Fact]
    public void ReceiptInvoicePrinter_สร้างและจำลองการวาด_PrintPage_A4_ไม่เกิด_Exception()
    {
        var booking = new Booking
        {
            Id = 1,
            BookingCode = "REC-000001",
            RoomId = 101,
            AgreedRate = 1200m,
            RatePlan = RatePlanType.Daily,
            CheckInPlanned = DateTime.Now.AddDays(-1),
            CheckInActual = DateTime.Now.AddDays(-1),
            CheckOutActual = DateTime.Now
        };
        var room = new Room { RoomNumber = "101", Floor = "1" };
        var customer = new Customer { FullName = "คุณทดสอบ การพิมพ์", Phone = "081-111-2222" };
        var settings = new SystemSettingsDto
        {
            ShopName = "PSOFT REST & RENT",
            ShopAddress = "123/45 Chonburi",
            ShopPhone = "038-123-456",
            PaperType = "A4",
            EnableVat = true,
            VatRate = 7.0m,
            LobbyTerms = "ข้อตกลงบรรทัดที่ 1\nข้อตกลงบรรทัดที่ 2"
        };

        var printer = new ReceiptInvoicePrinter(booking, room, customer, null, settings, "พนักงานต้อนรับ");

        using var bmp = new Bitmap(827, 1169);
        using var g = Graphics.FromImage(bmp);

        var marginBounds = new Rectangle(40, 40, 747, 1089);
        var pageBounds = new Rectangle(0, 0, 827, 1169);
        var args = new PrintPageEventArgs(g, marginBounds, pageBounds, new PageSettings());

        var printMethod = typeof(ReceiptInvoicePrinter).GetMethod("PrintDoc_PrintPage",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        Assert.NotNull(printMethod);
        printMethod.Invoke(printer, new object[] { printer, args });
    }

    [Fact]
    public void UtilityInvoicePrinter_สร้างและจำลองการวาด_PrintPage_80mm_ไม่เกิด_Exception()
    {
        var bill = new UtilityBill
        {
            Id = 50,
            BillCode = "INV-202607-101",
            RoomNumber = "101",
            BillingMonth = "2026-07",
            RoomCharge = 3500m,
            ElectricUnits = 100,
            ElectricAmount = 800m,
            WaterAmount = 150m,
            TotalAmount = 4450m,
            IsPaid = false
        };
        var customer = new Customer { FullName = "คุณผู้เช่า รายเดือน", Phone = "089-999-0000" };
        var settings = new SystemSettingsDto
        {
            ShopName = "PSOFT APARTMENT",
            PaperType = "80mm",
            PrinterFeedLines = 4
        };

        var printer = new UtilityInvoicePrinter(bill, customer, settings, "ผู้จัดการ");

        using var bmp = new Bitmap(283, 600);
        using var g = Graphics.FromImage(bmp);

        var marginBounds = new Rectangle(10, 10, 263, 580);
        var pageBounds = new Rectangle(0, 0, 283, 600);
        var args = new PrintPageEventArgs(g, marginBounds, pageBounds, new PageSettings());

        var printMethod = typeof(UtilityInvoicePrinter).GetMethod("PrintDoc_PrintPage",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        Assert.NotNull(printMethod);
        printMethod.Invoke(printer, new object[] { printer, args });
    }
}
