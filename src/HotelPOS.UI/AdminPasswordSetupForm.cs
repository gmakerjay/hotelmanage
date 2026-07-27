using System;
using System.Drawing;
using System.Windows.Forms;
using HotelPOS.Core.Services;

namespace HotelPOS.UI;

public class AdminPasswordSetupForm : Form
{
    private readonly ISettingsService _settingsService;
    private TextBox _txtNewPassword = null!;
    private TextBox _txtConfirmPassword = null!;
    private Button _btnSave = null!;
    private Button _btnSkip = null!;
    private Label _lblError = null!;

    public AdminPasswordSetupForm(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        InitializeComponent();
        try
        {
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        }
        catch { }
    }

    private void InitializeComponent()
    {
        Text = "ตั้งค่าความปลอดภัย - HotelPOS TH";
        Width = 520;
        Height = 380;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(248, 249, 250);
        Font = new Font("Segoe UI", 10F, FontStyle.Regular);

        // Top Banner Panel
        var topPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 80,
            BackColor = Color.FromArgb(30, 41, 59)
        };

        var lblIcon = new Label
        {
            Text = "",
            Font = new Font("Segoe UI", 22F),
            ForeColor = Color.White,
            Location = new Point(20, 16),
            AutoSize = true
        };

        var lblHeader = new Label
        {
            Text = "ตั้งค่ารหัสผ่านผู้ดูแลระบบ (Admin)",
            Font = new Font("Segoe UI", 12.5F, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(70, 16),
            AutoSize = true
        };

        var lblSubHeader = new Label
        {
            Text = "ระบบตรวจพบการลงทะเบียนสิทธิ์ใช้งานหลักครั้งแรก กรุณากำหนดรหัสผ่าน",
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            ForeColor = Color.FromArgb(203, 213, 225),
            Location = new Point(72, 44),
            AutoSize = true
        };

        topPanel.Controls.AddRange(new Control[] { lblIcon, lblHeader, lblSubHeader });

        var lblInfo = new Label
        {
            Text = "กรุณากำหนดรหัสผ่านสำหรับการเข้าใช้งานบัญชี admin เพื่อความปลอดภัยของข้อมูล หรือกดข้ามเพื่อใช้รหัสผ่านเริ่มต้น (psoft123)",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
            ForeColor = Color.FromArgb(71, 85, 105),
            Location = new Point(30, 95),
            Size = new Size(460, 45)
        };

        var lblNewPassword = new Label
        {
            Text = "รหัสผ่านใหม่:",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            Location = new Point(30, 150),
            AutoSize = true
        };

        _txtNewPassword = new TextBox
        {
            Location = new Point(180, 147),
            Width = 280,
            UseSystemPasswordChar = true,
            Font = new Font("Segoe UI", 10.5F)
        };

        var lblConfirmPassword = new Label
        {
            Text = "ยืนยันรหัสผ่านใหม่:",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            Location = new Point(30, 195),
            AutoSize = true
        };

        _txtConfirmPassword = new TextBox
        {
            Location = new Point(180, 192),
            Width = 280,
            UseSystemPasswordChar = true,
            Font = new Font("Segoe UI", 10.5F)
        };

        _lblError = new Label
        {
            Text = "",
            ForeColor = Color.Crimson,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Location = new Point(180, 230),
            AutoSize = true
        };

        _btnSave = new Button
        {
            Text = "บันทึกรหัสผ่าน",
            Location = new Point(180, 260),
            Size = new Size(135, 38),
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            BackColor = Color.FromArgb(61, 90, 128),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        _btnSave.FlatAppearance.BorderSize = 0;
        _btnSave.Click += BtnSave_Click;

        _btnSkip = new Button
        {
            Text = "ข้ามขั้นตอนนี้",
            Location = new Point(325, 260),
            Size = new Size(135, 38),
            Font = new Font("Segoe UI", 10F, FontStyle.Regular),
            BackColor = Color.FromArgb(226, 232, 240),
            ForeColor = Color.FromArgb(51, 65, 85),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        _btnSkip.FlatAppearance.BorderSize = 0;
        _btnSkip.Click += BtnSkip_Click;

        Controls.AddRange(new Control[]
        {
            topPanel, lblInfo,
            lblNewPassword, _txtNewPassword,
            lblConfirmPassword, _txtConfirmPassword,
            _lblError, _btnSave, _btnSkip
        });

        AcceptButton = _btnSave;
    }

    private async void BtnSave_Click(object? sender, EventArgs e)
    {
        string newPwd = _txtNewPassword.Text.Trim();
        string confirmPwd = _txtConfirmPassword.Text.Trim();

        if (string.IsNullOrEmpty(newPwd))
        {
            _lblError.Text = "กรุณากรอกรหัสผ่านใหม่";
            return;
        }

        if (newPwd != confirmPwd)
        {
            _lblError.Text = "รหัสผ่านและการยืนยันไม่ตรงกัน";
            return;
        }

        try
        {
            await _settingsService.SetAsync("admin_password", newPwd);
            await _settingsService.SetAsync("is_custom_admin_password_set", "1");
            MessageBox.Show("ตั้งค่ารหัสผ่านผู้ดูแลระบบใหม่เรียบร้อยแล้ว", "สำเร็จ", MessageBoxButtons.OK, MessageBoxIcon.Information);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            _lblError.Text = $"บันทึกไม่สำเร็จ: {ex.Message}";
        }
    }

    private async void BtnSkip_Click(object? sender, EventArgs e)
    {
        try
        {
            // ตรึงค่าว่าเคยแสดงถามและผู้ใช้กดข้าม เพื่อใช้ default
            await _settingsService.SetAsync("admin_password", "psoft123");
            await _settingsService.SetAsync("is_custom_admin_password_set", "1");
            DialogResult = DialogResult.Ignore;
            Close();
        }
        catch (Exception ex)
        {
            _lblError.Text = $"บันทึกไม่สำเร็จ: {ex.Message}";
        }
    }
}
