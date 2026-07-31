using System.Drawing.Printing;
using System.Security.Cryptography;
using System.Text;
using HotelPOS.Common.Models;
using HotelPOS.Core.Services;

namespace HotelPOS.UI;

public class SystemSettingsControl : UserControl
{
    private readonly ISettingsService _settingsService;
    private readonly IAuditService? _auditService;

    private SystemSettingsDto _existingSettings = new();

    // Group 1: Shop & Invoice
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

    // Group 2: Document Prefix & Sequences
    private TextBox _txtDocPrefix = null!;
    private NumericUpDown _numDocRunning = null!;
    private Button _btnResetSequences = null!;

    // Group 4: Printer & Paper
    private ComboBox _cboPrinterList = null!;
    private ComboBox _cboPaperType = null!;
    private CheckBox _chkAutoPrintOnCheckout = null!;
    private CheckBox _chkShowSignatureBox = null!;
    private NumericUpDown _numPrinterFeedLines = null!;
    private CheckBox _chkPrinterAutoCut = null!;

    // Group 5: Operations & Deposit & Auto-Backup
    private TextBox _txtCheckInTime = null!;
    private TextBox _txtCheckOutTime = null!;
    private NumericUpDown _numDeposit = null!;
    private NumericUpDown _numVatRate = null!;
    private CheckBox _chkEnableVat = null!;
    private CheckBox _chkAutoBackup = null!;
    private CheckBox _chkAutoBackupOnExit = null!;
    private NumericUpDown _numBackupMaxKeepFiles = null!;
    private TextBox _txtBackupFolder = null!;
    private Button _btnBrowseBackupFolder = null!;

    // Group 6: Security & Set Zero
    private TextBox _txtAdminPassword = null!;
    private TextBox _txtConfirmPassword = null!;
    private Button _btnZetZero = null!;
    private Button _btnOpenAuditLog = null!;

    private Button _btnSave = null!;
    private Button _btnReload = null!;
    private ToolTip _toolTip = null!;

    public SystemSettingsControl(ISettingsService settingsService, IAuditService? auditService = null)
    {
        _settingsService = settingsService;
        _auditService = auditService;

        InitializeUI();
        Load += async (s, e) => await LoadSettingsAsync();
    }

    private void InitializeUI()
    {
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(241, 245, 249);
        Font = new Font("Segoe UI", 10.5F, FontStyle.Regular);

        _toolTip = new ToolTip
        {
            AutoPopDelay = 10000,
            InitialDelay = 400,
            ReshowDelay = 200,
            ShowAlways = true
        };

        var mainScrollPanel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(24)
        };

        var headerContainer = new Panel
        {
            Dock = DockStyle.Top,
            Height = 65,
            Padding = new Padding(0, 0, 0, 15)
        };

        var titleLabel = new Label
        {
            Text = "ตั้งค่าระบบ (System Settings)",
            Font = new Font("Segoe UI", 16F, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59),
            Location = new Point(0, 0),
            AutoSize = true
        };

        var subtitleLabel = new Label
        {
            Text = "จัดการข้อมูลสถานประกอบการ โลโก้ ตั้งค่าเครื่องพิมพ์ ธีมแอปพลิเคชัน และการสำรองข้อมูลแบบครบวงจร",
            Font = new Font("Segoe UI", 10.5F, FontStyle.Regular),
            ForeColor = Color.FromArgb(100, 116, 139),
            Location = new Point(2, 32),
            AutoSize = true
        };

        headerContainer.Controls.Add(titleLabel);
        headerContainer.Controls.Add(subtitleLabel);

