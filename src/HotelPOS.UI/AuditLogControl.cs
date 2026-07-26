using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using HotelPOS.Core.Services;

namespace HotelPOS.UI;

public class AuditLogControl : UserControl
{
    private readonly IAuditService _auditService;

    private DataGridView _dgvLogs = null!;
    private TextBox _txtSearch = null!;
    private DateTimePicker _dtpStart = null!;
    private DateTimePicker _dtpEnd = null!;
    private Button _btnSearch = null!;

    public AuditLogControl(IAuditService auditService)
    {
        _auditService = auditService;
        InitializeUI();
        Load += async (s, e) => await LoadLogsAsync();
    }

    private void InitializeUI()
    {
        Dock = DockStyle.Fill;
        Font = new Font("Segoe UI", 11F, FontStyle.Regular);

        var topPanel = new Panel { Dock = DockStyle.Top, Height = 65, Padding = new Padding(15, 12, 15, 12), BackColor = Color.White };
        var lblTitle = new Label { Text = "📜 ประวัติการทำงานระบบ (Audit Log)", Font = new Font("Segoe UI", 14F, FontStyle.Bold), Location = new Point(15, 16), AutoSize = true };

        _txtSearch = new TextBox { Location = new Point(350, 15), Width = 200, Font = new Font("Segoe UI", 11F), PlaceholderText = "ค้นหากิจกรรม / รายละเอียด..." };
        _txtSearch.KeyDown += async (s, e) => { if (e.KeyCode == Keys.Enter) await LoadLogsAsync(); };

        var lblFrom = new Label { Text = "ตั้งแต่วันที่:", Location = new Point(565, 18), AutoSize = true };
        _dtpStart = new DateTimePicker { Location = new Point(645, 15), Width = 130, Font = new Font("Segoe UI", 11F), Format = DateTimePickerFormat.Short, Value = DateTime.Now.AddDays(-30) };

        var lblTo = new Label { Text = "ถึง:", Location = new Point(785, 18), AutoSize = true };
        _dtpEnd = new DateTimePicker { Location = new Point(820, 15), Width = 130, Font = new Font("Segoe UI", 11F), Format = DateTimePickerFormat.Short, Value = DateTime.Now };

        _btnSearch = new Button { Text = "🔍 ค้นหา", Location = new Point(960, 13), Size = new Size(100, 36), Font = new Font("Segoe UI", 10.5F, FontStyle.Bold) };
        _btnSearch.Click += async (s, e) => await LoadLogsAsync();

        // ToolTips Guide
        var tt = new ToolTip();
        tt.SetToolTip(_txtSearch, "พิมพ์ค้นหาชื่อกิจกรรม หรือรายละเอียดใน Audit Log");
        tt.SetToolTip(_dtpStart, "เลือกวันที่เริ่มต้นในการค้นหาประวัติกิจกรรม");
        tt.SetToolTip(_dtpEnd, "เลือกวันที่สิ้นสุดในการค้นหาประวัติกิจกรรม");
        tt.SetToolTip(_btnSearch, "กดเพื่อค้นหา Audit Log ตามเงื่อนไข");

        topPanel.Controls.AddRange(new Control[] { lblTitle, _txtSearch, lblFrom, _dtpStart, lblTo, _dtpEnd, _btnSearch });

        _dgvLogs = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AllowUserToAddRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            ColumnHeadersHeight = 38,
            RowTemplate = { Height = 35 }
        };
        _dgvLogs.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        _dgvLogs.DefaultCellStyle.Font = new Font("Segoe UI", 10.5F);

        Controls.Add(_dgvLogs);
        Controls.Add(topPanel);
    }

    public async Task LoadLogsAsync()
    {
        try
        {
            var logs = await _auditService.GetLogsAsync(_dtpStart.Value, _dtpEnd.Value, _txtSearch.Text);
            _dgvLogs.DataSource = logs.Select(l => new
            {
                l.Id,
                วันเวลา = l.CreatedAt.ToString("dd/MM/yyyy HH:mm:ss"),
                กิจกรรม = l.Action,
                ข้อมูล = l.EntityName ?? "-",
                รหัส = l.EntityId ?? "-",
                รายละเอียด = l.DetailJson ?? "-"
            }).ToList();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"อ่าน Audit Log ไม่สำเร็จ: {ex.Message}", "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
