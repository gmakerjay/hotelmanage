using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;
using HotelPOS.Common;
using HotelPOS.Licensing;

namespace HotelPOS.LicenseAdminTool;

public class AdminMainForm : Form
{
    // Default Private Key (สอดคล้องกับคีย์สาธารณะในฝั่งไคลเอนต์สำหรับการรันทันที)
    private const string DefaultPrivateKeyBase64 = "MIIEvgIBADANBgkqhkiG9w0BAQEFAASCBKgwggSkAgEAAoIBAQDHGAL/OQhKcQQC1jvBIrntmAX0Sbg/qtYkRm6QN0uvYOX4Mthlu8ADQK6KZSVBYxCXaCA6nho6bTOGpCJmnDakj1BtOs6n3D/LvPKj7MMZ3sCEqvktWiJlKFNPHKtZbMpfXI+bqrxSkCxBDbFmrnG/PaU94rR+bXAluzXbzhcCH6gEmtKTUx6VM+EI/PVIlCdZMjcrkTO7aP7UCMFEnTkvuWMpuuHp1NmWUTEwNvqH9BnkkIdlPIhHpqPdegu93YraD71F5WIG8SU3rSO/wvPgHQTM7HCd8xRbchULLktPrEORHN6JC1ZJBkr1RbacgkHIpljJaxep0Yj/+NHowyl1AgMBAAECggEBAIIY7bRrR0ClszJLXcap84cPZSypk41/C+muYIc6qulST1QtnXx1AFbfyG5FA+BDZM8bSpwjPg5Z12avEI+umoJT6AFIgUvtP37Z3FBD4YWhKnpG4wbAtGMXw8CZglqwHVnNOUZGfkMRVOm5kegAK/IEzVqwLrPCvZraR6p3dE98yseuQdKwy/KNuA0PbCOA8Md8Le+hng36DAAdcn8kHKksi9W8gBqS9qB5LKnla4kXNKeYPGDBKhjaCf45k2aJtnBHMd74/P1y+VkeJMlSjH8elx9rDbzkn+CvmSBY/BDLLlpuD2nftPSuZ8yWNp/krG5lufUdFsFa8kHoqJnH+W0CgYEA15L4D2ZFC7stFenwPLGjbh0SFtQZACzM48xMX3I2Ecuro+qONrdHgZ7Q0wm6b1W1dUkUeNSZ4wMiux/lhhaHYbBqMbjRpIagPGsN6+62KPOsK+L90OqPz5N49BYdF0NuBTQSif1xGP39cv7LX2JwUEYaoSs7lTYVGJ73yQMnU5MCgYEA7G3gIjYt7PTJBWNtVt1dUJ1TNdIz6B6UM/sMWw0t3qCMR4oQBJz7E8NmZLIUeS0TT7McDaaCymnn2/JKBdXWWu8dM8KGjm9tzq6CPPd5Lvt2aUWkijFfwtVg6SYSmwp786SfStsNjXKED7xiqU03GwT8nLf8TewgCB7lV6uBw9cCgYEAlVEVRQVfedqyReV+I2wfeVvlda5/iqF9YaPWmp3vWbArOSR0UO3uN5gbqLGqUweY4p418ePAm39GhTp4rsHYEBAz3jDX9Q/S2UaFpA/6WK8/aD6X9CckaXEKbHcMu1pXUH9a//1uYxM6hHZ7w5vZk6CbPVtGr/l/70fc9XybtsUCgYAtaJDyoStC5mSxZz45v7xLXlv760pS24SlUyM1XZugtX8bwlV/PVMvoYjJ8DXkbBbYaNMLgB6Al8STRr6WzlIkFuap6UOEmbwiRPv4j6MztdIxN9H5RLBasDazsL9EDchurAB4FQhOUV8x0oG0eIML6nJF+0Q3BxHD3YM4ylTa8wKBgB+UjpKeSIcMuG1Em5wbXbLbzNwxjuPX6TwyZKHmzOZZRfZq/4ppJaV66h8pngQC1ZZOBMDI/IWIKorM40hFqHGXmnp+Z7dFwsjoRWoC77/Y6plkb4qq/Od5ZnCVLbBN8uTK3hYdAUd2OfYQ/m6E5CNkSA/xUGGgm8Zhzlf58ezI";

