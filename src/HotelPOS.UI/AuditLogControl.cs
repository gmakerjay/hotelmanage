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

        var topPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Padding = new Padding(15, 12, 15, 12),
            BackColor = Color.White,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        var lblTitle = new Label { Text = "ประวัติการทำงานระบบ (Audit Log)", Font = new Font("Segoe UI", 14F, FontStyle.Bold), AutoSize = true, Margin = new Padding(0, 5, 20, 5) };

        _txtSearch = new TextBox { Width = 200, Font = new Font("Segoe UI", 11F), PlaceholderText = "ค้นหากิจกรรม / รายละเอียด...", Margin = new Padding(5, 6, 5, 5) };
        _txtSearch.KeyDown += async (s, e) => { if (e.KeyCode == Keys.Enter) await LoadLogsAsync(); };

        var lblFrom = new Label { Text = "ตั้งแต่วันที่:", AutoSize = true, Margin = new Padding(15, 10, 5, 5) };
        _dtpStart = new DateTimePicker { Width = 130, Font = new Font("Segoe UI", 11F), Format = DateTimePickerFormat.Short, Value = DateTime.Now.AddDays(-30), Margin = new Padding(5, 6, 5, 5) };

        var lblTo = new Label { Text = "ถึง:", AutoSize = true, Margin = new Padding(10, 10, 5, 5) };
        _dtpEnd = new DateTimePicker { Width = 130, Font = new Font("Segoe UI", 11F), Format = DateTimePickerFormat.Short, Value = DateTime.Now, Margin = new Padding(5, 6, 5, 5) };

        _btnSearch = new Button { Text = "ค้นหา", Size = new Size(100, 36), Font = new Font("Segoe UI", 10.5F, FontStyle.Bold), Margin = new Padding(15, 4, 5, 5) };
        _btnSearch.Click += async (s, e) => await LoadLogsAsync();

        var btnRefresh = new Button
        {
            Text = "รีเฟรช",
            Size = new Size(100, 36),
            BackColor = Color.FromArgb(241, 245, 249),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(5, 4, 5, 5)
        };
        btnRefresh.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        btnRefresh.Click += async (s, e) => {
            _txtSearch.Clear();
            _dtpStart.Value = DateTime.Now.AddDays(-30);
            _dtpEnd.Value = DateTime.Now;
            await LoadLogsAsync();
        };

        // ToolTips Guide (Large readable font & clipping safety)
        var tt = new AppToolTip();
        tt.SetToolTip(_txtSearch, "พิมพ์ค้นหาชื่อกิจกรรม หรือรายละเอียดใน Audit Log");
        tt.SetToolTip(_dtpStart, "เลือกวันที่เริ่มต้นในการค้นหาประวัติกิจกรรม");
        tt.SetToolTip(_dtpEnd, "เลือกวันที่สิ้นสุดในการค้นหาประวัติกิจกรรม");
        tt.SetToolTip(_btnSearch, "กดเพื่อค้นหา Audit Log ตามเงื่อนไข");
        tt.SetToolTip(btnRefresh, "รีเซ็ตตัวกรองและรีโหลดข้อมูลประวัติทั้งหมด");

        topPanel.Controls.AddRange(new Control[] { lblTitle, _txtSearch, lblFrom, _dtpStart, lblTo, _dtpEnd, _btnSearch, btnRefresh });

        _dgvLogs = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AllowUserToAddRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
            RowTemplate = { Height = 35 }
        };
        _dgvLogs.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        _dgvLogs.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
        _dgvLogs.DefaultCellStyle.Font = new Font("Segoe UI", 10.5F);
        _dgvLogs.DataBindingComplete += (s, e) =>
        {
            foreach (DataGridViewColumn col in _dgvLogs.Columns)
            {
                col.MinimumWidth = 90;
            }
        };

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
