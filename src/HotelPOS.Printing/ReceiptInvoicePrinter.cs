using System.Drawing.Printing;
using HotelPOS.Common;
using HotelPOS.Common.Models;

namespace HotelPOS.Printing;

public class ReceiptInvoicePrinter
{
    private readonly string _shopName;
    private readonly string _shopAddress;
    private readonly string _shopPhone;
    private readonly string _shopTaxId;
    private readonly Booking _booking;
    private readonly Room _room;
    private readonly Customer? _customer;
    private readonly Folio? _folio;
    private readonly string _staffName;
    private readonly SystemSettingsDto? _settings;

    public ReceiptInvoicePrinter(
        string shopName,
        string shopAddress,
        string shopPhone,
        string shopTaxId,
        Booking booking,
        Room room,
        Customer? customer,
        Folio? folio,
        string staffName = "admin",
        SystemSettingsDto? settings = null)
    {
        _settings = settings;
        _shopName = !string.IsNullOrWhiteSpace(settings?.ShopName) ? settings.ShopName : (string.IsNullOrWhiteSpace(shopName) ? "โรงแรม HotelPOS TH" : shopName);
        _shopAddress = !string.IsNullOrWhiteSpace(settings?.ShopAddress) ? settings.ShopAddress : (string.IsNullOrWhiteSpace(shopAddress) ? "123/45 ถนนสุขุมวิท กรุงเทพมหานคร" : shopAddress);
        _shopPhone = !string.IsNullOrWhiteSpace(settings?.ShopPhone) ? settings.ShopPhone : (string.IsNullOrWhiteSpace(shopPhone) ? "02-123-4567" : shopPhone);
        _shopTaxId = !string.IsNullOrWhiteSpace(settings?.ShopTaxId) ? settings.ShopTaxId : (string.IsNullOrWhiteSpace(shopTaxId) ? "0105560000000" : shopTaxId);
        
        _booking = booking;
        _room = room;
        _customer = customer;
        _folio = folio;
        _staffName = staffName;
    }

    public void ShowPrintPreview()
    {
        using var printDoc = CreatePrintDocument();
        using var previewDlg = new PrintPreviewDialog
        {
            Document = printDoc,
            Width = 900,
            Height = 750,
            StartPosition = FormStartPosition.CenterScreen,
            Text = $"ตัวอย่างก่อนพิมพ์ใบเสร็จ/ใบแจ้งหนี้ — ห้อง {_room.RoomNumber}"
        };

        if (previewDlg.Controls.OfType<Form>().FirstOrDefault() is Form previewForm)
        {
            previewForm.WindowState = FormWindowState.Maximized;
        }

        previewDlg.ShowDialog();
    }

    public void PrintDirect()
    {
        using var printDoc = CreatePrintDocument();
        
        // If a custom printer is specified in settings, set PrinterName
        if (!string.IsNullOrWhiteSpace(_settings?.PrinterName))
        {
            printDoc.PrinterSettings.PrinterName = _settings.PrinterName;
        }

        using var printDlg = new PrintDialog
        {
            Document = printDoc,
            UseEXDialog = true
        };

        if (printDlg.ShowDialog() == DialogResult.OK)
        {
            printDoc.Print();
        }
    }

    private PrintDocument CreatePrintDocument()
    {
        var printDoc = new PrintDocument();

        // Paper Size Handling (A4 vs 80mm vs 58mm)
        if (_settings?.PaperType == "80mm")
        {
            printDoc.DefaultPageSettings.PaperSize = new System.Drawing.Printing.PaperSize("80mm", 315, 1000); // 80mm = ~3.15 inches (315 hundredths of an inch)
            printDoc.DefaultPageSettings.Margins = new Margins(10, 10, 10, 10);
        }
        else if (_settings?.PaperType == "58mm")
        {
            printDoc.DefaultPageSettings.PaperSize = new System.Drawing.Printing.PaperSize("58mm", 228, 1000); // 58mm = ~2.28 inches
            printDoc.DefaultPageSettings.Margins = new Margins(5, 5, 5, 5);
        }
        else
        {
            printDoc.DefaultPageSettings.PaperSize = new System.Drawing.Printing.PaperSize("A4", 827, 1169); // Standard A4
            printDoc.DefaultPageSettings.Margins = new Margins(40, 40, 40, 40);
        }

        printDoc.PrintPage += PrintDoc_PrintPage;
        return printDoc;
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
        var fontHeader = new Font("Segoe UI", 11F, FontStyle.Bold);
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

        // 1. Logo Auto-Resize & Header Title
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
            catch { /* fallback if image error */ }
        }

