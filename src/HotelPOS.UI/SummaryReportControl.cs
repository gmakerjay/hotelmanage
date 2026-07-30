using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using HotelPOS.Common.Models;
using HotelPOS.Core.Services;
using HotelPOS.Printing;

namespace HotelPOS.UI;

public class SummaryReportControl : UserControl
{
    private readonly IUtilityBillService _utilityBillService;
    private readonly ISettingsService _settingsService;
    private readonly DateTimePicker _dtpStart;
    private readonly DateTimePicker _dtpEnd;
    private readonly DataGridView _dgvReport;
    private readonly Label _lblTotalRevenue;
    private readonly Button _btnGenerate;
    private readonly Button _btnPrint;

    private List<UtilityBill> _currentBills = new List<UtilityBill>();
    private SystemSettingsDto _settings = new SystemSettingsDto();

    public SummaryReportControl(IUtilityBillService utilityBillService, ISettingsService settingsService)
    {
        _utilityBillService = utilityBillService;
        _settingsService = settingsService;

        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(248, 250, 252);

        // Header Panel
        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 100,
            BackColor = Color.White,
            Padding = new Padding(20)
        };

        var lblTitle = new Label
        {
            Text = "รายงานสรุปรายได้ (Summary Report)",
            Font = new Font("Segoe UI", 16F, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Location = new Point(20, 15),
            AutoSize = true
        };

        var lblStart = new Label { Text = "ตั้งแต่วันที่:", Location = new Point(20, 60), AutoSize = true, Font = new Font("Segoe UI", 10F) };
        _dtpStart = new DateTimePicker { Location = new Point(90, 56), Format = DateTimePickerFormat.Short, Width = 120, Font = new Font("Segoe UI", 10F) };
        _dtpStart.Value = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

        var lblEnd = new Label { Text = "ถึงวันที่:", Location = new Point(230, 60), AutoSize = true, Font = new Font("Segoe UI", 10F) };
        _dtpEnd = new DateTimePicker { Location = new Point(290, 56), Format = DateTimePickerFormat.Short, Width = 120, Font = new Font("Segoe UI", 10F) };
        _dtpEnd.Value = DateTime.Now;

        _btnGenerate = new Button
        {
            Text = "ดึงข้อมูล",
            Location = new Point(430, 54),
            Size = new Size(100, 30),
            BackColor = Color.FromArgb(37, 99, 235),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnGenerate.FlatAppearance.BorderSize = 0;
        _btnGenerate.Click += async (s, e) => await LoadReportAsync();

        _btnPrint = new Button
        {
            Text = "พิมพ์ A4",
            Location = new Point(540, 54),
            Size = new Size(100, 30),
            BackColor = Color.FromArgb(22, 163, 74),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Enabled = false
        };
        _btnPrint.FlatAppearance.BorderSize = 0;
        _btnPrint.Click += BtnPrint_Click;

        pnlHeader.Controls.AddRange(new Control[] { lblTitle, lblStart, _dtpStart, lblEnd, _dtpEnd, _btnGenerate, _btnPrint });

        // Footer Panel (Summary)
        var pnlFooter = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 60,
            BackColor = Color.White,
            Padding = new Padding(20, 10, 20, 10)
        };

        _lblTotalRevenue = new Label
        {
            Text = "ยอดรวมสุทธิ: 0.00 บาท",
            Font = new Font("Segoe UI", 14F, FontStyle.Bold),
            ForeColor = Color.FromArgb(194, 65, 12),
            Dock = DockStyle.Right,
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleRight
        };
        pnlFooter.Controls.Add(_lblTotalRevenue);

        // Grid Panel
        var pnlGrid = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20)
        };

        _dgvReport = new DataGridView
        {
            Dock = DockStyle.Fill,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AlternatingRowsDefaultCellStyle = { BackColor = Color.FromArgb(248, 250, 252) },
            DefaultCellStyle = { Font = new Font("Segoe UI", 10F), ForeColor = Color.FromArgb(51, 65, 85) },
            ColumnHeadersDefaultCellStyle = { Font = new Font("Segoe UI", 10F, FontStyle.Bold), BackColor = Color.FromArgb(241, 245, 249), ForeColor = Color.FromArgb(15, 23, 42) },
            EnableHeadersVisualStyles = false
        };

        pnlGrid.Controls.Add(_dgvReport);

        Controls.Add(pnlGrid);
        Controls.Add(pnlFooter);
        Controls.Add(pnlHeader);

        Load += async (s, e) => {
            _settings = await _settingsService.GetAllSettingsAsync();
            await LoadReportAsync();
        };
    }

    private async Task LoadReportAsync()
    {
        try
        {
            _btnGenerate.Enabled = false;
            var bills = await _utilityBillService.GetPaidBillsByDateRangeAsync(_dtpStart.Value, _dtpEnd.Value);
            _currentBills = bills.ToList();

            var displayData = _currentBills.Select(b => new
            {
                วันที่จ่าย = b.PaidAt?.ToString("dd/MM/yyyy HH:mm") ?? "-",
                ห้อง = b.RoomNumber,
                เลขที่บิล = b.BillCode,
                ค่าเช่า = b.RoomCharge.ToString("N2"),
                ค่าไฟ = b.ElectricAmount.ToString("N2"),
                ค่าน้ำ = b.WaterAmount.ToString("N2"),
                รวมสุทธิ = b.TotalAmount.ToString("N2")
            }).ToList();

            _dgvReport.DataSource = displayData;

            decimal total = _currentBills.Sum(b => b.TotalAmount);
            _lblTotalRevenue.Text = $"ยอดรวมสุทธิ: {total:N2} บาท";

            _btnPrint.Enabled = _currentBills.Count > 0;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"เกิดข้อผิดพลาด: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _btnGenerate.Enabled = true;
        }
    }

    private void BtnPrint_Click(object? sender, EventArgs e)
    {
        if (_currentBills.Count == 0) return;

        try
        {
            var printer = new SummaryReportPrinter(_currentBills, _dtpStart.Value, _dtpEnd.Value, _settings);
            printer.Print();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"เกิดข้อผิดพลาดในการพิมพ์: {ex.Message}", "Print Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
