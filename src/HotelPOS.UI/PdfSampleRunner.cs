using System;
using System.IO;
using System.Drawing;
using System.Drawing.Printing;
using HotelPOS.Common;
using HotelPOS.Common.Models;
using HotelPOS.Printing;

namespace HotelPOS.UI;

public class PdfGenerator
{
    public static void GenerateSamplePdfs()
    {
        string outputDir = @"C:\Users\admin\Desktop";
        Directory.CreateDirectory(outputDir);

        string pngReceiptPath = Path.Combine(outputDir, "Sample_Receipt_Invoice.png");
        string pngUtilityPath = Path.Combine(outputDir, "Sample_Monthly_Utility_Invoice.png");

        var settings = new SystemSettingsDto
        {
            ShopName = "โรงแรม PSOFT HOTEL & RESORT",
            ShopAddress = "88/9 หมู่ 5 ถนนสุขุมวิท ตำบลแสนสุข อำเภอเมือง ชลบุรี 20130",
            ShopPhone = "038-765-4321 / 081-234-5678",
            ShopTaxId = "0105565012345",
            BillHeader = "ยินดีต้อนรับสู่ PSOFT HOTEL",
            BillFooter = "ขอบคุณที่เลือกใช้บริการ พักผ่อนให้สบาย / Thank you for staying with us",
            PaperType = "A4",
            EnableVat = true,
            VatRate = 7.00m,
            ShowSignatureBox = true,
            LobbyTerms = "1. ห้ามส่งเสียงดัง หรือเปิดเพลงรบกวนห้องข้างเคียงหลังเวลา 22:00 น.\n2. เงินประกันความเสียหาย 500 บาท จะคืนให้เต็มจำนวนในวันเช็คเอาท์เมื่อส่งคืนกุญแจห้องพัก\n3. ค่าน้ำ-ค่าไฟ กำหนดชำระไม่เกินวันที่ 5 ของทุกเดือน หากเกินกำหนดมีค่าปรับวันละ 50 บาท"
        };

        // 1. Generate Sample Checkout Receipt
        var sampleBooking = new Booking
        {
            Id = 1024,
            BookingCode = "RC-202607-0042",
            RoomId = 101,
            CustomerId = 1,
            RatePlan = RatePlanType.Daily,
            AgreedRate = 1200m,
            CheckInPlanned = DateTime.Now.AddDays(-2),
            CheckInActual = DateTime.Now.AddDays(-2),
            CheckOutActual = DateTime.Now,
            Status = BookingStatus.CheckedOut
        };

        var sampleRoom = new Room { RoomNumber = "101", Floor = "1" };
        var sampleCustomer = new Customer { FullName = "คุณสมชาย ใจดี", Phone = "089-876-5432", IdCardOrPassport = "1-1002-34567-89-0" };
        var sampleFolio = new Folio { RoomCharges = 2400m, ExtraCharges = 350m, DiscountAmount = 150m, TotalAmount = 2600m };

        var receiptPrinter = new ReceiptInvoicePrinter(sampleBooking, sampleRoom, sampleCustomer, sampleFolio, settings, "คุณวรรณา (พนักงานต้อนรับ)");
        RenderPrinterToImage(receiptPrinter, pngReceiptPath);

        // 2. Generate Sample Monthly Utility Invoice
        var sampleUtilityBill = new UtilityBill
        {
            Id = 501,
            BillCode = "INV-202607-101",
            RoomId = 101,
            RoomNumber = "101",
            BillingMonth = "2026-07",
            RoomCharge = 4500m,
            ElectricPrev = 1240,
            ElectricCurr = 1390,
            ElectricUnits = 150,
            ElectricRate = 8.00m,
            ElectricAmount = 1200m,
            WaterBillingMode = "METER",
            WaterPrev = 310,
            WaterCurr = 325,
            WaterUnits = 15,
            WaterRate = 18.00m,
            WaterAmount = 270m,
            WaterPersonCount = 1,
            CommonAreaFee = 300m,
            GarbageFee = 50m,
            ExtraCharges = 0m,
            DiscountAmount = 0m,
            TotalAmount = 6320m,
            IsPaid = false,
            Notes = "บิลรวมประจำเดือน กรกฎาคม 2569"
        };

        var utilityPrinter = new UtilityInvoicePrinter(sampleUtilityBill, sampleCustomer, settings, "คุณวรรณา (ผู้จัดการ)");
        RenderPrinterToImage(utilityPrinter, pngUtilityPath);

        Console.WriteLine($"Generated Sample Images successfully:\n1. {pngReceiptPath}\n2. {pngUtilityPath}");
    }

    private static void RenderPrinterToImage(object printerObj, string outputImagePath)
    {
        // High-res A4 dimensions at 150 DPI: 1240 x 1754 pixels
        int width = 1240;
        int height = 1754;

        using var bmp = new Bitmap(width, height);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.White);
            g.ScaleTransform(1.5f, 1.5f); // Scale 100 DPI to 150 DPI

            var marginBounds = new Rectangle(40, 40, 747, 1089);
            var pageBounds = new Rectangle(0, 0, 827, 1169);
            var printPageEventArgs = new PrintPageEventArgs(g, marginBounds, pageBounds, new PageSettings());

            var method = printerObj.GetType().GetMethod("PrintDoc_PrintPage",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            method?.Invoke(printerObj, new object[] { printerObj, printPageEventArgs });
        }

        bmp.Save(outputImagePath, System.Drawing.Imaging.ImageFormat.Png);
    }
}
