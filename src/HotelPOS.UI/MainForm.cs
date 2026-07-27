using HotelPOS.Common;
using HotelPOS.Common.Models;
using HotelPOS.Core.Services;
using HotelPOS.Data;
using HotelPOS.Data.Repositories;
using HotelPOS.Licensing;
using HotelPOS.Logging;

namespace HotelPOS.UI;

public class MainForm : Form
{
    private readonly ISettingsService _settingsService;
    private readonly IRoomService _roomService;
    private readonly IBookingService _bookingService;
    private readonly ICustomerService _customerService;
    private readonly IUtilityBillService _utilityBillService;
    private readonly IAppLogger _logger;

    private LicenseStatus _licenseStatus;
    private LicenseFile? _license;
    private int _daysRemaining;

    // USB Dongle continuous detection
    private System.Windows.Forms.Timer? _dongleCheckTimer;
    private int _gracePeriodRemainingSeconds;
    private bool _inGracePeriod;
    private Form? _graceWarningDlg;
    private Label? _graceTimeLabel;

    private System.Windows.Forms.Timer? _trialDongleCheckTimer;
    private bool _trialDongleDetected;

    private Panel _sidebarPanel = null!;
    private Panel _contentPanel = null!;
    private Panel? _licenseBannerPanel;
    private Label? _licenseBannerLabel;
    private Button? _licenseBannerButton;

    private RoomGridControl _roomGridControl = null!;
    private BookingListControl _bookingListControl = null!;
    private RoomManagementControl _roomManagementControl = null!;
    private CustomerManagementControl _customerManagementControl = null!;
    private MeterReadingControl _meterReadingControl = null!;
    private AuditLogControl _auditLogControl = null!;
    private SystemBackupControl _backupControl = null!;
    private SystemSettingsControl _systemSettingsControl = null!;
    private POSControl _posControl = null!;

    private readonly List<Button> _navButtons = new();
    private Control? _activeControl;

    public MainForm(
        ISettingsService settingsService,
        IAppLogger logger,
        LicenseStatus licenseStatus,
        LicenseFile? license,
        int daysRemaining)
    {
        _settingsService = settingsService;
        _logger = logger;
        _licenseStatus = licenseStatus;
        _license = license;
        _daysRemaining = daysRemaining;

        // Initialize Repositories & Services
        var connectionFactory = new DbConnectionFactory();
        IRoomRepository roomRepo = new RoomRepository(connectionFactory, _logger);
        IBookingRepository bookingRepo = new BookingRepository(connectionFactory, _logger);
        ICustomerRepository customerRepo = new CustomerRepository(connectionFactory, _logger);
        IFolioRepository folioRepo = new FolioRepository(connectionFactory, _logger);
        IAuditRepository auditRepo = new AuditRepository(connectionFactory, _logger);
        IProductRepository productRepo = new ProductRepository(connectionFactory, _logger);
        ISaleRepository saleRepo = new SaleRepository(connectionFactory, _logger);

        var auditService = new AuditService(auditRepo, _logger);
        _roomService = new RoomService(roomRepo, _logger);
        _customerService = new CustomerService(customerRepo, _logger);
        _bookingService = new BookingService(bookingRepo, roomRepo, customerRepo, folioRepo, _logger);
        var backupService = new BackupService(connectionFactory, auditService, _logger);
        var exportImportService = new ExportImportService(_customerService, _roomService, auditService);
        IPOSService posService = new POSService(productRepo, saleRepo, connectionFactory, _logger);

        // Utility Billing Services
        IMeterReadingRepository meterRepo = new MeterReadingRepository(connectionFactory, _logger);
        IUtilityBillRepository utilityBillRepo = new UtilityBillRepository(connectionFactory, _logger);
        _utilityBillService = new UtilityBillService(meterRepo, utilityBillRepo, _settingsService, roomRepo, _logger);

        Text = "PSoft Rest & Rent Manager - โปรแกรมจัดการห้องพักและห้องเช่า";
        Width = 1280;
        Height = 850;
        MinimumSize = new Size(1100, 720);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10.5F, FontStyle.Regular);
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

        InitializeViews(auditService, backupService, exportImportService, posService);
        InitializeLayout();

        if (_license != null && _license.LicenseType != LicenseType.Trial && !string.IsNullOrEmpty(_license.UsbHardwareId))
        {
            InitializeDongleTimer();
        }
        else if (_license != null && _license.LicenseType == LicenseType.Trial)
        {
            InitializeTrialDongleTimer();
        }

