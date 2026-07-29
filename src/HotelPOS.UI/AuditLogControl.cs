using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using HotelPOS.Common.Models;
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
    private System.Collections.Generic.List<HotelPOS.Data.Repositories.AuditLogEntry> _loadedLogs = new();
    private GridPaginationPanel _pgPanel = null!;

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

        var btnExport = new Button
        {
            Text = "ส่งออก/พิมพ์ A4",
            Size = new Size(130, 36),
            BackColor = Color.FromArgb(37, 99, 235),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(5, 4, 5, 5)
        };
        btnExport.FlatAppearance.BorderSize = 0;
        btnExport.Click += BtnExport_Click;

        // ToolTips Guide (Large readable font & clipping safety)
        var tt = new AppToolTip();
        tt.SetToolTip(_txtSearch, "พิมพ์ค้นหาชื่อกิจกรรม หรือรายละเอียดใน Audit Log");
        tt.SetToolTip(_dtpStart, "เลือกวันที่เริ่มต้นในการค้นหาประวัติกิจกรรม");
        tt.SetToolTip(_dtpEnd, "เลือกวันที่สิ้นสุดในการค้นหาประวัติกิจกรรม");
        tt.SetToolTip(_btnSearch, "กดเพื่อค้นหา Audit Log ตามเงื่อนไข");
        tt.SetToolTip(btnRefresh, "รีเซ็ตตัวกรองและรีโหลดข้อมูลประวัติทั้งหมด");
        tt.SetToolTip(btnExport, "ส่งออกรายงานในรูปแบบเอกสาร HTML เพื่อพิมพ์ขนาด A4 หรือเป็น CSV เข้า Excel");

        _dtpStart.ValueChanged += async (s, e) => await LoadLogsAsync();
        _dtpEnd.ValueChanged += async (s, e) => await LoadLogsAsync();

        topPanel.Controls.AddRange(new Control[] { lblTitle, _txtSearch, lblFrom, _dtpStart, lblTo, _dtpEnd, _btnSearch, btnRefresh, btnExport });

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

        _dgvLogs.CellDoubleClick += (s, e) =>
        {
            if (e.RowIndex < 0) return;
            var row = _dgvLogs.Rows[e.RowIndex];
            string time = row.Cells["วันเวลา"].Value?.ToString() ?? "-";
            string action = row.Cells["กิจกรรม"].Value?.ToString() ?? "-";
            string entity = row.Cells["ข้อมูล"].Value?.ToString() ?? "-";
            string entityId = row.Cells["รหัส"].Value?.ToString() ?? "-";
            string detail = row.Cells["รายละเอียด"].Value?.ToString() ?? "-";

            string msg = $"🗓️ วันเวลา: {time}\n" +
                         $"📌 กิจกรรม: {action}\n" +
                         $"📁 ข้อมูลหลัก: {entity} (ID: {entityId})\n\n" +
                         $"📝 รายละเอียด:\n{detail}";

            MessageBox.Show(msg, "รายละเอียดประวัติระบบ (Audit Log Detail)", MessageBoxButtons.OK, MessageBoxIcon.Information);
        };

        // Create Master-Detail layout using SplitContainer
        var splitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 380,
            BorderStyle = BorderStyle.Fixed3D
        };

        var detailsPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12, 8, 12, 8),
            BackColor = Color.White
        };

        var lblDetailsHeader = new Label
        {
            Text = "🔎 รายละเอียดประวัติกิจกรรมที่เลือก (คลิกแถวในตารางด้านบนเพื่ออ่านแบบเต็ม)",
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor = Color.FromArgb(37, 99, 235),
            Dock = DockStyle.Top,
            Height = 28
        };

        var txtDetails = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            Dock = DockStyle.Fill,
            ScrollBars = ScrollBars.Vertical,
            BackColor = Color.FromArgb(248, 250, 252),
            Font = new Font("Consolas", 10.5F),
            ForeColor = Color.FromArgb(15, 23, 42),
            BorderStyle = BorderStyle.FixedSingle
        };

        detailsPanel.Controls.Add(lblDetailsHeader);
        detailsPanel.Controls.Add(txtDetails);

        _pgPanel = new GridPaginationPanel(() => UpdatePagination());
        splitContainer.Panel1.Controls.Add(_pgPanel);
        splitContainer.Panel1.Controls.Add(_dgvLogs);
        _dgvLogs.BringToFront();
        splitContainer.Panel2.Controls.Add(detailsPanel);

        _dgvLogs.SelectionChanged += (s, e) =>
        {
            if (_dgvLogs.SelectedRows.Count > 0)
            {
                var row = _dgvLogs.SelectedRows[0];
                var cols = row.DataGridView.Columns;
                
                string time = cols.Contains("วันเวลา") ? (row.Cells["วันเวลา"].Value?.ToString() ?? "-") : "-";
                string action = cols.Contains("กิจกรรม") ? (row.Cells["กิจกรรม"].Value?.ToString() ?? "-") : "-";
                string entity = cols.Contains("ข้อมูล") ? (row.Cells["ข้อมูล"].Value?.ToString() ?? "-") : "-";
                string entityId = cols.Contains("รหัส") ? (row.Cells["รหัส"].Value?.ToString() ?? "-") : "-";
                string detail = cols.Contains("รายละเอียด") ? (row.Cells["รายละเอียด"].Value?.ToString() ?? "-") : "-";

                txtDetails.Text = $"🗓️ วันเวลา: {time}\r\n" +
                                  $"📌 กิจกรรม: {action}\r\n" +
                                  $"📁 ข้อมูลหลัก: {entity} (ID: {entityId})\r\n" +
                                  $"--------------------------------------------------\r\n" +
                                  $"📝 รายละเอียดกิจกรรม:\r\n{detail}";
            }
            else
            {
                txtDetails.Clear();
            }
        };

        Controls.Add(topPanel);
        Controls.Add(splitContainer);
        topPanel.BringToFront();
    }

    private void UpdatePagination()
    {
        _pgPanel.UpdateState(_loadedLogs.Count);
        var pageData = _pgPanel.GetPageData(_loadedLogs).ToList();

        _dgvLogs.DataSource = pageData.Select(l => new
        {
            l.Id,
            วันเวลา = l.CreatedAt.ToString("dd/MM/yyyy HH:mm:ss"),
            กิจกรรม = l.Action,
            ข้อมูล = l.EntityName ?? "-",
            รหัส = l.EntityId ?? "-",
            รายละเอียด = l.DetailJson ?? "-"
        }).ToList();
    }

    public async Task LoadLogsAsync()
    {
        try
        {
            var logs = await _auditService.GetLogsAsync(_dtpStart.Value, _dtpEnd.Value, _txtSearch.Text);
            _loadedLogs = logs.ToList();
            _pgPanel.Reset();
            UpdatePagination();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"อ่าน Audit Log ไม่สำเร็จ: {ex.Message}", "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void BtnExport_Click(object? sender, EventArgs e)
    {
        if (_loadedLogs.Count == 0)
        {
            MessageBox.Show("ไม่มีข้อมูลสำหรับการส่งออก", "คำเตือน", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using var sfd = new SaveFileDialog
        {
            Filter = "เอกสารหน้าเว็บสำหรับพิมพ์ A4 (*.html)|*.html|ไฟล์ตารางนำไปเปิดใน Excel (*.csv)|*.csv",
            Title = "บันทึกไฟล์รายงานประวัติระบบ (Audit Log Report)",
            FileName = $"AuditLog_Report_{DateTime.Now:yyyyMMdd_HHmmss}"
        };

        if (sfd.ShowDialog() == DialogResult.OK)
        {
            try
            {
                if (sfd.FilterIndex == 1) // HTML (A4 Print Layout)
                {
                    var sb = new System.Text.StringBuilder();
                    foreach (var log in _loadedLogs)
                    {
                        var time = log.CreatedAt.ToString("dd/MM/yyyy HH:mm:ss");
                        var action = log.Action;
                        var table = log.EntityName ?? "-";
                        var entityId = log.EntityId ?? "-";
                        var detail = log.DetailJson ?? "-";

                        sb.Append("<tr>");
                        sb.Append($"<td>{System.Net.WebUtility.HtmlEncode(time)}</td>");
                        sb.Append($"<td>{System.Net.WebUtility.HtmlEncode(action)}</td>");
                        sb.Append($"<td>{System.Net.WebUtility.HtmlEncode(table)}</td>");
                        sb.Append($"<td>{System.Net.WebUtility.HtmlEncode(entityId)}</td>");
                        sb.Append($"<td>{System.Net.WebUtility.HtmlEncode(detail)}</td>");
                        sb.Append("</tr>");
                    }

                    var template = @"<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"" />
    <title>รายงานประวัติระบบ (Audit Log Report)</title>
    <style>
        @media screen {
            body { background: #f1f5f9; padding: 20px; font-family: 'Segoe UI', Tahoma, sans-serif; }
            .page { background: white; width: 210mm; min-height: 297mm; padding: 15mm; margin: 0 auto; box-shadow: 0 4px 6px -1px rgba(0,0,0,0.1); box-sizing: border-box; }
        }
        @media print {
            body { background: white; margin: 0; padding: 0; }
            .page { width: 210mm; height: 297mm; padding: 10mm; box-sizing: border-box; }
            @page { size: A4 portrait; margin: 0; }
        }
        h1 { font-size: 16pt; text-align: center; margin-bottom: 5px; color: #0f172a; }
        h2 { font-size: 10pt; font-weight: normal; text-align: center; margin-top: 0; margin-bottom: 20px; color: #475569; }
        table { width: 100%; border-collapse: collapse; font-size: 9pt; table-layout: fixed; }
        th { background-color: #f8fafc; color: #0f172a; border: 1px solid #cbd5e1; padding: 6px; font-weight: bold; text-align: left; }
        td { border: 1px solid #cbd5e1; padding: 6px; color: #334155; word-wrap: break-word; overflow: hidden; }
        tr:nth-child(even) td { background-color: #f8fafc; }
    </style>
</head>
<body>
    <div class=""page"">
        <h1>รายงานประวัติการทำงานระบบ (Audit Log)</h1>
        <h2>ค้นหาจาก: " + _dtpStart.Value.ToString("dd/MM/yyyy") + " ถึง " + _dtpEnd.Value.ToString("dd/MM/yyyy") + @" | คำค้นหา: " + System.Net.WebUtility.HtmlEncode(_txtSearch.Text) + @"</h2>
        <table>
            <thead>
                <tr>
                    <th style=""width: 18%;"">วันเวลา</th>
                    <th style=""width: 20%;"">กิจกรรม</th>
                    <th style=""width: 12%;"">ข้อมูลหลัก</th>
                    <th style=""width: 8%;"">รหัส</th>
                    <th style=""width: 42%;"">รายละเอียด</th>
                </tr>
            </thead>
            <tbody>
                " + sb.ToString() + @"
            </tbody>
        </table>
    </div>
</body>
</html>";

                    System.IO.File.WriteAllText(sfd.FileName, template, System.Text.Encoding.UTF8);
                    MessageBox.Show("ส่งออกเอกสารรายงานสำหรับเตรียมสั่งพิมพ์ลงกระดาษ A4 เรียบร้อยแล้ว\n\nระบบจะเปิดหน้าแสดงผลของรายงานนี้ขึ้นมาให้โดยอัตโนมัติ เพื่อให้กดสั่งพิมพ์ (Print) ได้ทันทีครับ", "ส่งออกสำเร็จ", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    
                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = sfd.FileName,
                        UseShellExecute = true
                    };
                    System.Diagnostics.Process.Start(psi);
                }
                else // CSV
                {
                    var sb = new System.Text.StringBuilder();
                    sb.AppendLine("วันเวลา,กิจกรรม,ข้อมูลหลัก,รหัส,รายละเอียด");
                    foreach (var log in _loadedLogs)
                    {
                        var time = EscapeCsv(log.CreatedAt.ToString("dd/MM/yyyy HH:mm:ss"));
                        var action = EscapeCsv(log.Action);
                        var table = EscapeCsv(log.EntityName ?? "-");
                        var entityId = $"\"=\"\"{EscapeCsv(log.EntityId ?? "-")}\"\"\"";
                        var detail = EscapeCsv(log.DetailJson ?? "-");

                        sb.AppendLine($"{time},{action},{table},{entityId},{detail}");
                    }
                    System.IO.File.WriteAllText(sfd.FileName, sb.ToString(), System.Text.Encoding.UTF8);
                    MessageBox.Show("ส่งออกไฟล์ตารางข้อมูลเรียบร้อยแล้ว ท่านสามารถนำไฟล์นี้ไปเปิดและคำนวณต่อในโปรแกรม Excel ได้ทันทีครับ", "ส่งออกสำเร็จ", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"การส่งออกล้มเหลว: {ex.Message}", "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private string EscapeCsv(string value)
    {
        if (value.Contains(",") || value.Contains("\"") || value.Contains("\r") || value.Contains("\n"))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }
}
