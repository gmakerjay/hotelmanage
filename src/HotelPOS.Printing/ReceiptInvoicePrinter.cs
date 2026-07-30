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
    private readonly UtilityBill? _utilityBill;

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
        string staffName = "พนักงานหน้าเคาน์เตอร์",
        UtilityBill? utilityBill = null)
    {
        _booking = booking;
        _room = room;
        _customer = customer;
        _folio = folio;
        _settings = settings;
        _staffName = staffName;
        _utilityBill = utilityBill;

        _shopName = string.IsNullOrWhiteSpace(settings?.ShopName) ? "ชื่อร้าน/ที่พักของคุณ" : settings.ShopName;
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
        SystemSettingsDto? settings = null,
        UtilityBill? utilityBill = null)
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

        string targetPrinter = !string.IsNullOrWhiteSpace(printerName) ? printerName : (_settings?.PrinterName ?? "");
        if (!string.IsNullOrWhiteSpace(targetPrinter))
        {
            printDoc.PrinterSettings.PrinterName = targetPrinter;
        }

        if (_settings?.PaperType == "80mm")
        {
            int estimatedHeight = CalculateEstimatedHeight(80);
            printDoc.DefaultPageSettings.PaperSize = new System.Drawing.Printing.PaperSize("Receipt80", 283, estimatedHeight);
            printDoc.DefaultPageSettings.Margins = new Margins(10, 10, 10, 10);
        }
        else if (_settings?.PaperType == "58mm")
        {
            int estimatedHeight = CalculateEstimatedHeight(58);
            printDoc.DefaultPageSettings.PaperSize = new System.Drawing.Printing.PaperSize("Receipt58", 204, estimatedHeight);
            printDoc.DefaultPageSettings.Margins = new Margins(5, 5, 5, 5);
        }
        else
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
        
        if (!string.IsNullOrWhiteSpace(_settings?.PrinterName))
        {
            printDoc.PrinterSettings.PrinterName = _settings.PrinterName;
        }

        string paperLabel = "A4";
        if (_settings?.PaperType == "80mm")
        {
            int estimatedHeight = CalculateEstimatedHeight(80);
            printDoc.DefaultPageSettings.PaperSize = new System.Drawing.Printing.PaperSize("Receipt80", 283, estimatedHeight);
            printDoc.DefaultPageSettings.Margins = new Margins(10, 10, 10, 10);
            paperLabel = "80mm";
        }
        else if (_settings?.PaperType == "58mm")
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
            Text = $"ตัวอย่างก่อนพิมพ์ - ใบเสร็จรับเงิน ({paperLabel})"
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

        if (!string.IsNullOrEmpty(_settings?.LogoImagePath) && File.Exists(_settings.LogoImagePath))
        {
            height += 58f;
        }

        height += 24f * scale; // Shop name

        if (!string.IsNullOrEmpty(_shopAddress) && _shopAddress != "-")
        {
            using var bmpTemp = new Bitmap(1, 1);
            using var gTemp = Graphics.FromImage(bmpTemp);
            var fontSmall = new Font("Segoe UI", 7.5F * scale, FontStyle.Regular);
            float printableWidth = (paperWidthMm == 58) ? 180f : 260f;
            var size = gTemp.MeasureString(_shopAddress, fontSmall, (int)printableWidth);
            height += size.Height + 4f;
        }

        string contactInfo = "";
        if (!string.IsNullOrEmpty(_shopPhone) && _shopPhone != "-") contactInfo += $"โทร: {_shopPhone} ";
        if (!string.IsNullOrEmpty(_shopTaxId) && _shopTaxId != "-") contactInfo += $"TAX: {_shopTaxId}";
        if (!string.IsNullOrEmpty(contactInfo))
        {
            height += 18f * scale + 4f;
        }

        height += 50f * scale; // Title box/line

        height += 18f * scale * 4; // Metadata (Bill No, Dates, Room, Guest)
        height += 10f;

        height += 22f * scale; // Table Header

        int itemCount = 1; // Room charges is always printed
        if (_utilityBill != null)
        {
            if (_utilityBill.ElectricUnits > 0 || _utilityBill.ElectricAmount > 0) itemCount++;
            if (_utilityBill.WaterAmount > 0) itemCount++;
            if (_utilityBill.CommonAreaFee > 0) itemCount++;
            if (_utilityBill.GarbageFee > 0) itemCount++;
            if (_utilityBill.ExtraCharges > 0) itemCount++;
            if (_utilityBill.DiscountAmount > 0) itemCount++;
        }
        if ((_folio?.ExtraCharges ?? 0) > 0) itemCount++;
        if ((_folio?.DiscountAmount ?? 0) > 0) itemCount++;
        if (_settings?.EnableVat == true && _settings.VatRate > 0) itemCount++;
        height += itemCount * 18f * scale;
        height += 10f; // separators

        height += 30f * scale; // Totals
        height += 10f;

        if (!string.IsNullOrEmpty(_settings?.QrCodeImagePath) && File.Exists(_settings.QrCodeImagePath))
        {
            float qrSize = (paperWidthMm == 58) ? 90f : 110f;
            height += qrSize + 25f;
        }

        if (!string.IsNullOrEmpty(_settings?.LobbyTerms))
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

        int feedLines = _settings != null ? _settings.PrinterFeedLines : 4;
        height += feedLines * 15f; // Extra Bottom Spacing for clean feed before cut

        return (int)Math.Ceiling(height);
    }

    private void PrintDoc_PrintPage(object sender, PrintPageEventArgs e)
    {
        var g = e.Graphics;
        if (g == null) return;

        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        if (_settings?.PaperType == "80mm" || _settings?.PaperType == "58mm")
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
        using var logoImg = PrintLayoutHelper.LoadImageSafe(_settings?.LogoImagePath);
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
        g.DrawString(_shopName, fontTitle, Brushes.Black, new RectangleF(leftMargin, y, contentWidth, 40), sfCenter);
        y += 22 * scale + 4;

        if (!string.IsNullOrEmpty(_shopAddress) && _shopAddress != "-")
        {
            var size = g.MeasureString(_shopAddress, fontSmall, (int)contentWidth);
            g.DrawString(_shopAddress, fontSmall, Brushes.DimGray, new RectangleF(leftMargin, y, contentWidth, size.Height), sfCenter);
            y += size.Height + 2;
        }

        string contactInfo = "";
        if (!string.IsNullOrEmpty(_shopPhone) && _shopPhone != "-") contactInfo += $"โทร: {_shopPhone} ";
        if (!string.IsNullOrEmpty(_shopTaxId) && _shopTaxId != "-") contactInfo += $"TAX: {_shopTaxId}";
        if (!string.IsNullOrEmpty(contactInfo))
        {
            g.DrawString(contactInfo, fontSmall, Brushes.DimGray, new RectangleF(leftMargin, y, contentWidth, 30), sfCenter);
            y += 18 * scale + 4;
        }

        // 3. Document Title
        g.DrawLine(Pens.Black, leftMargin, y, rightMargin, y);
        y += 6;
        g.DrawString("ใบเสร็จรับเงิน / ใบแจ้งหนี้", fontSubtitle, Brushes.Black, new RectangleF(leftMargin, y, contentWidth, 30), sfCenter);
        y += 20 * scale + 4;
        g.DrawLine(Pens.Black, leftMargin, y, rightMargin, y);
        y += 8;

        // 4. Metadata
        string receiptNo = string.IsNullOrWhiteSpace(_booking.BookingCode) ? $"REC-{_booking.Id:D6}" : _booking.BookingCode;
        y += DrawLeftRight("เลขที่บิล:", receiptNo, fontBody, fontBodyBold, y) + 2;

        var checkInTime = _booking.CheckInActual ?? _booking.CheckInPlanned;
        var checkOutTime = _booking.CheckOutActual ?? DateTime.Now;
        y += DrawLeftRight("วันที่เข้าพัก:", $"{checkInTime:dd/MM/yyyy HH:mm} ถึง {checkOutTime:dd/MM/yyyy HH:mm}", fontBody, fontSmall, y) + 2;

        y += DrawLeftRight("ห้องพัก:", $"{_room.RoomNumber} ({GetRatePlanName(_booking.RatePlan)})", fontBody, fontBodyBold, y) + 2;

        if (_customer != null)
        {
            y += DrawLeftRight("ผู้เข้าพัก:", _customer.FullName, fontBody, fontBody, y) + 2;
        }

        g.DrawLine(Pens.LightGray, leftMargin, y, rightMargin, y);
        y += 6;

        // 5. Items List
        g.DrawString("รายการ", fontBodyBold, Brushes.Black, leftMargin, y);
        var priceTitleSize = g.MeasureString("จำนวนเงิน", fontBodyBold);
        g.DrawString("จำนวนเงิน", fontBodyBold, Brushes.Black, rightMargin - priceTitleSize.Width, y);
        y += 20 * scale;

        decimal roomCharges = _folio?.RoomCharges ?? _booking.AgreedRate;
        decimal extraCharges = _folio?.ExtraCharges ?? 0;
        decimal discountAmount = _folio?.DiscountAmount ?? 0;
        decimal subTotal = Math.Max(0, roomCharges + extraCharges - discountAmount);

        y += DrawLeftRight($"ค่าห้องพัก (ห้อง {_room.RoomNumber})", $"{roomCharges:N2}", fontBody, fontBodyBold, y) + 2;

        if (_utilityBill != null)
        {
            if (_utilityBill.ElectricUnits > 0 || _utilityBill.ElectricAmount > 0)
            {
                y += DrawLeftRight($"ค่าไฟ ({_utilityBill.ElectricUnits:N0} หน่วย)", $"{_utilityBill.ElectricAmount:N2}", fontBody, fontBody, y) + 2;
                subTotal += _utilityBill.ElectricAmount;
            }
            if (_utilityBill.WaterAmount > 0)
            {
                string waterLabel = _utilityBill.WaterBillingMode == "FLAT" ? $"ค่าน้ำ ({_utilityBill.WaterPersonCount} คน)" : $"ค่าน้ำ ({_utilityBill.WaterUnits:N0} หน่วย)";
                y += DrawLeftRight(waterLabel, $"{_utilityBill.WaterAmount:N2}", fontBody, fontBody, y) + 2;
                subTotal += _utilityBill.WaterAmount;
            }
            if (_utilityBill.CommonAreaFee > 0)
            {
                y += DrawLeftRight("ค่าบริการส่วนกลาง", $"{_utilityBill.CommonAreaFee:N2}", fontBody, fontBody, y) + 2;
                subTotal += _utilityBill.CommonAreaFee;
            }
            if (_utilityBill.GarbageFee > 0)
            {
                y += DrawLeftRight("ค่าขยะ", $"{_utilityBill.GarbageFee:N2}", fontBody, fontBody, y) + 2;
                subTotal += _utilityBill.GarbageFee;
            }
            if (_utilityBill.ExtraCharges > 0)
            {
                y += DrawLeftRight("ค่าบริการอื่นๆ", $"{_utilityBill.ExtraCharges:N2}", fontBody, fontBody, y) + 2;
                subTotal += _utilityBill.ExtraCharges;
            }
            if (_utilityBill.DiscountAmount > 0)
            {
                y += DrawLeftRight("ส่วนลดรอบบิล", $"-{_utilityBill.DiscountAmount:N2}", fontBody, fontBody, y) + 2;
                subTotal -= _utilityBill.DiscountAmount;
            }
        }

        if (extraCharges > 0)
        {
            y += DrawLeftRight("ค่าบริการเสริม / มินิบาร์", $"{extraCharges:N2}", fontBody, fontBody, y) + 2;
        }

        if (discountAmount > 0)
        {
            y += DrawLeftRight("ส่วนลดพิเศษ", $"-{discountAmount:N2}", fontBody, fontBody, y) + 2;
        }

        decimal finalTotal = subTotal;
        if (_settings?.EnableVat == true && _settings.VatRate > 0)
        {
            decimal vatAmount = Math.Round(subTotal * (_settings.VatRate / 100m), 2);
            finalTotal = subTotal + vatAmount;

            y += DrawLeftRight($"VAT ({_settings.VatRate:N0}%)", $"{vatAmount:N2}", fontBody, fontBody, y) + 2;
        }

        g.DrawLine(Pens.LightGray, leftMargin, y, rightMargin, y);
        y += 6;

        // 6. Grand Total
        y += DrawLeftRight("ยอดสุทธิ (TOTAL):", $"{finalTotal:N2} บาท", fontSubtitle, fontSubtitle, y) + 4;
        y += 24 * scale;
        g.DrawLine(Pens.Black, leftMargin, y, rightMargin, y);
        y += 8;

        // 7. QR Code
        using var qrImg = PrintLayoutHelper.LoadImageSafe(_settings?.QrCodeImagePath);
        if (qrImg != null)
        {
            float maxW = (paperType == "58mm") ? 90f : 110f;
            g.DrawImage(qrImg, center - maxW / 2, y, maxW, maxW);
            y += maxW + 4;
            g.DrawString("สแกนจ่าย PromptPay", fontSmall, Brushes.DimGray, new RectangleF(leftMargin, y, contentWidth, 20), sfCenter);
            y += 18 * scale + 10;
        }

        // 8. Lobby Terms
        if (!string.IsNullOrEmpty(_settings?.LobbyTerms))
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
        string footerMsg = !string.IsNullOrWhiteSpace(_settings?.BillFooter) ? _settings.BillFooter : "ขอบคุณที่ใช้บริการ / Thank you";
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
        var brushText = Brushes.Black;
        var brushDark = new SolidBrush(Color.FromArgb(15, 23, 42));
        var brushHeaderBg = new SolidBrush(Color.FromArgb(241, 245, 249));

        try
        {

        float leftMargin = e.MarginBounds.Left;
        float rightMargin = e.MarginBounds.Right;
        float contentWidth = rightMargin - leftMargin;
        float currentY = e.MarginBounds.Top;

        // 1. Logo & Header
        float logoOffsetX = leftMargin;
        using var logoImg = PrintLayoutHelper.LoadImageSafe(_settings?.LogoImagePath);
        if (logoImg != null)
        {
            float maxW = 140f;
            float maxH = 55f;
            float scale = Math.Min(maxW / logoImg.Width, maxH / logoImg.Height);
            float drawW = logoImg.Width * scale;
            float drawH = logoImg.Height * scale;

            g.DrawImage(logoImg, leftMargin, currentY, drawW, drawH);
            logoOffsetX += drawW + 15;
        }

        g.DrawString(_shopName, fontTitle, brushDark, logoOffsetX, currentY);
        currentY += 34;

        g.DrawString($"ที่อยู่: {_shopAddress} | โทรศัพท์: {_shopPhone} | เลขประจำตัวผู้เสียภาษี: {_shopTaxId}", fontSmall, Brushes.DimGray, logoOffsetX, currentY);
        currentY += 30;

        g.DrawLine(penDark, leftMargin, currentY, rightMargin, currentY);
        currentY += 20;

        // 2. Document Banner Box
        var rectHeaderBox = new RectangleF(leftMargin, currentY, contentWidth, 42);
        g.FillRectangle(brushHeaderBg, rectHeaderBox);
        g.DrawRectangle(penLight, rectHeaderBox.X, rectHeaderBox.Y, rectHeaderBox.Width, rectHeaderBox.Height);

        g.DrawString("ใบเสร็จรับเงิน / ใบแจ้งหนี้ (RECEIPT / INVOICE)", fontSubtitle, brushDark, leftMargin + 12, currentY + 9);

        string receiptNo = string.IsNullOrWhiteSpace(_booking.BookingCode) ? $"REC-{_booking.Id:D6}" : _booking.BookingCode;
        using (var sfTitleRight = new StringFormat { Alignment = StringAlignment.Far })
        {
            g.DrawString($"เลขที่ใบเสร็จ: {receiptNo}", fontBodyBold, brushDark, new RectangleF(leftMargin, currentY + 11, contentWidth - 12, 30), sfTitleRight);
        }
        currentY += 60;

        // 3. Info Table (Guest Info & Stay Info) - Balanced 120px height
        float halfWidth = (contentWidth - 15) / 2;

        // Guest Info Box
        var guestRect = new RectangleF(leftMargin, currentY, halfWidth, 135);
        g.DrawRectangle(penLight, guestRect.X, guestRect.Y, guestRect.Width, guestRect.Height);
        g.FillRectangle(brushHeaderBg, leftMargin, currentY, halfWidth, 30);
        g.DrawRectangle(penLight, leftMargin, currentY, halfWidth, 30);
        g.DrawString("ข้อมูลผู้เข้าพัก / Guest Details", fontHeader, brushDark, leftMargin + 8, currentY + 5);

        float guestY = currentY + 38;
        g.DrawString($"ชื่อ-นามสกุล: {(_customer != null ? _customer.FullName : "ผู้เข้าพักทั่วไป")}", fontBody, brushText, leftMargin + 8, guestY);
        guestY += 28;
        g.DrawString($"เบอร์โทรศัพท์: {(_customer?.Phone ?? "-")}", fontBody, brushText, leftMargin + 8, guestY);
        guestY += 28;
        g.DrawString($"เลขบัตร/พาสปอร์ต: {(_customer?.IdCardOrPassport ?? "-")}", fontBody, brushText, leftMargin + 8, guestY);

        // Stay Info Box
        float stayX = leftMargin + halfWidth + 15;
        var stayRect = new RectangleF(stayX, currentY, halfWidth, 135);
        g.DrawRectangle(penLight, stayRect.X, stayRect.Y, stayRect.Width, stayRect.Height);
        g.FillRectangle(brushHeaderBg, stayX, currentY, halfWidth, 30);
        g.DrawRectangle(penLight, stayX, currentY, halfWidth, 30);
        g.DrawString("ข้อมูลการเข้าพัก / Stay Details", fontHeader, brushDark, stayX + 8, currentY + 5);

        float stayY = currentY + 38;
        g.DrawString($"ห้องพัก: {_room.RoomNumber} (ชั้น {_room.Floor ?? "-"})", fontBodyBold, brushText, stayX + 8, stayY);
        stayY += 28;

        var checkInTime = _booking.CheckInActual ?? _booking.CheckInPlanned;
        var checkOutTime = _booking.CheckOutActual ?? DateTime.Now;
        g.DrawString($"เช็คอิน: {checkInTime:dd/MM/yyyy HH:mm} น.", fontBody, brushText, stayX + 8, stayY);
        stayY += 28;
        g.DrawString($"เช็คเอาท์: {checkOutTime:dd/MM/yyyy HH:mm} น.", fontBody, brushText, stayX + 8, stayY);

        currentY += 155;

        // 4. Financial Breakdown Table
        float col1X = leftMargin;
        float col2X = leftMargin + 340;
        float col3X = rightMargin - 140;

        // Table Header
        var tableHeaderRect = new RectangleF(leftMargin, currentY, contentWidth, 34);
        g.FillRectangle(brushHeaderBg, tableHeaderRect);
        g.DrawRectangle(penLight, tableHeaderRect.X, tableHeaderRect.Y, tableHeaderRect.Width, tableHeaderRect.Height);

        g.DrawString("รายการ (Description)", fontHeader, brushDark, col1X + 8, currentY + 7);
        g.DrawString("ประเภทราคา", fontHeader, brushDark, col2X, currentY + 7);
        g.DrawString("จำนวนเงิน (บาท)", fontHeader, brushDark, col3X, currentY + 7);

        currentY += 40;

        decimal roomCharges = _folio?.RoomCharges ?? _booking.AgreedRate;
        decimal extraCharges = _folio?.ExtraCharges ?? 0;
        decimal discountAmount = _folio?.DiscountAmount ?? 0;
        decimal subTotal = Math.Max(0, roomCharges + extraCharges - discountAmount);

        // Row 1: Room Charges
        g.DrawString($"ค่าห้องพัก (ห้อง {_room.RoomNumber})", fontBodyBold, brushText, col1X + 8, currentY);
        g.DrawString(GetRatePlanName(_booking.RatePlan), fontBody, brushText, col2X, currentY);
        g.DrawString($"{roomCharges:N2}", fontBodyBold, brushText, col3X, currentY);
        currentY += 34;

        if (_utilityBill != null)
        {
            // 2) ค่าไฟ (ตามมิเตอร์)
            if (_utilityBill.ElectricUnits > 0 || _utilityBill.ElectricAmount > 0)
            {
                g.DrawString($"ค่าไฟฟ้า ({_utilityBill.ElectricRate:N2} ฿/หน่วย: {_utilityBill.ElectricPrev:N0}->{_utilityBill.ElectricCurr:N0})", fontBody, brushText, col1X + 8, currentY);
                g.DrawString($"{_utilityBill.ElectricUnits:N0} หน่วย", fontBody, brushText, col2X, currentY);
                g.DrawString($"{_utilityBill.ElectricAmount:N2}", fontBody, brushText, col3X, currentY);
                currentY += 34;
                subTotal += _utilityBill.ElectricAmount;
            }

            // 3) ค่าน้ำ (ตามมิเตอร์ หรือ เหมาจ่าย)
            if (_utilityBill.WaterBillingMode == "FLAT")
            {
                g.DrawString($"ค่าน้ำประปา (เหมาจ่าย {_utilityBill.WaterRate:N2} ฿/คน)", fontBody, brushText, col1X + 8, currentY);
                g.DrawString($"{_utilityBill.WaterPersonCount} คน", fontBody, brushText, col2X, currentY);
                g.DrawString($"{_utilityBill.WaterAmount:N2}", fontBody, brushText, col3X, currentY);
                currentY += 34;
                subTotal += _utilityBill.WaterAmount;
            }
            else if (_utilityBill.WaterUnits > 0 || _utilityBill.WaterAmount > 0)
            {
                g.DrawString($"ค่าน้ำประปา ({_utilityBill.WaterRate:N2} ฿/หน่วย: {_utilityBill.WaterPrev:N0}->{_utilityBill.WaterCurr:N0})", fontBody, brushText, col1X + 8, currentY);
                g.DrawString($"{_utilityBill.WaterUnits:N0} หน่วย", fontBody, brushText, col2X, currentY);
                g.DrawString($"{_utilityBill.WaterAmount:N2}", fontBody, brushText, col3X, currentY);
                currentY += 34;
                subTotal += _utilityBill.WaterAmount;
            }

            // 4) ค่าบริการ/ส่วนกลาง (ถ้ามี)
            if (_utilityBill.CommonAreaFee > 0)
            {
                g.DrawString("ค่าบริการส่วนกลาง", fontBody, brushText, col1X + 8, currentY);
                g.DrawString("1 เดือน", fontBody, brushText, col2X, currentY);
                g.DrawString($"{_utilityBill.CommonAreaFee:N2}", fontBody, brushText, col3X, currentY);
                currentY += 34;
                subTotal += _utilityBill.CommonAreaFee;
            }

            // 5) ค่าขยะ (ถ้ามี)
            if (_utilityBill.GarbageFee > 0)
            {
                g.DrawString("ค่าจัดเก็บขยะรายเดือน", fontBody, brushText, col1X + 8, currentY);
                g.DrawString("1 เดือน", fontBody, brushText, col2X, currentY);
                g.DrawString($"{_utilityBill.GarbageFee:N2}", fontBody, brushText, col3X, currentY);
                currentY += 34;
                subTotal += _utilityBill.GarbageFee;
            }

            // 6) ค่าอื่นๆ (ถ้ามี)
            if (_utilityBill.ExtraCharges > 0)
            {
                g.DrawString("ค่าบริการเพิ่มเติมอื่นๆ (รอบบิล)", fontBody, brushText, col1X + 8, currentY);
                g.DrawString("-", fontBody, brushText, col2X, currentY);
                g.DrawString($"{_utilityBill.ExtraCharges:N2}", fontBody, brushText, col3X, currentY);
                currentY += 34;
                subTotal += _utilityBill.ExtraCharges;
            }

            // 7) ส่วนลดในบิลน้ำไฟ (ถ้ามี)
            if (_utilityBill.DiscountAmount > 0)
            {
                g.DrawString("ส่วนลดพิเศษ (รอบบิล)", fontBody, Brushes.DarkRed, col1X + 8, currentY);
                g.DrawString("-", fontBody, brushText, col2X, currentY);
                g.DrawString($"-{_utilityBill.DiscountAmount:N2}", fontBody, Brushes.DarkRed, col3X, currentY);
                currentY += 34;
                subTotal -= _utilityBill.DiscountAmount;
            }
        }

        // Row 2: Extra Charges (if any)
        if (extraCharges > 0)
        {
            g.DrawString("ค่าบริการเสริม / สินค้ามินิบาร์เพิ่มเติม", fontBody, brushText, col1X + 8, currentY);
            g.DrawString("-", fontBody, brushText, col2X, currentY);
            g.DrawString($"{extraCharges:N2}", fontBody, brushText, col3X, currentY);
            currentY += 34;
        }

        // Row 3: Discount (if any)
        if (discountAmount > 0)
        {
            g.DrawString("ส่วนลดพิเศษ (Discount)", fontBody, Brushes.DarkRed, col1X + 8, currentY);
            g.DrawString("-", fontBody, brushText, col2X, currentY);
            g.DrawString($"-{discountAmount:N2}", fontBody, Brushes.DarkRed, col3X, currentY);
            currentY += 34;
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
            currentY += 34;
        }

        g.DrawLine(penLight, leftMargin, currentY, rightMargin, currentY);
        currentY += 15;

        // Total Box (Large Green Callout Box)
        var totalRect = new RectangleF(rightMargin - 360, currentY, 360, 46);
        g.FillRectangle(new SolidBrush(Color.FromArgb(240, 253, 244)), totalRect);
        g.DrawRectangle(new Pen(Color.ForestGreen, 1.5F), totalRect.X, totalRect.Y, totalRect.Width, totalRect.Height);

        var sfLeft = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
        var sfRight = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };

        g.DrawString("ยอดเงินสุทธิที่ชำระ (TOTAL):", fontHeader, Brushes.DarkGreen, new RectangleF(rightMargin - 345, currentY, 210, 46), sfLeft);
        g.DrawString($"{finalTotal:N2} บาท", new Font("Segoe UI", 13.5F, FontStyle.Bold), Brushes.DarkGreen, new RectangleF(rightMargin - 360, currentY, 345, 46), sfRight);

        currentY += 75;

        // 5. Lobby Terms & Special Agreements Section (ข้อตกลงและเงื่อนไขหน้าเคาน์เตอร์/ล็อบบี้)
        if (!string.IsNullOrWhiteSpace(_settings?.LobbyTerms))
        {
            var termsRect = new RectangleF(leftMargin, currentY, contentWidth, 100);
            g.FillRectangle(new SolidBrush(Color.FromArgb(248, 250, 252)), termsRect);
            g.DrawRectangle(penLight, termsRect.X, termsRect.Y, termsRect.Width, termsRect.Height);

            g.DrawString("ข้อตกลงและเงื่อนไขหน้าล็อบบี้ / Lobby Agreements & Terms", fontHeader, brushDark, leftMargin + 10, currentY + 8);
            
            var sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Near };
            g.DrawString(_settings.LobbyTerms, fontSmall, Brushes.DarkSlateGray, 
                new RectangleF(leftMargin + 12, currentY + 34, contentWidth - 24, 60), sf);

            currentY += 120;
        }

        // 6. QR Code & Signature Boxes Container
        using var qrImg = PrintLayoutHelper.LoadImageSafe(_settings?.QrCodeImagePath);
        if (qrImg != null)
        {
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

        bool showSignature = _settings?.ShowSignatureBox ?? true;
        if (showSignature)
        {
            float sigBoxWidth = (contentWidth - 140) / 2;

            // Left Signature Box (Guest)
            float sig1X = leftMargin + 130;
            var sig1Rect = new RectangleF(sig1X, currentY, sigBoxWidth, 130);
            g.DrawRectangle(penLight, sig1Rect.X, sig1Rect.Y, sig1Rect.Width, sig1Rect.Height);

            g.DrawString("ลงลายมือชื่อผู้เข้าพัก / Guest Signature", fontSigHeader, brushDark, sig1X + 10, currentY + 8);
            g.DrawLine(penLight, sig1X + 15, currentY + 75, sig1X + sigBoxWidth - 15, currentY + 75);
            g.DrawString($"({(_customer != null ? _customer.FullName : "_________________________")})", fontSmall, Brushes.DimGray, sig1X + 20, currentY + 85);
            g.DrawString("วันที่ / Date: _____ / _____ / ________", fontSmall, Brushes.DimGray, sig1X + 20, currentY + 105);

            // Right Signature Box (Staff)
            float sig2X = sig1X + sigBoxWidth + 15;
            var sig2Rect = new RectangleF(sig2X, currentY, sigBoxWidth, 130);
            g.DrawRectangle(penLight, sig2Rect.X, sig2Rect.Y, sig2Rect.Width, sig2Rect.Height);

            g.DrawString("ลงลายมือชื่อเจ้าหน้าที่ / Staff Signature", fontSigHeader, brushDark, sig2X + 10, currentY + 8);
            g.DrawLine(penLight, sig2X + 15, currentY + 75, sig2X + sigBoxWidth - 15, currentY + 75);
            g.DrawString($"({_staffName})", fontSmall, Brushes.DimGray, sig2X + 45, currentY + 85);
            g.DrawString("วันที่ / Date: _____ / _____ / ________", fontSmall, Brushes.DimGray, sig2X + 20, currentY + 105);

            currentY += 150;
        }
        else
        {
            currentY += 20;
        }

        // 7. Footer Message Note
        string footerMsg = !string.IsNullOrWhiteSpace(_settings?.BillFooter) ? _settings.BillFooter : "ขอบคุณที่ใช้บริการ / Thank you for staying with us";
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
