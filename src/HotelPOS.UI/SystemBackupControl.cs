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
    private readonly ISettingsService? _settingsService;

    private Label _lblDbPath = null!;
    private Button _btnBackup = null!;
    private Button _btnRestore = null!;
    private Button _btnOptimizeDb = null!;
    private Button _btnOpenBackupFolder = null!;

    private Button _btnExportRooms = null!;
    private Button _btnImportRooms = null!;
    private Button _btnExportCustomers = null!;
    private Button _btnImportCustomers = null!;
    private Button _btnExportProducts = null!;
    private Button _btnImportProducts = null!;

    public SystemBackupControl(IBackupService backupService, IExportImportService exportImportService, ISettingsService? settingsService = null)
    {
        _backupService = backupService;
        _exportImportService = exportImportService;
        _settingsService = settingsService;
        InitializeUI();
    }

    private void InitializeUI()
    {
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(241, 245, 249);
        Font = new Font("Segoe UI", 10.5F, FontStyle.Regular);

        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 65,
            Padding = new Padding(20, 15, 20, 15),
            BackColor = Color.White
        };

        var lblTitle = new Label
        {
            Text = "ระบบสำรอง คืนค่า และนำเข้า/ส่งออกข้อมูล (Backup & Data Exchange)",
            Font = new Font("Segoe UI", 15F, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59),
            Location = new Point(20, 16),
            AutoSize = true
        };
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
        var grpBackup = CreateCardPanel("1. จัดการและสำรองฐานข้อมูล (Database & Backup Management)", 235, 950);
        BuildBackupSection(grpBackup);

        // Group 2: Import & Export CSV
        var grpCsv = CreateCardPanel("2. นำเข้าและส่งออกข้อมูล (CSV Import & Export)", 245, 950);
        BuildCsvSection(grpCsv);

        container.Controls.Add(grpBackup);
        container.Controls.Add(grpCsv);

        Controls.Add(container);
        Controls.Add(pnlHeader);
    }

    private static Panel CreateCardPanel(string title, int height, int width)
    {
        var panel = new Panel
        {
            Width = width,
            Height = height,
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(18),
            Margin = new Padding(0, 0, 0, 20),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        var lblHeader = new Label
        {
            Text = title,
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59),
            Location = new Point(18, 14),
            AutoSize = true
        };

        var line = new Panel
        {
            Location = new Point(18, 44),
            Size = new Size(width - 36, 1),
            BackColor = Color.FromArgb(226, 232, 240),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        panel.Controls.Add(lblHeader);
        panel.Controls.Add(line);
        return panel;
    }

    private void BuildBackupSection(Panel grp)
    {
        _lblDbPath = new Label
        {
            Text = $"ที่ตั้งไฟล์ฐานข้อมูลปัจจุบัน:  {_backupService.GetDatabasePath()}",
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            ForeColor = Color.FromArgb(71, 85, 105),
            Location = new Point(20, 56),
            AutoSize = true
        };

        // Row 1 Buttons: Backup, Restore, Optimize DB
        _btnBackup = new Button
        {
            Text = "สำรองฐานข้อมูลทันที (Backup DB)",
            Location = new Point(20, 92),
            Size = new Size(270, 44),
            BackColor = Color.FromArgb(22, 163, 74), // Green
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnBackup.FlatAppearance.BorderSize = 0;
        _btnBackup.Click += BtnBackup_Click;

        _btnRestore = new Button
        {
            Text = "คืนค่าฐานข้อมูล (Restore DB)",
            Location = new Point(302, 92),
            Size = new Size(270, 44),
            BackColor = Color.FromArgb(217, 119, 6), // Orange
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnRestore.FlatAppearance.BorderSize = 0;
        _btnRestore.Click += BtnRestore_Click;

        _btnOptimizeDb = new Button
        {
            Text = "ปรับปรุงประสิทธิภาพ DB (Optimize)",
            Location = new Point(584, 92),
            Size = new Size(320, 44),
            BackColor = Color.FromArgb(37, 99, 235), // Blue
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnOptimizeDb.FlatAppearance.BorderSize = 0;
        _btnOptimizeDb.Click += async (s, e) =>
        {
            var res = await _backupService.CheckAndOptimizeDatabaseAsync();
            if (res.IsOk)
            {
                MessageBox.Show($"การตรวจสอบและปรับปรุงประสิทธิภาพเสร็จสมบูรณ์:\n\n{res.Message}", "ผลการปรับปรุงฐานข้อมูล (DB Integrity & Vacuum)", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show($"พบข้อผิดพลาด:\n\n{res.Message}", "ข้อผิดพลาดฐานข้อมูล", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };

        // Row 2 Button: Open Backup Folder
        _btnOpenBackupFolder = new Button
        {
            Text = "เปิดโฟลเดอร์ไฟล์สำรองข้อมูล (Open Backup Dir)",
            Location = new Point(20, 146),
            Size = new Size(340, 40),
            BackColor = Color.FromArgb(71, 85, 105), // Slate
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnOpenBackupFolder.FlatAppearance.BorderSize = 0;
        _btnOpenBackupFolder.Click += async (s, e) =>
        {
            try
            {
                string backupDir = "";
                if (_settingsService != null)
                {
                    var settings = await _settingsService.GetAllSettingsAsync();
                    backupDir = settings.CustomBackupFolderPath ?? "";
                }

                if (string.IsNullOrWhiteSpace(backupDir) || !Directory.Exists(backupDir))
                {
                    backupDir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "PSoftRestRentManager",
                        "Backups");
                }
                if (!Directory.Exists(backupDir)) Directory.CreateDirectory(backupDir);
                System.Diagnostics.Process.Start("explorer.exe", backupDir);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"เปิดโฟลเดอร์ไม่สำเร็จ: {ex.Message}", "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };

        var lblAutoBackupNote = new Label
        {
            Text = "* ระบบจะทำการสำรองข้อมูลหมุนเวียนอัตโนมัติ (Auto-Backup) ทุกครั้งที่ปิดแอปพลิเคชัน โดยสามารถตั้งค่าโฟลเดอร์ปลายทางได้ในหน้าตั้งค่าระบบ",
            Location = new Point(370, 157),
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Italic),
            ForeColor = Color.FromArgb(100, 116, 139)
        };

        grp.Controls.AddRange(new Control[] { _lblDbPath, _btnBackup, _btnRestore, _btnOptimizeDb, _btnOpenBackupFolder, lblAutoBackupNote });

        var tt = new ToolTip();
        tt.SetToolTip(_btnBackup, "คัดลอกสำรองไฟล์ฐานข้อมูล SQLite ทั้งหมดเก็บไว้เพื่อความปลอดภัย");
        tt.SetToolTip(_btnRestore, "เลือกไฟล์ .db สำรองเพื่อนำกลับมาใช้งานแทนที่ฐานข้อมูลปัจจุบัน");
        tt.SetToolTip(_btnOptimizeDb, "ตรวจสอบโครงสร้างตาราง (Integrity Check) และคืนพื้นที่ว่างของฐานข้อมูล (VACUUM) เพื่อความรวดเร็วในการประมวลผล");
        tt.SetToolTip(_btnOpenBackupFolder, "เปิดโฟลเดอร์ Windows Explorer เพื่อดูไฟล์สำรองข้อมูลย้อนหลัง");
    }

    private void BuildCsvSection(Panel grp)
    {
        _btnExportRooms = CreateCsvButton("ส่งออกห้องพัก (Rooms.csv)", new Point(20, 56), Color.FromArgb(241, 245, 249));
        _btnExportRooms.Click += BtnExportRooms_Click;

        _btnImportRooms = CreateCsvButton("นำเข้าห้องพัก (Rooms.csv)", new Point(460, 56), Color.FromArgb(241, 245, 249));
        _btnImportRooms.Click += BtnImportRooms_Click;

        _btnExportCustomers = CreateCsvButton("ส่งออกลูกค้า (Customers.csv)", new Point(20, 110), Color.FromArgb(241, 245, 249));
        _btnExportCustomers.Click += BtnExportCustomers_Click;

        _btnImportCustomers = CreateCsvButton("นำเข้าลูกค้า (Customers.csv)", new Point(460, 110), Color.FromArgb(241, 245, 249));
        _btnImportCustomers.Click += BtnImportCustomers_Click;

        _btnExportProducts = CreateCsvButton("ส่งออกสินค้า/สต็อก (Products.csv)", new Point(20, 164), Color.FromArgb(241, 245, 249));
        _btnExportProducts.Click += BtnExportProducts_Click;

        _btnImportProducts = CreateCsvButton("นำเข้าสินค้า/สต็อก (Products.csv)", new Point(460, 164), Color.FromArgb(241, 245, 249));
        _btnImportProducts.Click += BtnImportProducts_Click;

        grp.Controls.AddRange(new Control[]
        {
            _btnExportRooms, _btnImportRooms,
            _btnExportCustomers, _btnImportCustomers,
            _btnExportProducts, _btnImportProducts
        });
    }

    private static Button CreateCsvButton(string text, Point loc, Color bg)
    {
        var btn = new Button
        {
            Text = text,
            Location = loc,
            Size = new Size(420, 44),
            BackColor = bg,
            ForeColor = Color.FromArgb(30, 41, 59),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btn.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(226, 232, 240);
        return btn;
    }

    private async void BtnBackup_Click(object? sender, EventArgs e)
    {
        try
        {
            using var sfd = new SaveFileDialog
            {
                Filter = "Database File (*.db)|*.db|All Files (*.*)|*.*",
                FileName = $"PSoftRestRent_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.db"
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

    private async void BtnExportProducts_Click(object? sender, EventArgs e)
    {
        using var sfd = new SaveFileDialog { Filter = "CSV File (*.csv)|*.csv", FileName = "Products.csv" };
        if (sfd.ShowDialog() == DialogResult.OK)
        {
            try
            {
                await _exportImportService.ExportProductsToCsvAsync(sfd.FileName);
                MessageBox.Show("ส่งออกข้อมูลสินค้าและสต็อกเรียบร้อยแล้ว", "สำเร็จ", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"ส่งออกไม่สำเร็จ: {ex.Message}", "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private async void BtnImportProducts_Click(object? sender, EventArgs e)
    {
        using var ofd = new OpenFileDialog { Filter = "CSV File (*.csv)|*.csv" };
        if (ofd.ShowDialog() == DialogResult.OK)
        {
            try
            {
                int count = await _exportImportService.ImportProductsFromCsvAsync(ofd.FileName);
                MessageBox.Show($"นำเข้าข้อมูลสินค้าและสต็อกเรียบร้อยแล้ว จำนวน {count} รายการ", "สำเร็จ", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"นำเข้าไม่สำเร็จ: {ex.Message}", "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