        FormClosing += (s, e) =>
        {
            _dongleCheckTimer?.Stop();
            _trialDongleCheckTimer?.Stop();
        };
    }

    private void InitializeViews(IAuditService auditService, IBackupService backupService, IExportImportService exportImportService, IPOSService posService)
    {
        _roomGridControl = new RoomGridControl(_roomService, _bookingService, _customerService, _settingsService) { Dock = DockStyle.Fill };
        _bookingListControl = new BookingListControl(_bookingService, _roomService, _customerService, _settingsService, _utilityBillService) { Dock = DockStyle.Fill };
        _roomManagementControl = new RoomManagementControl(_roomService) { Dock = DockStyle.Fill };
        _customerManagementControl = new CustomerManagementControl(_customerService) { Dock = DockStyle.Fill };
        _meterReadingControl = new MeterReadingControl(_utilityBillService, _roomService, _settingsService, _bookingService, _customerService) { Dock = DockStyle.Fill };
        _auditLogControl = new AuditLogControl(auditService) { Dock = DockStyle.Fill };
        _backupControl = new SystemBackupControl(backupService, exportImportService) { Dock = DockStyle.Fill };
        _systemSettingsControl = new SystemSettingsControl(_settingsService) { Dock = DockStyle.Fill };
        _posControl = new POSControl(posService, _settingsService, _logger) { Dock = DockStyle.Fill };
    }

    private void InitializeLayout()
    {
        Controls.Clear();

        BuildLicenseBanner();
        BuildSidebarPanel();

        _contentPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(245, 247, 250)
        };

        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var bodyContainer = new Panel { Dock = DockStyle.Fill };
        bodyContainer.Controls.Add(_contentPanel);
        bodyContainer.Controls.Add(_sidebarPanel);

        mainLayout.Controls.Add(_licenseBannerPanel!, 0, 0);
        mainLayout.Controls.Add(bodyContainer, 0, 1);

        Controls.Add(mainLayout);

        // Select initial view (Room Grid)
        if (_navButtons.Count > 0)
        {
            SwitchView(_navButtons[0], _roomGridControl);
        }

        Load += MainForm_Load;
    }

    private void BuildSidebarPanel()
    {
        _sidebarPanel = new Panel
        {
            Dock = DockStyle.Left,
            Width = 240,
            BackColor = Color.FromArgb(20, 20, 30), // Deep Luxury Dark Slate
            Padding = new Padding(0)
        };

        // Brand / Logo Section
        var brandPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 85,
            BackColor = Color.FromArgb(16, 16, 24),
            Padding = new Padding(20, 15, 15, 10)
        };

        var lblBrandTitle = new Label
        {
            Text = "PSoft R&R",
            Font = new Font("Segoe UI", 15F, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(18, 14),
            AutoSize = true
        };

        var lblBrandSub = new Label
        {
            Text = "Rest & Rent Manager",
            Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
            ForeColor = Color.FromArgb(148, 163, 184),
            Location = new Point(18, 48),
            AutoSize = true
        };

        var divLine = new Panel
        {
            Height = 1,
            Dock = DockStyle.Bottom,
            BackColor = Color.FromArgb(42, 42, 60)
        };

        brandPanel.Controls.Add(lblBrandTitle);
        brandPanel.Controls.Add(lblBrandSub);
        brandPanel.Controls.Add(divLine);

        // Navigation Buttons Container
        var navContainer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(10, 15, 10, 10),
            BackColor = Color.Transparent
        };

        _navButtons.Clear();

        var navItems = new (string title, Control control, Func<Task>? onActivate)[]
        {
            ("ผังห้องพัก", _roomGridControl, async () => await _roomGridControl.RefreshGridAsync()),
            ("รายการจอง", _bookingListControl, async () => await _bookingListControl.LoadBookingsAsync()),
            ("บริการเสริม & มินิบาร์ (POS)", _posControl, null),
            ("การจัดการห้องพัก", _roomManagementControl, null),
            ("ข้อมูลลูกค้า", _customerManagementControl, null),
            ("ค่าน้ำ / ค่าไฟ", _meterReadingControl, async () => await _meterReadingControl.LoadMeterDataAsync()),
            ("ประวัติระบบ (Audit Log)", _auditLogControl, async () => await _auditLogControl.LoadLogsAsync()),
            ("สำรอง/คืนค่าข้อมูล", _backupControl, null),
            ("ตั้งค่าระบบ", _systemSettingsControl, null)
        };

        foreach (var item in navItems)
        {
            var btn = new Button
            {
                Text = $"   {item.title}",
                TextAlign = ContentAlignment.MiddleLeft,
                Size = new Size(220, 46),
                Font = new Font("Segoe UI", 10.5F, FontStyle.Regular),
                ForeColor = Color.FromArgb(203, 213, 225),
                BackColor = Color.Transparent,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 0, 0, 6),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(38, 38, 55);

            btn.Click += async (s, e) =>
            {
                SwitchView(btn, item.control);
                if (item.onActivate != null)
                {
                    await item.onActivate();
                }
            };

            _navButtons.Add(btn);
            navContainer.Controls.Add(btn);
        }

        // Footer / User Section
        var footerPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 100,
            BackColor = Color.FromArgb(16, 16, 24),
            Padding = new Padding(15, 12, 15, 12)
        };

        var userCard = new Panel
        {
            Dock = DockStyle.Top,
            Height = 42,
            BackColor = Color.FromArgb(28, 28, 42)
        };

        var lblUser = new Label
        {
            Text = "ผู้ใช้: admin (ผู้ดูแลระบบ)",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(10, 11),
            AutoSize = true
        };
        userCard.Controls.Add(lblUser);

        var btnLogout = new Button
        {
            Text = "ออกจากระบบ",
            Dock = DockStyle.Bottom,
            Height = 32,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Color.FromArgb(185, 28, 28),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        btnLogout.FlatAppearance.BorderSize = 0;
        btnLogout.Click += (s, e) => Logout();

        footerPanel.Controls.Add(userCard);
        footerPanel.Controls.Add(btnLogout);

        _sidebarPanel.Controls.Add(navContainer);
        _sidebarPanel.Controls.Add(brandPanel);
        _sidebarPanel.Controls.Add(footerPanel);
    }

    private void SwitchView(Button selectedBtn, Control targetControl)
    {
        foreach (var btn in _navButtons)
        {
            if (btn == selectedBtn)
            {
                btn.BackColor = Color.FromArgb(37, 99, 235); // Electric Blue
                btn.ForeColor = Color.White;
                btn.Font = new Font("Segoe UI", 10.5F, FontStyle.Bold);
            }
            else
            {
                btn.BackColor = Color.Transparent;
                btn.ForeColor = Color.FromArgb(203, 213, 225);
                btn.Font = new Font("Segoe UI", 10.5F, FontStyle.Regular);
            }
        }

        if (_activeControl != targetControl)
        {
            _contentPanel.Controls.Clear();
            _contentPanel.Controls.Add(targetControl);
            _activeControl = targetControl;
        }
    }

    private void Logout()
    {
        if (MessageBox.Show("คุณต้องการออกจากระบบและกลับไปยังหน้าล็อกอินหรือไม่?", "ยืนยันการออกจากระบบ", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            _logger.Info(LogCategory.System, "ผู้ใช้งานออกจากระบบ");
            Application.Restart();
        }
    }

    private void BuildLicenseBanner()
    {
        _licenseBannerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 48,
            Padding = new Padding(15, 6, 15, 6),
            BackColor = Color.FromArgb(241, 245, 249)
        };

        _licenseBannerLabel = new Label
        {
            AutoSize = true,
            Location = new Point(15, 12),
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold)
        };

        _licenseBannerButton = new Button
        {
            Text = "ลงทะเบียนรหัสสิทธิ์ (Activate)",
            Size = new Size(240, 32),
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            Location = new Point(980, 8),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            BackColor = Color.FromArgb(30, 41, 59),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };

        _licenseBannerPanel.Controls.Add(_licenseBannerLabel);
        _licenseBannerPanel.Controls.Add(_licenseBannerButton);
        _licenseBannerButton.Click += LicenseBannerButton_Click;

        UpdateBannerUI();
    }

    private void UpdateBannerUI()
    {
        if (_licenseStatus != LicenseStatus.Active)
        {
            _licenseBannerPanel!.BackColor = Color.MistyRose;
            _licenseBannerLabel!.ForeColor = Color.Crimson;
            _licenseBannerLabel.Text = "สิทธิ์ใช้งานหมดอายุหรือผิดพลาด (ระบบเข้าสู่โหมดจำกัดสิทธิ์ / อ่านอย่างเดียว)";
            _licenseBannerButton!.Text = "ลงทะเบียนรหัสสิทธิ์ (Activate)";
            _licenseBannerButton!.Visible = true;
        }
        else if (_license != null && _license.LicenseType == LicenseType.Trial)
        {
            _licenseBannerPanel!.BackColor = Color.LightCyan;
            _licenseBannerLabel!.ForeColor = Color.DarkSlateGray;
            _licenseBannerLabel.Text = $"กำลังใช้งานโหมดทดลองใช้ (Trial Mode) มีเวลาเหลืออีก {_daysRemaining} วัน";
            _licenseBannerButton!.Text = "ลงทะเบียนรหัสสิทธิ์ (Activate)";
            _licenseBannerButton!.Visible = true;
        }
        else
        {
            _licenseBannerPanel!.BackColor = Color.Honeydew;
            _licenseBannerLabel!.ForeColor = Color.ForestGreen;
            var expireText = _license?.ExpireDate.HasValue == true
                ? $"หมดอายุวันที่ {_license.ExpireDate.Value:dd/MM/yyyy}"
                : "สิทธิ์การใช้งานแบบถาวร (Lifetime)";
            _licenseBannerLabel.Text = $"ระบบได้รับการลงทะเบียนเรียบร้อยแล้ว: {expireText}";
            _licenseBannerButton!.Text = "ต่ออายุ / อัปเกรดลิขสิทธิ์ (Renew)";
            _licenseBannerButton!.Visible = true;
        }
    }

    private void LicenseBannerButton_Click(object? sender, EventArgs e)
    {
        var currentStatusText = _licenseStatus == LicenseStatus.Expired
            ? "หมดอายุแล้ว"
            : (_licenseStatus == LicenseStatus.Invalid ? "คีย์ไม่ถูกต้อง" : "ยังไม่ได้ลงทะเบียน");

        using var activationForm = new LicenseActivationForm(currentStatusText);
        if (activationForm.ShowDialog() == DialogResult.OK)
        {
            var checkResult = LicenseManager.CheckLicense();
            _licenseStatus = checkResult.Status;
            _license = checkResult.License;
            _daysRemaining = checkResult.DaysRemaining;

            UpdateBannerUI();
        }
    }

    private async void MainForm_Load(object? sender, EventArgs e)
    {
        var correlationId = _logger.NewCorrelationId();
        try
        {
            var shopName = await _settingsService.GetShopNameAsync();
            Text = $"{shopName} - HotelPOS TH";
            _logger.Info(LogCategory.UI, "โหลดหน้าหลักพร้อมแถบไซด์บาร์สำเร็จ", correlationId);
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.UI, "โหลดข้อมูลหน้าหลักไม่สำเร็จ", ex, correlationId);
        }
    }

    private void InitializeDongleTimer()
    {
        _dongleCheckTimer = new System.Windows.Forms.Timer();
        _dongleCheckTimer.Interval = 15000; // Check every 15 seconds
        _dongleCheckTimer.Tick += DongleCheckTimer_Tick;
        _dongleCheckTimer.Start();
    }

    private void DongleCheckTimer_Tick(object? sender, EventArgs e)
    {
        var (dongleLicense, usbInfo, _) = UsbDongleManager.ScanForDongleKey();
        
        bool dongleFound = false;
        if (dongleLicense != null && usbInfo != null)
        {
            if (dongleLicense.UsbHardwareId == _license?.UsbHardwareId && usbInfo.UsbHardwareId == _license?.UsbHardwareId)
            {
                dongleFound = true;
            }
        }

        if (dongleFound)
        {
            if (_inGracePeriod)
            {
                _inGracePeriod = false;
                _gracePeriodRemainingSeconds = 0;
                if (_graceWarningDlg != null)
                {
                    _graceWarningDlg.Close();
                    _graceWarningDlg = null;
                }
                _logger.Info(LogCategory.License, "พบ USB Dongle กลับมาเชื่อมต่อตามปกติ ยกเลิกโหมดผ่อนผัน");
            }
        }
        else
        {
            if (!_inGracePeriod)
            {
                _inGracePeriod = true;
                _gracePeriodRemainingSeconds = 300; // 5 minutes grace period
                _logger.Info(LogCategory.License, "WARNING: ไม่พบ USB Dongle ลิขสิทธิ์หลัก เริ่มโหมดผ่อนผัน 5 นาที");
                ShowGraceWarningDialog();
            }
            else
            {
                _gracePeriodRemainingSeconds -= 15;
                if (_graceTimeLabel != null)
                {
                    int mins = _gracePeriodRemainingSeconds / 60;
                    int secs = _gracePeriodRemainingSeconds % 60;
                    _graceTimeLabel.Text = $"เวลาที่เหลือ: {mins} นาที {secs:D2} วินาที";
                }

                if (_gracePeriodRemainingSeconds <= 0)
                {
                    _dongleCheckTimer?.Stop();
                    if (_graceWarningDlg != null)
                    {
                        _graceWarningDlg.Close();
                        _graceWarningDlg = null;
                    }
                    _logger.Error(LogCategory.License, "หมดเวลาผ่อนผัน USB Dongle ยังคงไม่ถูกเสียบกลับคืน ทำการปิดโปรแกรม");
                    MessageBox.Show("หมดเวลาการผ่อนผันการตรวจพบอุปกรณ์ลิขสิทธิ์ (USB Dongle) ระบบจะทำการปิดตัวเองเพื่อความปลอดภัย", "ตรวจสอบลิขสิทธิ์ล้มเหลว", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Application.Exit();
                }
            }
        }
    }

    private void ShowGraceWarningDialog()
    {
        _graceWarningDlg = new Form
        {
            Text = "คำเตือน: อุปกรณ์ลิขสิทธิ์ขาดการเชื่อมต่อ",
            Size = new Size(480, 240),
            StartPosition = FormStartPosition.CenterScreen,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ControlBox = false,
            TopMost = true,
            Font = new Font("Segoe UI", 10F)
        };

        var lblIcon = new Label
        {
            Text = "",
            Font = new Font("Segoe UI", 24F),
            ForeColor = Color.OrangeRed,
            Location = new Point(20, 20),
            AutoSize = true
        };

        var lblTitle = new Label
        {
            Text = "ไม่พบ USB Dongle ลิขสิทธิ์หลัก!",
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            ForeColor = Color.FromArgb(220, 38, 38),
            Location = new Point(70, 20),
            AutoSize = true
        };

        var lblDesc = new Label
        {
            Text = "กรุณาเสียบ USB Dongle กลับคืนเข้าพอร์ตเดิมโดยเร็วที่สุด ระบบอนุญาตให้ท่านกรอกและบันทึกข้อมูลทำงานต่อชั่วคราวได้อีก 5 นาที หากหมดเวลาโปรแกรมจะปิดตัวเองโดยอัตโนมัติ",
            Location = new Point(70, 50),
            Size = new Size(370, 80)
        };

        _graceTimeLabel = new Label
        {
            Text = "เวลาที่เหลือ: 5 นาที 00 วินาที",
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            ForeColor = Color.OrangeRed,
            Location = new Point(70, 140),
            AutoSize = true
        };

        _graceWarningDlg.Controls.AddRange(new Control[] { lblIcon, lblTitle, lblDesc, _graceTimeLabel });
        _graceWarningDlg.Show(); // Non-modal so they can save their work
    }

    private void InitializeTrialDongleTimer()
    {
        _trialDongleCheckTimer = new System.Windows.Forms.Timer();
        _trialDongleCheckTimer.Interval = 15000; // Check every 15 seconds
        _trialDongleCheckTimer.Tick += TrialDongleCheckTimer_Tick;
        _trialDongleCheckTimer.Start();
    }

    private void TrialDongleCheckTimer_Tick(object? sender, EventArgs e)
    {
        if (_trialDongleDetected) return;

        var (dongleLicense, usbInfo, _) = UsbDongleManager.ScanForDongleKey();
        if (dongleLicense != null && usbInfo != null)
        {
            var currentAppSerial = AppWatermarkManager.GetCurrentAppSerial();
            var status = LicenseValidator.ValidateDongle(dongleLicense, usbInfo.UsbHardwareId, currentAppSerial);
            if (status == LicenseStatus.Active)
            {
                _trialDongleDetected = true;
                _trialDongleCheckTimer?.Stop();

                MessageBox.Show(
                    "ตรวจพบอุปกรณ์ลิขสิทธิ์ (USB Dongle) แล้ว!\n\nกรุณาปิดและเปิดโปรแกรมใหม่อีกครั้งเพื่อเริ่มใช้งานรุ่นเต็มรูปแบบ โดยข้อมูลทั้งหมดที่ถูกบันทึกไว้จะยังคงอยู่ครบถ้วน",
                    "ตรวจพบอุปกรณ์ลิขสิทธิ์",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }
    }
}