        g.DrawString(_shopName, fontTitle, brushDark, logoOffsetX, currentY);
        currentY += 32;

        g.DrawString($"ที่อยู่: {_shopAddress} | โทรศัพท์: {_shopPhone} | เลขประจำตัวผู้เสียภาษี: {_shopTaxId}", fontSmall, Brushes.DimGray, logoOffsetX, currentY);
        currentY += 28;

        g.DrawLine(penDark, leftMargin, currentY, rightMargin, currentY);
        currentY += 12;

        // 2. Receipt Subheader Box
        var rectHeaderBox = new RectangleF(leftMargin, currentY, contentWidth, 38);
        g.FillRectangle(brushHeaderBg, rectHeaderBox);
        g.DrawRectangle(penLight, rectHeaderBox.X, rectHeaderBox.Y, rectHeaderBox.Width, rectHeaderBox.Height);

        g.DrawString("ใบเสร็จรับเงิน / ใบแจ้งหนี้ (RECEIPT / INVOICE)", fontSubtitle, brushDark, leftMargin + 12, currentY + 8);
        
        string receiptNo = string.IsNullOrWhiteSpace(_booking.BookingCode) ? $"REC-{_booking.Id:D6}" : _booking.BookingCode;
        g.DrawString($"เลขที่ใบเสร็จ: {receiptNo}", fontBodyBold, brushDark, rightMargin - 220, currentY + 10);
        currentY += 50;

        // 3. Info Table (Guest Info & Stay Info)
        float halfWidth = (contentWidth - 15) / 2;

        // Guest Info Box
        var guestRect = new RectangleF(leftMargin, currentY, halfWidth, 110);
        g.DrawRectangle(penLight, guestRect.X, guestRect.Y, guestRect.Width, guestRect.Height);
        g.FillRectangle(brushHeaderBg, leftMargin, currentY, halfWidth, 26);
        g.DrawRectangle(penLight, leftMargin, currentY, halfWidth, 26);
        g.DrawString("ข้อมูลผู้เข้าพัก (Guest Information)", fontHeader, brushDark, leftMargin + 8, currentY + 4);

        float guestY = currentY + 32;
        g.DrawString($"ชื่อ-นามสกุล: {(_customer != null ? _customer.FullName : "ไม่ระบุ")}", fontBody, brushText, leftMargin + 8, guestY);
        guestY += 22;
        g.DrawString($"เบอร์โทรศัพท์: {(_customer?.Phone ?? "-")}", fontBody, brushText, leftMargin + 8, guestY);
        guestY += 22;
        g.DrawString($"เลขบัตร/พาสปอร์ต: {(_customer?.IdCardOrPassport ?? "-")}", fontBody, brushText, leftMargin + 8, guestY);

        // Stay Info Box
        float stayX = leftMargin + halfWidth + 15;
        var stayRect = new RectangleF(stayX, currentY, halfWidth, 110);
        g.DrawRectangle(penLight, stayRect.X, stayRect.Y, stayRect.Width, stayRect.Height);
        g.FillRectangle(brushHeaderBg, stayX, currentY, halfWidth, 26);
        g.DrawRectangle(penLight, stayX, currentY, halfWidth, 26);
        g.DrawString("ข้อมูลการพัก (Stay Information)", fontHeader, brushDark, stayX + 8, currentY + 4);

