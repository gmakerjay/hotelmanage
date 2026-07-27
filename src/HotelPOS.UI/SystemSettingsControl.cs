using System.Drawing.Printing;
using HotelPOS.Common.Models;
using HotelPOS.Core.Services;

namespace HotelPOS.UI;

public class SystemSettingsControl : UserControl
{
    private readonly ISettingsService _settingsService;

    // Section 1: Shop & Invoice
    private TextBox _txtShopName = null!;
    private TextBox _txtShopAddress = null!;
    private TextBox _txtShopPhone = null!;
    private TextBox _txtShopTaxId = null!;
    private TextBox _txtBillHeader = null!;
    private TextBox _txtBillFooter = null!;
    private TextBox _txtLobbyTerms = null!;

    // Logo & QR Images
    private PictureBox _picLogo = null!;
    private PictureBox _picQrCode = null!;
    private string? _logoPath;
    private string? _qrCodePath;

    // Section 2: Printer & Paper
    private ComboBox _cboPrinterList = null!;
    private ComboBox _cboPaperType = null!;
    private CheckBox _chkAutoPrintOnCheckout = null!;
    private CheckBox _chkShowSignatureBox = null!;
    private NumericUpDown _numPrinterFeedLines = null!;
    private CheckBox _chkPrinterAutoCut = null!;

    // Section 3: Operations & Deposit
    private TextBox _txtCheckInTime = null!;
    private TextBox _txtCheckOutTime = null!;
    private NumericUpDown _numDeposit = null!;
    private NumericUpDown _numVatRate = null!;
    private CheckBox _chkEnableVat = null!;

    // Section 4: Document Prefix & Sequences
    private TextBox _txtDocPrefix = null!;
    private NumericUpDown _numDocRunning = null!;
    private Button _btnResetSequences = null!;

    // Section 5: Security & Set Zero
    private TextBox _txtAdminPassword = null!;
    private TextBox _txtConfirmPassword = null!;
    private Button _btnZetZero = null!;

    private Button _btnSave = null!;
    private Button _btnReload = null!;

    public SystemSettingsControl(ISettingsService settingsService)
    {
        _settingsService = settingsService;

        InitializeUI();
        Load += async (s, e) => await LoadSettingsAsync();
    }

    private void InitializeUI()
    {
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(241, 245, 249);
        Font = new Font("Segoe UI", 10.5F, FontStyle.Regular);

        var mainScrollPanel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(20)
        };

        var titleLabel = new Label
        {
            Text = "ตั้งค่าระบบ (Backend System Settings)",
            Font = new Font("Segoe UI", 16F, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59),
            Location = new Point(20, 15),
            AutoSize = true
        };

        var subtitleLabel = new Label
        {
            Text = "จัดการข้อมูลสถานประกอบการ โลโก้ QR Code การออกใบเสร็จ ตั้งค่าเครื่องพิมพ์ และค่าเริ่มต้นการดำเนินงานโรงแรม",
            Font = new Font("Segoe UI", 10F, FontStyle.Regular),
            ForeColor = Color.DimGray,
            Location = new Point(22, 50),
            AutoSize = true
        };

        int currentY = 85;

        // Group 1: Shop Branding & Logo/QR
        var grpShop = CreateGroupPanel("1. ข้อมูลสถานประกอบการ โลโก้ และ QR Code ชำระเงิน (Shop & Branding)", currentY, 515);
        BuildShopFields(grpShop);
        currentY += 530;

        // Group 2: Printer & Paper
        var grpPrinter = CreateGroupPanel("2. ตั้งค่าเครื่องพิมพ์และขนาดกระดาษ (Printer & Paper Settings)", currentY, 245);
        BuildPrinterFields(grpPrinter);
        currentY += 260;

        // Group 3: Operations & Deposit
        var grpOps = CreateGroupPanel("3. ตั้งค่าการดำเนินงานและเงินประกัน (Operations & Deposit)", currentY, 230);
        BuildOpsFields(grpOps);
        currentY += 245;

