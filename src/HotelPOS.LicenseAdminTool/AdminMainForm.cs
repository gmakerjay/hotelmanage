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
    // Private Key ถูกโหลดจากไฟล์ภายนอก (private.pem) เท่านั้น เพื่อความปลอดภัย
    // ห้ามฝัง Private Key ไว้ในซอร์สโค้ดเด็ดขาด
    private string? _privateKeyBase64;

    private string GetPrivateKeyBase64()
    {
        if (!string.IsNullOrEmpty(_privateKeyBase64)) return _privateKeyBase64;

        // ลองอ่านจากไฟล์ private.pem ที่อยู่ข้าง .exe ก่อน
        string exeDir = AppContext.BaseDirectory;
        string[] keyFileNames = { "private.pem", "private.key", "rsa_private.pem" };
        foreach (var name in keyFileNames)
        {
            string path = Path.Combine(exeDir, name);
            if (File.Exists(path))
            {
                _privateKeyBase64 = File.ReadAllText(path).Trim()
                    .Replace("-----BEGIN PRIVATE KEY-----", "")
                    .Replace("-----END PRIVATE KEY-----", "")
                    .Replace("\r", "")
                    .Replace("\n", "")
                    .Trim();
                return _privateKeyBase64;
            }
        }

        throw new FileNotFoundException(
            "ไม่พบไฟล์ Private Key (private.pem) ในโฟลเดอร์โปรแกรม\n\n" +
            $"กรุณาวางไฟล์ private.pem ไว้ที่: {exeDir}\n\n" +
            "ไฟล์นี้จำเป็นสำหรับการลงลายเซ็นดิจิทัล RSA-2048 ลงบน USB Dongle Key");
    }

    private List<UsbDriveInfo> _connectedDrives = new();

    private ComboBox _cbUsbDrives = null!;
    private Button _btnRefreshDrives = null!;
    private Label _lblDriveDetail = null!;

    private TextBox _tbAppSerial = null!;
    private Button _btnSaveWatermark = null!;

    private ComboBox _cbLicenseType = null!;
    private DateTimePicker _dtpExpireDate = null!;
    private Label _lblExpire = null!;

    private CheckBox _chkFormatUsb = null!;
    private ComboBox _cbFileSystem = null!;
    private TextBox _tbVolumeLabel = null!;

    private Button _btnGenKey = null!;
    private Button _btnEditKey = null!;
    private Button _btnTestUnlock = null!;
    private Button _btnViewPayload = null!;

    private ProgressBar _progressBar = null!;
    private Label _lblStatus = null!;

    public AdminMainForm()
    {
        Text = "PSoft Rest & Rent Manager - เครื่องมือจัดการกุญแจ USB Dongle (สำหรับผู้พัฒนาเท่านั้น)";
        Width = 600;
        Height = 630;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9.5F);
        BackColor = Color.FromArgb(245, 247, 250);

        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

        InitializeLayout();
        RefreshUsbDrives();
    }

    private void InitializeLayout()
    {
        var labelFont = new Font("Segoe UI", 9.5F, FontStyle.Bold);
        var subFont = new Font("Segoe UI", 9.0F, FontStyle.Regular);
        var inputFont = new Font("Segoe UI", 9.5F, FontStyle.Regular);

        // ==================== [ส่วนที่ 1] เลือกแฟลชไดรฟ์ ====================
        var gbDrive = new GroupBox
        {
            Text = " 1. เลือก USB Flash Drive ที่เสียบอยู่ ",
            Font = labelFont,
            ForeColor = Color.FromArgb(30, 41, 59),
            Location = new Point(18, 15),
            Size = new Size(548, 115),
            FlatStyle = FlatStyle.Flat
        };

        var lblDrive = new Label { Text = "แฟลชไดรฟ์เป้าหมาย:", Font = subFont, Location = new Point(20, 28), AutoSize = true };
        _cbUsbDrives = new ComboBox
        {
            Font = inputFont,
            Location = new Point(20, 50),
            Width = 400,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _cbUsbDrives.SelectedIndexChanged += UsbDrives_SelectedIndexChanged;

        _btnRefreshDrives = new Button
        {
            Text = "รีเฟรช",
            Font = new Font("Segoe UI", 9.0F, FontStyle.Bold),
            BackColor = Color.FromArgb(71, 85, 105),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Location = new Point(430, 49),
            Width = 98,
            Height = 28
        };
        _btnRefreshDrives.FlatAppearance.BorderSize = 0;
        _btnRefreshDrives.Click += (s, e) => RefreshUsbDrives();

        _lblDriveDetail = new Label
        {
            Text = "รายละเอียดไดรฟ์: กรุณาเลือกแฟลชไดรฟ์",
            Font = new Font("Segoe UI", 8.5F, FontStyle.Italic),
            ForeColor = Color.FromArgb(100, 116, 139),
            Location = new Point(20, 84),
            Size = new Size(508, 20)
        };

        gbDrive.Controls.Add(lblDrive);
        gbDrive.Controls.Add(_cbUsbDrives);
        gbDrive.Controls.Add(_btnRefreshDrives);
        gbDrive.Controls.Add(_lblDriveDetail);

        // ==================== [ส่วนที่ 2] การตั้งค่าสิทธิ์ (License Config) ====================
        var gbConfig = new GroupBox
        {
            Text = " 2. ตั้งค่าคีย์และ App Serial Watermark ",
            Font = labelFont,
            ForeColor = Color.FromArgb(30, 41, 59),
            Location = new Point(18, 140),
            Size = new Size(548, 135),
            FlatStyle = FlatStyle.Flat
        };

        // App Serial Watermark
        var lblSerial = new Label { Text = "App Serial Watermark (รหัสลายน้ำโปรแกรม):", Font = subFont, Location = new Point(20, 26), AutoSize = true };
        _tbAppSerial = new TextBox
        {
            Text = "APP-2026-CLIENT-A",
            Font = new Font("Consolas", 9.5F),
            Location = new Point(20, 46),
            Width = 150,
            BorderStyle = BorderStyle.FixedSingle
        };

        _btnSaveWatermark = new Button
        {
            Text = "เซฟ app.watermark",
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            BackColor = Color.FromArgb(72, 149, 239),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Location = new Point(175, 45),
            Width = 95,
            Height = 26
        };
        _btnSaveWatermark.FlatAppearance.BorderSize = 0;
        _btnSaveWatermark.Click += SaveWatermark_Click;

        // ประเภทสิทธิ์
        var lblType = new Label { Text = "ประเภทสิทธิ์ใช้งาน:", Font = subFont, Location = new Point(280, 26), AutoSize = true };
        _cbLicenseType = new ComboBox
        {
            Font = inputFont,
            Location = new Point(280, 46),
            Width = 248,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _cbLicenseType.Items.Add("Lifetime (ใช้งานถาวร)");
        _cbLicenseType.Items.Add("Standard (รายปี / กำหนดวัน)");
        _cbLicenseType.Items.Add("Trial (ทดลองใช้งาน 30 วัน)");
        _cbLicenseType.SelectedIndex = 0; // Default Lifetime
        _cbLicenseType.SelectedIndexChanged += LicenseType_SelectedIndexChanged;

        // วันหมดอายุ
        _lblExpire = new Label { Text = "วันหมดอายุสิทธิ์:", Font = subFont, Location = new Point(280, 80), AutoSize = true, Visible = false };
        _dtpExpireDate = new DateTimePicker
        {
            Font = inputFont,
            Location = new Point(375, 78),
            Width = 153,
            Format = DateTimePickerFormat.Short,
            Value = DateTime.Today.AddYears(1),
            Visible = false
        };

        var lblNote = new Label
        {
            Text = "ผูกสิทธิ์กับ Physical USB Serial ระดับชิป ป้องกันการคัดลอกไฟล์ข้ามไดรฟ์ 100%",
            Font = new Font("Segoe UI", 8.5F, FontStyle.Italic),
            ForeColor = Color.FromArgb(71, 85, 105),
            Location = new Point(20, 104),
            Size = new Size(508, 20)
        };

        gbConfig.Controls.Add(lblSerial);
        gbConfig.Controls.Add(_tbAppSerial);
        gbConfig.Controls.Add(_btnSaveWatermark);
        gbConfig.Controls.Add(lblType);
        gbConfig.Controls.Add(_cbLicenseType);
        gbConfig.Controls.Add(_lblExpire);
        gbConfig.Controls.Add(_dtpExpireDate);
        gbConfig.Controls.Add(lblNote);

        // ==================== [ส่วนที่ 3] ตัวเลือกการฟอร์แมต ====================
        var gbFormat = new GroupBox
        {
            Text = " 3. ตัวเลือกการฟอร์แมตก่อนเขียนคีย์ ",
            Font = labelFont,
            ForeColor = Color.FromArgb(30, 41, 59),
            Location = new Point(18, 285),
            Size = new Size(548, 110),
            FlatStyle = FlatStyle.Flat
        };

        _chkFormatUsb = new CheckBox
        {
            Text = "ฟอร์แมต USB Drive (Quick Format) ก่อนเขียนคีย์ใหม่",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(185, 28, 28),
            Location = new Point(20, 25),
            Size = new Size(508, 25),
            Checked = true
        };
        _chkFormatUsb.CheckedChanged += (s, e) =>
        {
            _cbFileSystem.Enabled = _chkFormatUsb.Checked;
            _tbVolumeLabel.Enabled = _chkFormatUsb.Checked;
        };

        var lblFs = new Label { Text = "ระบบไฟล์:", Font = subFont, Location = new Point(40, 60), AutoSize = true };
        _cbFileSystem = new ComboBox
        {
            Font = inputFont,
            Location = new Point(110, 57),
            Width = 100,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _cbFileSystem.Items.Add("FAT32");
        _cbFileSystem.Items.Add("NTFS");
        _cbFileSystem.SelectedIndex = 0; // Default FAT32 (Recommended)

        var lblVol = new Label { Text = "ชื่อไดรฟ์:", Font = subFont, Location = new Point(230, 60), AutoSize = true };
        _tbVolumeLabel = new TextBox
        {
            Text = "REST_RENT_KEY",
            Font = inputFont,
            Location = new Point(290, 57),
            Width = 238,
            BorderStyle = BorderStyle.FixedSingle
        };

        gbFormat.Controls.Add(_chkFormatUsb);
        gbFormat.Controls.Add(lblFs);
        gbFormat.Controls.Add(_cbFileSystem);
        gbFormat.Controls.Add(lblVol);
        gbFormat.Controls.Add(_tbVolumeLabel);

        // ==================== [ส่วนที่ 4] ปุ่มGen Key & แก้ไขคีย์ ====================
        _btnGenKey = new Button
        {
            Text = "Gen Key (สร้างคีย์ใหม่ลง USB)",
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
            BackColor = Color.FromArgb(16, 185, 129), // Emerald Green
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Location = new Point(18, 408),
            Width = 268,
            Height = 45
        };
        _btnGenKey.FlatAppearance.BorderSize = 0;
        _btnGenKey.Click += (s, e) => SaveOrUpdateKey(isFormatAndNew: true);

        _btnEditKey = new Button
        {
            Text = "แก้ไขคีย์ (อัปเดตคีย์เดิมใน USB)",
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
            BackColor = Color.FromArgb(37, 99, 235), // Royal Blue
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Location = new Point(298, 408),
            Width = 268,
            Height = 45
        };
        _btnEditKey.FlatAppearance.BorderSize = 0;
        _btnEditKey.Click += (s, e) => SaveOrUpdateKey(isFormatAndNew: false);

        _btnTestUnlock = new Button
        {
            Text = "ทดสอบปลดล็อก (Validate)",
            Font = new Font("Segoe UI", 9.0F, FontStyle.Bold),
            BackColor = Color.FromArgb(245, 158, 11), // Amber
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Location = new Point(18, 462),
            Width = 268,
            Height = 35
        };
        _btnTestUnlock.FlatAppearance.BorderSize = 0;
        _btnTestUnlock.Click += TestUnlock_Click;

        _btnViewPayload = new Button
        {
            Text = "ดูข้อมูลคีย์ JSON Payload",
            Font = new Font("Segoe UI", 9.0F, FontStyle.Bold),
            BackColor = Color.FromArgb(100, 116, 139), // Slate
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Location = new Point(298, 462),
            Width = 268,
            Height = 35
        };
        _btnViewPayload.FlatAppearance.BorderSize = 0;
        _btnViewPayload.Click += ViewPayload_Click;

        // Progress Bar & Status
        _progressBar = new ProgressBar
        {
            Location = new Point(18, 508),
            Size = new Size(548, 12),
            Style = ProgressBarStyle.Blocks,
            Value = 0
        };

        _lblStatus = new Label
        {
            Text = "สถานะ: พร้อมใช้งาน",
            Font = new Font("Segoe UI", 9.0F, FontStyle.Regular),
            ForeColor = Color.FromArgb(51, 65, 85),
            Location = new Point(18, 526),
            Size = new Size(548, 22)
        };

        // นำองค์ประกอบทั้งหมดใส่ Form
        Controls.Add(gbDrive);
        Controls.Add(gbConfig);
        Controls.Add(gbFormat);
        Controls.Add(_btnGenKey);
        Controls.Add(_btnEditKey);
        Controls.Add(_btnTestUnlock);
        Controls.Add(_btnViewPayload);
        Controls.Add(_progressBar);
        Controls.Add(_lblStatus);
    }

    private void SaveWatermark_Click(object? sender, EventArgs e)
    {
        string appSerial = _tbAppSerial.Text.Trim();
        if (string.IsNullOrEmpty(appSerial))
        {
            MessageBox.Show("กรุณากรอก App Serial Watermark", "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using var sfd = new SaveFileDialog
        {
            Filter = "Watermark File (*.watermark)|*.watermark|All Files (*.*)|*.*",
            FileName = "app.watermark",
            Title = "บันทึกไฟล์ app.watermark สำหรับวางข้าง PSoftRestRentManager.exe"
        };

        if (sfd.ShowDialog() == DialogResult.OK)
        {
            try
            {
                var watermark = new AppWatermarkFile
                {
                    AppSerial = appSerial,
                    IssuedTo = "PSoft-RestRentManager-CLIENT",
                    IssuedDate = DateTime.Today
                };

                string watermarkSignable = watermark.GetSignableData();
                using (var rsaW = RSA.Create())
                {
                    rsaW.ImportPkcs8PrivateKey(Convert.FromBase64String(GetPrivateKeyBase64()), out _);
                    byte[] wSig = rsaW.SignData(Encoding.UTF8.GetBytes(watermarkSignable), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                    watermark.Signature = Convert.ToBase64String(wSig);
                }

                File.WriteAllText(sfd.FileName, watermark.ToJson());
                MessageBox.Show($"บันทึกไฟล์ app.watermark (AppSerial: {appSerial}) สำเร็จเรียบร้อย!\n\nกรุณานำไฟล์นี้ไปวางในโฟลเดอร์เดียวกับโปรแกรม PSoftRestRentManager.exe ของลูกค้า", "สำเร็จ", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"ไม่สามารถบันทึกไฟล์ได้: {ex.Message}", "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void RefreshUsbDrives()
    {
        _cbUsbDrives.Items.Clear();
        _connectedDrives = UsbDongleManager.GetConnectedUsbDrives();

        if (_connectedDrives.Count > 0)
        {
            foreach (var drive in _connectedDrives)
            {
                string label = string.IsNullOrWhiteSpace(drive.VolumeLabel) ? "NO LABEL" : drive.VolumeLabel;
                _cbUsbDrives.Items.Add($"{drive.DriveLetter}\\ ({label}) - HWID:{drive.UsbHardwareId.Substring(0, 12)}...");
            }
            _cbUsbDrives.SelectedIndex = 0;
            _btnGenKey.Enabled = true;
            _btnEditKey.Enabled = true;
            _btnTestUnlock.Enabled = true;
        }
        else
        {
            _cbUsbDrives.Items.Add("ไม่พบ USB Flash Drive (กรุณาเสียบแฟลชไดรฟ์แล้วกดรีเฟรช)");
            _cbUsbDrives.SelectedIndex = 0;
            _lblDriveDetail.Text = "รายละเอียดไดรฟ์: ไม่พบแฟลชไดรฟ์";
            _btnGenKey.Enabled = false;
            _btnEditKey.Enabled = false;
            _btnTestUnlock.Enabled = false;
        }
    }

    private void UsbDrives_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_connectedDrives.Count > 0 && _cbUsbDrives.SelectedIndex >= 0 && _cbUsbDrives.SelectedIndex < _connectedDrives.Count)
        {
            var drive = _connectedDrives[_cbUsbDrives.SelectedIndex];
            _lblDriveDetail.Text = $"รายละเอียดไดรฟ์: ไดรฟ์ {drive.DriveLetter}\\ | Serial: {drive.PhysicalSerial} | HWID: {drive.UsbHardwareId}";
            _lblStatus.Text = $"เลือกไดรฟ์ {drive.DriveLetter}\\ พร้อมเปิดใช้งาน";

            // ลอง Auto-load คีย์เดิมที่มีใน USB Drive (ถ้ามี) สำหรับปุ่มแก้ไข
            LoadExistingKeyFromUsb(drive);
        }
    }

    private void LoadExistingKeyFromUsb(UsbDriveInfo drive)
    {
        try
        {
            string donglePath = Path.Combine(drive.DriveLetter + "\\", UsbDongleManager.DongleFileName);
            if (File.Exists(donglePath))
            {
                string json = File.ReadAllText(donglePath);
                var license = LicenseFile.FromJson(json);
                if (license != null)
                {
                    if (!string.IsNullOrEmpty(license.AppSerial))
                        _tbAppSerial.Text = license.AppSerial;

                    _cbLicenseType.SelectedIndex = license.LicenseType switch
                    {
                        LicenseType.Lifetime => 0,
                        LicenseType.Standard => 1,
                        _ => 2
                    };

                    if (license.ExpireDate.HasValue)
                        _dtpExpireDate.Value = license.ExpireDate.Value;

                    _lblStatus.Text = $"พบไฟล์ dongle.key เดิมในไดรฟ์ {drive.DriveLetter}\\ (โหลดข้อมูลสำเร็จ สามารถปรับแก้ไขได้เลย)";
                }
            }
        }
        catch { }
    }

    private void LicenseType_SelectedIndexChanged(object? sender, EventArgs e)
    {
        bool isStandard = _cbLicenseType.SelectedIndex == 1;
        _lblExpire.Visible = isStandard;
        _dtpExpireDate.Visible = isStandard;
    }

    private async void SaveOrUpdateKey(bool isFormatAndNew)
    {
        if (_connectedDrives.Count == 0 || _cbUsbDrives.SelectedIndex < 0 || _cbUsbDrives.SelectedIndex >= _connectedDrives.Count)
        {
            MessageBox.Show("กรุณาเลือก USB Flash Drive ก่อนทำรายการ", "คำเตือน", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var drive = _connectedDrives[_cbUsbDrives.SelectedIndex];
        string appSerial = _tbAppSerial.Text.Trim();

        if (string.IsNullOrWhiteSpace(appSerial))
        {
            MessageBox.Show("กรุณากรอก App Serial Watermark", "คำเตือน", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // หากเป็นการ Gen Key ใหม่ และเลือกติ๊กฟอร์แมต ให้แสดงกล่องถามยืนยันก่อน
        if (isFormatAndNew && _chkFormatUsb.Checked)
        {
            string confirmMsg = $"คำเตือนการฟอร์แมตข้อมูล (Formatting Warning):\n\n" +
                                $"ข้อมูลทั้งหมดใน USB Flash Drive ไดรฟ์ [{drive.DriveLetter}\\] ({drive.VolumeLabel}) จะถูกลบอย่างถาวร!\n\n" +
                                $"ระบบกำลังจะ Quick Format เป็นระบบไฟล์ {_cbFileSystem.SelectedItem} และตั้งชื่อเป็น '{_tbVolumeLabel.Text.Trim()}'\n\n" +
                                $"คุณแน่ใจหรือไม่ว่าต้องการฟอร์แมตและสร้าง USB Dongle Key ใหม่?";

            var confirmResult = MessageBox.Show(confirmMsg, "ยืนยันการฟอร์แมต USB Drive", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (confirmResult != DialogResult.Yes)
            {
                _lblStatus.Text = "ยกเลิกการทำรายการโดยผู้ใช้";
                return;
            }
        }

        try
        {
            _btnGenKey.Enabled = false;
            _btnEditKey.Enabled = false;
            _btnRefreshDrives.Enabled = false;
            _progressBar.Value = 10;

            string actionText = isFormatAndNew ? "กำลัง Gen Key และสร้างกุญแจใหม่..." : "กำลังอัปเดต/แก้ไขข้อมูลคีย์เดิม...";
            _lblStatus.Text = actionText;

            // 1. ฟอร์แมต (กรณี Gen Key ใหม่และติ๊กเลือกไว้)
            if (isFormatAndNew && _chkFormatUsb.Checked)
            {
                _progressBar.Value = 30;
                _lblStatus.Text = $"กำลังฟอร์แมต USB Drive [{drive.DriveLetter}\\]...";

                // Sanitize volume label: อนุญาตเฉพาะ A-Z, 0-9, _, - (ป้องกัน command injection)
                string sanitizedLabel = System.Text.RegularExpressions.Regex.Replace(
                    _tbVolumeLabel.Text.Trim(), @"[^A-Za-z0-9_\-]", "");
                if (string.IsNullOrEmpty(sanitizedLabel)) sanitizedLabel = "REST_RENT_KEY";

                bool formatSuccess = await System.Threading.Tasks.Task.Run(() =>
                    UsbDongleManager.FormatUsbDrive(drive.DriveLetter, _cbFileSystem.SelectedItem?.ToString() ?? "FAT32", sanitizedLabel));

                if (!formatSuccess)
                {
                    MessageBox.Show($"ไม่สามารถฟอร์แมตไดรฟ์ {drive.DriveLetter}\\ ได้ กรุณาตรวจสอบว่าไม่มีโปรแกรมอื่นเปิดไดรฟ์นี้อยู่", "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    _lblStatus.Text = "ฟอร์แมตไม่สำเร็จ";
                    ResetUIState();
                    return;
                }
            }

            // 2. สร้าง License Object (ใช้ชื่อระบบทั่วไป ไม่ต้องพึ่งชื่อโรงแรม)
            var license = new LicenseFile
            {
                CustomerName = "PSoft-RestRentManager-CLIENT",
                UsbHardwareId = drive.UsbHardwareId,
                AppSerial = appSerial,
                LicenseType = _cbLicenseType.SelectedIndex switch
                {
                    0 => LicenseType.Lifetime,
                    1 => LicenseType.Standard,
                    _ => LicenseType.Trial
                },
                IssueDate = DateTime.Today,
                ExpireDate = _cbLicenseType.SelectedIndex == 1 ? _dtpExpireDate.Value.Date : null,
                Features = new List<string> { "BOOKING", "POS", "REPORT" }
            };

            // 3. ลงลายเซ็นดิจิทัล RSA-2048
            _progressBar.Value = 70;
            _lblStatus.Text = "กำลังลงลายเซ็นดิจิทัล RSA-2048...";

            string signableData = license.GetSignableData();
            byte[] dataBytes = Encoding.UTF8.GetBytes(signableData);

            using (var rsa = RSA.Create())
            {
                rsa.ImportPkcs8PrivateKey(Convert.FromBase64String(GetPrivateKeyBase64()), out _);
                byte[] signatureBytes = rsa.SignData(dataBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                license.Signature = Convert.ToBase64String(signatureBytes);
            }

            // 4. เขียน dongle.key และ app.watermark ลงใน USB Drive
            _progressBar.Value = 90;
            _lblStatus.Text = "กำลังบันทึกไฟล์ dongle.key ลง USB Drive...";

            string donglePath = Path.Combine(drive.DriveLetter + "\\", UsbDongleManager.DongleFileName);
            File.WriteAllText(donglePath, license.ToJson());

            // Gen app.watermark คู่กัน
            var watermark = new AppWatermarkFile
            {
                AppSerial = appSerial,
                IssuedTo = "PSoft-RestRentManager-CLIENT",
                IssuedDate = DateTime.Today
            };
            string watermarkSignable = watermark.GetSignableData();
            using (var rsaW = RSA.Create())
            {
                rsaW.ImportPkcs8PrivateKey(Convert.FromBase64String(GetPrivateKeyBase64()), out _);
                byte[] wSig = rsaW.SignData(Encoding.UTF8.GetBytes(watermarkSignable), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                watermark.Signature = Convert.ToBase64String(wSig);
            }

            string watermarkPath = Path.Combine(drive.DriveLetter + "\\", AppWatermarkManager.WatermarkFileName);
            File.WriteAllText(watermarkPath, watermark.ToJson());

            _progressBar.Value = 100;
            string statusMsg = isFormatAndNew ? "Gen Key และเขียนคีย์ลง USB Drive สำเร็จเรียบร้อย!" : "แก้ไข/อัปเดตคีย์ใน USB Drive สำเร็จเรียบร้อย!";
            _lblStatus.Text = $"{statusMsg}";

            MessageBox.Show($"{statusMsg}\n\n" +
                            $"• ไดรฟ์: {drive.DriveLetter}\\\n" +
                            $"• App Serial: {appSerial}\n" +
                            $"• ประเภทสิทธิ์: {license.LicenseType}\n" +
                            $"• Physical Serial: {drive.PhysicalSerial}\n\n" +
                            $"คีย์ถูกบันทึกเรียบร้อย สามารถนำ USB Dongle ไปเสียบใช้งานได้ทันที", "สำเร็จ", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"เกิดข้อผิดพลาด: {ex.Message}", "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _lblStatus.Text = "เกิดข้อผิดพลาดในการบันทึกคีย์";
        }
        finally
        {
            ResetUIState();
            RefreshUsbDrives();
        }
    }

    private void TestUnlock_Click(object? sender, EventArgs e)
    {
        try
        {
            var (dongleLicense, driveInfo, rawContent) = UsbDongleManager.ScanForDongleKey();
            if (dongleLicense == null || driveInfo == null)
            {
                MessageBox.Show("ไม่พบไฟล์ dongle.key ใน USB Flash Drive ที่เสียบอยู่", "ไม่พบคีย์", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var currentAppSerial = _tbAppSerial.Text.Trim();
            var status = LicenseValidator.ValidateDongle(dongleLicense, driveInfo.UsbHardwareId, currentAppSerial);

            if (status == LicenseStatus.Active)
            {
                MessageBox.Show($"ตรวจสอบ Dongle สำเร็จ 100%!\n\n" +
                                $"• ไดรฟ์: {driveInfo.DriveLetter}\\\n" +
                                $"• App Serial: {dongleLicense.AppSerial}\n" +
                                $"• Physical Serial: {driveInfo.PhysicalSerial}\n" +
                                $"• ประเภทสิทธิ์: {dongleLicense.LicenseType}\n" +
                                $"• สถานะ: ACTIVE (ปลดล็อกระบบสมบูรณ์)", "ตรวจสอบสำเร็จ", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show($"ตรวจสอบ Dongle ไม่ผ่าน! สถานะ: {status}\n(คีย์ถูกก๊อปปี้ข้าม Flash Drive หรือ App Serial ไม่ตรง)", "ตรวจสอบล้มเหลว", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"เกิดข้อผิดพลาดในการตรวจสอบ: {ex.Message}", "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ViewPayload_Click(object? sender, EventArgs e)
    {
        var (dongleLicense, driveInfo, rawContent) = UsbDongleManager.ScanForDongleKey();
        if (dongleLicense != null && rawContent != null)
        {
            using var dlg = new Form
            {
                Text = "ข้อมูล dongle.key Payload (JSON)",
                Width = 500,
                Height = 400,
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.SizableToolWindow
            };
            var tb = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill,
                Text = rawContent,
                Font = new Font("Consolas", 9.5F),
                ReadOnly = true
            };
            dlg.Controls.Add(tb);
            dlg.ShowDialog(this);
        }
        else
        {
            MessageBox.Show("ไม่พบไฟล์ dongle.key ใน USB Flash Drive ที่เสียบอยู่", "ไม่พบข้อมูล", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ResetUIState()
    {
        _btnGenKey.Enabled = true;
        _btnEditKey.Enabled = true;
        _btnRefreshDrives.Enabled = true;
        _progressBar.Value = 0;
    }
}
