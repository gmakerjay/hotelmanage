using System;
using System.Drawing;
using System.Windows.Forms;
using HotelPOS.Common;
using HotelPOS.Licensing;

namespace HotelPOS.UI;

public class LicenseActivationForm : Form
{
    private TextBox _tbHardwareId = null!;
    private TextBox _tbLicenseKey = null!;
    private Label _lblStatus = null!;
    private Button _btnActivate = null!;
    private Button _btnCancel = null!;

    public LicenseActivationForm(string currentStatusText)
    {
        Text = "เปิดใช้งานลิขสิทธิ์ระบบ - HotelPOS TH";
        Width = 600;
        Height = 460;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10F); // เปลี่ยนเป็นฟอนต์ที่ทันสมัยและอ่านง่ายกว่าเดิม

        InitializeLayout(currentStatusText);
    }

    private void InitializeLayout(string currentStatusText)
    {
        // พาเนลหลักเพื่อจัดระเบียบองค์ประกอบและสีพื้นหลังที่ดูซอฟต์
        var mainPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(25),
            BackColor = Color.FromArgb(248, 249, 250) // โทนขาว-ครีมอ่อน สบายตา
        };

        var titleLabel = new Label
        {
            Text = "ลงทะเบียนเปิดใช้งานระบบ (License Activation)",
            Font = new Font("Segoe UI", 13.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(43, 45, 66), // สีน้ำเงินเข้มหม่น สไตล์พรีเมียม
            Location = new Point(25, 20),
            AutoSize = true
        };

        _lblStatus = new Label
        {
            Text = $"สถานะปัจจุบัน: {currentStatusText}",
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            ForeColor = Color.FromArgb(230, 57, 70), // แดงสว่าง เตือนสายตาได้ดี
            Location = new Point(25, 48),
            AutoSize = true
        };

        // กรอบจัดกลุ่มฟอร์มให้อยู่กึ่งกลางดูลงตัว
        var gbForm = new GroupBox
        {
            Text = " กรอกข้อมูลลิขสิทธิ์ ",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(108, 117, 125),
            Location = new Point(25, 80),
            Size = new Size(530, 260),
            FlatStyle = FlatStyle.Flat
        };

        var labelFont = new Font("Segoe UI", 9.5F, FontStyle.Regular);
        var inputFont = new Font("Consolas", 10F);

        var lblHw = new Label
        {
            Text = "Hardware ID ของเครื่องนี้ (คัดลอกส่งให้ผู้ขายเพื่อรับคีย์):",
            Font = labelFont,
            ForeColor = Color.FromArgb(43, 45, 66),
            Location = new Point(20, 28),
            Size = new Size(490, 20)
        };

        _tbHardwareId = new TextBox
        {
            Text = HardwareIdGenerator.Generate(),
            ReadOnly = true,
            Font = inputFont,
            BackColor = Color.FromArgb(233, 236, 239), // สีเทาอ่อนของช่องที่ห้ามแก้ไข
            ForeColor = Color.FromArgb(73, 80, 87),
            Location = new Point(20, 50),
            Width = 360,
            Height = 28,
            BorderStyle = BorderStyle.FixedSingle
        };

        var btnCopyHw = new Button
        {
            Text = "คัดลอก ID เครื่อง",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            BackColor = Color.FromArgb(108, 117, 125),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Location = new Point(390, 49),
            Width = 120,
            Height = 26
        };
        btnCopyHw.FlatAppearance.BorderSize = 0;
        btnCopyHw.Click += (s, e) =>
        {
            Clipboard.SetText(_tbHardwareId.Text);
            MessageBox.Show("คัดลอก Hardware ID สำหรับลงทะเบียนแล้ว", "คัดลอกสำเร็จ", MessageBoxButtons.OK, MessageBoxIcon.Information);
        };

        var lblKey = new Label
        {
            Text = "รหัสคีย์เปิดใช้งาน (วางเนื้อหาคีย์ที่คุณได้รับลงในช่องด้านล่าง):",
            Font = labelFont,
            ForeColor = Color.FromArgb(43, 45, 66),
            Location = new Point(20, 92),
            Size = new Size(490, 20)
        };

        _tbLicenseKey = new TextBox
        {
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Font = inputFont,
            Location = new Point(20, 115),
            Width = 490,
            Height = 125,
            BorderStyle = BorderStyle.FixedSingle
        };

        gbForm.Controls.Add(lblHw);
        gbForm.Controls.Add(_tbHardwareId);
        gbForm.Controls.Add(btnCopyHw);
        gbForm.Controls.Add(lblKey);
        gbForm.Controls.Add(_tbLicenseKey);

        // จัดปุ่มยกเลิกและเปิดใช้งานให้ดูสะอาดตา
        _btnCancel = new Button
        {
            Text = "ยกเลิก (Cancel)",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            BackColor = Color.FromArgb(224, 224, 224),
            ForeColor = Color.FromArgb(43, 45, 66),
            FlatStyle = FlatStyle.Flat,
            Location = new Point(275, 360),
            Width = 130,
            Height = 35
        };
        _btnCancel.FlatAppearance.BorderSize = 0;
        _btnCancel.Click += (s, e) => DialogResult = DialogResult.Cancel;

        _btnActivate = new Button
        {
            Text = "ลงทะเบียน (Activate)",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            BackColor = Color.FromArgb(61, 90, 128), // สีน้ำเงินสไตล์ Modern Steel Blue
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Location = new Point(415, 360),
            Width = 140,
            Height = 35
        };
        _btnActivate.FlatAppearance.BorderSize = 0;
        _btnActivate.Click += BtnActivate_Click;

        mainPanel.Controls.Add(titleLabel);
        mainPanel.Controls.Add(_lblStatus);
        mainPanel.Controls.Add(gbForm);
        mainPanel.Controls.Add(_btnCancel);
        mainPanel.Controls.Add(_btnActivate);

        Controls.Add(mainPanel);
    }

    private void BtnActivate_Click(object? sender, EventArgs e)
    {
        string keyText = _tbLicenseKey.Text.Trim();
        if (string.IsNullOrEmpty(keyText))
        {
            MessageBox.Show("กรุณากรอกข้อความรหัสลิขสิทธิ์ก่อนกดลงทะเบียน", "แจ้งเตือน", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var result = LicenseManager.Activate(keyText);
        if (result.Success)
        {
            MessageBox.Show(result.Message, "เปิดใช้งานระบบสำเร็จ", MessageBoxButtons.OK, MessageBoxIcon.Information);
            DialogResult = DialogResult.OK;
            Close();
        }
        else
        {
            MessageBox.Show(result.Message, "ลงทะเบียนไม่สำเร็จ", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
