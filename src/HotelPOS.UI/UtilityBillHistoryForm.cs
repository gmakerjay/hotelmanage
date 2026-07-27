using HotelPOS.Common;
using HotelPOS.Common.Models;
using HotelPOS.Core.Services;
using HotelPOS.Printing;

namespace HotelPOS.UI;

/// <summary>
/// ฟอร์มดูประวัติใบแจ้งหนี้และสถิติค่าน้ำค่าไฟรายห้อง / รายเดือน ย้อนหลังได้ตลอดเวลา
/// </summary>
public class UtilityBillHistoryForm : Form
{
    private readonly IUtilityBillService _utilityBillService;
    private readonly string _billingMonth;
    private readonly ISettingsService? _settingsService;

    private DataGridView _dgvBills = null!;
    private Label _lblSummary = null!;
    private TextBox _txtSearch = null!;
    private ComboBox _cmbFilter = null!;
    private Button _btnPrintSelected = null!;

    private List<UtilityBill> _allBills = new();
    private SystemSettingsDto? _settings;

    public UtilityBillHistoryForm(IUtilityBillService utilityBillService, string billingMonth, ISettingsService? settingsService = null)
    {
        _utilityBillService = utilityBillService;
        _billingMonth = billingMonth;
        _settingsService = settingsService;

        InitializeComponents();
    }

    private void InitializeComponents()
    {
        Text = $"ประวัติใบแจ้งหนี้รายเดือน - รอบบิล {_billingMonth}";
        Width = 1180;
        Height = 680;
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 10.5F);
        BackColor = Color.FromArgb(245, 247, 250);

        // === Header ===
        var headerPanel = new Panel { Dock = DockStyle.Top, Height = 65, Padding = new Padding(15) };

        var lblTitle = new Label
        {
            Text = $"ประวัติใบแจ้งหนี้ประจำเดือน {_billingMonth}",
            Font = new Font("Segoe UI", 14F, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Location = new Point(15, 12),
            AutoSize = true
        };

        var lblSearch = new Label
        {
            Text = "ค้นหา:",
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
            Location = new Point(360, 18),
            AutoSize = true
        };

        _txtSearch = new TextBox
        {
            Location = new Point(415, 14),
            Width = 180,
            Font = new Font("Segoe UI", 10.5F),
            PlaceholderText = "พิมพ์เบอร์โทร / ชื่อ / เลขห้อง / บิล..."
        };
        _txtSearch.TextChanged += (s, e) => ApplyFilter();

        var lblFilter = new Label
        {
            Text = "สถานะ:",
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
            Location = new Point(610, 18),
            AutoSize = true
        };

        _cmbFilter = new ComboBox
        {
            Items = { "ทั้งหมด", "ชำระแล้ว", "ยังไม่ชำระ" },
            SelectedIndex = 0,
            Location = new Point(670, 14),
            Width = 130,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 10.5F)
        };
        _cmbFilter.SelectedIndexChanged += (s, e) => ApplyFilter();

        _btnPrintSelected = new Button
        {
            Text = "พิมพ์บิลใบนี้",
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Color.FromArgb(37, 99, 235),
            FlatStyle = FlatStyle.Flat,
            Size = new Size(120, 36),
            Location = new Point(815, 12),
            Cursor = Cursors.Hand
        };
        _btnPrintSelected.FlatAppearance.BorderSize = 0;
        _btnPrintSelected.Click += async (s, e) => await PrintSelectedBillAsync();

        var btnMarkPaid = new Button
        {
            Text = "บันทึกชำระ",
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Color.FromArgb(22, 163, 74),
            FlatStyle = FlatStyle.Flat,
            Size = new Size(120, 36),
            Location = new Point(945, 12),
            Cursor = Cursors.Hand
        };
        btnMarkPaid.FlatAppearance.BorderSize = 0;
        btnMarkPaid.Click += async (s, e) => await MarkSelectedAsPaidAsync();

        headerPanel.Controls.AddRange(new Control[] { lblTitle, lblSearch, _txtSearch, lblFilter, _cmbFilter, _btnPrintSelected, btnMarkPaid });

