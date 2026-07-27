using System.Drawing.Printing;
using HotelPOS.Common;
using HotelPOS.Common.Models;

namespace HotelPOS.Printing;

/// <summary>
/// พิมพ์ใบเสร็จรับเงิน / ใบแจ้งหนี้ (A4 และ 80mm)
/// ออกแบบ layout จัดหน้ากระดาษ A4 สมส่วน ไม่กระจุก ไม่ทับกัน และมีช่องข้อตกลงหน้าล็อบบี้
/// </summary>
public class ReceiptInvoicePrinter
{
    private readonly Booking _booking;
    private readonly Room _room;
    private readonly Customer? _customer;
    private readonly Folio? _folio;
    private readonly SystemSettingsDto? _settings;
    private readonly string _staffName;

    private readonly string _shopName;
    private readonly string _shopAddress;
    private readonly string _shopPhone;
    private readonly string _shopTaxId;

    public ReceiptInvoicePrinter(
        Booking booking,
        Room room,
        Customer? customer,
        Folio? folio,
        SystemSettingsDto? settings = null,
        string staffName = "พนักงานหน้าเคาน์เตอร์")
    {
        _booking = booking;
        _room = room;
        _customer = customer;
        _folio = folio;
        _settings = settings;
        _staffName = staffName;

        _shopName = string.IsNullOrWhiteSpace(settings?.ShopName) ? "โรงแรม HotelPOS TH" : settings.ShopName;
        _shopAddress = string.IsNullOrWhiteSpace(settings?.ShopAddress) ? "-" : settings.ShopAddress;
        _shopPhone = string.IsNullOrWhiteSpace(settings?.ShopPhone) ? "-" : settings.ShopPhone;
        _shopTaxId = string.IsNullOrWhiteSpace(settings?.ShopTaxId) ? "-" : settings.ShopTaxId;
    }

    public ReceiptInvoicePrinter(
        string shopName,
        string shopAddress,
        string shopPhone,
        string shopTaxId,
        Booking booking,
        Room room,
        Customer? customer,
        Folio? folio,
        string staffName = "พนักงานหน้าเคาน์เตอร์",
        SystemSettingsDto? settings = null)
        : this(booking, room, customer, folio, settings ?? new SystemSettingsDto
        {
            ShopName = shopName,
            ShopAddress = shopAddress,
            ShopPhone = shopPhone,
            ShopTaxId = shopTaxId
        }, staffName)
    {
    }

    public void Print(string printerName = "")
    {
        using var printDoc = new PrintDocument();

        if (!string.IsNullOrWhiteSpace(printerName))
        {
            printDoc.PrinterSettings.PrinterName = printerName;
        }

        if (_settings?.PaperType == "A4")
        {
            printDoc.DefaultPageSettings.PaperSize = new System.Drawing.Printing.PaperSize("A4", 827, 1169); // 8.27 x 11.69 inches
            printDoc.DefaultPageSettings.Margins = new Margins(40, 40, 40, 40);
        }

        printDoc.PrintPage += PrintDoc_PrintPage;
        printDoc.Print();
    }

    public void ShowPrintPreview()
    {
        using var printDoc = new PrintDocument();
        printDoc.DefaultPageSettings.PaperSize = new System.Drawing.Printing.PaperSize("A4", 827, 1169);
        printDoc.DefaultPageSettings.Margins = new Margins(40, 40, 40, 40);

        printDoc.PrintPage += PrintDoc_PrintPage;

        using var previewDlg = new PrintPreviewDialog
        {
            Document = printDoc,
            Width = 960,
            Height = 720,
            StartPosition = FormStartPosition.CenterScreen,
            Text = "ตัวอย่างก่อนพิมพ์ - ใบเสร็จรับเงิน (A4)"
        };
        previewDlg.ShowDialog();
    }

