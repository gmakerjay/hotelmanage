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
    private readonly IAppLogger _logger;

    private LicenseStatus _licenseStatus;
    private LicenseFile? _license;
    private int _daysRemaining;

    private Panel _sidebarPanel = null!;
    private Panel _contentPanel = null!;
    private Panel? _licenseBannerPanel;
    private Label? _licenseBannerLabel;
    private Button? _licenseBannerButton;

    private RoomGridControl _roomGridControl = null!;
    private BookingListControl _bookingListControl = null!;
    private RoomManagementControl _roomManagementControl = null!;
    private CustomerManagementControl _customerManagementControl = null!;
    private AuditLogControl _auditLogControl = null!;
    private SystemBackupControl _backupControl = null!;
    private SystemSettingsControl _systemSettingsControl = null!;

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

        var auditService = new AuditService(auditRepo, _logger);
        _roomService = new RoomService(roomRepo, _logger);
        _customerService = new CustomerService(customerRepo, _logger);
        _bookingService = new BookingService(bookingRepo, roomRepo, customerRepo, folioRepo, _logger);
        var backupService = new BackupService(connectionFactory, auditService, _logger);
        var exportImportService = new ExportImportService(_customerService, _roomService, auditService);

        Text = "โปรแกรมจัดการห้องพัก PSOFT";
        Width = 1280;
        Height = 850;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10.5F, FontStyle.Regular);
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

        InitializeViews(auditService, backupService, exportImportService);
        InitializeLayout();
    }

    private void InitializeViews(IAuditService auditService, IBackupService backupService, IExportImportService exportImportService)
    {
        _roomGridControl = new RoomGridControl(_roomService, _bookingService, _customerService) { Dock = DockStyle.Fill };
        _bookingListControl = new BookingListControl(_bookingService, _roomService, _customerService) { Dock = DockStyle.Fill };
        _roomManagementControl = new RoomManagementControl(_roomService) { Dock = DockStyle.Fill };
        _customerManagementControl = new CustomerManagementControl(_customerService) { Dock = DockStyle.Fill };
        _auditLogControl = new AuditLogControl(auditService) { Dock = DockStyle.Fill };
        _backupControl = new SystemBackupControl(backupService, exportImportService) { Dock = DockStyle.Fill };
        _systemSettingsControl = new SystemSettingsControl(_settingsService) { Dock = DockStyle.Fill };
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
            Text = "PSOFT HOTEL",
            Font = new Font("Segoe UI", 15F, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(18, 14),
            AutoSize = true
        };

        var lblBrandSub = new Label
        {
            Text = "โปรแกรมจัดการห้องพัก PSOFT",
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
            ("การจัดการห้องพัก", _roomManagementControl, null),
            ("ข้อมูลลูกค้า", _customerManagementControl, null),
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
}