        // === DataGridView ===
        _dgvBills = new DataGridView
        {
            Dock = DockStyle.Fill,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            DefaultCellStyle = new DataGridViewCellStyle
            {
                Font = new Font("Segoe UI", 10.5F),
                Padding = new Padding(6, 4, 6, 4)
            },
            ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                BackColor = Color.FromArgb(30, 41, 59),
                ForeColor = Color.White,
                Padding = new Padding(6),
                Alignment = DataGridViewContentAlignment.MiddleCenter
            },
            EnableHeadersVisualStyles = false,
            ColumnHeadersHeight = 42,
            RowTemplate = { Height = 40 },
            GridColor = Color.FromArgb(226, 232, 240)
        };

        _dgvBills.Columns.AddRange(new DataGridViewColumn[]
        {
            new DataGridViewTextBoxColumn { Name = "BillId", HeaderText = "ID", Visible = false },
            new DataGridViewTextBoxColumn { Name = "BillCode", HeaderText = "เลขที่บิล", FillWeight = 85 },
            new DataGridViewTextBoxColumn { Name = "RoomNumber", HeaderText = "ห้อง", FillWeight = 45,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 10.5F, FontStyle.Bold) } },
            new DataGridViewTextBoxColumn { Name = "RoomCharge", HeaderText = "ค่าห้อง", FillWeight = 65,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N2" } },
            new DataGridViewTextBoxColumn { Name = "ElecDetails", HeaderText = "รายละเอียดไฟ (ก่อน->หลัง / หน่วย)", FillWeight = 115,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter, ForeColor = Color.FromArgb(234, 88, 12) } },
            new DataGridViewTextBoxColumn { Name = "ElectricAmt", HeaderText = "ค่าไฟ (บาท)", FillWeight = 65,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N2", ForeColor = Color.FromArgb(234, 88, 12), Font = new Font("Segoe UI", 10F, FontStyle.Bold) } },
            new DataGridViewTextBoxColumn { Name = "WaterDetails", HeaderText = "รายละเอียดน้ำ (ก่อน->หลัง / หน่วย)", FillWeight = 115,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter, ForeColor = Color.FromArgb(14, 116, 144) } },
            new DataGridViewTextBoxColumn { Name = "WaterAmt", HeaderText = "ค่าน้ำ (บาท)", FillWeight = 65,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N2", ForeColor = Color.FromArgb(14, 116, 144), Font = new Font("Segoe UI", 10F, FontStyle.Bold) } },
            new DataGridViewTextBoxColumn { Name = "ServiceFees", HeaderText = "ค่าบริการ+ขยะ", FillWeight = 65,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N2" } },
            new DataGridViewTextBoxColumn { Name = "TotalAmount", HeaderText = "รวมสุทธิ (บาท)", FillWeight = 80,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N2",
                    Font = new Font("Segoe UI", 10.5F, FontStyle.Bold), ForeColor = Color.FromArgb(30, 41, 59) } },
            new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "สถานะ", FillWeight = 75,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } },
            new DataGridViewTextBoxColumn { Name = "Notes", HeaderText = "หมายเหตุ", FillWeight = 75 }
        });

        // === Footer ===
        var footerPanel = new Panel { Dock = DockStyle.Bottom, Height = 45, Padding = new Padding(15, 8, 15, 8) };
        _lblSummary = new Label
        {
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59),
            Location = new Point(15, 12),
            AutoSize = true
        };
        footerPanel.Controls.Add(_lblSummary);

        Controls.Add(_dgvBills);
        Controls.Add(footerPanel);
        Controls.Add(headerPanel);

        Load += async (s, e) => await LoadBillsAsync();
    }

    private async Task LoadBillsAsync()
    {
        if (_settingsService != null)
        {
            _settings = await _settingsService.GetAllSettingsAsync();
        }

        _allBills = (await _utilityBillService.GetBillsByMonthAsync(_billingMonth)).ToList();
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        string query = _txtSearch.Text.Trim();

        var filtered = _allBills.Where(b =>
        {
            bool matchStatus = _cmbFilter.SelectedIndex switch
            {
                1 => b.IsPaid,
                2 => !b.IsPaid,
                _ => true
            };

            if (!matchStatus) return false;
            if (string.IsNullOrWhiteSpace(query)) return true;

            bool matchCode = b.BillCode.Contains(query, StringComparison.OrdinalIgnoreCase);
            bool matchRoom = b.RoomNumber.Contains(query, StringComparison.OrdinalIgnoreCase);
            bool matchNotes = !string.IsNullOrEmpty(b.Notes) && b.Notes.Contains(query, StringComparison.OrdinalIgnoreCase);

            return matchCode || matchRoom || matchNotes;
        }).ToList();

        _dgvBills.Rows.Clear();

        foreach (var bill in filtered)
        {
            string status = bill.IsPaid ? "ชำระแล้ว" : "ยังไม่ชำระ";
            decimal serviceFees = bill.CommonAreaFee + bill.GarbageFee;

            string elecDetails = bill.ElectricUnits > 0
                ? $"{bill.ElectricPrev:N0} -> {bill.ElectricCurr:N0} ({bill.ElectricUnits:N0} หน่วย)"
                : "-";

            string waterDetails = bill.WaterBillingMode == "FLAT"
                ? $"เหมาจ่าย ({bill.WaterPersonCount} คน)"
                : (bill.WaterUnits > 0 ? $"{bill.WaterPrev:N0} -> {bill.WaterCurr:N0} ({bill.WaterUnits:N0} หน่วย)" : "-");

            int rowIndex = _dgvBills.Rows.Add(
                bill.Id, bill.BillCode, bill.RoomNumber,
                bill.RoomCharge,
                elecDetails, bill.ElectricAmount,
                waterDetails, bill.WaterAmount,
                serviceFees, bill.TotalAmount, status, bill.Notes ?? "");

            if (bill.IsPaid)
            {
                _dgvBills.Rows[rowIndex].DefaultCellStyle.BackColor = Color.FromArgb(240, 253, 244);
            }
            else
            {
                _dgvBills.Rows[rowIndex].DefaultCellStyle.BackColor = Color.FromArgb(254, 242, 242);
            }
        }

        decimal totalPaid = _allBills.Where(b => b.IsPaid).Sum(b => b.TotalAmount);
        decimal totalUnpaid = _allBills.Where(b => !b.IsPaid).Sum(b => b.TotalAmount);
        int paidCount = _allBills.Count(b => b.IsPaid);
        int unpaidCount = _allBills.Count(b => !b.IsPaid);

        _lblSummary.Text = $"ทั้งหมด {_allBills.Count} ห้อง | ชำระแล้ว {paidCount} ห้อง ({totalPaid:N2} บาท) | ยังไม่ชำระ {unpaidCount} ห้อง ({totalUnpaid:N2} บาท)";
    }

    private async Task PrintSelectedBillAsync()
    {
        if (_dgvBills.CurrentRow == null)
        {
            MessageBox.Show("กรุณาเลือกรายการที่ต้องการพิมพ์ใบแจ้งหนี้", "ข้อแนะนำ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        int billId = Convert.ToInt32(_dgvBills.CurrentRow.Cells["BillId"].Value);
        var bill = _allBills.FirstOrDefault(b => b.Id == billId);
        if (bill == null) return;

        var settings = _settings ?? new SystemSettingsDto();
        var printer = new UtilityInvoicePrinter(bill, null, settings);
        printer.ShowPrintPreview();
    }

    private async Task MarkSelectedAsPaidAsync()
    {
        if (_dgvBills.CurrentRow == null)
        {
            MessageBox.Show("กรุณาเลือกรายการที่ต้องการบันทึกชำระ", "ข้อแนะนำ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        int billId = Convert.ToInt32(_dgvBills.CurrentRow.Cells["BillId"].Value);
        string billCode = _dgvBills.CurrentRow.Cells["BillCode"].Value?.ToString() ?? "";
        string roomNumber = _dgvBills.CurrentRow.Cells["RoomNumber"].Value?.ToString() ?? "";

        var confirm = MessageBox.Show(
            $"บันทึกชำระใบแจ้งหนี้ {billCode} ห้อง {roomNumber}?",
            "ยืนยันการชำระ", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

        if (confirm == DialogResult.Yes)
        {
            await _utilityBillService.MarkBillAsPaidAsync(billId, PaymentMethod.Cash);
            await LoadBillsAsync();
            MessageBox.Show("บันทึกการชำระสำเร็จ", "สำเร็จ", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