        // 2-Column Responsive Grid TableLayout utilizing 100% full right side space
        var gridLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 10, 0, 20)
        };
        gridLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        gridLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

        var leftCol = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = new Padding(0, 0, 12, 0)
        };

        var rightCol = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = new Padding(12, 0, 0, 0)
        };

        // Resize group panels dynamically when column width changes
        leftCol.SizeChanged += (s, e) =>
        {
            int w = Math.Max(400, leftCol.Width - 10);
            foreach (Control c in leftCol.Controls) c.Width = w;
        };
        rightCol.SizeChanged += (s, e) =>
        {
            int w = Math.Max(400, rightCol.Width - 10);
            foreach (Control c in rightCol.Controls) c.Width = w;
        };

        // --- Left Column Groups ---
        var grpShop = CreateGroupPanel("1. ข้อมูลสถานประกอบการ โลโก้ และ QR Code รับชำระเงิน", 560);
        BuildShopFields(grpShop);
        leftCol.Controls.Add(grpShop);

        var grpDocSeq = CreateGroupPanel("2. คำนำหน้าและเลขที่เอกสาร", 185);
        BuildDocSeqFields(grpDocSeq);
        leftCol.Controls.Add(grpDocSeq);

        // --- Right Column Groups ---
        var grpPrinter = CreateGroupPanel("3. ตั้งค่าเครื่องพิมพ์และขนาดกระดาษเอกสาร", 260);
        BuildPrinterFields(grpPrinter);
        rightCol.Controls.Add(grpPrinter);

        var grpOps = CreateGroupPanel("4. ตั้งค่าการดำเนินงาน และการสำรองข้อมูลอัตโนมัติ", 365);
        BuildOpsFields(grpOps);
        rightCol.Controls.Add(grpOps);

        var grpSecurity = CreateGroupPanel("5. รหัสผ่านผู้ดูแล ประวัติระบบ และการล้างข้อมูลเริ่มระบบ", 370);
        BuildSecurityFields(grpSecurity);
        rightCol.Controls.Add(grpSecurity);

        gridLayout.Controls.Add(leftCol, 0, 0);
        gridLayout.Controls.Add(rightCol, 1, 0);

        // Bottom Action Bar
        var pnlActions = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0, 20, 0, 30)
        };

        _btnSave = new Button
        {
            Text = "บันทึกการตั้งค่าระบบ",
            BackColor = Color.FromArgb(37, 99, 235),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            Size = new Size(220, 46),
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 15, 0)
        };
        _btnSave.FlatAppearance.BorderSize = 0;
        _btnSave.Click += async (s, e) => await SaveSettingsAsync();
        _toolTip.SetToolTip(_btnSave, "กดเพื่อบันทึกข้อมูลการตั้งค่าระบบและธีมทั้งหมดลงในฐานข้อมูล");

        _btnReload = new Button
        {
            Text = "รีโหลดค่าเดิม",
            BackColor = Color.White,
            ForeColor = Color.FromArgb(30, 41, 59),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
            Size = new Size(150, 46),
            Cursor = Cursors.Hand
        };
        _btnReload.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        _btnReload.Click += async (s, e) => await LoadSettingsAsync();
        _toolTip.SetToolTip(_btnReload, "ยกเลิกการแก้ไขและโหลดข้อมูลการตั้งค่าเดิมล่าสุดจากฐานข้อมูล");

        pnlActions.Controls.Add(_btnSave);
        pnlActions.Controls.Add(_btnReload);

        mainScrollPanel.Controls.Add(pnlActions);
        mainScrollPanel.Controls.Add(gridLayout);
        mainScrollPanel.Controls.Add(headerContainer);

        headerContainer.SendToBack();
        gridLayout.BringToFront();
        pnlActions.BringToFront();

        Controls.Add(mainScrollPanel);
    }

    private static Panel CreateGroupPanel(string title, int height)
    {
        var panel = new Panel
        {
            Width = 560,
            Height = height,
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(15),
            Margin = new Padding(0, 0, 0, 16)
        };

        var lblHeader = new Label
        {
            Text = title,
            Font = new Font("Segoe UI", 11.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59),
            Location = new Point(15, 12),
            AutoSize = true
        };

        var line = new Panel
        {
            Location = new Point(15, 40),
            Height = 1,
            BackColor = Color.FromArgb(226, 232, 240),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        panel.Controls.Add(lblHeader);
        panel.Controls.Add(line);
        return panel;
    }

    #region Group 1: Shop & Branding
    private void BuildShopFields(Panel pnl)
    {
        var lblName = new Label { Text = "ชื่อโรงแรม / ที่พัก:", Location = new Point(15, 54), AutoSize = true, Font = new Font("Segoe UI", 10.5F, FontStyle.Bold) };
        _txtShopName = new TextBox { Location = new Point(160, 50), Width = 375, Font = new Font("Segoe UI", 10.5F), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };

        var lblTaxId = new Label { Text = "เลขประจำตัวผู้เสียภาษี:", Location = new Point(15, 94), AutoSize = true, Font = new Font("Segoe UI", 10.5F, FontStyle.Bold) };
        _txtShopTaxId = new TextBox { Location = new Point(160, 90), Width = 375, Font = new Font("Segoe UI", 10.5F), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };

        var lblPhone = new Label { Text = "เบอร์โทรศัพท์ติดต่อ:", Location = new Point(15, 134), AutoSize = true, Font = new Font("Segoe UI", 10.5F, FontStyle.Bold) };
        _txtShopPhone = new TextBox { Location = new Point(160, 130), Width = 375, Font = new Font("Segoe UI", 10.5F), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };

        var lblAddr = new Label { Text = "ที่อยู่สถานประกอบการ:", Location = new Point(15, 174), AutoSize = true, Font = new Font("Segoe UI", 10.5F, FontStyle.Bold) };
        _txtShopAddress = new TextBox { Location = new Point(160, 170), Width = 375, Height = 48, Multiline = true, Font = new Font("Segoe UI", 10F), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };

        var lblHeaderMsg = new Label { Text = "ข้อความต้อนรับหัวบิล:", Location = new Point(15, 228), AutoSize = true, Font = new Font("Segoe UI", 10.5F, FontStyle.Bold) };
        _txtBillHeader = new TextBox { Location = new Point(160, 224), Width = 375, Font = new Font("Segoe UI", 10F), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };

        var lblFooterMsg = new Label { Text = "ข้อความขอบคุณท้ายบิล:", Location = new Point(15, 268), AutoSize = true, Font = new Font("Segoe UI", 10.5F, FontStyle.Bold) };
        _txtBillFooter = new TextBox { Location = new Point(160, 264), Width = 375, Font = new Font("Segoe UI", 10F), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };

        var lblLobbyTerms = new Label { Text = "ข้อตกลงและเงื่อนไข:", Location = new Point(15, 308), AutoSize = true, Font = new Font("Segoe UI", 10.5F, FontStyle.Bold) };
        _txtLobbyTerms = new TextBox
        {
            Location = new Point(160, 304),
            Width = 375,
            Height = 90,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Segoe UI", 10.5F, FontStyle.Regular),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        var lblLobbyTermsNote = new Label
        {
            Text = "* ข้อตกลงนี้จะถูกพิมพ์ท้ายบิล/ใบเสร็จ โดยระบบจะคำนวณการขึ้นบรรทัดให้อัตโนมัติ ไม่ล้นขอบกระดาษ",
            Location = new Point(160, 400),
            AutoSize = true,
            Font = new Font("Segoe UI", 9F, FontStyle.Italic),
            ForeColor = Color.FromArgb(100, 116, 139)
        };

        // Logo & QR Upload
        var lblLogoHeader = new Label { Text = "รูปโลโก้:", Location = new Point(15, 435), AutoSize = true, Font = new Font("Segoe UI", 10.5F, FontStyle.Bold) };
        _picLogo = new PictureBox
        {
            Location = new Point(100, 430),
            Size = new Size(110, 75),
            BorderStyle = BorderStyle.FixedSingle,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.FromArgb(248, 250, 252)
        };

        var btnUploadLogo = new Button { Text = "เลือกรูปโลโก้", Location = new Point(100, 512), Size = new Size(110, 30), Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), Cursor = Cursors.Hand };
        btnUploadLogo.Click += (s, e) => UploadImage(true);

        var lblQrHeader = new Label { Text = "รูป PromptPay:", Location = new Point(245, 435), AutoSize = true, Font = new Font("Segoe UI", 10.5F, FontStyle.Bold) };
        _picQrCode = new PictureBox
        {
            Location = new Point(355, 430),
            Size = new Size(110, 75),
            BorderStyle = BorderStyle.FixedSingle,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.FromArgb(248, 250, 252)
        };

        var btnUploadQr = new Button { Text = "เลือกรูป QR", Location = new Point(355, 512), Size = new Size(110, 30), Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), Cursor = Cursors.Hand };
        btnUploadQr.Click += (s, e) => UploadImage(false);

        pnl.Controls.AddRange(new Control[]
        {
            lblName, _txtShopName, lblTaxId, _txtShopTaxId,
            lblPhone, _txtShopPhone, lblAddr, _txtShopAddress,
            lblHeaderMsg, _txtBillHeader, lblFooterMsg, _txtBillFooter,
            lblLobbyTerms, _txtLobbyTerms, lblLobbyTermsNote,
            lblLogoHeader, _picLogo, btnUploadLogo,
            lblQrHeader, _picQrCode, btnUploadQr
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
    #endregion

    #region Group 2: Document Sequences
    private void BuildDocSeqFields(Panel pnl)
    {
        var lblPrefix = new Label { Text = "คำนำหน้าเลขบิล:", Location = new Point(15, 54), AutoSize = true, Font = new Font("Segoe UI", 10.5F, FontStyle.Bold) };
        _txtDocPrefix = new TextBox { Location = new Point(160, 50), Width = 120, Font = new Font("Segoe UI", 10.5F) };

        var lblRunning = new Label { Text = "เลขรันบิลล่าสุด:", Location = new Point(300, 54), AutoSize = true, Font = new Font("Segoe UI", 10.5F, FontStyle.Bold) };
        _numDocRunning = new NumericUpDown { Location = new Point(415, 50), Width = 120, Maximum = 999999, Minimum = 0, Font = new Font("Segoe UI", 10.5F), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };

        _btnResetSequences = new Button
        {
            Text = "รีเซ็ตลำดับคีย์และเลขรันทั้งหมด (Reset Sequences)",
            BackColor = Color.FromArgb(239, 68, 68),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            Location = new Point(15, 95),
            Size = new Size(520, 38),
            Cursor = Cursors.Hand,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        _btnResetSequences.FlatAppearance.BorderSize = 0;
        _btnResetSequences.Click += async (s, e) => await ResetDatabaseSequencesAsync();

        var lblInfoSeq = new Label
        {
            Text = "* ปรับค่า Auto-increment ID ให้ต่อจาก ID ล่าสุดที่มีอยู่ เพื่อล้างช่องว่างจากการลบ",
            Location = new Point(15, 142),
            AutoSize = true,
            Font = new Font("Segoe UI", 9F, FontStyle.Italic),
            ForeColor = Color.DimGray
        };

        pnl.Controls.AddRange(new Control[]
        {
            lblPrefix, _txtDocPrefix, lblRunning, _numDocRunning, _btnResetSequences, lblInfoSeq
        });
    }
    #endregion

    #region Group 4: Printer & Paper
    private void BuildPrinterFields(Panel pnl)
    {
        var lblPrinter = new Label { Text = "เครื่องพิมพ์หลัก:", Location = new Point(15, 54), AutoSize = true, Font = new Font("Segoe UI", 10.5F, FontStyle.Bold) };
        _cboPrinterList = new ComboBox { Location = new Point(160, 50), Width = 375, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 10F), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };

        _cboPrinterList.Items.Add("(ใช้เครื่องพิมพ์ตั้งต้นของ Windows)");
        foreach (string printer in PrinterSettings.InstalledPrinters)
        {
            _cboPrinterList.Items.Add(printer);
        }
        _cboPrinterList.SelectedIndex = 0;

        var lblPaper = new Label { Text = "ขนาดกระดาษเอกสาร:", Location = new Point(15, 94), AutoSize = true, Font = new Font("Segoe UI", 10.5F, FontStyle.Bold) };
        _cboPaperType = new ComboBox { Location = new Point(160, 90), Width = 180, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 10F) };
        _cboPaperType.Items.AddRange(new object[] { "A4", "80mm", "58mm" });
        _cboPaperType.SelectedIndex = 0;

        _chkAutoPrintOnCheckout = new CheckBox
        {
            Text = "พิมพ์ใบเสร็จอัตโนมัติเมื่อทำการเช็คเอาท์สำเร็จ",
            Location = new Point(15, 132),
            AutoSize = true,
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold)
        };

        _chkShowSignatureBox = new CheckBox
        {
            Text = "แสดงช่องลงลายมือชื่อผู้เข้าพักและเจ้าหน้าที่ในใบเสร็จ",
            Location = new Point(15, 166),
            AutoSize = true,
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold)
        };

        var lblFeedLines = new Label { Text = "ระยะป้อนกระดาษท้ายสลิป (บรรทัด):", Location = new Point(15, 205), AutoSize = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
        _numPrinterFeedLines = new NumericUpDown
        {
            Location = new Point(255, 202),
            Width = 65,
            Minimum = 0,
            Maximum = 20,
            Value = 4,
            Font = new Font("Segoe UI", 10F)
        };

        _chkPrinterAutoCut = new CheckBox
        {
            Text = "ตัดกระดาษอัตโนมัติ (Auto Cut)",
            Location = new Point(335, 203),
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
    #endregion

    #region Group 5: Operations & Deposit & Auto-Backup
    private void BuildOpsFields(Panel pnl)
    {
        var lblCheckIn = new Label { Text = "เวลาเช็คอินมาตรฐาน:", Location = new Point(15, 54), AutoSize = true, Font = new Font("Segoe UI", 10.5F, FontStyle.Bold) };
        _txtCheckInTime = new TextBox { Text = "14:00", Location = new Point(180, 50), Width = 100, Font = new Font("Segoe UI", 10.5F) };

        var lblCheckOut = new Label { Text = "เวลาเช็คเอาท์:", Location = new Point(300, 54), AutoSize = true, Font = new Font("Segoe UI", 10.5F, FontStyle.Bold) };
        _txtCheckOutTime = new TextBox { Text = "415", Location = new Point(400, 50), Width = 135, Font = new Font("Segoe UI", 10.5F), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        _txtCheckOutTime.Text = "12:00";

        var lblDeposit = new Label { Text = "เงินประกันห้องพักเริ่มต้น:", Location = new Point(15, 94), AutoSize = true, Font = new Font("Segoe UI", 10.5F, FontStyle.Bold) };
        _numDeposit = new NumericUpDown { Location = new Point(180, 90), Width = 100, Maximum = 100000, DecimalPlaces = 2, Font = new Font("Segoe UI", 10.5F) };

        var lblVat = new Label { Text = "อัตราภาษี VAT (%):", Location = new Point(300, 94), AutoSize = true, Font = new Font("Segoe UI", 10.5F, FontStyle.Bold) };
        _numVatRate = new NumericUpDown { Location = new Point(420, 90), Width = 115, Maximum = 30, DecimalPlaces = 2, Value = 7, Font = new Font("Segoe UI", 10.5F), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };

        _chkEnableVat = new CheckBox
        {
            Text = "คำนวณและแสดงภาษีมูลค่าเพิ่ม (VAT) ในใบเสร็จ",
            Location = new Point(15, 132),
            AutoSize = true,
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold)
        };

        _chkAutoBackup = new CheckBox
        {
            Text = "เปิดใช้งานการสำรองข้อมูลอัตโนมัติ (Auto-Backup)",
            Location = new Point(15, 168),
            AutoSize = true,
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold)
        };

        _chkAutoBackupOnExit = new CheckBox
        {
            Text = "สำรองข้อมูลอัตโนมัติขณะปิดแอปพลิเคชัน",
            Location = new Point(15, 202),
            AutoSize = true,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold)
        };

        var lblKeepFiles = new Label { Text = "จำนวนไฟล์สำรองสูงสุดที่เก็บย้อนหลัง:", Location = new Point(15, 240), AutoSize = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
        _numBackupMaxKeepFiles = new NumericUpDown { Location = new Point(275, 236), Width = 80, Minimum = 5, Maximum = 365, Value = 30, Font = new Font("Segoe UI", 10F) };

        var lblBackupDir = new Label { Text = "โฟลเดอร์เก็บไฟล์สำรอง:", Location = new Point(15, 280), AutoSize = true, Font = new Font("Segoe UI", 10.5F, FontStyle.Bold) };
        _txtBackupFolder = new TextBox { Location = new Point(180, 276), Width = 245, Font = new Font("Segoe UI", 10F), PlaceholderText = "เว้นว่างไว้เพื่อใช้โฟลเดอร์ AppData", Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };

        _btnBrowseBackupFolder = new Button
        {
            Text = "เลือกโฟลเดอร์...",
            Location = new Point(435, 274),
            Size = new Size(100, 32),
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        _btnBrowseBackupFolder.Click += (s, e) =>
        {
            using var fbd = new FolderBrowserDialog { Description = "เลือกโฟลเดอร์ปลายทางสำหรับเก็บไฟล์สำรองข้อมูล (Backup Directory)" };
            if (fbd.ShowDialog() == DialogResult.OK)
            {
                _txtBackupFolder.Text = fbd.SelectedPath;
            }
        };

        pnl.Controls.AddRange(new Control[]
        {
            lblCheckIn, _txtCheckInTime, lblCheckOut, _txtCheckOutTime,
            lblDeposit, _numDeposit, lblVat, _numVatRate, _chkEnableVat,
            _chkAutoBackup, _chkAutoBackupOnExit, lblKeepFiles, _numBackupMaxKeepFiles,
            lblBackupDir, _txtBackupFolder, _btnBrowseBackupFolder
        });
    }
    #endregion

    #region Group 6: Security & Set Zero
    private void BuildSecurityFields(Panel pnl)
    {
        var lblPassword = new Label { Text = "รหัสผ่าน Admin ใหม่:", Location = new Point(15, 54), AutoSize = true, Font = new Font("Segoe UI", 10.5F, FontStyle.Bold) };
        _txtAdminPassword = new TextBox { Location = new Point(180, 50), Width = 355, UseSystemPasswordChar = true, Font = new Font("Segoe UI", 10.5F), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };

        var lblConfirm = new Label { Text = "ยืนยันรหัสผ่านใหม่:", Location = new Point(15, 94), AutoSize = true, Font = new Font("Segoe UI", 10.5F, FontStyle.Bold) };
        _txtConfirmPassword = new TextBox { Location = new Point(180, 90), Width = 355, UseSystemPasswordChar = true, Font = new Font("Segoe UI", 10.5F), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };

        var lblPasswordInfo = new Label
        {
            Text = "* เว้นว่างไว้หากไม่ต้องการเปลี่ยนรหัสผ่านผู้ดูแลระบบ",
            Location = new Point(15, 128),
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
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            Location = new Point(15, 160),
            Size = new Size(520, 38),
            Cursor = Cursors.Hand,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        _btnZetZero.FlatAppearance.BorderSize = 0;
        _btnZetZero.Click += BtnZetZero_Click;

        var lblZetZeroInfo = new Label
        {
            Text = "* ล้างข้อมูลการจอง ประวัติลูกค้า ประวัติมิเตอร์ ค่าน้ำไฟ และ POS เพื่อเริ่มใช้งานจริง",
            Location = new Point(15, 203),
            AutoSize = true,
            Font = new Font("Segoe UI", 9F, FontStyle.Italic),
            ForeColor = Color.DimGray
        };

        _btnOpenAuditLog = new Button
        {
            Text = "เปิดดูบันทึกประวัติระบบ (Open Audit Log)",
            BackColor = Color.FromArgb(79, 70, 229),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            Location = new Point(15, 235),
            Size = new Size(520, 38),
            Cursor = Cursors.Hand,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        _btnOpenAuditLog.FlatAppearance.BorderSize = 0;
        _btnOpenAuditLog.Click += (s, e) => OpenAuditLog();

        var lblAuditLogInfo = new Label
        {
            Text = "* ตรวจสอบประวัติการเช็คอิน เช็คเอาท์ บันทึกมิเตอร์ หรือล้างระบบย้อนหลัง",
            Location = new Point(15, 278),
            AutoSize = true,
            Font = new Font("Segoe UI", 9F, FontStyle.Italic),
            ForeColor = Color.DimGray
        };

        pnl.Controls.AddRange(new Control[]
        {
            lblPassword, _txtAdminPassword, lblConfirm, _txtConfirmPassword, lblPasswordInfo,
            _btnZetZero, lblZetZeroInfo, _btnOpenAuditLog, lblAuditLogInfo
        });
    }

    private void OpenAuditLog()
    {
        using var form = new Form
        {
            Text = "ประวัติการทำงานระบบ (Audit Log Trail)",
            Width = 1050,
            Height = 680,
            StartPosition = FormStartPosition.CenterParent,
            Font = new Font("Segoe UI", 10.5F),
            MinimizeBox = false,
            MaximizeBox = true
        };
        try { form.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

        var auditCtrl = new AuditLogControl(_auditService ?? new AuditService(null!, null!)) { Dock = DockStyle.Fill };
        form.Controls.Add(auditCtrl);
        form.Load += async (sender, ev) => await auditCtrl.LoadLogsAsync();
        form.ShowDialog(this);
    }
    #endregion

    #region Data Loading & Saving Logic
    private async Task ResetDatabaseSequencesAsync()
    {
        if (MessageBox.Show("ยืนยันการรีเซ็ตคีย์หลักในฐานข้อมูลและเลขรันบิลทั้งหมด?\nการดำเนินการนี้จะปรับค่า Auto-increment ID ของทุกตารางให้รันต่อจากข้อมูลล่าสุดที่มีอยู่ และตั้งค่าเลขรันบิลกลับไปเป็น 0", "ยืนยันการรีเซ็ต", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
        {
            try
            {
                await _settingsService.ResetDatabaseSequencesAsync();
                if (_auditService != null)
                {
                    await _auditService.LogAsync("RESET_SEQUENCES", "database", "settings", "รีเซ็ตลำดับคีย์หลักและเลขรันบิลเริ่มต้นใหม่");
                }
                MessageBox.Show("รีเซ็ตลำดับคีย์หลักและเลขรันบิลเรียบร้อยแล้ว", "สำเร็จ", MessageBoxButtons.OK, MessageBoxIcon.Information);
                await LoadSettingsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"รีเซ็ตไม่สำเร็จ: {ex.Message}", "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
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

            var lblPrompt = new Label { Text = "กรุณากรอกรหัสผ่าน Admin เพื่อดำเนินการต่อ:", Location = new Point(20, 15), Size = new Size(320, 25), Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
            var txtPwd = new TextBox { Location = new Point(20, 45), Width = 320, UseSystemPasswordChar = true, Font = new Font("Segoe UI", 10.5F) };
            var lblHint = new Label { Text = "* รหัสผ่านเริ่มต้นคือ psoft123 หรือรหัส Admin ที่ตั้งไว้", Location = new Point(20, 78), AutoSize = true, Font = new Font("Segoe UI", 8.5F, FontStyle.Italic), ForeColor = Color.DimGray };

            var btnOk = new Button { Text = "ยืนยัน", Location = new Point(140, 110), Size = new Size(95, 36), DialogResult = DialogResult.OK, BackColor = Color.FromArgb(220, 38, 38), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnOk.FlatAppearance.BorderSize = 0;
            var btnCancel = new Button { Text = "ยกเลิก", Location = new Point(245, 110), Size = new Size(95, 36), DialogResult = DialogResult.Cancel, BackColor = Color.FromArgb(226, 232, 240), FlatStyle = FlatStyle.Flat };
            btnCancel.FlatAppearance.BorderSize = 0;

            confirmDlg.Controls.AddRange(new Control[] { lblPrompt, txtPwd, lblHint, btnOk, btnCancel });
            confirmDlg.AcceptButton = btnOk;
            confirmDlg.CancelButton = btnCancel;

            if (confirmDlg.ShowDialog() == DialogResult.OK)
            {
                var inputPwd = txtPwd.Text.Trim();
                var currentPwd = await _settingsService.GetAsync("admin_password") ?? "psoft123";
                if (string.IsNullOrWhiteSpace(currentPwd)) currentPwd = "psoft123";

                // ใช้ PasswordHelper.VerifyPassword เพื่อรองรับ PBKDF2/SHA256/PlainText + auto-upgrade
                var (matches, upgradedHash) = PasswordHelper.VerifyPassword(inputPwd, currentPwd);
                if (matches && upgradedHash != null)
                {
                    await _settingsService.SetAsync("admin_password", upgradedHash);
                }

                if (!matches)
                {
                    MessageBox.Show("รหัสผ่านไม่ถูกต้อง การทำรายการล้มเหลว\n(รหัสผ่านเริ่มต้นของระบบคือ: psoft123)", "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                try
                {
                    await _settingsService.ZetZeroDatabaseAsync();
                    if (_auditService != null)
                    {
                        await _auditService.LogAsync("SYSTEM_RESET", "database", "settings", "เคลียร์ระบบข้อมูลทั้งหมดเพื่อเริ่มใช้งานจริง (Set Zero) และรีสตาร์ทแอปพลิเคชัน");
                    }
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

    private async Task LoadSettingsAsync()
    {
        try
        {
            var dto = await _settingsService.GetAllSettingsAsync();
            _existingSettings = dto;

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

            // Printer
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

            // Operations & Auto-Backup
            _txtCheckInTime.Text = dto.DefaultCheckInTime;
            _txtCheckOutTime.Text = dto.DefaultCheckOutTime;
            _numDeposit.Value = Math.Min(_numDeposit.Maximum, Math.Max(0, dto.DefaultSecurityDeposit));
            _numVatRate.Value = Math.Min(_numVatRate.Maximum, Math.Max(0, dto.VatRate));
            _chkEnableVat.Checked = dto.EnableVat;

            _chkAutoBackup.Checked = dto.AutoBackupEnabled;
            _chkAutoBackupOnExit.Checked = dto.AutoBackupOnExit;
            _numBackupMaxKeepFiles.Value = Math.Min(_numBackupMaxKeepFiles.Maximum, Math.Max(5, dto.AutoBackupMaxKeepFiles));
            _txtBackupFolder.Text = dto.CustomBackupFolderPath ?? "";

            // Document Sequences
            _txtDocPrefix.Text = dto.ReceiptDocPrefix;
            _numDocRunning.Value = Math.Min(_numDocRunning.Maximum, Math.Max(0, dto.ReceiptDocRunningNumber));

            _txtAdminPassword.Text = "";
            _txtConfirmPassword.Text = "";

            // Apply default theme
            ThemeManager.ApplyTheme("Slate", dto.AppFontSize ?? "Medium");
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

                // Preserve utility rates from existing settings
                ElectricBillingMode = _existingSettings.ElectricBillingMode,
                ElectricRatePerUnit = _existingSettings.ElectricRatePerUnit,
                ElectricFlatRate = _existingSettings.ElectricFlatRate,
                WaterBillingMode = _existingSettings.WaterBillingMode,
                WaterRatePerUnit = _existingSettings.WaterRatePerUnit,
                WaterFlatRatePerPerson = _existingSettings.WaterFlatRatePerPerson,
                CommonAreaFee = _existingSettings.CommonAreaFee,
                GarbageFee = _existingSettings.GarbageFee,

                AppTheme = "Slate",
                AppFontSize = _existingSettings.AppFontSize ?? "Medium",

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

                AutoBackupEnabled = _chkAutoBackup.Checked,
                AutoBackupOnExit = _chkAutoBackupOnExit.Checked,
                AutoBackupMaxKeepFiles = (int)_numBackupMaxKeepFiles.Value,
                CustomBackupFolderPath = _txtBackupFolder.Text.Trim(),

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

                var currentPwd = await _settingsService.GetAsync("admin_password") ?? "psoft123";
                if (string.IsNullOrWhiteSpace(currentPwd)) currentPwd = "psoft123";

                bool isVerified = false;
                using (var verifyDlg = new Form())
                {
                    verifyDlg.Text = "ยืนยันรหัสผ่านเดิมเพื่อเปลี่ยนรหัสผ่าน";
                    verifyDlg.Size = new Size(380, 200);
                    verifyDlg.StartPosition = FormStartPosition.CenterParent;
                    verifyDlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                    verifyDlg.MaximizeBox = false;
                    verifyDlg.MinimizeBox = false;
                    verifyDlg.Font = new Font("Segoe UI", 10F);

                    var lblP = new Label { Text = "กรุณากรอกรหัสผ่าน Admin เดิมปัจจุบัน:", Location = new Point(20, 15), Size = new Size(320, 25), Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
                    var txtP = new TextBox { Location = new Point(20, 45), Width = 320, UseSystemPasswordChar = true };
                    var btnOk = new Button { Text = "ยืนยัน", Location = new Point(140, 95), Size = new Size(95, 36), DialogResult = DialogResult.OK, BackColor = Color.FromArgb(16, 185, 129), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
                    btnOk.FlatAppearance.BorderSize = 0;
                    var btnCancel = new Button { Text = "ยกเลิก", Location = new Point(245, 95), Size = new Size(95, 36), DialogResult = DialogResult.Cancel, BackColor = Color.FromArgb(226, 232, 240), FlatStyle = FlatStyle.Flat };
                    btnCancel.FlatAppearance.BorderSize = 0;

                    verifyDlg.Controls.AddRange(new Control[] { lblP, txtP, btnOk, btnCancel });
                    verifyDlg.AcceptButton = btnOk;
                    verifyDlg.CancelButton = btnCancel;

                    if (verifyDlg.ShowDialog(this) == DialogResult.OK)
                    {
                        var typedPwd = txtP.Text.Trim();
                        // ใช้ PasswordHelper.VerifyPassword เพื่อรองรับ PBKDF2/SHA256/PlainText + auto-upgrade
                        var (isMatch, upgHash) = PasswordHelper.VerifyPassword(typedPwd, currentPwd);
                        if (isMatch)
                        {
                            isVerified = true;
                            if (upgHash != null)
                            {
                                await _settingsService.SetAsync("admin_password", upgHash);
                            }
                        }
                    }
                }

                if (!isVerified)
                {
                    MessageBox.Show("รหัสผ่านเดิมไม่ถูกต้อง การเปลี่ยนรหัสผ่านถูกยกเลิก", "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Hash รหัสผ่านด้วย PBKDF2 ก่อนบันทึก (ห้ามเก็บ plaintext)
                string hashedPwd = PasswordHelper.HashPassword(pwd);
                await _settingsService.SetAsync("admin_password", hashedPwd);
                await _settingsService.SetAsync("is_custom_admin_password_set", "1");
            }

            await _settingsService.SaveAllSettingsAsync(dto);

            MessageBox.Show("บันทึกการตั้งค่าระบบเรียบร้อยแล้ว", "สำเร็จ", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

    private static string ComputeSha256Hash(string rawData)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawData));
        var sb = new StringBuilder();
        foreach (var b in bytes) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
    #endregion
}
