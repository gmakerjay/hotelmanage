using System.Drawing;
using System.Drawing.Printing;
using HotelPOS.Common.Models;

namespace HotelPOS.Printing;

public class SummaryReportPrinter
{
    private readonly IEnumerable<UtilityBill> _paidBills;
    private readonly DateTime _startDate;
    private readonly DateTime _endDate;
    private readonly SystemSettingsDto _settings;
    
    // Pagination state
    private int _currentPage = 1;
    private int _currentRow = 0;
    private readonly List<UtilityBill> _billsList;

    public SummaryReportPrinter(IEnumerable<UtilityBill> paidBills, DateTime startDate, DateTime endDate, SystemSettingsDto settings)
    {
        _paidBills = paidBills;
        _startDate = startDate;
        _endDate = endDate;
        _settings = settings;
        _billsList = _paidBills.ToList();
    }

    public void Print()
    {
        using var pd = new PrintDocument();
        // A4 Paper Size
        pd.DefaultPageSettings.PaperSize = new PaperSize("A4", 827, 1169);
        pd.DefaultPageSettings.Margins = new Margins(50, 50, 50, 50);

        pd.PrintPage += Pd_PrintPage;

        using var printDialog = new PrintDialog { Document = pd };
        if (printDialog.ShowDialog() == DialogResult.OK)
        {
            pd.Print();
        }
    }

    private void Pd_PrintPage(object sender, PrintPageEventArgs e)
    {
        var g = e.Graphics;
        if (g == null) return;

        // Fonts
        var fontTitle = new Font("Segoe UI", 18, FontStyle.Bold);
        var fontSubTitle = new Font("Segoe UI", 12, FontStyle.Regular);
        var fontHeader = new Font("Segoe UI", 10, FontStyle.Bold);
        var fontNormal = new Font("Segoe UI", 10, FontStyle.Regular);
        var fontBold = new Font("Segoe UI", 10, FontStyle.Bold);

        var brushDark = new SolidBrush(Color.FromArgb(15, 23, 42));
        var brushLight = new SolidBrush(Color.FromArgb(71, 85, 105));
        var brushGray = new SolidBrush(Color.FromArgb(241, 245, 249));
        var penBorder = new Pen(Color.FromArgb(203, 213, 225), 1);

        float yPos = e.MarginBounds.Top;
        float xPos = e.MarginBounds.Left;
        float width = e.MarginBounds.Width;

        // --- Draw Header (Only on Page 1) ---
        if (_currentPage == 1)
        {
            string title = "รายงานสรุปรายได้ (Paid Bills Summary)";
            string subtitle = $"ช่วงวันที่: {_startDate:dd/MM/yyyy} ถึง {_endDate:dd/MM/yyyy}";
            string storeName = _settings.ShopName;

            g.DrawString(storeName, fontTitle, brushDark, xPos, yPos);
            yPos += fontTitle.Height + 5;
            g.DrawString(title, fontSubTitle, brushDark, xPos, yPos);
            yPos += fontSubTitle.Height + 5;
            g.DrawString(subtitle, fontSubTitle, brushLight, xPos, yPos);
            yPos += fontSubTitle.Height + 20;
        }

        // --- Draw Table Header ---
        float[] columnWidths = { 60, 100, 100, 100, 100, 100, 140 };
        string[] headers = { "ห้อง", "เลขที่บิล", "ค่าเช่า", "ค่าไฟ", "ค่าน้ำ", "อื่นๆ", "ยอดรวม" };
        
        g.FillRectangle(brushGray, xPos, yPos, width, 30);
        g.DrawRectangle(penBorder, xPos, yPos, width, 30);

        float currentX = xPos;
        for (int i = 0; i < headers.Length; i++)
        {
            g.DrawString(headers[i], fontHeader, brushDark, new RectangleF(currentX, yPos, columnWidths[i], 30), new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
            currentX += columnWidths[i];
        }
        yPos += 30;

        // --- Draw Table Rows ---
        float rowHeight = 25;
        while (_currentRow < _billsList.Count)
        {
            if (yPos + rowHeight > e.MarginBounds.Bottom - 100) // Leave space for footer
            {
                e.HasMorePages = true;
                _currentPage++;
                return;
            }

            var bill = _billsList[_currentRow];
            currentX = xPos;

            decimal others = bill.CommonAreaFee + bill.GarbageFee + bill.ExtraCharges - bill.DiscountAmount;

            string[] rowData = {
                bill.RoomNumber,
                bill.BillCode,
                bill.RoomCharge.ToString("N2"),
                bill.ElectricAmount.ToString("N2"),
                bill.WaterAmount.ToString("N2"),
                others.ToString("N2"),
                bill.TotalAmount.ToString("N2")
            };

            for (int i = 0; i < rowData.Length; i++)
            {
                var sf = new StringFormat { LineAlignment = StringAlignment.Center };
                sf.Alignment = (i == 0 || i == 1) ? StringAlignment.Center : StringAlignment.Far;
                
                // Add padding for numbers
                RectangleF cellRect = new RectangleF(currentX, yPos, columnWidths[i] - 5, rowHeight);
                if (i > 1) cellRect.X -= 5; 

                g.DrawString(rowData[i], fontNormal, brushDark, cellRect, sf);
                currentX += columnWidths[i];
            }

            g.DrawLine(penBorder, xPos, yPos + rowHeight, xPos + width, yPos + rowHeight);
            
            yPos += rowHeight;
            _currentRow++;
        }

        // --- Draw Summary Footer (Last Page) ---
        if (_currentRow >= _billsList.Count)
        {
            yPos += 20;
            decimal totalRoom = _billsList.Sum(b => b.RoomCharge);
            decimal totalElec = _billsList.Sum(b => b.ElectricAmount);
            decimal totalWater = _billsList.Sum(b => b.WaterAmount);
            decimal totalTotal = _billsList.Sum(b => b.TotalAmount);

            g.DrawString("สรุปยอดรวม (Total Summary):", fontTitle, brushDark, xPos, yPos);
            yPos += fontTitle.Height + 10;

            string summaryText = $"รวมค่าเช่า: {totalRoom:N2} บาท\n" +
                                 $"รวมค่าไฟ: {totalElec:N2} บาท\n" +
                                 $"รวมค่าน้ำ: {totalWater:N2} บาท\n" +
                                 $"ยอดรับสุทธิ: {totalTotal:N2} บาท";

            g.DrawString(summaryText, fontSubTitle, brushDark, xPos, yPos);

            e.HasMorePages = false;
        }

        // --- Draw Page Number ---
        string pageNum = $"หน้า {_currentPage}";
        g.DrawString(pageNum, fontNormal, brushLight, new RectangleF(xPos, e.PageBounds.Height - 50, width, 20), new StringFormat { Alignment = StringAlignment.Center });
    }
}
