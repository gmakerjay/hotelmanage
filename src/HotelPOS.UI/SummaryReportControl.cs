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
    private readonly ComboBox _cboReportType;
    private readonly DataGridView _dgvReport;
    private readonly Label _lblTotalRevenue;
    private readonly Label _lblBreakdownInfo;
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
        Font = new Font("Segoe UI", 10.5F, FontStyle.Regular);

        // Header Panel - Spacious 2 Rows
        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 115,
            BackColor = Color.White,
            Padding = new Padding(20, 12, 20, 12)
        };

        var lblTitle = new Label
        {
            Text = "รายงานสรุปรายได้และประวัติย้อนหลัง (Summary & Analytics Report)",
            Font = new Font("Segoe UI", 15F, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Location = new Point(20, 12),
            AutoSize = true
        };

        var lblType = new Label { Text = "ประเภทรายงาน:", Location = new Point(20, 58), AutoSize = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
        _cboReportType = new ComboBox
        {
            Location = new Point(125, 54),
            Width = 270,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 10F)
        };
        _cboReportType.Items.AddRange(new object[]
        {
            "สรุปรายได้รวมทั้งหมด (ค่าห้อง + ค่าน้ำไฟ + POS)",
            "สรุปรายได้ค่าห้องพักอย่างเดียว",
            "สรุปบิลค่าน้ำและค่าไฟประจำเดือน",
            "สรุปยอดขายสินค้า POS",
            "สรุปประวัติการเข้าพัก (Occupancy History)"
        });
        _cboReportType.SelectedIndex = 0;
        _cboReportType.SelectedIndexChanged += async (s, e) => await LoadReportAsync();

        var lblStart = new Label { Text = "ตั้งแต่วันที่:", Location = new Point(410, 58), AutoSize = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
        _dtpStart = new DateTimePicker { Location = new Point(485, 54), Format = DateTimePickerFormat.Short, Width = 115, Font = new Font("Segoe UI", 10F) };
        _dtpStart.Value = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

        var lblEnd = new Label { Text = "ถึงวันที่:", Location = new Point(610, 58), AutoSize = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
        _dtpEnd = new DateTimePicker { Location = new Point(665, 54), Format = DateTimePickerFormat.Short, Width = 115, Font = new Font("Segoe UI", 10F) };
        _dtpEnd.Value = DateTime.Now;

        // Quick Date Range Filter Buttons
        var btnThisMonth = new Button { Text = "เดือนนี้", Location = new Point(790, 54), Size = new Size(65, 30), Font = new Font("Segoe UI", 9F, FontStyle.Bold), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
        btnThisMonth.Click += async (s, e) =>
        {
            _dtpStart.Value = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            _dtpEnd.Value = DateTime.Now;
            await LoadReportAsync();
        };

        var btnThisYear = new Button { Text = "ปีนี้", Location = new Point(860, 54), Size = new Size(55, 30), Font = new Font("Segoe UI", 9F, FontStyle.Bold), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
        btnThisYear.Click += async (s, e) =>
        {
            _dtpStart.Value = new DateTime(DateTime.Now.Year, 1, 1);
            _dtpEnd.Value = DateTime.Now;
            await LoadReportAsync();
        };

        var btnOneYear = new Button { Text = "ย้อนหลัง 1 ปี", Location = new Point(920, 54), Size = new Size(95, 30), Font = new Font("Segoe UI", 9F, FontStyle.Bold), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
        btnOneYear.Click += async (s, e) =>
        {
            _dtpStart.Value = DateTime.Now.AddYears(-1);
            _dtpEnd.Value = DateTime.Now;
            await LoadReportAsync();
        };

        var btnThreeYears = new Button { Text = "ย้อนหลัง 3 ปี", Location = new Point(1020, 54), Size = new Size(95, 30), Font = new Font("Segoe UI", 9F, FontStyle.Bold), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
        btnThreeYears.Click += async (s, e) =>
        {
            _dtpStart.Value = DateTime.Now.AddYears(-3);
            _dtpEnd.Value = DateTime.Now;
            await LoadReportAsync();
        };

        _btnGenerate = new Button
        {
            Text = "ดึงข้อมูล",
            Location = new Point(1125, 54),
            Size = new Size(85, 30),
            BackColor = Color.FromArgb(37, 99, 235),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnGenerate.FlatAppearance.BorderSize = 0;
        _btnGenerate.Click += async (s, e) => await LoadReportAsync();

        _btnPrint = new Button
        {
            Text = "พิมพ์ A4",
            Location = new Point(1215, 54),
            Size = new Size(85, 30),
            BackColor = Color.FromArgb(22, 163, 74),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Enabled = false
        };
        _btnPrint.FlatAppearance.BorderSize = 0;
        _btnPrint.Click += BtnPrint_Click;

        pnlHeader.Controls.AddRange(new Control[]
        {
            lblTitle, lblType, _cboReportType, lblStart, _dtpStart, lblEnd, _dtpEnd,
            btnThisMonth, btnThisYear, btnOneYear, btnThreeYears, _btnGenerate, _btnPrint
        });

        // Footer Panel (Summary & Analytics Breakdown Bar)
        var pnlFooter = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 65,
            BackColor = Color.White,
            Padding = new Padding(20, 10, 20, 10)
        };

        _lblBreakdownInfo = new Label
        {
            Text = "สรุปแยกหมวด: ค่าห้อง 0.00 บ. | ค่าน้ำไฟ 0.00 บ. | ค่าบริการ/อื่นๆ 0.00 บ.",
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(71, 85, 105),
            Dock = DockStyle.Left,
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft
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
        pnlFooter.Controls.Add(_lblBreakdownInfo);
        pnlFooter.Controls.Add(_lblTotalRevenue);

        // Grid Panel
        var pnlGrid = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(15)
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
        _dgvReport.EnableDoubleBuffering();

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

            int selectedIndex = _cboReportType.SelectedIndex;

            if (selectedIndex == 1) // ค่าห้องอย่างเดียว
            {
                var displayData = _currentBills.Select(b => new
                {
                    วันที่จ่าย = b.PaidAt?.ToString("dd/MM/yyyy HH:mm") ?? "-",
                    ห้อง = b.RoomNumber,
                    เลขที่บิล = b.BillCode,
                    ค่าห้องพัก = b.RoomCharge.ToString("N2"),
                    สถานะ = b.IsPaid ? "ชำระเรียบร้อย" : "ค้างชำระ"
                }).ToList();
                _dgvReport.DataSource = displayData;
            }
            else if (selectedIndex == 2) // ค่าน้ำไฟอย่างเดียว
            {
                var displayData = _currentBills.Select(b => new
                {
                    วันที่จ่าย = b.PaidAt?.ToString("dd/MM/yyyy HH:mm") ?? "-",
                    ห้อง = b.RoomNumber,
                    เลขที่บิล = b.BillCode,
                    มิเตอร์ไฟ = $"{b.ElectricPrev}➔{b.ElectricCurr} ({b.ElectricUnits} u)",
                    ค่าไฟ = b.ElectricAmount.ToString("N2"),
                    มิเตอร์น้ำ = $"{b.WaterPrev}➔{b.WaterCurr} ({b.WaterUnits} u)",
                    ค่าน้ำ = b.WaterAmount.ToString("N2"),
                    รวมน้ำไฟ = (b.ElectricAmount + b.WaterAmount).ToString("N2")
                }).ToList();
                _dgvReport.DataSource = displayData;
            }
            else // รวมทั้งหมด / ค่าเริ่มต้น
            {
                var displayData = _currentBills.Select(b => new
                {
                    วันที่จ่าย = b.PaidAt?.ToString("dd/MM/yyyy HH:mm") ?? "-",
                    ห้อง = b.RoomNumber,
                    เลขที่บิล = b.BillCode,
                    ค่าเช่าห้อง = b.RoomCharge.ToString("N2"),
                    ค่าไฟ = b.ElectricAmount.ToString("N2"),
                    ค่าน้ำ = b.WaterAmount.ToString("N2"),
                    ค่าบริการอื่น = (b.CommonAreaFee + b.GarbageFee).ToString("N2"),
                    รวมสุทธิ = b.TotalAmount.ToString("N2")
                }).ToList();
                _dgvReport.DataSource = displayData;
            }

            decimal totalRoom = _currentBills.Sum(b => b.RoomCharge);
            decimal totalElecWater = _currentBills.Sum(b => b.ElectricAmount + b.WaterAmount);
            decimal totalOther = _currentBills.Sum(b => b.CommonAreaFee + b.GarbageFee);
            decimal totalNet = _currentBills.Sum(b => b.TotalAmount);

            _lblBreakdownInfo.Text = $"สรุปแยกหมวด: ค่าห้อง {totalRoom:N2} บ. | ค่าน้ำไฟ {totalElecWater:N2} บ. | ค่าบริการ/อื่น {totalOther:N2} บ.";
            _lblTotalRevenue.Text = $"ยอดรวมสุทธิ ({_currentBills.Count} รายการ): {totalNet:N2} บาท";

            _btnPrint.Enabled = _currentBills.Count > 0;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"เกิดข้อผิดพลาดในการโหลดรายงาน: {ex.Message}", "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
