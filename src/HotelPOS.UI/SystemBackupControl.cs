using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using HotelPOS.Core.Services;

namespace HotelPOS.UI;

public class SystemBackupControl : UserControl
{
    private readonly IBackupService _backupService;
    private readonly IExportImportService _exportImportService;

    private Label _lblDbPath = null!;
    private Button _btnBackup = null!;
    private Button _btnRestore = null!;
    private Button _btnExportCustomers = null!;
    private Button _btnImportCustomers = null!;
    private Button _btnExportRooms = null!;
    private Button _btnImportRooms = null!;

    public SystemBackupControl(IBackupService backupService, IExportImportService exportImportService)
    {
        _backupService = backupService;
        _exportImportService = exportImportService;
        InitializeUI();
    }

    private void InitializeUI()
    {
        Dock = DockStyle.Fill;
        Font = new Font("Segoe UI", 11F, FontStyle.Regular);

        var pnlHeader = new Panel { Dock = DockStyle.Top, Height = 60, Padding = new Padding(15, 12, 15, 12), BackColor = Color.White };
        var lblTitle = new Label { Text = "⚙️ ระบบสำรอง คืนค่า และนำเข้า/ส่งออกข้อมูล", Font = new Font("Segoe UI", 14F, FontStyle.Bold), Location = new Point(15, 16), AutoSize = true };
        pnlHeader.Controls.Add(lblTitle);

        var container = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(20),
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false
        };