        // Group 4: Document Prefix & Reset
        var grpDocSeq = CreateGroupPanel("4. ตั้งค่าเลขที่เอกสารและการรีเซ็ตคีย์หลัก (Document Prefix & Reset)", currentY, 160);
        BuildDocSeqFields(grpDocSeq);
        currentY += 175;

        // Group 5: Security & Set Zero
        var grpSecurity = CreateGroupPanel("5. ความปลอดภัยและล้างข้อมูลเริ่มใช้งานจริง (Security & Set Zero)", currentY, 170);
        BuildSecurityFields(grpSecurity);
        currentY += 185;

        // Bottom Action Bar
        var pnlActions = new Panel
        {
            Location = new Point(20, currentY),
            Size = new Size(880, 60),
            BackColor = Color.Transparent
        };

        _btnSave = new Button
        {
            Text = "บันทึกการตั้งค่าระบบ",
            BackColor = Color.FromArgb(37, 99, 235),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            Location = new Point(0, 8),
            Size = new Size(180, 44),
            Cursor = Cursors.Hand
        };
        _btnSave.FlatAppearance.BorderSize = 0;
        _btnSave.Click += async (s, e) => await SaveSettingsAsync();

        _btnReload = new Button
        {
            Text = "รีโหลดค่าเดิม",
            BackColor = Color.White,
            ForeColor = Color.FromArgb(30, 41, 59),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
            Location = new Point(195, 8),
            Size = new Size(130, 44),
            Cursor = Cursors.Hand
        };
        _btnReload.Click += async (s, e) => await LoadSettingsAsync();

        pnlActions.Controls.Add(_btnSave);
        pnlActions.Controls.Add(_btnReload);

        // Extra Bottom Padding Spacer to ensure no frame clipping when scrolling to bottom
        var pnlBottomSpacer = new Panel
        {
            Location = new Point(20, currentY + 65),
            Size = new Size(880, 40),
            BackColor = Color.Transparent
        };

        mainScrollPanel.Controls.AddRange(new Control[]
        {
            titleLabel, subtitleLabel, grpShop, grpPrinter, grpOps, grpDocSeq, grpSecurity, pnlActions, pnlBottomSpacer
        });

