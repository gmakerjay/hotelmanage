using System.Drawing.Printing;
using HotelPOS.Common;
using HotelPOS.Common.Models;

namespace HotelPOS.Printing;

/// <summary>
/// เครื่องพิมพ์ใบแจ้งหนี้ค่าเช่าห้องพักและสาธารณูปโภคประจำเดือน (บิลรวมใบเดียว)
/// รวม: ค่าห้อง + ค่าไฟ (มิเตอร์) + ค่าน้ำ (มิเตอร์/เหมาจ่าย) + ค่าบริการ + ค่าขยะ
/// </summary>
public class UtilityInvoicePrinter
{
    private readonly UtilityBill _bill;
    private readonly Customer? _customer;
    private readonly SystemSettingsDto _settings;
    private readonly string _staffName;

    public UtilityInvoicePrinter(
        UtilityBill bill,
        Customer? customer,
        SystemSettingsDto settings,
        string staffName = "admin")
    {
        _bill = bill;
        _customer = customer;
        _settings = settings;
        _staffName = staffName;
    }

    public void ShowPrintPreview()
    {
        using var printDoc = CreatePrintDocument();
        using var previewDlg = new PrintPreviewDialog
        {
            Document = printDoc,
            Width = 900,
            Height = 800,
            StartPosition = FormStartPosition.CenterScreen,
            Text = $"ใบแจ้งหนี้ประจำเดือน — ห้อง {_bill.RoomNumber} ({_bill.BillingMonth})"
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
        
        if (!string.IsNullOrWhiteSpace(_settings.PrinterName))
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

        if (_settings.PaperType == "80mm")
        {
            printDoc.DefaultPageSettings.PaperSize = new System.Drawing.Printing.PaperSize("80mm", 315, 1200);
            printDoc.DefaultPageSettings.Margins = new Margins(10, 10, 10, 10);
        }
        else if (_settings.PaperType == "58mm")
        {
            printDoc.DefaultPageSettings.PaperSize = new System.Drawing.Printing.PaperSize("58mm", 228, 1200);
            printDoc.DefaultPageSettings.Margins = new Margins(5, 5, 5, 5);
        }
        else
        {
            printDoc.DefaultPageSettings.PaperSize = new System.Drawing.Printing.PaperSize("A4", 827, 1169);
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

        string shopDetails = $"ที่อยู่: {(_settings.ShopAddress ?? "-")} | โทร: {(_settings.ShopPhone ?? "-")}";
        g.DrawString(shopDetails, fontSmall, Brushes.DimGray, logoOffsetX, currentY);
        currentY += 25;

        g.DrawLine(penDark, leftMargin, currentY, rightMargin, currentY);
        currentY += 12;

        // 2. Title Box: ใบแจ้งหนี้ประจำเดือน (บิลรวม)
        var rectHeaderBox = new RectangleF(leftMargin, currentY, contentWidth, 38);
        g.FillRectangle(brushHeaderBg, rectHeaderBox);
        g.DrawRectangle(penLight, rectHeaderBox.X, rectHeaderBox.Y, rectHeaderBox.Width, rectHeaderBox.Height);

        g.DrawString("ใบแจ้งหนี้ค่าเช่าและค่าบริการ (MONTHLY INVOICE)", fontSubtitle, brushDark, leftMargin + 12, currentY + 8);
        g.DrawString($"เลขที่บิล: {_bill.BillCode}", fontBodyBold, brushDark, rightMargin - 220, currentY + 10);
        currentY += 48;

        // 3. Info Table (Room & Tenant Details)
        float halfWidth = (contentWidth - 15) / 2;

        // Tenant Info
        var guestRect = new RectangleF(leftMargin, currentY, halfWidth, 90);
        g.DrawRectangle(penLight, guestRect.X, guestRect.Y, guestRect.Width, guestRect.Height);
        g.FillRectangle(brushHeaderBg, leftMargin, currentY, halfWidth, 24);
        g.DrawRectangle(penLight, leftMargin, currentY, halfWidth, 24);
        g.DrawString("ข้อมูลผู้เช่า / Tenant Details", fontHeader, brushDark, leftMargin + 8, currentY + 3);

        float guestY = currentY + 30;
        g.DrawString($"ชื่อผู้เช่า: {(_customer?.FullName ?? "ไม่ระบุ")}", fontBody, Brushes.Black, leftMargin + 8, guestY);
        guestY += 20;
        g.DrawString($"เบอร์โทรศัพท์: {(_customer?.Phone ?? "-")}", fontBody, Brushes.Black, leftMargin + 8, guestY);

        // Bill Info
        float billInfoX = leftMargin + halfWidth + 15;
        var billRect = new RectangleF(billInfoX, currentY, halfWidth, 90);
        g.DrawRectangle(penLight, billRect.X, billRect.Y, billRect.Width, billRect.Height);
        g.FillRectangle(brushHeaderBg, billInfoX, currentY, halfWidth, 24);
        g.DrawRectangle(penLight, billInfoX, currentY, halfWidth, 24);
        g.DrawString("รายละเอียดรอบบิล / Billing Info", fontHeader, brushDark, billInfoX + 8, currentY + 3);

        float billY = currentY + 30;
        g.DrawString($"ห้องพัก: {_bill.RoomNumber}", fontBodyBold, Brushes.Black, billInfoX + 8, billY);
        g.DrawString($"ประจำเดือน: {_bill.BillingMonth}", fontBodyBold, Brushes.DarkBlue, billInfoX + 150, billY);
        billY += 20;
        g.DrawString($"สถานะ: {(_bill.IsPaid ? "✅ ชำระแล้ว" : "❌ ยังไม่ชำระ")}", fontBodyBold, _bill.IsPaid ? Brushes.DarkGreen : Brushes.DarkRed, billInfoX + 8, billY);

        currentY += 105;

        // 4. Combined Financial Items Table (บิลรวมใบเดียว)
        float col1X = leftMargin;
        float col2X = leftMargin + 220;
        float col3X = leftMargin + 320;
        float col4X = leftMargin + 420;
        float col5X = rightMargin - 120;

        var tableHeaderRect = new RectangleF(leftMargin, currentY, contentWidth, 28);
        g.FillRectangle(brushHeaderBg, tableHeaderRect);
        g.DrawRectangle(penLight, tableHeaderRect.X, tableHeaderRect.Y, tableHeaderRect.Width, tableHeaderRect.Height);

        g.DrawString("รายการค่าใช้จ่าย", fontHeader, brushDark, col1X + 8, currentY + 4);
        g.DrawString("เลขก่อน", fontHeader, brushDark, col2X, currentY + 4);
        g.DrawString("เลขหลัง", fontHeader, brushDark, col3X, currentY + 4);
        g.DrawString("หน่วย/จำนวน", fontHeader, brushDark, col4X, currentY + 4);
        g.DrawString("จำนวนเงิน (฿)", fontHeader, brushDark, col5X, currentY + 4);

        currentY += 32;

        // 1) ค่าเช่าห้อง
        g.DrawString("ค่าเช่าห้องพักรายเดือน", fontBodyBold, Brushes.Black, col1X + 8, currentY);
        g.DrawString("-", fontBody, Brushes.Gray, col2X, currentY);
        g.DrawString("-", fontBody, Brushes.Gray, col3X, currentY);
        g.DrawString("1 เดือน", fontBody, Brushes.Black, col4X, currentY);
        g.DrawString($"{_bill.RoomCharge:N2}", fontBodyBold, Brushes.Black, col5X, currentY);
        currentY += 26;

        // 2) ค่าไฟ (ตามมิเตอร์)
        if (_bill.ElectricUnits > 0 || _bill.ElectricAmount > 0)
        {
            g.DrawString($"ค่าไฟฟ้า ({_bill.ElectricRate:N2} ฿/หน่วย)", fontBody, Brushes.Black, col1X + 8, currentY);
            g.DrawString($"{_bill.ElectricPrev:N0}", fontBody, Brushes.Black, col2X, currentY);
            g.DrawString($"{_bill.ElectricCurr:N0}", fontBody, Brushes.Black, col3X, currentY);
            g.DrawString($"{_bill.ElectricUnits:N0} หน่วย", fontBody, Brushes.Black, col4X, currentY);
            g.DrawString($"{_bill.ElectricAmount:N2}", fontBody, Brushes.Black, col5X, currentY);
            currentY += 26;
        }

        // 3) ค่าน้ำ (ตามมิเตอร์ หรือ เหมาจ่าย)
        if (_bill.WaterBillingMode == "FLAT")
        {
            g.DrawString($"ค่าน้ำประปา (เหมาจ่าย {_bill.WaterRate:N2} ฿/คน)", fontBody, Brushes.Black, col1X + 8, currentY);
            g.DrawString("-", fontBody, Brushes.Gray, col2X, currentY);
            g.DrawString("-", fontBody, Brushes.Gray, col3X, currentY);
            g.DrawString($"{_bill.WaterPersonCount} คน", fontBody, Brushes.Black, col4X, currentY);
            g.DrawString($"{_bill.WaterAmount:N2}", fontBody, Brushes.Black, col5X, currentY);
            currentY += 26;
        }
        else if (_bill.WaterUnits > 0 || _bill.WaterAmount > 0)
        {
            g.DrawString($"ค่าน้ำประปา ({_bill.WaterRate:N2} ฿/หน่วย)", fontBody, Brushes.Black, col1X + 8, currentY);
            g.DrawString($"{_bill.WaterPrev:N0}", fontBody, Brushes.Black, col2X, currentY);
            g.DrawString($"{_bill.WaterCurr:N0}", fontBody, Brushes.Black, col3X, currentY);
            g.DrawString($"{_bill.WaterUnits:N0} หน่วย", fontBody, Brushes.Black, col4X, currentY);
            g.DrawString($"{_bill.WaterAmount:N2}", fontBody, Brushes.Black, col5X, currentY);
            currentY += 26;
        }

        // 4) ค่าบริการ/ส่วนกลาง (ถ้ามี)
        if (_bill.CommonAreaFee > 0)
        {
            g.DrawString("ค่าบริการส่วนกลาง", fontBody, Brushes.Black, col1X + 8, currentY);
            g.DrawString("-", fontBody, Brushes.Gray, col2X, currentY);
            g.DrawString("-", fontBody, Brushes.Gray, col3X, currentY);
            g.DrawString("1 เดือน", fontBody, Brushes.Black, col4X, currentY);
            g.DrawString($"{_bill.CommonAreaFee:N2}", fontBody, Brushes.Black, col5X, currentY);
            currentY += 26;
        }

        // 5) ค่าขยะ (ถ้ามี)
        if (_bill.GarbageFee > 0)
        {
            g.DrawString("ค่าจัดเก็บขยะรายเดือน", fontBody, Brushes.Black, col1X + 8, currentY);
            g.DrawString("-", fontBody, Brushes.Gray, col2X, currentY);
            g.DrawString("-", fontBody, Brushes.Gray, col3X, currentY);
            g.DrawString("1 เดือน", fontBody, Brushes.Black, col4X, currentY);
            g.DrawString($"{_bill.GarbageFee:N2}", fontBody, Brushes.Black, col5X, currentY);
            currentY += 26;
        }

        // 6) ค่าอื่นๆ (ถ้ามี)
        if (_bill.ExtraCharges > 0)
        {
            g.DrawString("ค่าบริการเพิ่มเติมอื่นๆ", fontBody, Brushes.Black, col1X + 8, currentY);
            g.DrawString("-", fontBody, Brushes.Gray, col2X, currentY);
            g.DrawString("-", fontBody, Brushes.Gray, col3X, currentY);
            g.DrawString("-", fontBody, Brushes.Gray, col4X, currentY);
            g.DrawString($"{_bill.ExtraCharges:N2}", fontBody, Brushes.Black, col5X, currentY);
            currentY += 26;
        }

        // 7) ส่วนลด (ถ้ามี)
        if (_bill.DiscountAmount > 0)
        {
            g.DrawString("ส่วนลดพิเศษ", fontBody, Brushes.DarkRed, col1X + 8, currentY);
            g.DrawString("-", fontBody, Brushes.Gray, col2X, currentY);
            g.DrawString("-", fontBody, Brushes.Gray, col3X, currentY);
            g.DrawString("-", fontBody, Brushes.Gray, col4X, currentY);
            g.DrawString($"-{_bill.DiscountAmount:N2}", fontBody, Brushes.DarkRed, col5X, currentY);
            currentY += 26;
        }

        g.DrawLine(penDark, leftMargin, currentY, rightMargin, currentY);
        currentY += 12;

        // Total Box (รวมสุทธิ)
        var totalRect = new RectangleF(rightMargin - 320, currentY, 320, 42);
        g.FillRectangle(new SolidBrush(Color.FromArgb(240, 253, 244)), totalRect);
        g.DrawRectangle(new Pen(Color.ForestGreen, 1.5F), totalRect.X, totalRect.Y, totalRect.Width, totalRect.Height);

        g.DrawString("ยอดรวมทั้งสิ้น (TOTAL DUE):", fontHeader, Brushes.DarkGreen, rightMargin - 305, currentY + 10);
        g.DrawString($"{_bill.TotalAmount:N2} บาท", new Font("Segoe UI", 13F, FontStyle.Bold), Brushes.DarkGreen, rightMargin - 130, currentY + 8);

        currentY += 60;

        // Payment QR Code (if configured)
        if (!string.IsNullOrEmpty(_settings.QrCodeImagePath) && File.Exists(_settings.QrCodeImagePath))
        {
            try
            {
                using var qrImg = Image.FromFile(_settings.QrCodeImagePath);
                float scale = Math.Min(90f / qrImg.Width, 90f / qrImg.Height);
                float drawW = qrImg.Width * scale;
                float drawH = qrImg.Height * scale;
                g.DrawImage(qrImg, leftMargin + 10, currentY, drawW, drawH);
                g.DrawString("สแกนจ่ายเงินผ่าน PromptPay", fontSmall, Brushes.DimGray, leftMargin + 10, currentY + drawH + 4);
            }
            catch { }
        }

        // Signatures
        float sigBoxWidth = (contentWidth - 140) / 2;

        float sig1X = leftMargin + 120;
        var sig1Rect = new RectangleF(sig1X, currentY, sigBoxWidth, 110);
        g.DrawRectangle(penLight, sig1Rect.X, sig1Rect.Y, sig1Rect.Width, sig1Rect.Height);
        g.DrawString("ผู้รับใบแจ้งหนี้ (ผู้เช่า)", fontHeader, brushDark, sig1X + 10, currentY + 6);
        g.DrawLine(penLight, sig1X + 15, currentY + 65, sig1X + sigBoxWidth - 15, currentY + 65);
        g.DrawString($"({(_customer?.FullName ?? "_________________________")})", fontSmall, Brushes.DimGray, sig1X + 15, currentY + 72);
        g.DrawString("วันที่: _____ / _____ / ________", fontSmall, Brushes.DimGray, sig1X + 15, currentY + 88);

        float sig2X = sig1X + sigBoxWidth + 15;
        var sig2Rect = new RectangleF(sig2X, currentY, sigBoxWidth, 110);
        g.DrawRectangle(penLight, sig2Rect.X, sig2Rect.Y, sig2Rect.Width, sig2Rect.Height);
        g.DrawString("ผู้ออกใบแจ้งหนี้ (เจ้าของ/เจ้าหน้าที่)", fontHeader, brushDark, sig2X + 10, currentY + 6);
        g.DrawLine(penLight, sig2X + 15, currentY + 65, sig2X + sigBoxWidth - 15, currentY + 65);
        g.DrawString($"({_staffName})", fontSmall, Brushes.DimGray, sig2X + 50, currentY + 72);
        g.DrawString("วันที่: _____ / _____ / ________", fontSmall, Brushes.DimGray, sig2X + 15, currentY + 88);

        currentY += 125;

        // Footer Note
        string footerMsg = !string.IsNullOrWhiteSpace(_settings.BillFooter) ? _settings.BillFooter : "กรุณาชำระเงินภายในวันที่ 5 ของทุกเดือน ขอบคุณครับ";
        g.DrawString(footerMsg, new Font("Segoe UI", 9.5F, FontStyle.Bold), Brushes.DarkSlateBlue, leftMargin + (contentWidth / 2) - 160, currentY);
    }
}