    private void PrintDoc_PrintPage(object sender, PrintPageEventArgs e)
    {
        var g = e.Graphics;
        if (g == null) return;

        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        // Fonts
        var fontTitle = new Font("Segoe UI", 18F, FontStyle.Bold);
        var fontSubtitle = new Font("Segoe UI", 12F, FontStyle.Bold);
        var fontHeader = new Font("Segoe UI", 10.5F, FontStyle.Bold);
        var fontBody = new Font("Segoe UI", 10F, FontStyle.Regular);
        var fontBodyBold = new Font("Segoe UI", 10F, FontStyle.Bold);
        var fontSmall = new Font("Segoe UI", 9F, FontStyle.Regular);

        // Pens & Brushes
        var penDark = new Pen(Color.FromArgb(30, 41, 59), 1.5F);
        var penLight = new Pen(Color.FromArgb(203, 213, 225), 1F);
        var brushText = Brushes.Black;
        var brushDark = new SolidBrush(Color.FromArgb(15, 23, 42));
        var brushHeaderBg = new SolidBrush(Color.FromArgb(241, 245, 249));

        float leftMargin = e.MarginBounds.Left;
        float rightMargin = e.MarginBounds.Right;
        float contentWidth = rightMargin - leftMargin;
        float currentY = e.MarginBounds.Top;

        // 1. Logo & Header
        float logoOffsetX = leftMargin;
        if (!string.IsNullOrEmpty(_settings?.LogoImagePath) && File.Exists(_settings.LogoImagePath))
        {
            try
            {
                using var logoImg = Image.FromFile(_settings.LogoImagePath);
                float maxW = 140f;
                float maxH = 55f;
                float scale = Math.Min(maxW / logoImg.Width, maxH / logoImg.Height);
                float drawW = logoImg.Width * scale;
                float drawH = logoImg.Height * scale;

                g.DrawImage(logoImg, leftMargin, currentY, drawW, drawH);
                logoOffsetX += drawW + 15;
            }
            catch { }
        }

        g.DrawString(_shopName, fontTitle, brushDark, logoOffsetX, currentY);
        currentY += 32;

        g.DrawString($"ที่อยู่: {_shopAddress} | โทรศัพท์: {_shopPhone} | เลขประจำตัวผู้เสียภาษี: {_shopTaxId}", fontSmall, Brushes.DimGray, logoOffsetX, currentY);
        currentY += 28;

        g.DrawLine(penDark, leftMargin, currentY, rightMargin, currentY);
        currentY += 15;

        // 2. Document Banner Box
        var rectHeaderBox = new RectangleF(leftMargin, currentY, contentWidth, 40);
        g.FillRectangle(brushHeaderBg, rectHeaderBox);
        g.DrawRectangle(penLight, rectHeaderBox.X, rectHeaderBox.Y, rectHeaderBox.Width, rectHeaderBox.Height);

        g.DrawString("ใบเสร็จรับเงิน / ใบแจ้งหนี้ (RECEIPT / INVOICE)", fontSubtitle, brushDark, leftMargin + 12, currentY + 8);

        string receiptNo = string.IsNullOrWhiteSpace(_booking.BookingCode) ? $"REC-{_booking.Id:D6}" : _booking.BookingCode;
        g.DrawString($"เลขที่ใบเสร็จ: {receiptNo}", fontBodyBold, brushDark, rightMargin - 220, currentY + 10);
        currentY += 55;

        // 3. Info Table (Guest Info & Stay Info) - Balanced 120px height
        float halfWidth = (contentWidth - 15) / 2;

        // Guest Info Box
        var guestRect = new RectangleF(leftMargin, currentY, halfWidth, 120);
        g.DrawRectangle(penLight, guestRect.X, guestRect.Y, guestRect.Width, guestRect.Height);
        g.FillRectangle(brushHeaderBg, leftMargin, currentY, halfWidth, 26);
        g.DrawRectangle(penLight, leftMargin, currentY, halfWidth, 26);
        g.DrawString("ข้อมูลผู้เข้าพัก / Guest Details", fontHeader, brushDark, leftMargin + 8, currentY + 4);

        float guestY = currentY + 34;
        g.DrawString($"ชื่อ-นามสกุล: {(_customer != null ? _customer.FullName : "ผู้เข้าพักทั่วไป")}", fontBody, brushText, leftMargin + 8, guestY);
        guestY += 25;
        g.DrawString($"เบอร์โทรศัพท์: {(_customer?.Phone ?? "-")}", fontBody, brushText, leftMargin + 8, guestY);
        guestY += 25;
        g.DrawString($"เลขบัตร/พาสปอร์ต: {(_customer?.IdCardOrPassport ?? "-")}", fontBody, brushText, leftMargin + 8, guestY);

        // Stay Info Box
        float stayX = leftMargin + halfWidth + 15;
        var stayRect = new RectangleF(stayX, currentY, halfWidth, 120);
        g.DrawRectangle(penLight, stayRect.X, stayRect.Y, stayRect.Width, stayRect.Height);
        g.FillRectangle(brushHeaderBg, stayX, currentY, halfWidth, 26);
        g.DrawRectangle(penLight, stayX, currentY, halfWidth, 26);
        g.DrawString("ข้อมูลการเข้าพัก / Stay Details", fontHeader, brushDark, stayX + 8, currentY + 4);

        float stayY = currentY + 34;
        g.DrawString($"ห้องพัก: {_room.RoomNumber} (ชั้น {_room.Floor ?? "-"})", fontBodyBold, brushText, stayX + 8, stayY);
        stayY += 25;

        var checkInTime = _booking.CheckInActual ?? _booking.CheckInPlanned;
        var checkOutTime = _booking.CheckOutActual ?? DateTime.Now;
        g.DrawString($"เช็คอิน: {checkInTime:dd/MM/yyyy HH:mm} น.", fontBody, brushText, stayX + 8, stayY);
        stayY += 25;
        g.DrawString($"เช็คเอาท์: {checkOutTime:dd/MM/yyyy HH:mm} น.", fontBody, brushText, stayX + 8, stayY);

        currentY += 140;

        // 4. Financial Breakdown Table
        float col1X = leftMargin;
        float col2X = leftMargin + 340;
        float col3X = rightMargin - 140;

        // Table Header
        var tableHeaderRect = new RectangleF(leftMargin, currentY, contentWidth, 30);
        g.FillRectangle(brushHeaderBg, tableHeaderRect);
        g.DrawRectangle(penLight, tableHeaderRect.X, tableHeaderRect.Y, tableHeaderRect.Width, tableHeaderRect.Height);

        g.DrawString("รายการ (Description)", fontHeader, brushDark, col1X + 8, currentY + 5);
        g.DrawString("ประเภทราคา", fontHeader, brushDark, col2X, currentY + 5);
        g.DrawString("จำนวนเงิน (บาท)", fontHeader, brushDark, col3X, currentY + 5);

        currentY += 36;

        decimal roomCharges = _folio?.RoomCharges ?? _booking.AgreedRate;
        decimal extraCharges = _folio?.ExtraCharges ?? 0;
        decimal discountAmount = _folio?.DiscountAmount ?? 0;
        decimal subTotal = Math.Max(0, roomCharges + extraCharges - discountAmount);

        // Row 1: Room Charges
        g.DrawString($"ค่าห้องพัก (ห้อง {_room.RoomNumber})", fontBodyBold, brushText, col1X + 8, currentY);
        g.DrawString(GetRatePlanName(_booking.RatePlan), fontBody, brushText, col2X, currentY);
        g.DrawString($"{roomCharges:N2}", fontBodyBold, brushText, col3X, currentY);
        currentY += 28;

        // Row 2: Extra Charges (if any)
        if (extraCharges > 0)
        {
            g.DrawString("ค่าบริการเสริม / สินค้ามินิบาร์เพิ่มเติม", fontBody, brushText, col1X + 8, currentY);
            g.DrawString("-", fontBody, brushText, col2X, currentY);
            g.DrawString($"{extraCharges:N2}", fontBody, brushText, col3X, currentY);
            currentY += 28;
        }

        // Row 3: Discount (if any)
        if (discountAmount > 0)
        {
            g.DrawString("ส่วนลดพิเศษ (Discount)", fontBody, Brushes.DarkRed, col1X + 8, currentY);
            g.DrawString("-", fontBody, brushText, col2X, currentY);
            g.DrawString($"-{discountAmount:N2}", fontBody, Brushes.DarkRed, col3X, currentY);
            currentY += 28;
        }

        // VAT Calculation if enabled
        decimal finalTotal = subTotal;
        if (_settings?.EnableVat == true && _settings.VatRate > 0)
        {
            decimal vatAmount = Math.Round(subTotal * (_settings.VatRate / 100m), 2);
            finalTotal = subTotal + vatAmount;

            g.DrawString($"ภาษีมูลค่าเพิ่ม VAT ({_settings.VatRate:N0}%)", fontBody, Brushes.DarkSlateGray, col1X + 8, currentY);
            g.DrawString("-", fontBody, brushText, col2X, currentY);
            g.DrawString($"{vatAmount:N2}", fontBody, Brushes.DarkSlateGray, col3X, currentY);
            currentY += 28;
        }

        g.DrawLine(penLight, leftMargin, currentY, rightMargin, currentY);
        currentY += 12;

        // Total Box (Large Green Callout Box)
        var totalRect = new RectangleF(rightMargin - 320, currentY, 320, 42);
        g.FillRectangle(new SolidBrush(Color.FromArgb(240, 253, 244)), totalRect);
        g.DrawRectangle(new Pen(Color.ForestGreen, 1.5F), totalRect.X, totalRect.Y, totalRect.Width, totalRect.Height);

        g.DrawString("ยอดเงินสุทธิที่ชำระ (TOTAL):", fontHeader, Brushes.DarkGreen, rightMargin - 305, currentY + 10);
        g.DrawString($"{finalTotal:N2} บาท", new Font("Segoe UI", 13F, FontStyle.Bold), Brushes.DarkGreen, rightMargin - 130, currentY + 8);

        currentY += 60;

        // 5. Lobby Terms & Special Agreements Section (ข้อตกลงและเงื่อนไขหน้าเคาน์เตอร์/ล็อบบี้)
        if (!string.IsNullOrWhiteSpace(_settings?.LobbyTerms))
        {
            var termsRect = new RectangleF(leftMargin, currentY, contentWidth, 90);
            g.FillRectangle(new SolidBrush(Color.FromArgb(248, 250, 252)), termsRect);
            g.DrawRectangle(penLight, termsRect.X, termsRect.Y, termsRect.Width, termsRect.Height);

            g.DrawString("ข้อตกลงและเงื่อนไขหน้าล็อบบี้ / Lobby Agreements & Terms", fontHeader, brushDark, leftMargin + 10, currentY + 6);
            
            var sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Near };
            g.DrawString(_settings.LobbyTerms, fontSmall, Brushes.DarkSlateGray, 
                new RectangleF(leftMargin + 12, currentY + 30, contentWidth - 24, 55), sf);

            currentY += 105;
        }

