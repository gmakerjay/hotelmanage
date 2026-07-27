using System.Drawing.Printing;
using HotelPOS.Common.Models;

namespace HotelPOS.Printing;

/// <summary>
/// พิมพ์ใบแจ้งหนี้รายเดือน (ค่าน้ำ + ค่าไฟ + ค่าห้อง + ค่าส่วนกลาง + ค่าขยะ) ในใบเดียว
/// รองรับกระดาษ A4 จัดหน้าสมส่วนเรียบหรู พร้อมช่องข้อตกลงหน้าล็อบบี้
/// </summary>
public class UtilityInvoicePrinter
{
    private readonly UtilityBill _bill;
    private readonly Customer? _customer;
    private readonly SystemSettingsDto _settings;
    private readonly string _staffName;

    public UtilityInvoicePrinter(
        UtilityBill bill,
        Customer? customer = null,
        SystemSettingsDto? settings = null,
        string staffName = "พนักงานหน้าเคาน์เตอร์")
    {
        _bill = bill;
        _customer = customer;
        _settings = settings ?? new SystemSettingsDto();
        _staffName = staffName;
    }

    public void Print(string printerName = "")
    {
        using var printDoc = new PrintDocument();

        if (!string.IsNullOrWhiteSpace(printerName))
        {
            printDoc.PrinterSettings.PrinterName = printerName;
        }

        if (_settings.PaperType == "A4")
        {
            printDoc.DefaultPageSettings.PaperSize = new System.Drawing.Printing.PaperSize("A4", 827, 1169);
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
            Text = $"ตัวอย่างก่อนพิมพ์ - ใบแจ้งหนี้ประจำเดือน {_bill.BillingMonth} (A4)"
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
        var brushDark = new SolidBrush(Color.FromArgb(15, 23, 42));
        var brushHeaderBg = new SolidBrush(Color.FromArgb(241, 245, 249));

        float leftMargin = e.MarginBounds.Left;
        float rightMargin = e.MarginBounds.Right;
        float contentWidth = rightMargin - leftMargin;
        float currentY = e.MarginBounds.Top;

        // 1. Header (Logo & Shop Details)
        float logoOffsetX = leftMargin;
        if (!string.IsNullOrEmpty(_settings.LogoImagePath) && File.Exists(_settings.LogoImagePath))
        {
            try
            {
                using var logoImg = Image.FromFile(_settings.LogoImagePath);
                float scale = Math.Min(140f / logoImg.Width, 55f / logoImg.Height);
                float drawW = logoImg.Width * scale;
                float drawH = logoImg.Height * scale;
                g.DrawImage(logoImg, leftMargin, currentY, drawW, drawH);
                logoOffsetX += drawW + 15;
            }
            catch { }
        }

        string shopName = string.IsNullOrWhiteSpace(_settings.ShopName) ? "อพาร์ทเม้นท์ / ห้องเช่า" : _settings.ShopName;
        g.DrawString(shopName, fontTitle, brushDark, logoOffsetX, currentY);
        currentY += 32;

        string shopDetails = $"ที่อยู่: {(_settings.ShopAddress ?? "-")} | โทร: {(_settings.ShopPhone ?? "-")} | เลขภาษี: {(_settings.ShopTaxId ?? "-")}";
        g.DrawString(shopDetails, fontSmall, Brushes.DimGray, logoOffsetX, currentY);
        currentY += 28;

        g.DrawLine(penDark, leftMargin, currentY, rightMargin, currentY);
        currentY += 15;

        // 2. Title Box: ใบแจ้งหนี้ประจำเดือน (บิลรวม)
        var rectHeaderBox = new RectangleF(leftMargin, currentY, contentWidth, 40);
        g.FillRectangle(brushHeaderBg, rectHeaderBox);
        g.DrawRectangle(penLight, rectHeaderBox.X, rectHeaderBox.Y, rectHeaderBox.Width, rectHeaderBox.Height);

        g.DrawString("ใบแจ้งหนี้ค่าเช่าและค่าบริการประจำเดือน (MONTHLY INVOICE)", fontSubtitle, brushDark, leftMargin + 12, currentY + 8);
        g.DrawString($"เลขที่บิล: {_bill.BillCode}", fontBodyBold, brushDark, rightMargin - 220, currentY + 10);
        currentY += 55;

        // 3. Info Table (Room & Tenant Details)
        float halfWidth = (contentWidth - 15) / 2;

        // Tenant Info Box
        var guestRect = new RectangleF(leftMargin, currentY, halfWidth, 105);
        g.DrawRectangle(penLight, guestRect.X, guestRect.Y, guestRect.Width, guestRect.Height);
        g.FillRectangle(brushHeaderBg, leftMargin, currentY, halfWidth, 26);
        g.DrawRectangle(penLight, leftMargin, currentY, halfWidth, 26);
        g.DrawString("ข้อมูลผู้เช่า / Tenant Details", fontHeader, brushDark, leftMargin + 8, currentY + 4);

        float guestY = currentY + 34;
        g.DrawString($"ชื่อผู้เช่า: {(_customer?.FullName ?? "ผู้เช่าทั่วไป")}", fontBody, Brushes.Black, leftMargin + 8, guestY);
        guestY += 25;
        g.DrawString($"เบอร์โทรศัพท์: {(_customer?.Phone ?? "-")}", fontBody, Brushes.Black, leftMargin + 8, guestY);

        // Bill Info Box
        float billInfoX = leftMargin + halfWidth + 15;
        var billRect = new RectangleF(billInfoX, currentY, halfWidth, 105);
        g.DrawRectangle(penLight, billRect.X, billRect.Y, billRect.Width, billRect.Height);
        g.FillRectangle(brushHeaderBg, billInfoX, currentY, halfWidth, 26);
        g.DrawRectangle(penLight, billInfoX, currentY, halfWidth, 26);
        g.DrawString("รายละเอียดรอบบิล / Billing Details", fontHeader, brushDark, billInfoX + 8, currentY + 4);

        float billY = currentY + 34;
        g.DrawString($"ห้องพัก: {_bill.RoomNumber}", fontBodyBold, Brushes.Black, billInfoX + 8, billY);
        g.DrawString($"ประจำเดือน: {_bill.BillingMonth}", fontBodyBold, Brushes.DarkBlue, billInfoX + 150, billY);
        billY += 25;
        g.DrawString($"สถานะการชำระ: {(_bill.IsPaid ? "ชำระแล้ว" : "ยังไม่ชำระ")}", fontBodyBold, _bill.IsPaid ? Brushes.DarkGreen : Brushes.DarkRed, billInfoX + 8, billY);

        currentY += 125;

        // 4. Financial Items Table (บิลรวมใบเดียว)
        float col1X = leftMargin;
        float col2X = leftMargin + 240;
        float col3X = leftMargin + 340;
        float col4X = leftMargin + 440;
        float col5X = rightMargin - 130;

        var tableHeaderRect = new RectangleF(leftMargin, currentY, contentWidth, 30);
        g.FillRectangle(brushHeaderBg, tableHeaderRect);
        g.DrawRectangle(penLight, tableHeaderRect.X, tableHeaderRect.Y, tableHeaderRect.Width, tableHeaderRect.Height);

        g.DrawString("รายการค่าใช้จ่าย", fontHeader, brushDark, col1X + 8, currentY + 5);
        g.DrawString("เลขก่อน", fontHeader, brushDark, col2X, currentY + 5);
        g.DrawString("เลขหลัง", fontHeader, brushDark, col3X, currentY + 5);
        g.DrawString("หน่วย/จำนวน", fontHeader, brushDark, col4X, currentY + 5);
        g.DrawString("จำนวนเงิน (บาท)", fontHeader, brushDark, col5X, currentY + 5);

        currentY += 36;

        // 1) ค่าเช่าห้อง
        g.DrawString("ค่าเช่าห้องพักรายเดือน", fontBodyBold, Brushes.Black, col1X + 8, currentY);
        g.DrawString("-", fontBody, Brushes.Gray, col2X, currentY);
        g.DrawString("-", fontBody, Brushes.Gray, col3X, currentY);
        g.DrawString("1 เดือน", fontBody, Brushes.Black, col4X, currentY);
        g.DrawString($"{_bill.RoomCharge:N2}", fontBodyBold, Brushes.Black, col5X, currentY);
        currentY += 28;

        // 2) ค่าไฟ (ตามมิเตอร์)
        if (_bill.ElectricUnits > 0 || _bill.ElectricAmount > 0)
        {
            g.DrawString($"ค่าไฟฟ้า ({_bill.ElectricRate:N2} ฿/หน่วย)", fontBody, Brushes.Black, col1X + 8, currentY);
            g.DrawString($"{_bill.ElectricPrev:N0}", fontBody, Brushes.Black, col2X, currentY);
            g.DrawString($"{_bill.ElectricCurr:N0}", fontBody, Brushes.Black, col3X, currentY);
            g.DrawString($"{_bill.ElectricUnits:N0} หน่วย", fontBody, Brushes.Black, col4X, currentY);
            g.DrawString($"{_bill.ElectricAmount:N2}", fontBody, Brushes.Black, col5X, currentY);
            currentY += 28;
        }

        // 3) ค่าน้ำ (ตามมิเตอร์ หรือ เหมาจ่าย)
        if (_bill.WaterBillingMode == "FLAT")
        {
            g.DrawString($"ค่าน้ำประปา (เหมาจ่าย {_bill.WaterRate:N2} ฿/คน)", fontBody, Brushes.Black, col1X + 8, currentY);
            g.DrawString("-", fontBody, Brushes.Gray, col2X, currentY);
            g.DrawString("-", fontBody, Brushes.Gray, col3X, currentY);
            g.DrawString($"{_bill.WaterPersonCount} คน", fontBody, Brushes.Black, col4X, currentY);
            g.DrawString($"{_bill.WaterAmount:N2}", fontBody, Brushes.Black, col5X, currentY);
            currentY += 28;
        }
        else if (_bill.WaterUnits > 0 || _bill.WaterAmount > 0)
        {
            g.DrawString($"ค่าน้ำประปา ({_bill.WaterRate:N2} ฿/หน่วย)", fontBody, Brushes.Black, col1X + 8, currentY);
            g.DrawString($"{_bill.WaterPrev:N0}", fontBody, Brushes.Black, col2X, currentY);
            g.DrawString($"{_bill.WaterCurr:N0}", fontBody, Brushes.Black, col3X, currentY);
            g.DrawString($"{_bill.WaterUnits:N0} หน่วย", fontBody, Brushes.Black, col4X, currentY);
            g.DrawString($"{_bill.WaterAmount:N2}", fontBody, Brushes.Black, col5X, currentY);
            currentY += 28;
        }

        // 4) ค่าบริการ/ส่วนกลาง (ถ้ามี)
        if (_bill.CommonAreaFee > 0)
        {
            g.DrawString("ค่าบริการส่วนกลาง", fontBody, Brushes.Black, col1X + 8, currentY);
            g.DrawString("-", fontBody, Brushes.Gray, col2X, currentY);
            g.DrawString("-", fontBody, Brushes.Gray, col3X, currentY);
            g.DrawString("1 เดือน", fontBody, Brushes.Black, col4X, currentY);
            g.DrawString($"{_bill.CommonAreaFee:N2}", fontBody, Brushes.Black, col5X, currentY);
            currentY += 28;
        }

        // 5) ค่าขยะ (ถ้ามี)
        if (_bill.GarbageFee > 0)
        {
            g.DrawString("ค่าจัดเก็บขยะรายเดือน", fontBody, Brushes.Black, col1X + 8, currentY);
            g.DrawString("-", fontBody, Brushes.Gray, col2X, currentY);
            g.DrawString("-", fontBody, Brushes.Gray, col3X, currentY);
            g.DrawString("1 เดือน", fontBody, Brushes.Black, col4X, currentY);
            g.DrawString($"{_bill.GarbageFee:N2}", fontBody, Brushes.Black, col5X, currentY);
            currentY += 28;
        }

        // 6) ค่าอื่นๆ (ถ้ามี)
        if (_bill.ExtraCharges > 0)
        {
            g.DrawString("ค่าบริการเพิ่มเติมอื่นๆ", fontBody, Brushes.Black, col1X + 8, currentY);
            g.DrawString("-", fontBody, Brushes.Gray, col2X, currentY);
            g.DrawString("-", fontBody, Brushes.Gray, col3X, currentY);
            g.DrawString("-", fontBody, Brushes.Gray, col4X, currentY);
            g.DrawString($"{_bill.ExtraCharges:N2}", fontBody, Brushes.Black, col5X, currentY);
            currentY += 28;
        }

        // 7) ส่วนลด (ถ้ามี)
        if (_bill.DiscountAmount > 0)
        {
            g.DrawString("ส่วนลดพิเศษ", fontBody, Brushes.DarkRed, col1X + 8, currentY);
            g.DrawString("-", fontBody, Brushes.Gray, col2X, currentY);
            g.DrawString("-", fontBody, Brushes.Gray, col3X, currentY);
            g.DrawString("-", fontBody, Brushes.Gray, col4X, currentY);
            g.DrawString($"-{_bill.DiscountAmount:N2}", fontBody, Brushes.DarkRed, col5X, currentY);
            currentY += 28;
        }

        g.DrawLine(penDark, leftMargin, currentY, rightMargin, currentY);
        currentY += 12;

        // Total Box (Large Green Callout Box)
        var totalRect = new RectangleF(rightMargin - 320, currentY, 320, 42);
        g.FillRectangle(new SolidBrush(Color.FromArgb(240, 253, 244)), totalRect);
        g.DrawRectangle(new Pen(Color.ForestGreen, 1.5F), totalRect.X, totalRect.Y, totalRect.Width, totalRect.Height);

        g.DrawString("ยอดรวมทั้งสิ้น (TOTAL DUE):", fontHeader, Brushes.DarkGreen, rightMargin - 305, currentY + 10);
        g.DrawString($"{_bill.TotalAmount:N2} บาท", new Font("Segoe UI", 13F, FontStyle.Bold), Brushes.DarkGreen, rightMargin - 130, currentY + 8);

        currentY += 60;

        // 5. Lobby Terms & Special Agreements Section (ข้อตกลงและเงื่อนไขหน้าเคาน์เตอร์/ล็อบบี้)
        if (!string.IsNullOrWhiteSpace(_settings.LobbyTerms))
        {
            var termsRect = new RectangleF(leftMargin, currentY, contentWidth, 90);
            g.FillRectangle(new SolidBrush(Color.FromArgb(248, 250, 252)), termsRect);
            g.DrawRectangle(penLight, termsRect.X, termsRect.Y, termsRect.Width, termsRect.Height);

            g.DrawString("ข้อตกลงและเงื่อนไขหน้าล็อบบี้ / Lobby Terms & Conditions", fontHeader, brushDark, leftMargin + 10, currentY + 6);

            var sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Near };
            g.DrawString(_settings.LobbyTerms, fontSmall, Brushes.DarkSlateGray,
                new RectangleF(leftMargin + 12, currentY + 30, contentWidth - 24, 55), sf);

            currentY += 105;
        }

        // 6. QR Code & Signatures Container
        if (!string.IsNullOrEmpty(_settings.QrCodeImagePath) && File.Exists(_settings.QrCodeImagePath))
        {
            try
            {
                using var qrImg = Image.FromFile(_settings.QrCodeImagePath);
                float scale = Math.Min(90f / qrImg.Width, 90f / qrImg.Height);
                float drawW = qrImg.Width * scale;
                float drawH = qrImg.Height * scale;
                g.DrawImage(qrImg, leftMargin + 10, currentY, drawW, drawH);
                g.DrawString("สแกนชำระเงิน PromptPay", fontSmall, Brushes.DimGray, leftMargin + 10, currentY + drawH + 4);
            }
            catch { }
        }

        bool showSignature = _settings.ShowSignatureBox;
        if (showSignature)
        {
            float sigBoxWidth = (contentWidth - 140) / 2;

            // Left Signature Box (Tenant)
            float sig1X = leftMargin + 130;
            var sig1Rect = new RectangleF(sig1X, currentY, sigBoxWidth, 115);
            g.DrawRectangle(penLight, sig1Rect.X, sig1Rect.Y, sig1Rect.Width, sig1Rect.Height);

            g.DrawString("ลงลายมือชื่อผู้เช่า / Tenant Signature", fontHeader, brushDark, sig1X + 10, currentY + 8);
            g.DrawLine(penLight, sig1X + 15, currentY + 68, sig1X + sigBoxWidth - 15, currentY + 68);
            g.DrawString($"({(_customer?.FullName ?? "_________________________")})", fontSmall, Brushes.DimGray, sig1X + 20, currentY + 75);
            g.DrawString("วันที่ / Date: _____ / _____ / ________", fontSmall, Brushes.DimGray, sig1X + 20, currentY + 93);

            // Right Signature Box (Staff)
            float sig2X = sig1X + sigBoxWidth + 15;
            var sig2Rect = new RectangleF(sig2X, currentY, sigBoxWidth, 115);
            g.DrawRectangle(penLight, sig2Rect.X, sig2Rect.Y, sig2Rect.Width, sig2Rect.Height);

            g.DrawString("ลงลายมือชื่อผู้รับเงิน / Receiver Signature", fontHeader, brushDark, sig2X + 10, currentY + 8);
            g.DrawLine(penLight, sig2X + 15, currentY + 68, sig2X + sigBoxWidth - 15, currentY + 68);
            g.DrawString($"({_staffName})", fontSmall, Brushes.DimGray, sig2X + 45, currentY + 75);
            g.DrawString("วันที่ / Date: _____ / _____ / ________", fontSmall, Brushes.DimGray, sig2X + 20, currentY + 93);

            currentY += 130;
        }
        else
        {
            currentY += 20;
        }

        // 7. Footer Note
        string footerMsg = !string.IsNullOrWhiteSpace(_settings.BillFooter) ? _settings.BillFooter : "ขอบคุณที่ใช้บริการ / Thank you for staying with us";
        g.DrawString(footerMsg, new Font("Segoe UI", 10F, FontStyle.Bold), Brushes.DarkSlateBlue, leftMargin + (contentWidth / 2) - 150, currentY);
    }
}