        float stayY = currentY + 32;
        g.DrawString($"เลขที่ห้องพัก: {_room.RoomNumber} (ชั้น {_room.Floor ?? "-"})", fontBodyBold, brushText, stayX + 8, stayY);
        stayY += 22;
        
        var checkInTime = _booking.CheckInActual ?? _booking.CheckInPlanned;
        var checkOutTime = _booking.CheckOutActual ?? DateTime.Now;
        g.DrawString($"วันเวลาเช็คอิน: {checkInTime:dd/MM/yyyy HH:mm} น.", fontBody, brushText, stayX + 8, stayY);
        stayY += 22;
        g.DrawString($"วันเวลาเช็คเอาท์: {checkOutTime:dd/MM/yyyy HH:mm} น.", fontBody, brushText, stayX + 8, stayY);

        currentY += 125;

        // 4. Financial Breakdown Table
        float col1X = leftMargin;
        float col2X = leftMargin + 320;
        float col3X = leftMargin + 440;
        float col4X = rightMargin - 120;

        // Table Header
        var tableHeaderRect = new RectangleF(leftMargin, currentY, contentWidth, 28);
        g.FillRectangle(brushHeaderBg, tableHeaderRect);
        g.DrawRectangle(penLight, tableHeaderRect.X, tableHeaderRect.Y, tableHeaderRect.Width, tableHeaderRect.Height);

        g.DrawString("รายการ (Description)", fontHeader, brushDark, col1X + 8, currentY + 4);
        g.DrawString("ประเภทราคา", fontHeader, brushDark, col2X, currentY + 4);
        g.DrawString("จำนวนเงิน (บาท)", fontHeader, brushDark, col4X, currentY + 4);

        currentY += 32;

        decimal roomCharges = _folio?.RoomCharges ?? _booking.AgreedRate;
        decimal extraCharges = _folio?.ExtraCharges ?? 0;
        decimal discountAmount = _folio?.DiscountAmount ?? 0;
        decimal subTotal = Math.Max(0, roomCharges + extraCharges - discountAmount);

        // Row 1: Room Charges
        g.DrawString($"ค่าห้องพัก (ห้อง {_room.RoomNumber})", fontBody, brushText, col1X + 8, currentY);
        g.DrawString(GetRatePlanName(_booking.RatePlan), fontBody, brushText, col2X, currentY);
        g.DrawString($"{roomCharges:N2}", fontBody, brushText, col4X, currentY);
        currentY += 24;

        // Row 2: Extra Charges (if any)
        if (extraCharges > 0)
        {
            g.DrawString("ค่าบริการเสริม / มินิบาร์เพิ่มเติม", fontBody, brushText, col1X + 8, currentY);
            g.DrawString("-", fontBody, brushText, col2X, currentY);
            g.DrawString($"{extraCharges:N2}", fontBody, brushText, col4X, currentY);
            currentY += 24;
        }

        // Row 3: Discount (if any)
        if (discountAmount > 0)
        {
            g.DrawString("ส่วนลดพิเศษ (Discount)", fontBody, Brushes.DarkRed, col1X + 8, currentY);
            g.DrawString("-", fontBody, brushText, col2X, currentY);
            g.DrawString($"-{discountAmount:N2}", fontBody, Brushes.DarkRed, col4X, currentY);
            currentY += 24;
        }

        // VAT Calculation if enabled
        decimal finalTotal = subTotal;
        if (_settings?.EnableVat == true && _settings.VatRate > 0)
        {
            decimal vatAmount = Math.Round(subTotal * (_settings.VatRate / 100m), 2);
            finalTotal = subTotal + vatAmount;

            g.DrawString($"ภาษีมูลค่าเพิ่ม VAT ({_settings.VatRate:N0}%)", fontBody, Brushes.DarkSlateGray, col1X + 8, currentY);
            g.DrawString("-", fontBody, brushText, col2X, currentY);
            g.DrawString($"{vatAmount:N2}", fontBody, Brushes.DarkSlateGray, col4X, currentY);
            currentY += 24;
        }