        // Group 1: Backup & Restore DB
        var grpBackup = new GroupBox
        {
            Text = "💾 สำรองและคืนค่าฐานข้อมูล (Database Backup & Restore)",
            Size = new Size(780, 180),
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 20)
        };

        _lblDbPath = new Label
        {
            Text = $"ที่ตั้งไฟล์ DB ปัจจุบัน: {_backupService.GetDatabasePath()}",
            Font = new Font("Segoe UI", 10F, FontStyle.Regular),
            ForeColor = Color.DarkSlateGray,
            Location = new Point(20, 35),
            AutoSize = true
        };

        _btnBackup = new Button
        {
            Text = "💾 สำรองฐานข้อมูลทันที (Backup DB)",
            Location = new Point(20, 85),
            Size = new Size(260, 45),
            BackColor = Color.ForestGreen,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold)
        };
        _btnBackup.Click += BtnBackup_Click;

        _btnRestore = new Button
        {
            Text = "🔄 คืนค่าฐานข้อมูล (Restore DB)",
            Location = new Point(300, 85),
            Size = new Size(260, 45),
            BackColor = Color.DarkOrange,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold)
        };
        _btnRestore.Click += BtnRestore_Click;

        grpBackup.Controls.AddRange(new Control[] { _lblDbPath, _btnBackup, _btnRestore });

        // Group 2: Import & Export CSV
        var grpCsv = new GroupBox
        {
            Text = "📁 นำเข้าและส่งออกข้อมูล (CSV Import & Export)",
            Size = new Size(780, 220),
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 20)
        };

        _btnExportRooms = new Button
        {
            Text = "📤 ส่งออกห้องพัก (Rooms.csv)",
            Location = new Point(20, 45),
            Size = new Size(250, 40),
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold)
        };
        _btnExportRooms.Click += BtnExportRooms_Click;

        _btnImportRooms = new Button
        {
            Text = "📥 นำเข้าห้องพัก (Rooms.csv)",
            Location = new Point(290, 45),
            Size = new Size(250, 40),
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold)
        };
        _btnImportRooms.Click += BtnImportRooms_Click;

        _btnExportCustomers = new Button
        {
            Text = "📤 ส่งออกลูกค้า (Customers.csv)",
            Location = new Point(20, 105),
            Size = new Size(250, 40),
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold)
        };
        _btnExportCustomers.Click += BtnExportCustomers_Click;

        _btnImportCustomers = new Button
        {
            Text = "📥 นำเข้าลูกค้า (Customers.csv)",
            Location = new Point(290, 105),
            Size = new Size(250, 40),
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold)
        };
        _btnImportCustomers.Click += BtnImportCustomers_Click;

        grpCsv.Controls.AddRange(new Control[] { _btnExportRooms, _btnImportRooms, _btnExportCustomers, _btnImportCustomers });

        // ToolTips Guide
        var tt = new ToolTip();
        tt.SetToolTip(_btnBackup, "คัดลอกสำรองไฟล์ฐานข้อมูล SQLite ทั้งหมดเก็บไว้เพื่อความปลอดภัย");
        tt.SetToolTip(_btnRestore, "เลือกไฟล์ .db สำรองเพื่อนำกลับมาใช้งานแทนที่ฐานข้อมูลปัจจุบัน");
        tt.SetToolTip(_btnExportRooms, "ส่งออกรายชื่อห้องพักและประเภทห้องเป็นไฟล์ CSV สำหรับเปิดบน Excel");
        tt.SetToolTip(_btnImportRooms, "นำเข้าข้อมูลห้องพักจากไฟล์ CSV เข้าสู่ระบบ");
        tt.SetToolTip(_btnExportCustomers, "ส่งออกรายชื่อและข้อมูลติดต่อลูกค้าเป็นไฟล์ CSV");
        tt.SetToolTip(_btnImportCustomers, "นำเข้าประวัติและรายชื่อลูกค้าจากไฟล์ CSV");

        container.Controls.Add(grpBackup);
        container.Controls.Add(grpCsv);

        Controls.Add(container);
        Controls.Add(pnlHeader);
    }

    private async void BtnBackup_Click(object? sender, EventArgs e)
    {
        try
        {
            using var sfd = new SaveFileDialog
            {
                Filter = "Database File (*.db)|*.db|All Files (*.*)|*.*",
                FileName = $"HotelPOS_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.db"
            };

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                var resultPath = await _backupService.CreateBackupAsync(sfd.FileName);
                MessageBox.Show($"สำรองฐานข้อมูลสำเร็จที่:\n{resultPath}", "สำเร็จ", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"สำรองข้อมูลไม่สำเร็จ: {ex.Message}", "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void BtnRestore_Click(object? sender, EventArgs e)
    {
        using var ofd = new OpenFileDialog
        {
            Filter = "Database File (*.db)|*.db|All Files (*.*)|*.*"
        };

        if (ofd.ShowDialog() == DialogResult.OK)
        {
            if (MessageBox.Show("คำเตือน: การคืนค่าจะเขียนทับฐานข้อมูลปัจจุบันทั้งหมด คุณแน่ใจหรือไม่ที่จะดำเนินการต่อ?", "ยืนยันการคืนค่า", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                try
                {
                    await _backupService.RestoreBackupAsync(ofd.FileName);
                    MessageBox.Show("คืนค่าฐานข้อมูลเรียบร้อยแล้ว กรุณารีสตาร์ทโปรแกรมเพื่อให้ข้อมูลปรับปรุงสมบูรณ์", "สำเร็จ", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"คืนค่าไม่สำเร็จ: {ex.Message}", "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }

    private async void BtnExportRooms_Click(object? sender, EventArgs e)
    {
        using var sfd = new SaveFileDialog { Filter = "CSV File (*.csv)|*.csv", FileName = "Rooms.csv" };
        if (sfd.ShowDialog() == DialogResult.OK)
        {
            try
            {
                await _exportImportService.ExportRoomsToCsvAsync(sfd.FileName);
                MessageBox.Show("ส่งออกข้อมูลห้องพักเรียบร้อยแล้ว", "สำเร็จ", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"ส่งออกไม่สำเร็จ: {ex.Message}", "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private async void BtnImportRooms_Click(object? sender, EventArgs e)
    {
        using var ofd = new OpenFileDialog { Filter = "CSV File (*.csv)|*.csv" };
        if (ofd.ShowDialog() == DialogResult.OK)
        {
            try
            {
                int count = await _exportImportService.ImportRoomsFromCsvAsync(ofd.FileName);
                MessageBox.Show($"นำเข้าข้อมูลห้องพักเรียบร้อยแล้ว จำนวน {count} รายการ", "สำเร็จ", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"นำเข้าไม่สำเร็จ: {ex.Message}", "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private async void BtnExportCustomers_Click(object? sender, EventArgs e)
    {
        using var sfd = new SaveFileDialog { Filter = "CSV File (*.csv)|*.csv", FileName = "Customers.csv" };
        if (sfd.ShowDialog() == DialogResult.OK)
        {
            try
            {
                await _exportImportService.ExportCustomersToCsvAsync(sfd.FileName);
                MessageBox.Show("ส่งออกข้อมูลลูกค้าเรียบร้อยแล้ว", "สำเร็จ", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"ส่งออกไม่สำเร็จ: {ex.Message}", "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private async void BtnImportCustomers_Click(object? sender, EventArgs e)
    {
        using var ofd = new OpenFileDialog { Filter = "CSV File (*.csv)|*.csv" };
        if (ofd.ShowDialog() == DialogResult.OK)
        {
            try
            {
                int count = await _exportImportService.ImportCustomersFromCsvAsync(ofd.FileName);
                MessageBox.Show($"นำเข้าข้อมูลลูกค้าเรียบร้อยแล้ว จำนวน {count} รายการ", "สำเร็จ", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"นำเข้าไม่สำเร็จ: {ex.Message}", "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
