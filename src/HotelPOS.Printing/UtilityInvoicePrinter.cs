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

        string targetPrinter = !string.IsNullOrWhiteSpace(printerName) ? printerName : (_settings.PrinterName ?? "");
        if (!string.IsNullOrWhiteSpace(targetPrinter))
        {
            printDoc.PrinterSettings.PrinterName = targetPrinter;
        }

        if (_settings.PaperType == "80mm")
        {
            int estimatedHeight = CalculateEstimatedHeight(80);
            printDoc.DefaultPageSettings.PaperSize = new System.Drawing.Printing.PaperSize("Receipt80", 283, estimatedHeight);
            printDoc.DefaultPageSettings.Margins = new Margins(10, 10, 10, 10);
        }
        else if (_settings.PaperType == "58mm")
        {
            int estimatedHeight = CalculateEstimatedHeight(58);
            printDoc.DefaultPageSettings.PaperSize = new System.Drawing.Printing.PaperSize("Receipt58", 204, estimatedHeight);
            printDoc.DefaultPageSettings.Margins = new Margins(5, 5, 5, 5);
        }
        else
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
        
        if (!string.IsNullOrWhiteSpace(_settings.PrinterName))
        {
            printDoc.PrinterSettings.PrinterName = _settings.PrinterName;
        }

        string paperLabel = "A4";
        if (_settings.PaperType == "80mm")
        {
            int estimatedHeight = CalculateEstimatedHeight(80);
            printDoc.DefaultPageSettings.PaperSize = new System.Drawing.Printing.PaperSize("Receipt80", 283, estimatedHeight);
            printDoc.DefaultPageSettings.Margins = new Margins(10, 10, 10, 10);
            paperLabel = "80mm";
        }
        else if (_settings.PaperType == "58mm")
        {
            int estimatedHeight = CalculateEstimatedHeight(58);
            printDoc.DefaultPageSettings.PaperSize = new System.Drawing.Printing.PaperSize("Receipt58", 204, estimatedHeight);
            printDoc.DefaultPageSettings.Margins = new Margins(5, 5, 5, 5);
            paperLabel = "58mm";
        }
        else
        {
            printDoc.DefaultPageSettings.PaperSize = new System.Drawing.Printing.PaperSize("A4", 827, 1169);
            printDoc.DefaultPageSettings.Margins = new Margins(40, 40, 40, 40);
        }

        printDoc.PrintPage += PrintDoc_PrintPage;

        using var previewDlg = new PrintPreviewDialog
        {
            Document = printDoc,
            Width = 960,
            Height = 720,
            StartPosition = FormStartPosition.CenterScreen,
            Text = $"ตัวอย่างก่อนพิมพ์ - ใบแจ้งหนี้ประจำเดือน {_bill.BillingMonth} ({paperLabel})"
        };

        if (previewDlg.Controls.Count > 1 && previewDlg.Controls[1] is ToolStrip toolStrip)
        {
            try
            {
                var printButton = toolStrip.Items[0];
                if (printButton != null)
                {
                    printButton.Visible = false;

                    var customPrintBtn = new ToolStripButton
                    {
                        Image = printButton.Image,
                        ToolTipText = "พิมพ์ (เลือกเครื่องพิมพ์)"
                    };
                    customPrintBtn.Click += (s, ev) =>
                    {
                        using var printDlg = new PrintDialog
                        {
                            Document = printDoc,
                            UseEXDialog = true
                        };
                        if (printDlg.ShowDialog() == DialogResult.OK)
                        {
                            printDoc.Print();
                            previewDlg.Close();
                        }
                    };
                    toolStrip.Items.Insert(0, customPrintBtn);
                }
            }
            catch { }
        }

        previewDlg.ShowDialog();
    }

    private int CalculateEstimatedHeight(int paperWidthMm)
    {
        float scale = (paperWidthMm == 58) ? 0.75f : 1.0f;
        float height = 20f; // Top padding

        if (!string.IsNullOrEmpty(_settings.LogoImagePath) && File.Exists(_settings.LogoImagePath))
        {
            height += 58f;
        }

        string shopName = string.IsNullOrWhiteSpace(_settings.ShopName) ? "ชื่อร้าน/ที่พักของคุณ" : _settings.ShopName;
        height += 24f * scale; // Shop name

        if (!string.IsNullOrEmpty(_settings.ShopAddress) && _settings.ShopAddress != "-")
        {
            using var bmpTemp = new Bitmap(1, 1);
            using var gTemp = Graphics.FromImage(bmpTemp);
            var fontSmall = new Font("Segoe UI", 7.5F * scale, FontStyle.Regular);
            float printableWidth = (paperWidthMm == 58) ? 180f : 260f;
            var size = gTemp.MeasureString(_settings.ShopAddress, fontSmall, (int)printableWidth);
            height += size.Height + 4f;
        }

        string contactInfo = "";
        if (!string.IsNullOrEmpty(_settings.ShopPhone) && _settings.ShopPhone != "-") contactInfo += $"โทร: {_settings.ShopPhone} ";
        if (!string.IsNullOrEmpty(_settings.ShopTaxId) && _settings.ShopTaxId != "-") contactInfo += $"TAX: {_settings.ShopTaxId}";
        if (!string.IsNullOrEmpty(contactInfo))
        {
            height += 18f * scale + 4f;
        }

        height += 50f * scale; // Title box/line

        height += 18f * scale * 5; // Metadata (Bill No, Room, Month, Guest, Status)
        height += 10f;

        height += 22f * scale; // Table Header

        int itemCount = 1; // Room charges is always printed
        if (_bill.ElectricUnits > 0 || _bill.ElectricAmount > 0) itemCount++;
        if (_bill.WaterAmount > 0) itemCount++;
        if (_bill.CommonAreaFee > 0) itemCount++;
        if (_bill.GarbageFee > 0) itemCount++;
        if (_bill.ExtraCharges > 0) itemCount++;
        if (_bill.DiscountAmount > 0) itemCount++;
        height += itemCount * 18f * scale;
        height += 10f; // separators

        height += 30f * scale; // Totals
        height += 10f;

        if (!string.IsNullOrEmpty(_settings.QrCodeImagePath) && File.Exists(_settings.QrCodeImagePath))
        {
            float qrSize = (paperWidthMm == 58) ? 90f : 110f;
            height += qrSize + 25f;
        }

        if (!string.IsNullOrEmpty(_settings.LobbyTerms))
        {
            height += 20f * scale;
            using var bmpTemp = new Bitmap(1, 1);
            using var gTemp = Graphics.FromImage(bmpTemp);
            var fontSmall = new Font("Segoe UI", 7.5F * scale, FontStyle.Regular);
            float printableWidth = (paperWidthMm == 58) ? 180f : 260f;
            foreach (var termLine in _settings.LobbyTerms.Split('\n'))
            {
                var size = gTemp.MeasureString(termLine, fontSmall, (int)printableWidth);
                height += size.Height + 2f;
            }
        }

        height += 25f * scale + 10f; // Footer

        int feedLines = _settings.PrinterFeedLines;
        height += feedLines * 15f; // Extra Spacing

        return (int)Math.Ceiling(height);
    }

    private void PrintDoc_PrintPage(object sender, PrintPageEventArgs e)
    {
        var g = e.Graphics;
        if (g == null) return;

        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        if (_settings.PaperType == "80mm" || _settings.PaperType == "58mm")
        {
            string paperType = _settings.PaperType;
            int paperWidthMm = (paperType == "58mm") ? 58 : 80;
            int paperWidthUnits = (paperType == "58mm") ? 204 : 283;
            int estimatedHeightUnits = CalculateEstimatedHeight(paperWidthMm);

            // Rasterize to memory bitmap at 203 DPI to bypass buggy Chinese printer drivers and map fonts cleanly
            float dpi = 203f;
            int widthPixels = (paperType == "58mm") ? 384 : 576;
            int heightPixels = (int)Math.Ceiling(estimatedHeightUnits * (dpi / 100f));

            using var bmp = new Bitmap(widthPixels, heightPixels);
            using (var bg = Graphics.FromImage(bmp))
            {
                bg.Clear(Color.White);
                bg.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                bg.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                // Scale drawing units from 100ths of inch to 203 DPI pixel grid
                float transformScale = dpi / 100f;
                bg.ScaleTransform(transformScale, transformScale);

                float printableWidth = (paperType == "58mm") ? 194f : 263f;
                float leftMarg = (paperType == "58mm") ? 5f : 10f;
                float rightMarg = leftMarg + printableWidth;

                RenderReceiptLayout(bg, printableWidth, leftMarg, rightMarg, 15f, paperType);
            }

            g.DrawImage(bmp, e.MarginBounds.Left, e.MarginBounds.Top, paperWidthUnits, estimatedHeightUnits);
        }
        else
        {
            RenderA4Layout(g, e);
        }
    }

    private void RenderReceiptLayout(Graphics g, float contentWidth, float leftMargin, float rightMargin, float topMargin, string paperType)
    {
        float scale = (paperType == "58mm") ? 0.75f : 1.0f;
        // ใช้ Tahoma เพราะรองรับ Thai complex script ได้ดีกว่า Segoe UI บนเครื่องพิมพ์ Thermal
        var fontTitle = new Font("Tahoma", 12F * scale, FontStyle.Bold);
        var fontSubtitle = new Font("Tahoma", 9.5F * scale, FontStyle.Bold);
        var fontBody = new Font("Tahoma", 8.5F * scale, FontStyle.Regular);
        var fontBodyBold = new Font("Tahoma", 8.5F * scale, FontStyle.Bold);
        var fontSmall = new Font("Tahoma", 7.5F * scale, FontStyle.Regular);

        try
        {

        float y = topMargin;
        float center = leftMargin + contentWidth / 2;

        float DrawLeftRight(string left, string right, Font fLeft, Font fRight, float drawY)
        {
            return PrintLayoutHelper.DrawLeftRight(g, left, right, fLeft, fRight, drawY, leftMargin, rightMargin, contentWidth, scale);
        }

        // 1. Logo
        using var logoImg = PrintLayoutHelper.LoadImageSafe(_settings.LogoImagePath);
        if (logoImg != null)
        {
            float maxW = (paperType == "58mm") ? 90f : 120f;
            float maxH = 50f;
            float scaleImg = Math.Min(maxW / logoImg.Width, maxH / logoImg.Height);
            float drawW = logoImg.Width * scaleImg;
            float drawH = logoImg.Height * scaleImg;
            g.DrawImage(logoImg, center - drawW / 2, y, drawW, drawH);
            y += drawH + 8;
        }

        // 2. Shop Header
        var sfCenter = new StringFormat { Alignment = StringAlignment.Center };
        string shopName = string.IsNullOrWhiteSpace(_settings.ShopName) ? "ชื่อร้าน/ที่พักของคุณ" : _settings.ShopName;
        g.DrawString(shopName, fontTitle, Brushes.Black, new RectangleF(leftMargin, y, contentWidth, 40), sfCenter);
        y += 22 * scale + 4;

        if (!string.IsNullOrEmpty(_settings.ShopAddress) && _settings.ShopAddress != "-")
        {
            var size = g.MeasureString(_settings.ShopAddress, fontSmall, (int)contentWidth);
            g.DrawString(_settings.ShopAddress, fontSmall, Brushes.DimGray, new RectangleF(leftMargin, y, contentWidth, size.Height), sfCenter);
            y += size.Height + 2;
        }

        string contactInfo = "";
        if (!string.IsNullOrEmpty(_settings.ShopPhone) && _settings.ShopPhone != "-") contactInfo += $"โทร: {_settings.ShopPhone} ";
        if (!string.IsNullOrEmpty(_settings.ShopTaxId) && _settings.ShopTaxId != "-") contactInfo += $"TAX: {_settings.ShopTaxId}";
        if (!string.IsNullOrEmpty(contactInfo))
        {
            g.DrawString(contactInfo, fontSmall, Brushes.DimGray, new RectangleF(leftMargin, y, contentWidth, 30), sfCenter);
            y += 18 * scale + 4;
        }

        // 3. Document Title
        g.DrawLine(Pens.Black, leftMargin, y, rightMargin, y);
        y += 6;
        g.DrawString("ใบแจ้งหนี้ค่าเช่าและค่าบริการ", fontSubtitle, Brushes.Black, new RectangleF(leftMargin, y, contentWidth, 30), sfCenter);
        y += 20 * scale + 4;
        g.DrawLine(Pens.Black, leftMargin, y, rightMargin, y);
        y += 8;

        // 4. Metadata
        y += DrawLeftRight("เลขที่บิล:", _bill.BillCode ?? "", fontBody, fontBodyBold, y) + 2;

        y += DrawLeftRight("ห้องพัก:", _bill.RoomNumber ?? "", fontBody, fontBodyBold, y) + 2;

        y += DrawLeftRight("ประจำเดือน:", _bill.BillingMonth ?? "", fontBody, fontBody, y) + 2;

        if (_customer != null)
        {
            y += DrawLeftRight("ผู้เช่า:", _customer.FullName ?? "", fontBody, fontBody, y) + 2;
        }

        y += DrawLeftRight("สถานะ:", _bill.IsPaid ? "ชำระแล้ว" : "ยังไม่ชำระ", fontBody, fontBodyBold, y) + 2;

        g.DrawLine(Pens.LightGray, leftMargin, y, rightMargin, y);
        y += 6;

        // 5. Items List
        g.DrawString("รายการค่าใช้จ่าย", fontBodyBold, Brushes.Black, leftMargin, y);
        var priceTitleSize = g.MeasureString("จำนวนเงิน", fontBodyBold);
        g.DrawString("จำนวนเงิน", fontBodyBold, Brushes.Black, rightMargin - priceTitleSize.Width, y);
        y += 20 * scale;

        y += DrawLeftRight("ค่าเช่าห้องพักรายเดือน", $"{_bill.RoomCharge:N2}", fontBody, fontBodyBold, y) + 2;

        if (_bill.ElectricUnits > 0 || _bill.ElectricAmount > 0)
        {
            y += DrawLeftRight($"ค่าไฟ ({_bill.ElectricPrev:N0}->{_bill.ElectricCurr:N0} = {_bill.ElectricUnits:N0} หน่วย)", $"{_bill.ElectricAmount:N2}", fontBody, fontBody, y) + 2;
        }

        if (_bill.WaterBillingMode == "FLAT")
        {
            y += DrawLeftRight($"ค่าน้ำ (เหมาจ่าย {_bill.WaterRate:N2} ฿/คน x {_bill.WaterPersonCount} คน)", $"{_bill.WaterAmount:N2}", fontBody, fontBody, y) + 2;
        }
        else if (_bill.WaterUnits > 0 || _bill.WaterAmount > 0)
        {
            y += DrawLeftRight($"ค่าน้ำ ({_bill.WaterPrev:N0}->{_bill.WaterCurr:N0} = {_bill.WaterUnits:N0} หน่วย)", $"{_bill.WaterAmount:N2}", fontBody, fontBody, y) + 2;
        }

        if (_bill.CommonAreaFee > 0)
        {
            y += DrawLeftRight("ค่าบริการส่วนกลาง", $"{_bill.CommonAreaFee:N2}", fontBody, fontBody, y) + 2;
        }

        if (_bill.GarbageFee > 0)
        {
            y += DrawLeftRight("ค่าจัดเก็บขยะรายเดือน", $"{_bill.GarbageFee:N2}", fontBody, fontBody, y) + 2;
        }

        if (_bill.ExtraCharges > 0)
        {
            y += DrawLeftRight("ค่าบริการอื่นๆ", $"{_bill.ExtraCharges:N2}", fontBody, fontBody, y) + 2;
        }

        if (_bill.DiscountAmount > 0)
        {
            y += DrawLeftRight("ส่วนลดพิเศษ", $"-{_bill.DiscountAmount:N2}", fontBody, fontBody, y) + 2;
        }

        g.DrawLine(Pens.LightGray, leftMargin, y, rightMargin, y);
        y += 6;

        // 6. Grand Total
        y += DrawLeftRight("ยอดรวมทั้งสิ้น (TOTAL):", $"{_bill.TotalAmount:N2} บาท", fontSubtitle, fontSubtitle, y) + 4;
        y += 24 * scale;
        g.DrawLine(Pens.Black, leftMargin, y, rightMargin, y);
        y += 8;

        // 7. QR Code
        using var qrImg = PrintLayoutHelper.LoadImageSafe(_settings.QrCodeImagePath);
        if (qrImg != null)
        {
            float maxW = (paperType == "58mm") ? 90f : 110f;
            g.DrawImage(qrImg, center - maxW / 2, y, maxW, maxW);
            y += maxW + 4;
            g.DrawString("สแกนจ่าย PromptPay", fontSmall, Brushes.DimGray, new RectangleF(leftMargin, y, contentWidth, 20), sfCenter);
            y += 18 * scale + 10;
        }

        // 8. Lobby Terms
        if (!string.IsNullOrEmpty(_settings.LobbyTerms))
        {
            g.DrawString("ข้อตกลงและเงื่อนไข:", fontBodyBold, Brushes.Black, leftMargin, y);
            y += 18 * scale;
            foreach (var termLine in _settings.LobbyTerms.Split('\n'))
            {
                var size = g.MeasureString(termLine, fontSmall, (int)contentWidth);
                g.DrawString(termLine, fontSmall, Brushes.DarkSlateGray, new RectangleF(leftMargin, y, contentWidth, size.Height));
                y += size.Height + 1;
            }
            y += 8;
        }

        // 9. Footer Message
        string footerMsg = !string.IsNullOrWhiteSpace(_settings.BillFooter) ? _settings.BillFooter : "ขอบคุณที่ใช้บริการ / Thank you";
        g.DrawString(footerMsg, fontSubtitle, Brushes.Black, new RectangleF(leftMargin, y, contentWidth, 30), sfCenter);
        y += 20 * scale;
        }
        finally
        {
            fontTitle.Dispose();
            fontSubtitle.Dispose();
            fontBody.Dispose();
            fontBodyBold.Dispose();
            fontSmall.Dispose();
        }
    }

    private void RenderA4Layout(Graphics g, PrintPageEventArgs e)
    {
        // Fonts (ใช้ Tahoma สำหรับความเข้ากันได้กับภาษาไทย Thai complex script)
        var fontTitle = new Font("Tahoma", 20F, FontStyle.Bold);
        var fontSubtitle = new Font("Tahoma", 13.5F, FontStyle.Bold);
        var fontHeader = new Font("Tahoma", 11.5F, FontStyle.Bold);
        var fontBody = new Font("Tahoma", 10.5F, FontStyle.Regular);
        var fontBodyBold = new Font("Tahoma", 10.5F, FontStyle.Bold);
        var fontSmall = new Font("Tahoma", 9.5F, FontStyle.Regular);
        var fontSigHeader = new Font("Tahoma", 9.5F, FontStyle.Bold);

        // Pens & Brushes
        var penDark = new Pen(Color.FromArgb(30, 41, 59), 1.5F);
        var penLight = new Pen(Color.FromArgb(203, 213, 225), 1F);
        var brushDark = new SolidBrush(Color.FromArgb(15, 23, 42));
        var brushHeaderBg = new SolidBrush(Color.FromArgb(241, 245, 249));

        try
        {

        float leftMargin = e.MarginBounds.Left;
        float rightMargin = e.MarginBounds.Right;
        float contentWidth = rightMargin - leftMargin;
        float currentY = e.MarginBounds.Top;

        // 1. Header (Logo & Shop Details)
        float logoOffsetX = leftMargin;
        using var logoImg = PrintLayoutHelper.LoadImageSafe(_settings.LogoImagePath);
        if (logoImg != null)
        {
            float scale = Math.Min(140f / logoImg.Width, 55f / logoImg.Height);
            float drawW = logoImg.Width * scale;
            float drawH = logoImg.Height * scale;
            g.DrawImage(logoImg, leftMargin, currentY, drawW, drawH);
            logoOffsetX += drawW + 15;
        }

        string shopName = string.IsNullOrWhiteSpace(_settings.ShopName) ? "ชื่อร้าน/ที่พักของคุณ" : _settings.ShopName;
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
        using (var sfTitleRight = new StringFormat { Alignment = StringAlignment.Far })
        {
            g.DrawString($"เลขที่บิล: {_bill.BillCode}", fontBodyBold, brushDark, new RectangleF(leftMargin, currentY + 10, contentWidth - 12, 30), sfTitleRight);
        }
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
        currentY += 15;

        // Total Box (Large Green Callout Box)
        var totalRect = new RectangleF(rightMargin - 360, currentY, 360, 46);
        g.FillRectangle(new SolidBrush(Color.FromArgb(240, 253, 244)), totalRect);
        g.DrawRectangle(new Pen(Color.ForestGreen, 1.5F), totalRect.X, totalRect.Y, totalRect.Width, totalRect.Height);

        var sfLeft = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
        var sfRight = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };

        g.DrawString("ยอดรวมทั้งสิ้น (TOTAL DUE):", fontHeader, Brushes.DarkGreen, new RectangleF(rightMargin - 345, currentY, 210, 46), sfLeft);
        g.DrawString($"{_bill.TotalAmount:N2} บาท", new Font("Segoe UI", 13.5F, FontStyle.Bold), Brushes.DarkGreen, new RectangleF(rightMargin - 360, currentY, 345, 46), sfRight);

        currentY += 75;

        // 5. Lobby Terms & Special Agreements Section (ข้อตกลงและเงื่อนไขหน้าเคาน์เตอร์/ล็อบบี้)
        if (!string.IsNullOrWhiteSpace(_settings.LobbyTerms))
        {
            var termsRect = new RectangleF(leftMargin, currentY, contentWidth, 100);
            g.FillRectangle(new SolidBrush(Color.FromArgb(248, 250, 252)), termsRect);
            g.DrawRectangle(penLight, termsRect.X, termsRect.Y, termsRect.Width, termsRect.Height);

            g.DrawString("ข้อตกลงและเงื่อนไขหน้าล็อบบี้ / Lobby Terms & Conditions", fontHeader, brushDark, leftMargin + 10, currentY + 8);

            var sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Near };
            g.DrawString(_settings.LobbyTerms, fontSmall, Brushes.DarkSlateGray,
                new RectangleF(leftMargin + 12, currentY + 34, contentWidth - 24, 60), sf);

            currentY += 120;
        }

        // 6. QR Code & Signatures Container
        using var qrImgA4 = PrintLayoutHelper.LoadImageSafe(_settings.QrCodeImagePath);
        if (qrImgA4 != null)
        {
            float scale = Math.Min(90f / qrImgA4.Width, 90f / qrImgA4.Height);
            float drawW = qrImgA4.Width * scale;
            float drawH = qrImgA4.Height * scale;
            g.DrawImage(qrImgA4, leftMargin + 10, currentY, drawW, drawH);
            g.DrawString("สแกนชำระเงิน PromptPay", fontSmall, Brushes.DimGray, leftMargin + 10, currentY + drawH + 4);
        }

        bool showSignature = _settings.ShowSignatureBox;
        if (showSignature)
        {
            float sigBoxWidth = (contentWidth - 140) / 2;

            // Left Signature Box (Tenant)
            float sig1X = leftMargin + 130;
            var sig1Rect = new RectangleF(sig1X, currentY, sigBoxWidth, 130);
            g.DrawRectangle(penLight, sig1Rect.X, sig1Rect.Y, sig1Rect.Width, sig1Rect.Height);

            g.DrawString("ลงลายมือชื่อผู้เช่า / Tenant Signature", fontSigHeader, brushDark, sig1X + 10, currentY + 8);
            g.DrawLine(penLight, sig1X + 15, currentY + 75, sig1X + sigBoxWidth - 15, currentY + 75);
            g.DrawString($"({(_customer?.FullName ?? "_________________________")})", fontSmall, Brushes.DimGray, sig1X + 20, currentY + 85);
            g.DrawString("วันที่ / Date: _____ / _____ / ________", fontSmall, Brushes.DimGray, sig1X + 20, currentY + 105);

            // Right Signature Box (Staff)
            float sig2X = sig1X + sigBoxWidth + 15;
            var sig2Rect = new RectangleF(sig2X, currentY, sigBoxWidth, 130);
            g.DrawRectangle(penLight, sig2Rect.X, sig2Rect.Y, sig2Rect.Width, sig2Rect.Height);

            g.DrawString("ลงลายมือชื่อผู้รับเงิน / Receiver Signature", fontSigHeader, brushDark, sig2X + 10, currentY + 8);
            g.DrawLine(penLight, sig2X + 15, currentY + 75, sig2X + sigBoxWidth - 15, currentY + 75);
            g.DrawString($"({_staffName})", fontSmall, Brushes.DimGray, sig2X + 45, currentY + 85);
            g.DrawString("วันที่ / Date: _____ / _____ / ________", fontSmall, Brushes.DimGray, sig2X + 20, currentY + 105);

            currentY += 150;
        }
        else
        {
            currentY += 20;
        }

        // 7. Footer Note
        string footerMsg = !string.IsNullOrWhiteSpace(_settings.BillFooter) ? _settings.BillFooter : "ขอบคุณที่ใช้บริการ / Thank you for staying with us";
        g.DrawString(footerMsg, fontBodyBold, Brushes.DarkSlateBlue, leftMargin + (contentWidth / 2) - 150, currentY);
        }
        finally
        {
            fontTitle.Dispose();
            fontSubtitle.Dispose();
            fontHeader.Dispose();
            fontBody.Dispose();
            fontBodyBold.Dispose();
            fontSmall.Dispose();
            fontSigHeader.Dispose();
            penDark.Dispose();
            penLight.Dispose();
            brushDark.Dispose();
            brushHeaderBg.Dispose();
        }
    }
}