    private RadioButton _rbDefaultKey;
    private RadioButton _rbCustomKey;
    private TextBox _tbPrivateKeyPath;
    private Button _btnBrowseKey;
    private Button _btnGenerateNewKeys;

    private TextBox _tbCustomerName;
    private TextBox _tbHardwareId;
    private Button _btnGetCurrentHardwareId;
    private ComboBox _cbLicenseType;
    private DateTimePicker _dtpExpireDate;
    private CheckBox _chkLimitRooms;
    private NumericUpDown _nudMaxRooms;

    private CheckedListBox _clbFeatures;

    private TextBox _tbGeneratedLicense;
    private Button _btnGenerateLicense;
    private Button _btnSaveLicense;
    private Button _btnCopyLicense;

    public AdminMainForm()
    {
        Text = "HotelPOS TH - เครื่องมือจัดการลิขสิทธิ์ระบบฝั่งผู้ขาย (License Admin Tool)";
        Width = 1000;
        Height = 670;
        FormBorderStyle = FormBorderStyle.FixedSingle; // ล็อกขนาดไม่ให้เพี้ยน
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9.5F); // เปลี่ยนฟอนต์ให้อ่านง่ายขึ้น
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

        InitializeLayout();
        LoadDefaultSettings();
    }

    private void InitializeLayout()
    {
        // กำหนดพื้นหลังแบบซอฟต์คล้ายหน้าแอปหลัก
        BackColor = Color.FromArgb(248, 249, 250);

        var labelFont = new Font("Segoe UI", 9.5F, FontStyle.Regular);
        var inputFont = new Font("Segoe UI", 9.5F, FontStyle.Regular);
        var consoleFont = new Font("Consolas", 9.5F);

        // ==================== [กรอบซ้าย] ตั้งค่าใบอนุญาต (License Info) ====================
        var gbLicenseInfo = new GroupBox
        {
            Text = " ข้อมูลใบอนุญาตลิขสิทธิ์ (License Info) ",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(108, 117, 125),
            Location = new Point(15, 15),
            Size = new Size(450, 600),
            FlatStyle = FlatStyle.Flat
        };

        // ชื่อลูกค้า
        var lblCustomer = new Label
        {
            Text = "ชื่อลูกค้า / โรงแรมที่พัก:",
            Font = labelFont,
            ForeColor = Color.FromArgb(43, 45, 66),
            Location = new Point(20, 30),
            Size = new Size(410, 20)
        };
        _tbCustomerName = new TextBox
        {
            Font = inputFont,
            Location = new Point(20, 52),
            Width = 410,
            BorderStyle = BorderStyle.FixedSingle
        };

        // Hardware ID ของลูกค้า
        var lblHw = new Label
        {
            Text = "Hardware ID ของลูกค้า (ระบุระบุได้ 64 อักขระ):",
            Font = labelFont,
            ForeColor = Color.FromArgb(43, 45, 66),
            Location = new Point(20, 92),
            Size = new Size(410, 20)
        };
        _tbHardwareId = new TextBox
        {
            Font = consoleFont,
            Location = new Point(20, 114),
            Width = 270,
            BorderStyle = BorderStyle.FixedSingle
        };
        _btnGetCurrentHardwareId = new Button
        {
            Text = "ใช้ ID เครื่องนี้",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            BackColor = Color.FromArgb(108, 117, 125),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Location = new Point(300, 113),
            Width = 130,
            Height = 26
        };
        _btnGetCurrentHardwareId.FlatAppearance.BorderSize = 0;
        _btnGetCurrentHardwareId.Click += (s, e) => _tbHardwareId.Text = HardwareIdGenerator.Generate();

        // ประเภทสิทธิ์
        var lblType = new Label
        {
            Text = "ประเภทใบอนุญาตใช้งาน:",
            Font = labelFont,
            ForeColor = Color.FromArgb(43, 45, 66),
            Location = new Point(20, 155),
            Size = new Size(410, 20)
        };
        _cbLicenseType = new ComboBox
        {
            Font = inputFont,
            Location = new Point(20, 177),
            Width = 410,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _cbLicenseType.Items.Add("Trial (ทดลองใช้งาน)");
        _cbLicenseType.Items.Add("Standard (รายปี/จำกัดเวลา)");
        _cbLicenseType.Items.Add("Lifetime (ใช้งานถาวร)");
        _cbLicenseType.SelectedIndex = 1;
        _cbLicenseType.SelectedIndexChanged += LicenseType_Changed;

        // วันหมดอายุ
        var lblExpire = new Label
        {
            Text = "วันที่หมดอายุสิทธิ์การใช้งาน (Standard / Trial):",
            Font = labelFont,
            ForeColor = Color.FromArgb(43, 45, 66),
            Location = new Point(20, 218),
            Size = new Size(410, 20)
        };
        _dtpExpireDate = new DateTimePicker
        {
            Font = inputFont,
            Location = new Point(20, 240),
            Width = 410,
            Format = DateTimePickerFormat.Short,
            Value = DateTime.Today.AddYears(1)
        };

        // จำกัดห้องพัก
        _chkLimitRooms = new CheckBox
        {
            Text = "จำกัดจำนวนห้องพักสูงสุด:",
            Font = labelFont,
            ForeColor = Color.FromArgb(43, 45, 66),
            Location = new Point(20, 290),
            Size = new Size(200, 25),
            Checked = false
        };
        _chkLimitRooms.CheckedChanged += (s, e) => _nudMaxRooms.Enabled = _chkLimitRooms.Checked;
        _nudMaxRooms = new NumericUpDown
        {
            Font = inputFont,
            Location = new Point(230, 290),
            Width = 120,
            Minimum = 1,
            Maximum = 9999,
            Value = 50,
            Enabled = false
        };

        // ฟีเจอร์แพ็กเกจ
        var lblFeatures = new Label
        {
            Text = "โมดูลที่เปิดใช้งานในระบบ (Features):",
            Font = labelFont,
            ForeColor = Color.FromArgb(43, 45, 66),
            Location = new Point(20, 335),
            Size = new Size(410, 20)
        };
        _clbFeatures = new CheckedListBox
        {
            Font = inputFont,
            Location = new Point(20, 357),
            Width = 410,
            Height = 110,
            BorderStyle = BorderStyle.FixedSingle
        };
        _clbFeatures.Items.Add("BOOKING (ระบบจองและผังเช็คอินห้องพัก)", true);
        _clbFeatures.Items.Add("POS (ระบบขายมินิบาร์สินค้าหน้าร้าน)", true);
        _clbFeatures.Items.Add("REPORT (รายงานวิเคราะห์สถิติยอดขาย)", true);

        // ปุ่มคำนวณออกรหัสสิทธิ์
        _btnGenerateLicense = new Button
        {
            Text = "สร้างไฟล์และดิจิทัลซิกเนเจอร์ (Generate)",
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
            BackColor = Color.FromArgb(61, 90, 128), // Premium Steel Blue
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Location = new Point(20, 520),
            Width = 410,
            Height = 45
        };
        _btnGenerateLicense.FlatAppearance.BorderSize = 0;
        _btnGenerateLicense.Click += GenerateLicense_Click;

        gbLicenseInfo.Controls.Add(lblCustomer);
        gbLicenseInfo.Controls.Add(_tbCustomerName);
        gbLicenseInfo.Controls.Add(lblHw);
        gbLicenseInfo.Controls.Add(_tbHardwareId);
        gbLicenseInfo.Controls.Add(_btnGetCurrentHardwareId);
        gbLicenseInfo.Controls.Add(lblType);
        gbLicenseInfo.Controls.Add(_cbLicenseType);
        gbLicenseInfo.Controls.Add(lblExpire);
        gbLicenseInfo.Controls.Add(_dtpExpireDate);
        gbLicenseInfo.Controls.Add(_chkLimitRooms);
        gbLicenseInfo.Controls.Add(_nudMaxRooms);
        gbLicenseInfo.Controls.Add(lblFeatures);
        gbLicenseInfo.Controls.Add(_clbFeatures);
        gbLicenseInfo.Controls.Add(_btnGenerateLicense);


        // ==================== [กรอบขวาบน] คีย์ลายเซ็น (RSA Cryptography) ====================
        var gbKeys = new GroupBox
        {
            Text = " กุญแจและลายเซ็นดิจิทัล (RSA Cryptography) ",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(108, 117, 125),
            Location = new Point(485, 15),
            Size = new Size(480, 185),
            FlatStyle = FlatStyle.Flat
        };

        _rbDefaultKey = new RadioButton
        {
            Text = "ใช้ Default Private Key (คีย์ร่วมสำหรับการทดสอบระบบ)",
            Font = labelFont,
            ForeColor = Color.FromArgb(43, 45, 66),
            Checked = true,
            Location = new Point(20, 25),
            Size = new Size(440, 24)
        };
        _rbDefaultKey.CheckedChanged += (s, e) => ToggleKeyPathControls();

        _rbCustomKey = new RadioButton
        {
            Text = "ใช้ Custom Private Key (คู่คีย์เฉพาะกรณีจำหน่ายจริง)",
            Font = labelFont,
            ForeColor = Color.FromArgb(43, 45, 66),
            Checked = false,
            Location = new Point(20, 52),
            Size = new Size(440, 24)
        };

        var lblKeyFile = new Label
        {
            Text = "ตำแหน่งไฟล์กุญแจสำคัญส่วนตัว (Private Key File):",
            Font = labelFont,
            ForeColor = Color.FromArgb(43, 45, 66),
            Location = new Point(20, 85),
            Size = new Size(440, 18)
        };

        _tbPrivateKeyPath = new TextBox
        {
            Font = inputFont,
            Location = new Point(20, 106),
            Width = 270,
            Enabled = false,
            BorderStyle = BorderStyle.FixedSingle
        };

        _btnBrowseKey = new Button
        {
            Text = "เลือก...",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            BackColor = Color.FromArgb(108, 117, 125),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Location = new Point(300, 105),
            Width = 60,
            Height = 26,
            Enabled = false
        };
        _btnBrowseKey.FlatAppearance.BorderSize = 0;
        _btnBrowseKey.Click += BrowsePrivateKey_Click;

        _btnGenerateNewKeys = new Button
        {
            Text = "สร้างคีย์ใหม่",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            BackColor = Color.FromArgb(108, 117, 125),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Location = new Point(370, 105),
            Width = 90,
            Height = 26,
            Enabled = false
        };
        _btnGenerateNewKeys.FlatAppearance.BorderSize = 0;
        _btnGenerateNewKeys.Click += GenerateNewKeys_Click;

        var lblKeyWarn = new Label
        {
            Text = "ℹ️ เมื่อใช้ Custom Key ต้องแก้ PublicKeyBase64 ในแอปลูกค้าให้ตรงกัน",
            Font = new Font("Segoe UI", 8.5F, FontStyle.Italic),
            ForeColor = Color.FromArgb(108, 117, 125),
            Location = new Point(20, 145),
            Size = new Size(440, 25)
        };

        gbKeys.Controls.Add(_rbDefaultKey);
        gbKeys.Controls.Add(_rbCustomKey);
        gbKeys.Controls.Add(lblKeyFile);
        gbKeys.Controls.Add(_tbPrivateKeyPath);
        gbKeys.Controls.Add(_btnBrowseKey);
        gbKeys.Controls.Add(_btnGenerateNewKeys);
        gbKeys.Controls.Add(lblKeyWarn);


        // ==================== [กรอบขวาล่าง] รหัสผลลัพธ์ (license.dat) ====================
        var gbOutput = new GroupBox
        {
            Text = " รหัสลิขสิทธิ์ผลลัพธ์ (license.dat Output) ",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(108, 117, 125),
            Location = new Point(485, 215),
            Size = new Size(480, 400),
            FlatStyle = FlatStyle.Flat
        };

        _tbGeneratedLicense = new TextBox
        {
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Font = consoleFont,
            Location = new Point(20, 28),
            Width = 440,
            Height = 290,
            ReadOnly = true,
            BorderStyle = BorderStyle.FixedSingle
        };

        _btnCopyLicense = new Button
        {
            Text = "คัดลอกข้อความ (Copy)",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            BackColor = Color.FromArgb(108, 117, 125),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Location = new Point(20, 335),
            Width = 210,
            Height = 42
        };
        _btnCopyLicense.FlatAppearance.BorderSize = 0;
        _btnCopyLicense.Click += (s, e) =>
        {
            if (!string.IsNullOrEmpty(_tbGeneratedLicense.Text))
            {
                Clipboard.SetText(_tbGeneratedLicense.Text);
                MessageBox.Show("คัดลอกรหัสข้อมูลลิขสิทธิ์ลงคลิปบอร์ดสำเร็จ", "คัดลอกแล้ว", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        };

        _btnSaveLicense = new Button
        {
            Text = "บันทึกเป็นไฟล์... (Save)",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            BackColor = Color.FromArgb(108, 117, 125),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Location = new Point(250, 335),
            Width = 210,
            Height = 42
        };
        _btnSaveLicense.FlatAppearance.BorderSize = 0;
        _btnSaveLicense.Click += SaveLicense_Click;

        gbOutput.Controls.Add(_tbGeneratedLicense);
        gbOutput.Controls.Add(_btnCopyLicense);
        gbOutput.Controls.Add(_btnSaveLicense);

        // นำเข้ามาประกอบร่างลง Form
        Controls.Add(gbLicenseInfo);
        Controls.Add(gbKeys);
        Controls.Add(gbOutput);
    }

    private void LoadDefaultSettings()
    {
        _tbCustomerName.Text = "โรงแรมตัวอย่าง แสนสุขสบาย";
        _tbHardwareId.Text = string.Empty;
        
        var customKeyPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "HotelPOSAdmin", "private_key.txt");
        _tbPrivateKeyPath.Text = customKeyPath;
    }

    private void ToggleKeyPathControls()
    {
        bool custom = _rbCustomKey.Checked;
        _tbPrivateKeyPath.Enabled = custom;
        _btnBrowseKey.Enabled = custom;
        _btnGenerateNewKeys.Enabled = custom;
    }

    private void LicenseType_Changed(object? sender, EventArgs e)
    {
        int index = _cbLicenseType.SelectedIndex;
        if (index == 2) // Lifetime
        {
            _dtpExpireDate.Enabled = false;
        }
        else
        {
            _dtpExpireDate.Enabled = true;
            if (index == 0) // Trial
            {
                _dtpExpireDate.Value = DateTime.Today.AddDays(30);
            }
        }
    }

    private void BrowsePrivateKey_Click(object? sender, EventArgs e)
    {
        using var ofd = new OpenFileDialog
        {
            Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
            Title = "เลือกไฟล์ Private Key"
        };
        if (ofd.ShowDialog() == DialogResult.OK)
        {
            _tbPrivateKeyPath.Text = ofd.FileName;
        }
    }

    private void GenerateNewKeys_Click(object? sender, EventArgs e)
    {
        try
        {
            using var rsa = RSA.Create(2048);
            var pubKey = Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo());
            var privKey = Convert.ToBase64String(rsa.ExportPkcs8PrivateKey());

            var folder = Path.GetDirectoryName(_tbPrivateKeyPath.Text);
            if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            File.WriteAllText(_tbPrivateKeyPath.Text, privKey);

            var pubKeyPath = Path.Combine(folder ?? "", "public_key_to_paste_in_client.txt");
            File.WriteAllText(pubKeyPath, pubKey);

            var msg = $"สร้างคู่กุญแจ RSA ใหม่สำเร็จ!\n\n" +
                      $"1. บันทึก Private Key ไปยัง:\n{_tbPrivateKeyPath.Text}\n\n" +
                      $"2. บันทึก Public Key (สำหรับไปวางแทนค่าเดิมใน LicenseValidator.cs) ไปยัง:\n{pubKeyPath}\n\n" +
                      $"กรุณานำคีย์สาธารณะในข้อ 2 ไปอัปเดตลงในซอร์สโค้ดไฟล์ LicenseValidator.cs";

            MessageBox.Show(msg, "สร้างคู่กุญแจสำเร็จ", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"เกิดข้อผิดพลาด: {ex.Message}", "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void GenerateLicense_Click(object? sender, EventArgs e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_tbCustomerName.Text))
            {
                MessageBox.Show("กรุณากรอกชื่อลูกค้า", "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(_tbHardwareId.Text) || _tbHardwareId.Text.Trim().Length < 10)
            {
                MessageBox.Show("กรุณากรอก Hardware ID ของเครื่องลูกค้าที่ถูกต้อง", "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 1. โหลด Private Key
            string privateKeyBase64 = "";
            if (_rbDefaultKey.Checked)
            {
                privateKeyBase64 = DefaultPrivateKeyBase64;
            }
            else
            {
                if (!File.Exists(_tbPrivateKeyPath.Text))
                {
                    MessageBox.Show("ไม่พบไฟล์คีย์ส่วนตัว! กรุณากดปุ่มสร้างคีย์คู่ใหม่ก่อน", "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                privateKeyBase64 = File.ReadAllText(_tbPrivateKeyPath.Text).Trim();
            }

            // 2. สร้างโครงข้อมูล LicenseFile
            var license = new LicenseFile
            {
                CustomerName = _tbCustomerName.Text.Trim(),
                HardwareId = _tbHardwareId.Text.Trim(),
                LicenseType = (LicenseType)_cbLicenseType.SelectedIndex,
                IssueDate = DateTime.Today,
                ExpireDate = _cbLicenseType.SelectedIndex == 2 ? null : _dtpExpireDate.Value.Date,
                MaxRooms = _chkLimitRooms.Checked ? (int)_nudMaxRooms.Value : null
            };

            // กรองฟีเจอร์ที่ติ๊กเลือก
            foreach (var item in _clbFeatures.CheckedItems)
            {
                string text = item.ToString() ?? "";
                if (text.Contains("BOOKING")) license.Features.Add("BOOKING");
                if (text.Contains("POS")) license.Features.Add("POS");
                if (text.Contains("REPORT")) license.Features.Add("REPORT");
            }

            // 3. คำนวณค่าแฮชและเซ็นลายเซ็นดิจิทัล
            string signableData = license.GetSignableData();
            byte[] dataBytes = Encoding.UTF8.GetBytes(signableData);

            using var rsa = RSA.Create();
            rsa.ImportPkcs8PrivateKey(Convert.FromBase64String(privateKeyBase64), out _);
            byte[] signatureBytes = rsa.SignData(dataBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            
            license.Signature = Convert.ToBase64String(signatureBytes);

            // 4. แปลงโครงสิทธิ์เป็น JSON และนำเสนอ
            _tbGeneratedLicense.Text = license.ToJson();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"เกิดข้อผิดพลาดในการสร้างสิทธิ์ลิขสิทธิ์: {ex.Message}", "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SaveLicense_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(_tbGeneratedLicense.Text))
        {
            MessageBox.Show("ไม่มีข้อมูลรหัสลิขสิทธิ์ที่จะบันทึก กรุณากดปุ่มสร้างลิขสิทธิ์ก่อน", "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using var sfd = new SaveFileDialog
        {
            Filter = "License Data (*.dat)|*.dat|All Files (*.*)|*.*",
            FileName = "license.dat",
            Title = "บันทึกไฟล์ลิขสิทธิ์ออกใบอนุญาต"
        };

        if (sfd.ShowDialog() == DialogResult.OK)
        {
            try
            {
                File.WriteAllText(sfd.FileName, _tbGeneratedLicense.Text);
                MessageBox.Show("บันทึกไฟล์ลิขสิทธิ์สำเร็จแล้ว!", "สำเร็จ", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"ไม่สามารถบันทึกไฟล์ได้: {ex.Message}", "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