        // 6. QR Code & Signature Boxes Container
        if (!string.IsNullOrEmpty(_settings?.QrCodeImagePath) && File.Exists(_settings.QrCodeImagePath))
        {
            try
            {
                using var qrImg = Image.FromFile(_settings.QrCodeImagePath);
                float maxW = 90f;
                float maxH = 90f;
                float scale = Math.Min(maxW / qrImg.Width, maxH / qrImg.Height);
                float drawW = qrImg.Width * scale;
                float drawH = qrImg.Height * scale;

                float qrX = leftMargin + 10;
                float qrY = currentY;
                g.DrawImage(qrImg, qrX, qrY, drawW, drawH);
                g.DrawString("สแกนชำระเงิน PromptPay", fontSmall, Brushes.DimGray, qrX, qrY + drawH + 4);
            }
            catch { }
        }

        bool showSignature = _settings?.ShowSignatureBox ?? true;
        if (showSignature)
        {
            float sigBoxWidth = (contentWidth - 140) / 2;

            // Left Signature Box (Guest)
            float sig1X = leftMargin + 130;
            var sig1Rect = new RectangleF(sig1X, currentY, sigBoxWidth, 115);
            g.DrawRectangle(penLight, sig1Rect.X, sig1Rect.Y, sig1Rect.Width, sig1Rect.Height);

            g.DrawString("ลงลายมือชื่อผู้เข้าพัก / Guest Signature", fontHeader, brushDark, sig1X + 10, currentY + 8);
            g.DrawLine(penLight, sig1X + 15, currentY + 68, sig1X + sigBoxWidth - 15, currentY + 68);
            g.DrawString($"({(_customer != null ? _customer.FullName : "_________________________")})", fontSmall, Brushes.DimGray, sig1X + 20, currentY + 75);
            g.DrawString("วันที่ / Date: _____ / _____ / ________", fontSmall, Brushes.DimGray, sig1X + 20, currentY + 93);

            // Right Signature Box (Staff)
            float sig2X = sig1X + sigBoxWidth + 15;
            var sig2Rect = new RectangleF(sig2X, currentY, sigBoxWidth, 115);
            g.DrawRectangle(penLight, sig2Rect.X, sig2Rect.Y, sig2Rect.Width, sig2Rect.Height);

            g.DrawString("ลงลายมือชื่อเจ้าหน้าที่ / Staff Signature", fontHeader, brushDark, sig2X + 10, currentY + 8);
            g.DrawLine(penLight, sig2X + 15, currentY + 68, sig2X + sigBoxWidth - 15, currentY + 68);
            g.DrawString($"({_staffName})", fontSmall, Brushes.DimGray, sig2X + 45, currentY + 75);
            g.DrawString("วันที่ / Date: _____ / _____ / ________", fontSmall, Brushes.DimGray, sig2X + 20, currentY + 93);

            currentY += 130;
        }
        else
        {
            currentY += 20;
        }

        // 7. Footer Message Note
        string footerMsg = !string.IsNullOrWhiteSpace(_settings?.BillFooter) ? _settings.BillFooter : "ขอบคุณที่ใช้บริการ / Thank you for staying with us";
        g.DrawString(footerMsg, new Font("Segoe UI", 10F, FontStyle.Bold), Brushes.DarkSlateBlue, leftMargin + (contentWidth / 2) - 150, currentY);
    }

    private static string GetRatePlanName(RatePlanType plan)
    {
        return plan switch
        {
            RatePlanType.Daily => "รายวัน (Daily)",
            RatePlanType.Hourly => "รายชั่วโมง (Hourly)",
            RatePlanType.Monthly => "รายเดือน (Monthly)",
            _ => "-"
        };
    }
}
