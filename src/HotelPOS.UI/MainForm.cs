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
    public IUtilityBillService UtilityBillService => _utilityBillService;
    private readonly IBackupService _backupService;
    private readonly IAppLogger _logger;

    private LicenseStatus _licenseStatus;
    private LicenseFile? _license;
    private int _daysRemaining;
    private bool _isReadOnlyMode;

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
    private SystemBackupControl _backupControl = null!;
    private SystemSettingsControl _systemSettingsControl = null!;
    private POSControl _posControl = null!;
    private SummaryReportControl _summaryReportControl = null!;

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
        _bookingService = new BookingService(bookingRepo, roomRepo, customerRepo, folioRepo, _logger, auditService);
        var backupService = new BackupService(connectionFactory, auditService, _logger, _settingsService);
        _backupService = backupService;
        IPOSService posService = new POSService(productRepo, saleRepo, connectionFactory, _logger);
        var exportImportService = new ExportImportService(_customerService, _roomService, auditService, posService);

        // Utility Billing Services
        IMeterReadingRepository meterRepo = new MeterReadingRepository(connectionFactory, _logger);
        IUtilityBillRepository utilityBillRepo = new UtilityBillRepository(connectionFactory, _logger);
        _utilityBillService = new UtilityBillService(meterRepo, utilityBillRepo, _settingsService, roomRepo, _logger, auditService);

        Text = "PSoft Rest & Rent Manager - โปรแกรมจัดการห้องพักและห้องเช่า";
        Width = 1280;
        Height = 850;
        MinimumSize = new Size(1100, 720);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10.5F, FontStyle.Regular);
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

        // ตรวจสอบสิทธิ์การใช้งาน: ถ้าไม่ Active และไม่ใช่ Trial ให้เข้าโหมดอ่านอย่างเดียว (ดูข้อมูลเก่า + Backup ได้)
        _isReadOnlyMode = _licenseStatus != LicenseStatus.Active;

        InitializeViews(auditService, backupService, exportImportService, posService);
        InitializeLayout();

        Load += async (s, e) =>
        {
            try
            {
                var settings = await _settingsService.GetAllSettingsAsync();
                ThemeManager.ApplyTheme(settings.AppTheme, settings.AppFontSize);
                ApplyAppThemeAndFont();
                await CheckAndPromptFirstTimeActivationAsync();
            }
            catch { }
        };

        ThemeManager.OnThemeChanged += ApplyAppThemeAndFont;

        if (_license != null && _license.LicenseType != LicenseType.Trial && !string.IsNullOrEmpty(_license.UsbHardwareId))
        {
            InitializeDongleTimer();
        }
        else if (_license != null && _license.LicenseType == LicenseType.Trial)
        {
            InitializeTrialDongleTimer();
        }

        FormClosing += async (s, e) =>
        {
            _dongleCheckTimer?.Stop();
            _trialDongleCheckTimer?.Stop();

            try
            {
                var settings = await _settingsService.GetAllSettingsAsync();
                if (settings.AutoBackupEnabled && settings.AutoBackupOnExit)
                {
                    await _backupService.AutoPerformRollingBackupAsync(settings.AutoBackupMaxKeepFiles > 0 ? settings.AutoBackupMaxKeepFiles : 30);
                }
            }
            catch { }
        };
    }

    public void ApplyAppThemeAndFont()
    {
        Font = new Font("Segoe UI", ThemeManager.BaseFontSize, FontStyle.Regular);
        if (_sidebarPanel != null)
        {
            _sidebarPanel.BackColor = ThemeManager.SidebarColor;
        }
        if (_contentPanel != null)
        {
            _contentPanel.BackColor = ThemeManager.BackgroundColor;
        }

        for (int i = 0; i < _navButtons.Count; i++)
        {
            var btn = _navButtons[i];
            btn.Font = new Font("Segoe UI", _isSidebarCollapsed ? ThemeManager.BaseFontSize - 1f : ThemeManager.BaseFontSize, FontStyle.Bold);
            if (btn.BackColor != Color.Transparent)
            {
                btn.BackColor = ThemeManager.PrimaryColor;
            }
        }

        // Apply theme & font size recursively to ALL views across the program
        Control[] views = new Control[]
        {
            _roomGridControl,
            _bookingListControl,
            _roomManagementControl,
            _customerManagementControl,
            _meterReadingControl,
            _backupControl,
            _systemSettingsControl,
            _posControl,
            _summaryReportControl
        };

        foreach (var view in views)
        {
            if (view != null)
            {
                ThemeManager.ApplyThemeToControlTree(view);
            }
        }
    }

    private void InitializeViews(IAuditService auditService, IBackupService backupService, IExportImportService exportImportService, IPOSService posService)
    {
        _roomGridControl = new RoomGridControl(_roomService, _bookingService, _customerService, _settingsService, _utilityBillService) { Dock = DockStyle.Fill };
        _bookingListControl = new BookingListControl(_bookingService, _roomService, _customerService, _settingsService, _utilityBillService) { Dock = DockStyle.Fill };
        _roomManagementControl = new RoomManagementControl(_roomService) { Dock = DockStyle.Fill };
        _roomManagementControl.OnDataChangedAsync += async () => await _roomGridControl.RefreshGridAsync();
        _customerManagementControl = new CustomerManagementControl(_customerService, _bookingService, _roomService, _settingsService, _utilityBillService, posService) { Dock = DockStyle.Fill };
        _meterReadingControl = new MeterReadingControl(_utilityBillService, _roomService, _settingsService, _logger, _bookingService, _customerService) { Dock = DockStyle.Fill };
        _backupControl = new SystemBackupControl(backupService, exportImportService, _settingsService) { Dock = DockStyle.Fill };
        _systemSettingsControl = new SystemSettingsControl(_settingsService, auditService) { Dock = DockStyle.Fill };
        _posControl = new POSControl(posService, _settingsService, _logger, auditService) { Dock = DockStyle.Fill };
        _summaryReportControl = new SummaryReportControl(_utilityBillService, _settingsService) { Dock = DockStyle.Fill };
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
        _contentPanel.BringToFront();

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

    private bool _isSidebarCollapsed = false;
    private Button _btnToggleSidebar = null!;
    private FlowLayoutPanel _navContainer = null!;
    private Panel _brandPanel = null!;
    private Panel _footerPanel = null!;
    private Label _lblBrandTitle = null!;
    private Label _lblBrandSub = null!;
    private Label _lblUser = null!;
    private Button _btnLogout = null!;

    private readonly string[] _fullNavTitles = new[]
    {
        "ผังห้องพัก",
        "รายการจอง",
        "บริการเสริม & มินิบาร์ (POS)",
        "การจัดการห้องพัก",
        "ข้อมูลลูกค้า",
        "ระบบบิลค่าไฟ/ค่าน้ำ",
        "รายงานสรุป",
        "สำรอง/คืนค่าข้อมูล",
        "ตั้งค่าระบบ"
    };

    private readonly string[] _shortNavTitles = new[]
    {
        "ผัง",
        "จอง",
        "POS",
        "ห้อง",
        "ลูกค้า",
        "บิล",
        "รายงาน",
        "สำรอง",
        "ตั้งค่า"
    };

    private void BuildSidebarPanel()
    {
        _sidebarPanel = new Panel
        {
            Dock = DockStyle.Left,
            Width = _isSidebarCollapsed ? 68 : 240,
            BackColor = Color.FromArgb(20, 20, 30), // Deep Luxury Dark Slate
            Padding = new Padding(0)
        };

        // Brand / Logo Section
        _brandPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 85,
            BackColor = Color.FromArgb(16, 16, 24),
            Padding = new Padding(15, 15, 10, 10)
        };

        _lblBrandTitle = new Label
        {
            Text = _isSidebarCollapsed ? "PS" : "PSoft R&R",
            Font = new Font("Segoe UI", 14F, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(12, 14),
            AutoSize = true
        };

        _lblBrandSub = new Label
        {
            Text = "Rest & Rent Manager",
            Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
            ForeColor = Color.FromArgb(148, 163, 184),
            Location = new Point(12, 48),
            AutoSize = true,
            Visible = !_isSidebarCollapsed
        };

        _btnToggleSidebar = new Button
        {
            Text = _isSidebarCollapsed ? ">>" : "<<",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(203, 213, 225),
            BackColor = Color.FromArgb(30, 41, 59),
            FlatStyle = FlatStyle.Flat,
            Size = new Size(36, 32),
            Location = new Point(185, 14),
            Cursor = Cursors.Hand
        };
        _btnToggleSidebar.FlatAppearance.BorderSize = 0;
        _btnToggleSidebar.Click += (s, e) => ToggleSidebarState();

        var divLine = new Panel
        {
            Height = 1,
            Dock = DockStyle.Bottom,
            BackColor = Color.FromArgb(42, 42, 60)
        };

        _brandPanel.Controls.Add(_lblBrandTitle);
        _brandPanel.Controls.Add(_lblBrandSub);
        _brandPanel.Controls.Add(_btnToggleSidebar);
        _brandPanel.Controls.Add(divLine);

        // Navigation Buttons Container
        _navContainer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(8, 15, 8, 10),
            BackColor = Color.Transparent
        };

        _navButtons.Clear();

        var navItems = new (string title, Control control, Func<Task>? onActivate)[]
        {
            (_fullNavTitles[0], _roomGridControl, async () => await _roomGridControl.RefreshGridAsync()),
            (_fullNavTitles[1], _bookingListControl, async () => await _bookingListControl.LoadBookingsAsync()),
            (_fullNavTitles[2], _posControl, null),
            (_fullNavTitles[3], _roomManagementControl, null),
            (_fullNavTitles[4], _customerManagementControl, null),
            (_fullNavTitles[5], _meterReadingControl, async () => await _meterReadingControl.LoadMeterDataAsync()),
            (_fullNavTitles[6], _summaryReportControl, null),
            (_fullNavTitles[7], _backupControl, null),
            (_fullNavTitles[8], _systemSettingsControl, null)
        };

        for (int i = 0; i < navItems.Length; i++)
        {
            var item = navItems[i];
            int index = i;
            var btn = new Button
            {
                Text = _isSidebarCollapsed ? _shortNavTitles[index] : $"   {item.title}",
                TextAlign = _isSidebarCollapsed ? ContentAlignment.MiddleCenter : ContentAlignment.MiddleLeft,
                Size = _isSidebarCollapsed ? new Size(52, 46) : new Size(220, 46),
                Font = new Font("Segoe UI", _isSidebarCollapsed ? 9.5F : 10.5F, FontStyle.Bold),
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
            _navContainer.Controls.Add(btn);
        }

        // Footer / User Section
        _footerPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 95,
            BackColor = Color.FromArgb(16, 16, 24),
            Padding = new Padding(8, 10, 8, 10)
        };

        var userCard = new Panel
        {
            Dock = DockStyle.Top,
            Height = 38,
            BackColor = Color.FromArgb(28, 28, 42)
        };

        _lblUser = new Label
        {
            Text = _isSidebarCollapsed ? "admin" : "ผู้ใช้: admin (ผู้ดูแลระบบ)",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(_isSidebarCollapsed ? 4 : 8, 9),
            AutoSize = true
        };
        userCard.Controls.Add(_lblUser);

        _btnLogout = new Button
        {
            Text = _isSidebarCollapsed ? "ออก" : "ออกจากระบบ",
            Dock = DockStyle.Bottom,
            Height = 32,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Color.FromArgb(185, 28, 28),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        _btnLogout.FlatAppearance.BorderSize = 0;
        _btnLogout.Click += (s, e) => Logout();

        _footerPanel.Controls.Add(userCard);
        _footerPanel.Controls.Add(_btnLogout);

        _sidebarPanel.Controls.Add(_navContainer);
        _sidebarPanel.Controls.Add(_brandPanel);
        _sidebarPanel.Controls.Add(_footerPanel);

        UpdateTogglePos();
    }

    private void UpdateTogglePos()
    {
        if (_isSidebarCollapsed)
        {
            _btnToggleSidebar.Location = new Point(16, 44);
            _btnToggleSidebar.Size = new Size(36, 30);
            _lblBrandTitle.Location = new Point(12, 12);
        }
        else
        {
            _btnToggleSidebar.Location = new Point(185, 14);
            _btnToggleSidebar.Size = new Size(36, 32);
            _lblBrandTitle.Location = new Point(12, 14);
        }
    }

    private void ToggleSidebarState()
    {
        _isSidebarCollapsed = !_isSidebarCollapsed;

        _sidebarPanel.SuspendLayout();
        _sidebarPanel.Width = _isSidebarCollapsed ? 68 : 240;

        _lblBrandTitle.Text = _isSidebarCollapsed ? "PS" : "PSoft R&R";
        _lblBrandSub.Visible = !_isSidebarCollapsed;
        _btnToggleSidebar.Text = _isSidebarCollapsed ? ">>" : "<<";

        _lblUser.Text = _isSidebarCollapsed ? "admin" : "ผู้ใช้: admin (ผู้ดูแลระบบ)";
        _btnLogout.Text = _isSidebarCollapsed ? "ออก" : "ออกจากระบบ";

        UpdateTogglePos();

        for (int i = 0; i < _navButtons.Count; i++)
        {
            var btn = _navButtons[i];
            btn.Size = _isSidebarCollapsed ? new Size(52, 46) : new Size(220, 46);
            btn.TextAlign = _isSidebarCollapsed ? ContentAlignment.MiddleCenter : ContentAlignment.MiddleLeft;
            btn.Text = _isSidebarCollapsed ? _shortNavTitles[i] : $"   {_fullNavTitles[i]}";
            btn.Font = new Font("Segoe UI", _isSidebarCollapsed ? 9.5F : 10.5F, FontStyle.Bold);
        }

        _sidebarPanel.ResumeLayout(true);
    }

    private void SwitchView(Button selectedBtn, Control targetControl)
    {
        foreach (var btn in _navButtons)
        {
            if (btn == selectedBtn)
            {
                btn.BackColor = Color.FromArgb(37, 99, 235); // Electric Blue
                btn.ForeColor = Color.White;
                btn.Font = new Font("Segoe UI", _isSidebarCollapsed ? 9.5F : 10.5F, FontStyle.Bold);
            }
            else
            {
                btn.BackColor = Color.Transparent;
                btn.ForeColor = Color.FromArgb(203, 213, 225);
                btn.Font = new Font("Segoe UI", _isSidebarCollapsed ? 9.5F : 10.5F, FontStyle.Regular);
            }
        }

        if (_activeControl != targetControl)
        {
            _contentPanel.Controls.Clear();
            _contentPanel.Controls.Add(targetControl);
            _activeControl = targetControl;

            // ล็อคฟีเจอร์เมื่ออยู่ในโหมดอ่านอย่างเดียว (ยกเว้น Backup, Audit Log, Settings ที่ยังใช้ได้)
            if (_isReadOnlyMode)
            {
                bool isAllowedInReadOnly = targetControl == _backupControl 
                    || targetControl == _systemSettingsControl
                    || targetControl == _roomGridControl
                    || targetControl == _bookingListControl
                    || targetControl == _customerManagementControl;

                if (!isAllowedInReadOnly)
                {
                    _contentPanel.Controls.Clear();
                    var lockPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(254, 243, 199) };
                    var lockLabel = new Label
                    {
                        Text = "ฟีเจอร์นี้ถูกล็อคเนื่องจากสิทธิ์ใช้งานหมดอายุ/ไม่ถูกต้อง\n\nคุณสามารถดูข้อมูลเก่า, สำรองข้อมูล, และดูประวัติระบบได้ตามปกติ\nกรุณาลงทะเบียนรหัสสิทธิ์หรือเสียบ USB Dongle เพื่อปลดล็อคฟีเจอร์ทั้งหมด",
                        Font = new Font("Segoe UI", 13F, FontStyle.Bold),
                        ForeColor = Color.FromArgb(146, 64, 14),
                        TextAlign = ContentAlignment.MiddleCenter,
                        Dock = DockStyle.Fill
                    };
                    lockPanel.Controls.Add(lockLabel);
                    _contentPanel.Controls.Add(lockPanel);
                    _activeControl = null; // รีเซ็ตเพื่อให้คลิกได้อีกครั้งหลัง activate
                }
            }
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
            Text = $"{shopName} - PSoft Rest & Rent Manager";
            _logger.Info(LogCategory.UI, "โหลดหน้าหลักพร้อมแถบไซด์บาร์สำเร็จ", correlationId);

            // Run auto backup in background
            _ = Task.Run(async () => await RunAutoBackupAsync());
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.UI, "โหลดข้อมูลหน้าหลักไม่สำเร็จ", ex, correlationId);
        }
    }

    private async Task RunAutoBackupAsync()
    {
        var correlationId = _logger.NewCorrelationId();
        try
        {
            var autoEnabled = await _settingsService.GetAsync("backup_auto_enabled");
            if (autoEnabled != "1")
            {
                return;
            }

            _logger.Info(LogCategory.Backup, "เริ่มกระบวนการสำรองข้อมูลอัตโนมัติ (Auto-Backup)", correlationId);

            var backupDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "PSoftRestRentManager",
                "Backups");

            if (!Directory.Exists(backupDir))
            {
                Directory.CreateDirectory(backupDir);
            }

            var timeStamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var targetFilePath = Path.Combine(backupDir, $"PSoftRestRent_AutoBackup_{timeStamp}.db");

            // Perform SQLite Online Backup
            await _backupService.CreateBackupAsync(targetFilePath);
            _logger.Info(LogCategory.Backup, $"สำรองข้อมูลอัตโนมัติสำเร็จที่: {targetFilePath}", correlationId);

            // Clean up old auto backups
            var retentionStr = await _settingsService.GetAsync("backup_retention_days");
            if (!int.TryParse(retentionStr, out int retentionDays))
            {
                retentionDays = 90; // Default retention
            }

            var files = Directory.GetFiles(backupDir, "PSoftRestRent_AutoBackup_*.db");
            var thresholdDate = DateTime.Now.AddDays(-retentionDays);
            int deletedCount = 0;

            foreach (var file in files)
            {
                var fileInfo = new FileInfo(file);
                if (fileInfo.LastWriteTime < thresholdDate)
                {
                    try
                    {
                        File.Delete(file);
                        deletedCount++;
                    }
                    catch (Exception deleteEx)
                    {
                        _logger.Warning(LogCategory.Backup, $"ไม่สามารถลบไฟล์ Backup เก่าได้: {file}. Error: {deleteEx.Message}", correlationId);
                    }
                }
            }

            if (deletedCount > 0)
            {
                _logger.Info(LogCategory.Backup, $"ลบไฟล์สำรองข้อมูลเก่าที่หมดอายุรวม {deletedCount} ไฟล์ (พ้นกำหนด {retentionDays} วัน)", correlationId);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Backup, "เกิดข้อผิดพลาดระหว่างกระบวนการสำรองข้อมูลอัตโนมัติ (Auto-Backup)", ex, correlationId);
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
        _trialDongleCheckTimer.Interval = 3000; // Check every 3 seconds for instant response
        _trialDongleCheckTimer.Tick += TrialDongleCheckTimer_Tick;
        _trialDongleCheckTimer.Start();
    }

    private async void TrialDongleCheckTimer_Tick(object? sender, EventArgs e)
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

                _licenseStatus = LicenseStatus.Active;
                _license = dongleLicense;
                _isReadOnlyMode = false;

                InitializeDongleTimer();

                await CheckAndPromptFirstTimeActivationAsync();
            }
        }
    }

    public async Task CheckAndPromptFirstTimeActivationAsync()
    {
        try
        {
            if (_licenseStatus != LicenseStatus.Active || _license == null || _license.LicenseType == LicenseType.Trial) return;

            var shown = await _settingsService.GetAsync("is_first_activation_prompt_shown");
            if (shown == "1") return;

            // Mark as shown immediately so it happens ONCE only
            await _settingsService.SetAsync("is_first_activation_prompt_shown", "1");

            var result = MessageBox.Show(
                "ยินดีต้อนรับสู่ระบบ PSoft Rest & Rent Manager รุ่นเต็มรูปแบบ (Full Version)!\n\n" +
                "ท่านต้องการตั้งรหัสผ่าน Admin สำหรับผู้ดูแลระบบใหม่ในตอนนี้เลยหรือไม่?\n" +
                "(หากเลือกไม่ตั้ง ระบบจะใช้รหัสผ่านเริ่มต้น: psoft123)",
                "ยินดีต้อนรับสู่ระบบ Full Version",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                ShowSetAdminPasswordDialog();
            }
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.System, "Error checking first time activation prompt", ex);
        }
    }

    private void ShowSetAdminPasswordDialog()
    {
        using (var dlg = new Form())
        {
            dlg.Text = "ตั้งรหัสผ่าน Admin ผู้ดูแลระบบ";
            dlg.Size = new Size(420, 240);
            dlg.StartPosition = FormStartPosition.CenterParent;
            dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
            dlg.MaximizeBox = false;
            dlg.MinimizeBox = false;
            dlg.Font = new Font("Segoe UI", 10F);

            var lblPrompt = new Label
            {
                Text = "กรุณากรอกรหัสผ่าน Admin ใหม่สำหรับเข้าใช้งานระบบ:",
                Location = new Point(20, 15),
                Size = new Size(360, 25),
                Font = new Font("Segoe UI", 10F, FontStyle.Bold)
            };

            var lblPwd1 = new Label { Text = "รหัสผ่านใหม่:", Location = new Point(20, 48), AutoSize = true };
            var txtPwd1 = new TextBox { Location = new Point(140, 45), Width = 235, UseSystemPasswordChar = true };

            var lblPwd2 = new Label { Text = "ยืนยันรหัสผ่าน:", Location = new Point(20, 88), AutoSize = true };
            var txtPwd2 = new TextBox { Location = new Point(140, 85), Width = 235, UseSystemPasswordChar = true };

            var btnOk = new Button
            {
                Text = "บันทึกรหัสผ่าน",
                Location = new Point(150, 135),
                Size = new Size(115, 36),
                DialogResult = DialogResult.OK,
                BackColor = Color.FromArgb(16, 185, 129),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnOk.FlatAppearance.BorderSize = 0;

            var btnSkip = new Button
            {
                Text = "ข้าม (ใช้ psoft123)",
                Location = new Point(275, 135),
                Size = new Size(125, 36),
                DialogResult = DialogResult.Cancel,
                BackColor = Color.FromArgb(226, 232, 240),
                FlatStyle = FlatStyle.Flat
            };
            btnSkip.FlatAppearance.BorderSize = 0;

            dlg.Controls.AddRange(new Control[] { lblPrompt, lblPwd1, txtPwd1, lblPwd2, txtPwd2, btnOk, btnSkip });
            dlg.AcceptButton = btnOk;
            dlg.CancelButton = btnSkip;

            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                string p1 = txtPwd1.Text.Trim();
                string p2 = txtPwd2.Text.Trim();

                if (string.IsNullOrEmpty(p1))
                {
                    MessageBox.Show("รหัสผ่านไม่สามารถเป็นค่าว่างได้ ระบบจะใช้ psoft123", "แจ้งเตือน", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                if (p1 != p2)
                {
                    MessageBox.Show("รหัสผ่านไม่ตรงกัน ระบบจะใช้ psoft123 (ท่านสามารถเปลี่ยนรหัสได้ในหน้าตั้งค่าระบบ)", "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Hash รหัสผ่านด้วย PBKDF2 ก่อนบันทึก (ห้ามเก็บ plaintext)
                string hashedPwd = PasswordHelper.HashPassword(p1);
                _settingsService.SetAsync("admin_password", hashedPwd).GetAwaiter().GetResult();
                _settingsService.SetAsync("is_custom_admin_password_set", "1").GetAwaiter().GetResult();
                MessageBox.Show("บันทึกรหัสผ่าน Admin เรียบร้อยแล้ว (สามารถแก้ไขได้ภายหลังในเมนูตั้งค่าระบบ)", "สำเร็จ", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }

    public async Task NavigateToPOSWithRoomChargeAsync(string roomNumber)
    {
        var posBtn = _navButtons.FirstOrDefault(b => b.Text.Contains("บริการเสริม & มินิบาร์ (POS)"));
        if (posBtn != null)
        {
            SwitchView(posBtn, _posControl);
            await _posControl.LoadInitialDataAsync();
            _posControl.SetRoomCharge(roomNumber);
        }
    }
}