        g.DrawLine(penLight, leftMargin, currentY, rightMargin, currentY);
        currentY += 10;

        // Total Box
        var totalRect = new RectangleF(rightMargin - 300, currentY, 300, 36);
        g.FillRectangle(new SolidBrush(Color.FromArgb(240, 253, 244)), totalRect);
        g.DrawRectangle(new Pen(Color.ForestGreen, 1.5F), totalRect.X, totalRect.Y, totalRect.Width, totalRect.Height);

        g.DrawString("ยอดเงินสุทธิที่ชำระ (TOTAL):", fontHeader, Brushes.DarkGreen, rightMargin - 290, currentY + 8);
        g.DrawString($"{finalTotal:N2} บาท", new Font("Segoe UI", 12F, FontStyle.Bold), Brushes.DarkGreen, rightMargin - 125, currentY + 6);

        currentY += 55;

        // 5. Payment QR Code Image Auto-Resize (if uploaded)
        if (!string.IsNullOrEmpty(_settings?.QrCodeImagePath) && File.Exists(_settings.QrCodeImagePath))
        {
            try
            {
                using var qrImg = Image.FromFile(_settings.QrCodeImagePath);
                float maxW = 95f;
                float maxH = 95f;
                float scale = Math.Min(maxW / qrImg.Width, maxH / qrImg.Height);
                float drawW = qrImg.Width * scale;
                float drawH = qrImg.Height * scale;

                float qrX = leftMargin + 10;
                float qrY = currentY;
                g.DrawImage(qrImg, qrX, qrY, drawW, drawH);
                g.DrawString("สแกนชำระเงิน (PromptPay QR)", fontSmall, Brushes.DimGray, qrX, qrY + drawH + 4);
            }
            catch { /* fallback if image error */ }
        }

        // 6. Signature Lines Box (ช่องเซ็นชื่อผู้เข้าพักและเจ้าหน้าที่)
        bool showSignature = _settings?.ShowSignatureBox ?? true;
        if (showSignature)
        {
            float sigBoxWidth = (contentWidth - 130) / 2;

            // Left Signature Box (Guest)
            float sig1X = leftMargin + 120;
            var sig1Rect = new RectangleF(sig1X, currentY, sigBoxWidth, 125);
            g.DrawRectangle(penLight, sig1Rect.X, sig1Rect.Y, sig1Rect.Width, sig1Rect.Height);

            g.DrawString("ลงลายมือชื่อผู้เข้าพัก / Guest Signature", fontHeader, brushDark, sig1X + 10, currentY + 8);
            g.DrawLine(penLight, sig1X + 15, currentY + 75, sig1X + sigBoxWidth - 15, currentY + 75);
            g.DrawString($"({(_customer != null ? _customer.FullName : "_________________________")})", fontSmall, Brushes.DimGray, sig1X + 20, currentY + 83);
            g.DrawString("วันที่ / Date: _____ / _____ / ________", fontSmall, Brushes.DimGray, sig1X + 20, currentY + 100);

            // Right Signature Box (Staff)
            float sig2X = sig1X + sigBoxWidth + 15;
            var sig2Rect = new RectangleF(sig2X, currentY, sigBoxWidth, 125);
            g.DrawRectangle(penLight, sig2Rect.X, sig2Rect.Y, sig2Rect.Width, sig2Rect.Height);

            g.DrawString("ลงลายมือชื่อเจ้าหน้าที่ / Staff Signature", fontHeader, brushDark, sig2X + 10, currentY + 8);
            g.DrawLine(penLight, sig2X + 15, currentY + 75, sig2X + sigBoxWidth - 15, currentY + 75);
            g.DrawString($"({_staffName})", fontSmall, Brushes.DimGray, sig2X + 60, currentY + 83);
            g.DrawString("วันที่ / Date: _____ / _____ / ________", fontSmall, Brushes.DimGray, sig2X + 20, currentY + 100);

            currentY += 140;
        }
        else
        {
            currentY += 20;
        }

        // 7. Footer Note / Custom Message
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