        Controls.Add(mainScrollPanel);
    }

    private static Panel CreateGroupPanel(string title, int y, int height)
    {
        var panel = new Panel
        {
            Location = new Point(20, y),
            Size = new Size(880, height),
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(15)
        };

        var lblHeader = new Label
        {
            Text = title,
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59),
            Location = new Point(15, 12),
            AutoSize = true
        };

        var line = new Panel
        {
            Location = new Point(15, 42),
            Size = new Size(848, 1),
            BackColor = Color.FromArgb(226, 232, 240)
        };

        panel.Controls.Add(lblHeader);
        panel.Controls.Add(line);
        return panel;
    }

    private void BuildShopFields(Panel pnl)
    {
        var lblName = new Label { Text = "ชื่อโรงแรม / ที่พัก:", Location = new Point(20, 58), AutoSize = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
        _txtShopName = new TextBox { Location = new Point(180, 54), Width = 300, Font = new Font("Segoe UI", 10.5F) };

        var lblTaxId = new Label { Text = "เลขประจำตัวผู้เสียภาษี:", Location = new Point(500, 58), AutoSize = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
        _txtShopTaxId = new TextBox { Location = new Point(670, 54), Width = 190, Font = new Font("Segoe UI", 10.5F) };

        var lblPhone = new Label { Text = "เบอร์โทรศัพท์ติดต่อ:", Location = new Point(20, 98), AutoSize = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
        _txtShopPhone = new TextBox { Location = new Point(180, 94), Width = 300, Font = new Font("Segoe UI", 10.5F) };

        var lblAddr = new Label { Text = "ที่อยู่สถานประกอบการ:", Location = new Point(20, 138), AutoSize = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
        _txtShopAddress = new TextBox { Location = new Point(180, 134), Width = 680, Height = 45, Multiline = true, Font = new Font("Segoe UI", 10F) };

        var lblHeaderMsg = new Label { Text = "ข้อความต้อนรับหัวบิล:", Location = new Point(20, 192), AutoSize = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
        _txtBillHeader = new TextBox { Location = new Point(180, 188), Width = 680, Font = new Font("Segoe UI", 10F) };

        var lblFooterMsg = new Label { Text = "ข้อความขอบคุณท้ายบิล:", Location = new Point(20, 230), AutoSize = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
        _txtBillFooter = new TextBox { Location = new Point(180, 226), Width = 680, Font = new Font("Segoe UI", 10F) };

        var lblLobbyTerms = new Label { Text = "ข้อตกลงหน้าล็อบบี้/ใบเสร็จ:", Location = new Point(20, 268), AutoSize = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
        _txtLobbyTerms = new TextBox { Location = new Point(210, 264), Width = 650, Height = 65, Multiline = true, ScrollBars = ScrollBars.Vertical, Font = new Font("Segoe UI", 9.5F) };

        // Logo & QR Code Image Upload Box
        var lblLogoHeader = new Label { Text = "รูปโลโก้โรงแรม (Logo):", Location = new Point(20, 345), AutoSize = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
        _picLogo = new PictureBox
        {
            Location = new Point(180, 341),
            Size = new Size(130, 85),
            BorderStyle = BorderStyle.FixedSingle,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.FromArgb(248, 250, 252)
        };

        var btnUploadLogo = new Button
        {
            Text = "เลือกรูปโลโก้",
            Location = new Point(180, 432),
            Size = new Size(100, 32),
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnUploadLogo.Click += (s, e) => UploadImage(true);

        var btnClearLogo = new Button
        {
            Text = "ลบรูป",
            Location = new Point(285, 432),
            Size = new Size(60, 32),
            Font = new Font("Segoe UI", 9F),
            Cursor = Cursors.Hand
        };
        btnClearLogo.Click += (s, e) => ClearImage(true);

        var lblQrHeader = new Label { Text = "รูป PromptPay QR Code:", Location = new Point(460, 345), AutoSize = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
        _picQrCode = new PictureBox
        {
            Location = new Point(650, 341),
            Size = new Size(130, 85),
            BorderStyle = BorderStyle.FixedSingle,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.FromArgb(248, 250, 252)
        };

        var btnUploadQr = new Button
        {
            Text = "เลือกรูป QR Code",
            Location = new Point(650, 432),
            Size = new Size(120, 32),
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnUploadQr.Click += (s, e) => UploadImage(false);

        var btnClearQr = new Button
        {
            Text = "ลบรูป",
            Location = new Point(775, 432),
            Size = new Size(60, 32),
            Font = new Font("Segoe UI", 9F),
            Cursor = Cursors.Hand
        };
        btnClearQr.Click += (s, e) => ClearImage(false);

        var lblInfoNote = new Label
        {
            Text = "* รูปโลโก้และ QR Code จะถูกคำนวณย่อ/ขยาย (Auto-Resize) ให้เข้ากับขนาดกระดาษ A4 / 80mm โดยไม่เสียอัตราส่วน",
            Location = new Point(20, 475),
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Italic),
            ForeColor = Color.DarkSlateGray
        };

        pnl.Controls.AddRange(new Control[]
        {
            lblName, _txtShopName, lblTaxId, _txtShopTaxId,
            lblPhone, _txtShopPhone, lblAddr, _txtShopAddress,
            lblHeaderMsg, _txtBillHeader, lblFooterMsg, _txtBillFooter,
            lblLobbyTerms, _txtLobbyTerms,
            lblLogoHeader, _picLogo, btnUploadLogo, btnClearLogo,
            lblQrHeader, _picQrCode, btnUploadQr, btnClearQr, lblInfoNote
        });
    }

    private void UploadImage(bool isLogo)
    {
        using var dlg = new OpenFileDialog
        {
            Title = isLogo ? "เลือกรูปภาพโลโก้โรงแรม" : "เลือกรูปภาพ PromptPay QR Code",
            Filter = "Image Files (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp"
        };

        if (dlg.ShowDialog() == DialogResult.OK)
        {
            try
            {
                var assetsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets");
                if (!Directory.Exists(assetsDir)) Directory.CreateDirectory(assetsDir);

                var fileName = isLogo ? "logo.png" : "qrcode.png";
                var destPath = Path.Combine(assetsDir, fileName);

                File.Copy(dlg.FileName, destPath, true);

                if (isLogo)
                {
                    _logoPath = destPath;
                    using var stream = new MemoryStream(File.ReadAllBytes(destPath));
                    _picLogo.Image = Image.FromStream(stream);
                }
                else
                {
                    _qrCodePath = destPath;
                    using var stream = new MemoryStream(File.ReadAllBytes(destPath));
                    _picQrCode.Image = Image.FromStream(stream);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"เกิดข้อผิดพลาดในการโหลดรูปภาพ: {ex.Message}", "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void ClearImage(bool isLogo)
    {
        if (isLogo)
        {
            _logoPath = null;
            _picLogo.Image?.Dispose();
            _picLogo.Image = null;
        }
        else
        {
            _qrCodePath = null;
            _picQrCode.Image?.Dispose();
            _picQrCode.Image = null;
        }
    }

    private void BuildPrinterFields(Panel pnl)
    {
        var lblPrinter = new Label { Text = "เครื่องพิมพ์หลัก (Printer):", Location = new Point(20, 58), AutoSize = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
        _cboPrinterList = new ComboBox { Location = new Point(200, 54), Width = 350, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 10F) };

        // Populate installed printers
        _cboPrinterList.Items.Add("(ใช้เครื่องพิมพ์ตั้งต้นของ Windows)");
        foreach (string printer in PrinterSettings.InstalledPrinters)
        {
            _cboPrinterList.Items.Add(printer);
        }
        _cboPrinterList.SelectedIndex = 0;

        var lblPaper = new Label { Text = "ขนาดกระดาษเอกสาร:", Location = new Point(570, 58), AutoSize = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
        _cboPaperType = new ComboBox { Location = new Point(715, 54), Width = 145, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 10F) };
        _cboPaperType.Items.AddRange(new object[] { "A4", "80mm", "58mm" });
        _cboPaperType.SelectedIndex = 0;

        _chkAutoPrintOnCheckout = new CheckBox
        {
            Text = "พิมพ์ใบเสร็จอัตโนมัติเมื่อทำการเช็คเอาท์สำเร็จ",
            Location = new Point(20, 105),
            AutoSize = true,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold)
        };

        _chkShowSignatureBox = new CheckBox
        {
            Text = "แสดงช่องลงลายมือชื่อผู้เข้าพักและเจ้าหน้าที่ในใบเสร็จ",
            Location = new Point(20, 142),
            AutoSize = true,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold)
        };

        var lblFeedLines = new Label { Text = "ระยะป้อนกระดาษท้ายสลิป (บรรทัด):", Location = new Point(20, 180), AutoSize = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
        _numPrinterFeedLines = new NumericUpDown
        {
            Location = new Point(245, 177),
            Width = 70,
            Minimum = 0,
            Maximum = 20,
            Value = 4,
            Font = new Font("Segoe UI", 10F)
        };

        _chkPrinterAutoCut = new CheckBox
        {
            Text = "สั่งตัดกระดาษอัตโนมัติหลังพิมพ์ (Auto Cut)",
            Location = new Point(340, 178),
            AutoSize = true,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold)
        };

        pnl.Controls.AddRange(new Control[]
        {
            lblPrinter, _cboPrinterList, lblPaper, _cboPaperType,
            _chkAutoPrintOnCheckout, _chkShowSignatureBox,
            lblFeedLines, _numPrinterFeedLines, _chkPrinterAutoCut
        });
    }

    private void BuildOpsFields(Panel pnl)
    {
        var lblCheckIn = new Label { Text = "เวลาเช็คอินมาตรฐาน:", Location = new Point(20, 58), AutoSize = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
        _txtCheckInTime = new TextBox { Text = "14:00", Location = new Point(180, 54), Width = 110, Font = new Font("Segoe UI", 10F) };

        var lblCheckOut = new Label { Text = "เวลาเช็คเอาท์มาตรฐาน:", Location = new Point(340, 58), AutoSize = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
        _txtCheckOutTime = new TextBox { Text = "12:00", Location = new Point(500, 54), Width = 110, Font = new Font("Segoe UI", 10F) };

        var lblDeposit = new Label { Text = "เงินประกันห้องพักเริ่มต้น:", Location = new Point(20, 105), AutoSize = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
        _numDeposit = new NumericUpDown { Location = new Point(180, 101), Width = 130, Maximum = 100000, DecimalPlaces = 2, Font = new Font("Segoe UI", 10F) };

        var lblVat = new Label { Text = "อัตราภาษี VAT (%):", Location = new Point(340, 105), AutoSize = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
        _numVatRate = new NumericUpDown { Location = new Point(500, 101), Width = 110, Maximum = 30, DecimalPlaces = 2, Value = 7, Font = new Font("Segoe UI", 10F) };

        _chkEnableVat = new CheckBox
        {
            Text = "คำนวณและแสดงภาษีมูลค่าเพิ่ม (VAT) ในใบเสร็จ",
            Location = new Point(20, 150),
            AutoSize = true,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold)
        };

        pnl.Controls.AddRange(new Control[]
        {
            lblCheckIn, _txtCheckInTime, lblCheckOut, _txtCheckOutTime,
            lblDeposit, _numDeposit, lblVat, _numVatRate, _chkEnableVat
        });
    }

    private void BuildDocSeqFields(Panel pnl)
    {
        var lblPrefix = new Label { Text = "คำนำหน้าเลขที่บิล (Prefix):", Location = new Point(20, 58), AutoSize = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
        _txtDocPrefix = new TextBox { Location = new Point(200, 54), Width = 110, Font = new Font("Segoe UI", 10F) };

        var lblRunning = new Label { Text = "เลขรันบิลล่าสุด (Running No.):", Location = new Point(340, 58), AutoSize = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
        _numDocRunning = new NumericUpDown { Location = new Point(540, 54), Width = 110, Maximum = 999999, Minimum = 0, Font = new Font("Segoe UI", 10F) };

        _btnResetSequences = new Button
        {
            Text = "รีเซ็ตลำดับคีย์และเลขรันทั้งหมด (Reset Sequences)",
            BackColor = Color.FromArgb(239, 68, 68),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            Location = new Point(20, 105),
            Size = new Size(320, 36),
            Cursor = Cursors.Hand
        };
        _btnResetSequences.Click += BtnResetSequences_Click;

        var lblInfoSeq = new Label
        {
            Text = "* ปุ่มรีเซ็ตจะตั้งค่า Auto-increment ในระบบทั้งหมดให้ต่อจาก ID ล่าสุดที่มีอยู่ เพื่อลบล้างช่องว่างที่ลบไป และตั้งค่าเลขบิลเริ่มใหม่",
            Location = new Point(350, 112),
            AutoSize = true,
            Font = new Font("Segoe UI", 9F, FontStyle.Italic),
            ForeColor = Color.DimGray
        };

        pnl.Controls.AddRange(new Control[]
        {
            lblPrefix, _txtDocPrefix, lblRunning, _numDocRunning, _btnResetSequences, lblInfoSeq
        });
    }

    private void BuildSecurityFields(Panel pnl)
    {
        var lblPassword = new Label { Text = "รหัสผ่าน Admin ใหม่:", Location = new Point(20, 48), AutoSize = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
        _txtAdminPassword = new TextBox { Location = new Point(180, 44), Width = 200, UseSystemPasswordChar = true, Font = new Font("Segoe UI", 10F) };

        var lblConfirm = new Label { Text = "ยืนยันรหัสผ่าน Admin:", Location = new Point(410, 48), AutoSize = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
        _txtConfirmPassword = new TextBox { Location = new Point(580, 44), Width = 200, UseSystemPasswordChar = true, Font = new Font("Segoe UI", 10F) };

        var lblPasswordInfo = new Label
        {
            Text = "* เว้นว่างไว้หากไม่ต้องการเปลี่ยนรหัสผ่าน / ใช้สำหรับเข้าใช้งานบัญชี admin",
            Location = new Point(20, 80),
            AutoSize = true,
            Font = new Font("Segoe UI", 9F, FontStyle.Italic),
            ForeColor = Color.DimGray
        };

        _btnZetZero = new Button
        {
            Text = "ล้างข้อมูลระบบทั้งหมดเป็น 0 (Set Zero)",
            BackColor = Color.FromArgb(220, 38, 38),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            Location = new Point(20, 115),
            Size = new Size(320, 36),
            Cursor = Cursors.Hand
        };
        _btnZetZero.FlatAppearance.BorderSize = 0;
        _btnZetZero.Click += BtnZetZero_Click;

        var lblZetZeroInfo = new Label
        {
            Text = "* ปุ่ม Set Zero จะล้างลูกค้า การจอง การเงิน ค่าน้ำค่าไฟ และบันทึกประวัติเพื่อเริ่มใช้งานจริง (ห้ามลบประเภทและห้องพัก)",
            Location = new Point(350, 122),
            AutoSize = true,
            Font = new Font("Segoe UI", 9F, FontStyle.Italic),
            ForeColor = Color.DimGray
        };

        pnl.Controls.AddRange(new Control[]
        {
            lblPassword, _txtAdminPassword, lblConfirm, _txtConfirmPassword, lblPasswordInfo,
            _btnZetZero, lblZetZeroInfo
        });
    }

    private async void BtnZetZero_Click(object? sender, EventArgs e)
    {
        var confirmResult = MessageBox.Show(
            "คุณต้องการล้างธุรกรรมในระบบทั้งหมดเพื่อเริ่มใช้งานจริง (Set Zero) ใช่หรือไม่?\n\nการดำเนินการนี้จะลบการจอง ประวัติลูกค้า ประวัติมิเตอร์ บิลน้ำไฟ คลังสินค้า และบันทึกธุรกรรมทั้งหมด แต่จะยังคงเหลือรายการประเภทห้อง รายชื่อห้องพัก และการตั้งค่าของท่านไว้",
            "ยืนยันการล้างข้อมูลระบบ (Set Zero)",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);

        if (confirmResult != DialogResult.Yes) return;

        using (var confirmDlg = new Form())
        {
            confirmDlg.Text = "ยืนยันรหัสผ่านเพื่อล้างระบบ";
            confirmDlg.Size = new Size(380, 200);
            confirmDlg.StartPosition = FormStartPosition.CenterParent;
            confirmDlg.FormBorderStyle = FormBorderStyle.FixedDialog;
            confirmDlg.MaximizeBox = false;
            confirmDlg.MinimizeBox = false;
            confirmDlg.Font = new Font("Segoe UI", 10F);

            var lblPrompt = new Label { Text = "กรุณากรอกรหัสผ่าน Admin เพื่อดำเนินการต่อ:", Location = new Point(20, 20), Size = new Size(320, 25) };
            var txtPwd = new TextBox { Location = new Point(20, 50), Width = 320, UseSystemPasswordChar = true };
            var btnOk = new Button { Text = "ยืนยัน", Location = new Point(140, 100), Size = new Size(95, 36), DialogResult = DialogResult.OK, BackColor = Color.FromArgb(220, 38, 38), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnOk.FlatAppearance.BorderSize = 0;
            var btnCancel = new Button { Text = "ยกเลิก", Location = new Point(245, 100), Size = new Size(95, 36), DialogResult = DialogResult.Cancel, BackColor = Color.FromArgb(226, 232, 240), FlatStyle = FlatStyle.Flat };
            btnCancel.FlatAppearance.BorderSize = 0;

            confirmDlg.Controls.AddRange(new Control[] { lblPrompt, txtPwd, btnOk, btnCancel });
            confirmDlg.AcceptButton = btnOk;
            confirmDlg.CancelButton = btnCancel;

            if (confirmDlg.ShowDialog() == DialogResult.OK)
            {
                var inputPwd = txtPwd.Text.Trim();
                var currentPwd = await _settingsService.GetAsync("admin_password") ?? "psoft123";
                if (string.IsNullOrWhiteSpace(currentPwd)) currentPwd = "psoft123";

                if (inputPwd != currentPwd)
                {
                    MessageBox.Show("รหัสผ่านไม่ถูกต้อง การทำรายการล้มเหลว", "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                try
                {
                    await _settingsService.ZetZeroDatabaseAsync();
                    MessageBox.Show("ทำความสะอาดและเคลียร์ระบบเป็น 0 (Set Zero) เรียบร้อยแล้ว\n\nระบบจะทำการรีสตาร์ทแอปพลิเคชันเพื่อโหลดข้อมูลใหม่ทั้งหมด", "สำเร็จ", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    Application.Restart();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"เกิดข้อผิดพลาดในการล้างระบบ: {ex.Message}", "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }

    private async Task ResetDatabaseSequencesAsync()
    {
        if (MessageBox.Show("ยืนยันการรีเซ็ตคีย์หลักในฐานข้อมูลและเลขรันบิลทั้งหมด?\nการดำเนินการนี้จะปรับค่า Auto-increment ID ของทุกตารางให้รันต่อจากข้อมูลล่าสุดที่มีอยู่ และตั้งค่าเลขรันบิลกลับไปเป็น 0", "ยืนยันการรีเซ็ต", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
        {
            try
            {
                await _settingsService.ResetDatabaseSequencesAsync();
                MessageBox.Show("รีเซ็ตลำดับคีย์หลักและเลขรันบิลเรียบร้อยแล้ว", "สำเร็จ", MessageBoxButtons.OK, MessageBoxIcon.Information);
                await LoadSettingsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"รีเซ็ตไม่สำเร็จ: {ex.Message}", "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private async void BtnResetSequences_Click(object? sender, EventArgs e)
    {
        await ResetDatabaseSequencesAsync();
    }

    private async Task LoadSettingsAsync()
    {
        try
        {
            var dto = await _settingsService.GetAllSettingsAsync();

            _txtShopName.Text = dto.ShopName;
            _txtShopAddress.Text = dto.ShopAddress;
            _txtShopPhone.Text = dto.ShopPhone;
            _txtShopTaxId.Text = dto.ShopTaxId;
            _txtBillHeader.Text = dto.BillHeader;
            _txtBillFooter.Text = dto.BillFooter;
            _txtLobbyTerms.Text = dto.LobbyTerms;

            _logoPath = dto.LogoImagePath;
            if (!string.IsNullOrEmpty(_logoPath) && File.Exists(_logoPath))
            {
                using var stream = new MemoryStream(File.ReadAllBytes(_logoPath));
                _picLogo.Image = Image.FromStream(stream);
            }

            _qrCodePath = dto.QrCodeImagePath;
            if (!string.IsNullOrEmpty(_qrCodePath) && File.Exists(_qrCodePath))
            {
                using var stream = new MemoryStream(File.ReadAllBytes(_qrCodePath));
                _picQrCode.Image = Image.FromStream(stream);
            }

            if (string.IsNullOrWhiteSpace(dto.PrinterName) || !_cboPrinterList.Items.Contains(dto.PrinterName))
            {
                _cboPrinterList.SelectedIndex = 0;
            }
            else
            {
                _cboPrinterList.SelectedItem = dto.PrinterName;
            }

            if (_cboPaperType.Items.Contains(dto.PaperType))
            {
                _cboPaperType.SelectedItem = dto.PaperType;
            }

            _chkAutoPrintOnCheckout.Checked = dto.AutoPrintOnCheckout;
            _chkShowSignatureBox.Checked = dto.ShowSignatureBox;
            _numPrinterFeedLines.Value = Math.Min(_numPrinterFeedLines.Maximum, Math.Max(0, dto.PrinterFeedLines));
            _chkPrinterAutoCut.Checked = dto.PrinterAutoCut;

            _txtCheckInTime.Text = dto.DefaultCheckInTime;
            _txtCheckOutTime.Text = dto.DefaultCheckOutTime;
            _numDeposit.Value = Math.Min(_numDeposit.Maximum, Math.Max(0, dto.DefaultSecurityDeposit));
            _numVatRate.Value = Math.Min(_numVatRate.Maximum, Math.Max(0, dto.VatRate));
            _chkEnableVat.Checked = dto.EnableVat;

            _txtDocPrefix.Text = dto.ReceiptDocPrefix;
            _numDocRunning.Value = Math.Min(_numDocRunning.Maximum, Math.Max(0, dto.ReceiptDocRunningNumber));

            _txtAdminPassword.Text = "";
            _txtConfirmPassword.Text = "";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"โหลดข้อมูลการตั้งค่าไม่สำเร็จ: {ex.Message}", "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task SaveSettingsAsync()
    {
        try
        {
            _btnSave.Enabled = false;
            var dto = new SystemSettingsDto
            {
                ShopName = _txtShopName.Text.Trim(),
                ShopAddress = _txtShopAddress.Text.Trim(),
                ShopPhone = _txtShopPhone.Text.Trim(),
                ShopTaxId = _txtShopTaxId.Text.Trim(),
                BillHeader = _txtBillHeader.Text.Trim(),
                BillFooter = _txtBillFooter.Text.Trim(),
                LobbyTerms = _txtLobbyTerms.Text.Trim(),

                LogoImagePath = _logoPath,
                QrCodeImagePath = _qrCodePath,

                PrinterName = _cboPrinterList.SelectedIndex > 0 ? _cboPrinterList.SelectedItem?.ToString() ?? "" : "",
                PaperType = _cboPaperType.SelectedItem?.ToString() ?? "A4",
                AutoPrintOnCheckout = _chkAutoPrintOnCheckout.Checked,
                ShowSignatureBox = _chkShowSignatureBox.Checked,
                PrinterFeedLines = (int)_numPrinterFeedLines.Value,
                PrinterAutoCut = _chkPrinterAutoCut.Checked,

                DefaultCheckInTime = _txtCheckInTime.Text.Trim(),
                DefaultCheckOutTime = _txtCheckOutTime.Text.Trim(),
                DefaultSecurityDeposit = _numDeposit.Value,
                VatRate = _numVatRate.Value,
                EnableVat = _chkEnableVat.Checked,

                ReceiptDocPrefix = _txtDocPrefix.Text.Trim(),
                ReceiptDocRunningNumber = (int)_numDocRunning.Value
            };

            string pwd = _txtAdminPassword.Text.Trim();
            string confirm = _txtConfirmPassword.Text.Trim();
            if (!string.IsNullOrEmpty(pwd))
            {
                if (pwd != confirm)
                {
                    MessageBox.Show("รหัสผ่านใหม่และการยืนยันรหัสผ่านไม่ตรงกัน", "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                await _settingsService.SetAsync("admin_password", pwd);
                await _settingsService.SetAsync("is_custom_admin_password_set", "1");
            }

            await _settingsService.SaveAllSettingsAsync(dto);
            MessageBox.Show("บันทึกการตั้งค่าระบบและรูปภาพเรียบร้อยแล้ว", "สำเร็จ", MessageBoxButtons.OK, MessageBoxIcon.Information);
            _txtAdminPassword.Text = "";
            _txtConfirmPassword.Text = "";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"เกิดข้อผิดพลาดในการบันทึกการตั้งค่า: {ex.Message}", "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _btnSave.Enabled = true;
        }
    }
}
