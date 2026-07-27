using HotelPOS.Common;
using HotelPOS.Common.Models;
using HotelPOS.Core.Services;

namespace HotelPOS.UI;

/// <summary>
/// ฟอร์มดูรายการใบแจ้งหนี้รายเดือน แยกสถานะชำระ/ยังไม่ชำระ
/// </summary>
public class UtilityBillHistoryForm : Form
{
    private readonly IUtilityBillService _utilityBillService;
    private readonly string _billingMonth;

    private DataGridView _dgvBills = null!;
    private Label _lblSummary = null!;
    private ComboBox _cmbFilter = null!;

    private List<UtilityBill> _allBills = new();

    public UtilityBillHistoryForm(IUtilityBillService utilityBillService, string billingMonth)
    {
        _utilityBillService = utilityBillService;
        _billingMonth = billingMonth;

        InitializeComponents();
    }

    private void InitializeComponents()
    {
        Text = $"📋 ใบแจ้งหนี้ค่าสาธารณูปโภค - เดือน {_billingMonth}";
        Width = 1100;
        Height = 650;
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 10.5F);
        BackColor = Color.FromArgb(245, 247, 250);

        // === Header ===
        var headerPanel = new Panel { Dock = DockStyle.Top, Height = 65, Padding = new Padding(15) };

        var lblTitle = new Label
        {
            Text = $"📋 รายการใบแจ้งหนี้ - {_billingMonth}",
            Font = new Font("Segoe UI", 14F, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Location = new Point(15, 8),
            AutoSize = true
        };

        var lblFilter = new Label
        {
            Text = "กรอง:",
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
            Location = new Point(600, 14),
            AutoSize = true
        };

        _cmbFilter = new ComboBox
        {
            Items = { "ทั้งหมด", "✅ ชำระแล้ว", "❌ ยังไม่ชำระ" },
            SelectedIndex = 0,
            Location = new Point(650, 10),
            Width = 180,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 10.5F)
        };
        _cmbFilter.SelectedIndexChanged += (s, e) => ApplyFilter();

        var btnMarkPaid = new Button
        {
            Text = "💰 บันทึกชำระ",
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Color.FromArgb(22, 163, 74),
            FlatStyle = FlatStyle.Flat,
            Size = new Size(150, 36),
            Location = new Point(860, 8),
            Cursor = Cursors.Hand
        };
        btnMarkPaid.FlatAppearance.BorderSize = 0;
        btnMarkPaid.Click += async (s, e) => await MarkSelectedAsPaidAsync();

        headerPanel.Controls.AddRange(new Control[] { lblTitle, lblFilter, _cmbFilter, btnMarkPaid });

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
            ColumnHeadersHeight = 40,
            RowTemplate = { Height = 38 },
            GridColor = Color.FromArgb(226, 232, 240)
        };

        _dgvBills.Columns.AddRange(new DataGridViewColumn[]
        {
            new DataGridViewTextBoxColumn { Name = "BillId", HeaderText = "ID", Visible = false },
            new DataGridViewTextBoxColumn { Name = "BillCode", HeaderText = "เลขที่บิล", FillWeight = 100 },
            new DataGridViewTextBoxColumn { Name = "RoomNumber", HeaderText = "ห้อง", FillWeight = 50,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 10.5F, FontStyle.Bold) } },
            new DataGridViewTextBoxColumn { Name = "RoomCharge", HeaderText = "ค่าห้อง", FillWeight = 70,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N2" } },
            new DataGridViewTextBoxColumn { Name = "ElectricAmt", HeaderText = "ค่าไฟ", FillWeight = 70,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N2", ForeColor = Color.FromArgb(234, 88, 12) } },
            new DataGridViewTextBoxColumn { Name = "WaterAmt", HeaderText = "ค่าน้ำ", FillWeight = 70,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N2", ForeColor = Color.FromArgb(14, 116, 144) } },
            new DataGridViewTextBoxColumn { Name = "ServiceFees", HeaderText = "ค่าบริการ+ขยะ", FillWeight = 80,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N2" } },
            new DataGridViewTextBoxColumn { Name = "TotalAmount", HeaderText = "รวมทั้งหมด", FillWeight = 85,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N2",
                    Font = new Font("Segoe UI", 10.5F, FontStyle.Bold), ForeColor = Color.FromArgb(30, 41, 59) } },
            new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "สถานะ", FillWeight = 80,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } },
            new DataGridViewTextBoxColumn { Name = "Notes", HeaderText = "หมายเหตุ", FillWeight = 80 }
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
        _allBills = (await _utilityBillService.GetBillsByMonthAsync(_billingMonth)).ToList();
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var filtered = _cmbFilter.SelectedIndex switch
        {
            1 => _allBills.Where(b => b.IsPaid).ToList(),
            2 => _allBills.Where(b => !b.IsPaid).ToList(),
            _ => _allBills
        };

        _dgvBills.Rows.Clear();

        foreach (var bill in filtered)
        {
            string status = bill.IsPaid ? "✅ ชำระแล้ว" : "❌ ยังไม่ชำระ";
            decimal serviceFees = bill.CommonAreaFee + bill.GarbageFee;

            int rowIndex = _dgvBills.Rows.Add(
                bill.Id, bill.BillCode, bill.RoomNumber,
                bill.RoomCharge, bill.ElectricAmount, bill.WaterAmount,
                serviceFees, bill.TotalAmount, status, bill.Notes ?? "");

            // Color coding for payment status
            if (bill.IsPaid)
            {
                _dgvBills.Rows[rowIndex].DefaultCellStyle.BackColor = Color.FromArgb(240, 253, 244);
            }
            else
            {
                _dgvBills.Rows[rowIndex].DefaultCellStyle.BackColor = Color.FromArgb(254, 242, 242);
            }
        }

        // Summary
        decimal totalPaid = _allBills.Where(b => b.IsPaid).Sum(b => b.TotalAmount);
        decimal totalUnpaid = _allBills.Where(b => !b.IsPaid).Sum(b => b.TotalAmount);
        int paidCount = _allBills.Count(b => b.IsPaid);
        int unpaidCount = _allBills.Count(b => !b.IsPaid);

        _lblSummary.Text = $"ทั้งหมด {_allBills.Count} ห้อง | ✅ ชำระแล้ว {paidCount} ห้อง (฿{totalPaid:N2}) | ❌ ยังไม่ชำระ {unpaidCount} ห้อง (฿{totalUnpaid:N2})";
    }

    private async Task MarkSelectedAsPaidAsync()
    {
        if (_dgvBills.CurrentRow == null)
        {
            MessageBox.Show("กรุณาเลือกรายการที่ต้องการบันทึกชำระ", "⚠️", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        int billId = Convert.ToInt32(_dgvBills.CurrentRow.Cells["BillId"].Value);
        string billCode = _dgvBills.CurrentRow.Cells["BillCode"].Value?.ToString() ?? "";
        string roomNumber = _dgvBills.CurrentRow.Cells["RoomNumber"].Value?.ToString() ?? "";

        var confirm = MessageBox.Show(
            $"บันทึกชำระใบแจ้งหนี้ {billCode} ห้อง {roomNumber}?",
            "💰 ยืนยันการชำระ", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

        if (confirm == DialogResult.Yes)
        {
            await _utilityBillService.MarkBillAsPaidAsync(billId, PaymentMethod.Cash);
            await LoadBillsAsync();
            MessageBox.Show("✅ บันทึกการชำระสำเร็จ", "สำเร็จ", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
