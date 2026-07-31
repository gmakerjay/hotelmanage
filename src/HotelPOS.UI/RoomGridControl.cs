using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using HotelPOS.Common;
using HotelPOS.Common.Models;
using HotelPOS.Core.Services;
using HotelPOS.Printing;

namespace HotelPOS.UI;

/// <summary>
/// ผังห้องพักแสดงผลระดับพรีเมียม กะทัดรัด ประหยัดพื้นที่ เลื่อนลื่นไหล ไร้อาการหน่วงสลับแท็บ (Flicker-Free & Instant Cache Redraw)
/// </summary>
public class RoomGridControl : UserControl
{
    private readonly IRoomService _roomService;
    private readonly IBookingService _bookingService;
    private readonly ICustomerService _customerService;
    private readonly ISettingsService _settingsService;
    private readonly IUtilityBillService? _utilityBillService;

    private Panel _headerPanel = null!;
    private ComboBox _cboFloorFilter = null!;
    private ComboBox _cboTypeFilter = null!;
    private TextBox _txtSearch = null!;
    private Button _btnRefresh = null!;
    private Button _btnNewBooking = null!;

    private Label _lblAvailableCount = null!;
    private Label _lblOccupiedCount = null!;
    private Label _lblCleaningCount = null!;
    private Label _lblReservedCount = null!;
    private Label _lblNearCheckoutCount = null!;
    private Label _lblOverdueCount = null!;

    private FlowLayoutPanel _statusFilterPanel = null!;
    private FlowLayoutPanel _cardsContainer = null!;

    private List<Room> _allRooms = new();
    private List<RoomType> _allRoomTypes = new();

    // Data Caching for 0ms Instant Tab Switch & Lag-Free Redraw
    private Dictionary<int, (Booking Booking, Customer? Customer)> _cachedActiveBookingsMap = new();
    private List<UtilityBill> _cachedUnpaidBills = new();
    private SystemSettingsDto? _cachedSettings = null;
    private bool _isDataLoaded = false;

    private string? _selectedFilterMode = null; // null = ทั้งหมด
    private System.Windows.Forms.Timer _autoRefreshTimer = null!;
    private Label _lblUtilityRates = null!;
    private float _cardFontScale = 1.15F;

    // ----------------------------------------------------
    // Optimization: Shared ContextMenu & Control Caches
    // ----------------------------------------------------
    private ContextMenuStrip _sharedContextMenu = null!;
    private Dictionary<int, Panel> _cardPool = new();
    private Dictionary<string, Panel> _floorHeaderPool = new();
    private Dictionary<string, FlowLayoutPanel> _floorFlowPool = new();

    public RoomGridControl(
        IRoomService roomService,
        IBookingService bookingService,
        ICustomerService customerService,
        ISettingsService settingsService,
        IUtilityBillService? utilityBillService = null)
    {
        _roomService = roomService;
        _bookingService = bookingService;
        _customerService = customerService;
        _settingsService = settingsService;
        _utilityBillService = utilityBillService;

        InitializeUI();
        InitializeSharedContextMenu();
        InitializeTimer();

        ThemeManager.OnThemeChanged += () =>
        {
            UpdateCardFontScale();
            ApplyFilter();
        };

        Load += async (s, e) =>
        {
            UpdateCardFontScale();
            await RefreshGridAsync();
        };
        VisibleChanged += async (s, e) =>
        {
            if (Visible && _isDataLoaded)
            {
                UpdateCardFontScale();
                // Instant draw from cache (0ms lag when switching tabs!)
                ApplyFilter();
                // Silent background update in case data changed
                await LoadDataCachesAsync(silent: true);
            }
        };
    }

    private void UpdateCardFontScale()
    {
        _cardFontScale = ThemeManager.CurrentFontSize switch
        {
            "Standard" => 1.00F,
            "Large" => 1.25F,
            "ExtraLarge" => 1.35F,
            _ => 1.15F
        };
    }

    private void InitializeSharedContextMenu()
    {
        _sharedContextMenu = new ContextMenuStrip { Font = new Font("Segoe UI", 10F) };
    }

    private void InitializeTimer()
    {
        _autoRefreshTimer = new System.Windows.Forms.Timer { Interval = 30000 };
        _autoRefreshTimer.Tick += async (s, e) =>
        {
            if (IsHandleCreated && !IsDisposed && Visible)
            {
                await LoadDataCachesAsync(silent: true);
            }
        };
        _autoRefreshTimer.Start();
    }

    private void InitializeUI()
    {
        Dock = DockStyle.Fill;
        Font = new Font("Segoe UI", 10.5F, FontStyle.Regular);
        BackColor = Color.FromArgb(241, 245, 249);

        // Header Panel - Compact (Height: 82px)
        _headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 82,
            BackColor = Color.White,
            Padding = new Padding(12, 6, 12, 6)
        };

        // Row 1: Title + Badges + Search & Controls
        var pnlRow1 = new Panel
        {
            Dock = DockStyle.Top,
            Height = 36,
            BackColor = Color.Transparent
        };

        var titleLabel = new Label
        {
            Text = "ผังห้องพัก",
            Font = new Font("Segoe UI", 12.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59),
            Location = new Point(0, 4),
            AutoSize = true
        };

        var badgeFlow = new FlowLayoutPanel
        {
            Location = new Point(100, 3),
            Height = 32,
            Width = 480,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent
        };

        _lblAvailableCount = CreateBadgeLabel("ว่าง: 0", Color.FromArgb(236, 253, 245), Color.FromArgb(6, 95, 70));
        _lblOccupiedCount = CreateBadgeLabel("มีคนพัก: 0", Color.FromArgb(254, 226, 226), Color.FromArgb(153, 27, 27));
        _lblCleaningCount = CreateBadgeLabel("รอทำสะอาด: 0", Color.FromArgb(254, 243, 199), Color.FromArgb(146, 64, 14));
        _lblReservedCount = CreateBadgeLabel("จอง: 0", Color.FromArgb(239, 246, 255), Color.FromArgb(30, 58, 138));
        _lblNearCheckoutCount = CreateBadgeLabel("ใกล้ครบ: 0", Color.FromArgb(254, 243, 199), Color.DarkOrange);
        _lblOverdueCount = CreateBadgeLabel("เลยกำหนด: 0", Color.FromArgb(254, 226, 226), Color.DarkRed);

        badgeFlow.Controls.AddRange(new Control[] {
            _lblAvailableCount, _lblOccupiedCount, _lblCleaningCount,
            _lblReservedCount, _lblNearCheckoutCount, _lblOverdueCount
        });

        // Controls Right Side Flow
        var controlsFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Height = 36,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 0, 6, 0)
        };

        var lblSearch = new Label { Text = "ค้นหา:", Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), AutoSize = true, Margin = new Padding(0, 7, 2, 0) };
        _txtSearch = new TextBox
        {
            Width = 100,
            Font = new Font("Segoe UI", 9.5F),
            PlaceholderText = "เลขห้อง / ชื่อ...",
            Margin = new Padding(0, 4, 4, 0)
        };
        _txtSearch.TextChanged += (s, e) => ApplyFilter();

        var lblFloor = new Label { Text = "โซน/ชั้น:", Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), AutoSize = true, Margin = new Padding(2, 7, 2, 0) };
        _cboFloorFilter = new ComboBox { Width = 80, Font = new Font("Segoe UI", 9.5F), DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 4, 4, 0) };
        _cboFloorFilter.SelectedIndexChanged += (s, e) => ApplyFilter();

        var lblType = new Label { Text = "ประเภท:", Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), AutoSize = true, Margin = new Padding(2, 7, 2, 0) };
        _cboTypeFilter = new ComboBox { Width = 80, Font = new Font("Segoe UI", 9.5F), DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 4, 4, 0) };
        _cboTypeFilter.SelectedIndexChanged += (s, e) => ApplyFilter();

        _btnRefresh = new Button
        {
            Text = "รีเฟรช",
            Size = new Size(58, 28),
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 3, 3, 0)
        };
        _btnRefresh.Click += async (s, e) => await RefreshGridAsync();

        _btnNewBooking = new Button
        {
            Text = "จองล่วงหน้า",
            Size = new Size(85, 28),
            BackColor = Color.FromArgb(37, 99, 235),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 3, 0, 0)
        };
        _btnNewBooking.FlatAppearance.BorderSize = 0;
        _btnNewBooking.Click += BtnNewBooking_Click;

        controlsFlow.Controls.AddRange(new Control[] {
            lblSearch, _txtSearch, lblFloor, _cboFloorFilter, lblType, _cboTypeFilter,
            _btnRefresh, _btnNewBooking
        });

        pnlRow1.Controls.Add(titleLabel);
        pnlRow1.Controls.Add(badgeFlow);
        pnlRow1.Controls.Add(controlsFlow);

        // Row 2: Status Pills + Utility Rates Info
        var pnlRow2 = new Panel
        {
            Dock = DockStyle.Top,
            Height = 36,
            Margin = new Padding(0, 4, 0, 0),
            BackColor = Color.Transparent
        };

        _statusFilterPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Left,
            AutoSize = true,
            Height = 34,
            BackColor = Color.Transparent,
            WrapContents = false,
            AutoScroll = false
        };

        _lblUtilityRates = new Label
        {
            Text = "ค่าไฟ: - /หน่วย | ค่าน้ำ: -",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = Color.FromArgb(234, 88, 12),
            Dock = DockStyle.Right,
            AutoSize = true,
            Margin = new Padding(0, 6, 6, 0),
            TextAlign = ContentAlignment.MiddleRight
        };

        BuildStatusFilterButtons();

        pnlRow2.Controls.Add(_statusFilterPanel);
        pnlRow2.Controls.Add(_lblUtilityRates);

        _headerPanel.Controls.Add(pnlRow2);
        _headerPanel.Controls.Add(pnlRow1);

        // Cards Container
        _cardsContainer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(10, 8, 10, 8),
            BackColor = Color.FromArgb(241, 245, 249),
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false
        };
        _cardsContainer.EnableDoubleBuffering();
        _cardsContainer.SizeChanged += (s, e) =>
        {
            int w = _cardsContainer.ClientSize.Width - 30;
            if (w < 200) w = 200;
            foreach (Control ctrl in _cardsContainer.Controls)
            {
                if (ctrl is FlowLayoutPanel or Panel)
                {
                    ctrl.Width = w;
                }
            }
        };

        Controls.Add(_headerPanel);
        Controls.Add(_cardsContainer);
        _cardsContainer.BringToFront();
    }

    private void BuildStatusFilterButtons()
    {
        _statusFilterPanel.Controls.Clear();
        var filters = new (string label, string? modeValue, Color colorTag)[]
        {
            ("ทั้งหมด", null, Color.FromArgb(30, 41, 59)),
            ("ห้องว่าง", "Available", Color.FromArgb(6, 95, 70)),
            ("มีผู้เข้าพัก", "Occupied", Color.FromArgb(30, 58, 138)),
            ("รายเดือน", "Monthly", Color.FromArgb(107, 33, 168)),
            ("รายวัน", "Daily", Color.FromArgb(37, 99, 235)),
            ("รายชั่วโมง", "Hourly", Color.FromArgb(146, 64, 14)),
            ("รอทำความสะอาด", "Cleaning", Color.FromArgb(180, 83, 9)),
            ("จองล่วงหน้า", "Reserved", Color.FromArgb(91, 33, 182)),
            ("ปิดซ่อม", "Maintenance", Color.FromArgb(71, 85, 105))
        };

        foreach (var item in filters)
        {
            var btn = new Button
            {
                Text = item.label,
                AutoSize = true,
                Height = 28,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                Margin = new Padding(0, 2, 5, 0),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 1;

            bool isSelected = _selectedFilterMode == item.modeValue;
            if (isSelected)
            {
                btn.BackColor = item.colorTag;
                btn.ForeColor = Color.White;
                btn.FlatAppearance.BorderColor = item.colorTag;
            }
            else
            {
                btn.BackColor = Color.White;
                btn.ForeColor = item.colorTag;
                btn.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            }

            btn.Click += (s, e) =>
            {
                _selectedFilterMode = item.modeValue;
                BuildStatusFilterButtons();
                ApplyFilter();
            };

            _statusFilterPanel.Controls.Add(btn);
        }
    }

    private static Label CreateBadgeLabel(string text, Color backColor, Color foreColor)
    {
        return new Label
        {
            Text = text,
            BackColor = backColor,
            ForeColor = foreColor,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            AutoSize = true,
            Padding = new Padding(5, 2, 5, 2),
            Margin = new Padding(2, 2, 2, 2),
            BorderStyle = BorderStyle.FixedSingle
        };
    }

    public async Task RefreshGridAsync()
    {
        try
        {
            _allRooms = (await _roomService.GetRoomsAsync()).ToList();
            _allRoomTypes = (await _roomService.GetRoomTypesAsync(true)).ToList();

            var floors = await _roomService.GetFloorsAsync();
            _cboFloorFilter.Items.Clear();
            _cboFloorFilter.Items.Add("ทุกชั้น");
            foreach (var f in floors)
            {
                _cboFloorFilter.Items.Add($"ชั้น {f}");
            }
            _cboFloorFilter.SelectedIndex = 0;

            _cboTypeFilter.Items.Clear();
            _cboTypeFilter.Items.Add("ทุกประเภท");
            foreach (var t in _allRoomTypes)
            {
                _cboTypeFilter.Items.Add(t.Name);
            }
            _cboTypeFilter.SelectedIndex = 0;

            await LoadDataCachesAsync(silent: false);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"โหลดข้อมูลผังห้องพักไม่สำเร็จ: {ex.Message}", "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task LoadDataCachesAsync(bool silent = false)
    {
        try
        {
            _cachedSettings = await _settingsService.GetAllSettingsAsync();
            _cachedActiveBookingsMap = await _bookingService.GetAllActiveBookingsWithCustomersAsync();

            var now = DateTime.Now;
            if (_utilityBillService != null)
            {
                var unpaid = await _utilityBillService.GetAllUnpaidBillsAsync();
                _cachedUnpaidBills = unpaid.ToList();
            }

            _isDataLoaded = true;
            ApplyFilter();
        }
        catch (Exception ex)
        {
            if (!silent)
            {
                MessageBox.Show($"โหลดข้อมูลไม่สำเร็จ: {ex.Message}", "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void ApplyFilter()
    {
        if (_cachedSettings != null)
        {
            _lblUtilityRates.Text = $"ค่าไฟ: {_cachedSettings.ElectricRatePerUnit:N2} บ./หน่วย | ค่าน้ำ: " +
                (_cachedSettings.WaterBillingMode == "METER" ? $"{_cachedSettings.WaterRatePerUnit:N2} บ./หน่วย" : $"เหมาจ่าย {_cachedSettings.WaterFlatRatePerPerson:N2} บ./คน");
        }
        else
        {
            _lblUtilityRates.Text = "ค่าไฟ: - บ./หน่วย | ค่าน้ำ: -";
        }

        _cardsContainer.SuspendLayout();
        _cardsContainer.Controls.Clear();

        string query = _txtSearch.Text.Trim();

        string? selectedFloor = _cboFloorFilter.SelectedIndex > 0
            ? _cboFloorFilter.SelectedItem?.ToString()?.Replace("ชั้น ", "")
            : null;

        int? selectedTypeId = null;
        if (_cboTypeFilter.SelectedIndex > 0 && _cboTypeFilter.SelectedIndex - 1 < _allRoomTypes.Count)
        {
            selectedTypeId = _allRoomTypes[_cboTypeFilter.SelectedIndex - 1].Id;
        }

        int avail = _allRooms.Count(r => r.Status == RoomStatus.Available);
        int occ = _allRooms.Count(r => r.Status == RoomStatus.Occupied);
        int clean = _allRooms.Count(r => r.Status == RoomStatus.Cleaning);
        int res = _allRooms.Count(r => r.Status == RoomStatus.Reserved);
        int nearCheckoutCount = 0;
        int overdueCount = 0;

        var now = DateTime.Now;

        var roomsToDisplay = new List<(Room Room, RoomType? Type, Booking? Booking, Customer? Customer, bool IsUtilityOverdue, bool IsUtilityDueSoon, int OverdueDays, int DaysLeft, decimal TotalUnpaid)>();

        foreach (var room in _allRooms)
        {
            var roomType = _allRoomTypes.FirstOrDefault(t => t.Id == room.RoomTypeId);
            Booking? booking = null;
            Customer? customer = null;

            bool isUtilityOverdue = false;
            bool isUtilityDueSoon = false;
            int overdueDays = 0;
            int daysLeft = 0;
            decimal totalUnpaid = 0;

            if (room.Status == RoomStatus.Occupied || room.Status == RoomStatus.Reserved)
            {
                if (_cachedActiveBookingsMap.TryGetValue(room.Id, out var entry))
                {
                    booking = entry.Booking;
                    customer = entry.Customer;
                }

                if (room.Status == RoomStatus.Occupied && booking?.CheckOutPlanned.HasValue == true && booking.RatePlan != RatePlanType.Monthly)
                {
                    var span = booking.CheckOutPlanned.Value - now;
                    if (span.TotalMinutes <= 0) overdueCount++;
                    else if (span.TotalMinutes <= 30) nearCheckoutCount++;
                }

                if (room.Status == RoomStatus.Occupied && (booking?.RatePlan == RatePlanType.Monthly || (roomType != null && roomType.MonthlyRate > 0)))
                {
                    var roomUnpaidBills = _cachedUnpaidBills.Where(b => b.RoomId == room.Id).OrderBy(b => b.CreatedAt).ToList();
                    if (roomUnpaidBills.Any())
                    {
                        totalUnpaid = roomUnpaidBills.Sum(b => b.TotalAmount);
                        var oldestBill = roomUnpaidBills.First();
                        var dueDate = oldestBill.CreatedAt.Date.AddDays(5);
                        int daysDiff = (now.Date - dueDate).Days;

                        if (daysDiff > 0)
                        {
                            isUtilityOverdue = true;
                            overdueDays = daysDiff;
                            overdueCount++;
                        }
                        else
                        {
                            isUtilityDueSoon = true;
                            daysLeft = Math.Abs(daysDiff);
                            nearCheckoutCount++;
                        }
                    }
                }
            }

            if (selectedFloor != null && room.Floor != selectedFloor) continue;
            if (selectedTypeId != null && room.RoomTypeId != selectedTypeId) continue;

            if (_selectedFilterMode != null)
            {
                if (_selectedFilterMode == "Available" && room.Status != RoomStatus.Available) continue;
                if (_selectedFilterMode == "Occupied" && room.Status != RoomStatus.Occupied) continue;
                if (_selectedFilterMode == "Cleaning" && room.Status != RoomStatus.Cleaning) continue;
                if (_selectedFilterMode == "Reserved" && room.Status != RoomStatus.Reserved) continue;
                if (_selectedFilterMode == "Maintenance" && room.Status != RoomStatus.Maintenance) continue;

                if (_selectedFilterMode == "Monthly" && (roomType == null || roomType.MonthlyRate == 0)) continue;
                if (_selectedFilterMode == "Hourly" && (roomType == null || roomType.HourlyRate == 0)) continue;
                if (_selectedFilterMode == "Daily" && (roomType == null || roomType.DailyRate == 0)) continue;
            }

            if (!string.IsNullOrWhiteSpace(query))
            {
                bool matchRoom = room.RoomNumber.Contains(query, StringComparison.OrdinalIgnoreCase);
                bool matchCust = customer != null && (customer.FullName.Contains(query, StringComparison.OrdinalIgnoreCase) || (!string.IsNullOrEmpty(customer.Phone) && customer.Phone.Contains(query, StringComparison.OrdinalIgnoreCase)));
                bool matchType = roomType != null && roomType.Name.Contains(query, StringComparison.OrdinalIgnoreCase);

                if (!matchRoom && !matchCust && !matchType) continue;
            }

            roomsToDisplay.Add((room, roomType, booking, customer, isUtilityOverdue, isUtilityDueSoon, overdueDays, daysLeft, totalUnpaid));
        }

        var grouped = roomsToDisplay
            .GroupBy(x => x.Room.Floor ?? "1")
            .OrderBy(x => x.Key);

        int containerWidth = _cardsContainer.ClientSize.Width - 30;
        if (containerWidth < 200) containerWidth = 200;

        foreach (var group in grouped)
        {
            var floorName = group.Key;

            if (!_floorHeaderPool.TryGetValue(floorName, out var header))
            {
                header = new Panel
                {
                    Height = 36,
                    Margin = new Padding(0, 8, 0, 6),
                    BackColor = Color.FromArgb(30, 41, 59)
                };

                var leftStrip = new Panel
                {
                    Dock = DockStyle.Left,
                    Width = 6,
                    BackColor = Color.FromArgb(14, 116, 144)
                };

                var lblFloorHeader = new Label
                {
                    Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                    ForeColor = Color.White,
                    Location = new Point(14, 6),
                    AutoSize = true
                };

                var bottomBorder = new Panel
                {
                    Dock = DockStyle.Bottom,
                    Height = 3,
                    BackColor = Color.FromArgb(14, 116, 144)
                };

                header.Controls.Add(lblFloorHeader);
                header.Controls.Add(leftStrip);
                header.Controls.Add(bottomBorder);
                _floorHeaderPool[floorName] = header;
            }

            int totalInFloor = group.Count();
            int availInFloor = group.Count(x => x.Room.Status == RoomStatus.Available);
            int occInFloor = group.Count(x => x.Room.Status == RoomStatus.Occupied);

            var lblHeader = (Label)header.Controls[0];
            lblHeader.Text = $"ชั้น {floorName}  —  รวม {totalInFloor} ห้อง (ว่าง {availInFloor} | มีผู้พัก {occInFloor})";
            header.Width = containerWidth;

            if (!_floorFlowPool.TryGetValue(floorName, out var floorFlow))
            {
                floorFlow = new FlowLayoutPanel
                {
                    FlowDirection = FlowDirection.LeftToRight,
                    WrapContents = true,
                    AutoSize = true,
                    BackColor = Color.Transparent,
                    Padding = new Padding(0),
                    Margin = new Padding(0, 0, 0, 12)
                };
                _floorFlowPool[floorName] = floorFlow;
            }
            floorFlow.Width = containerWidth;
            floorFlow.SuspendLayout();
            floorFlow.Controls.Clear();

            foreach (var item in group.OrderBy(x => x.Room.RoomNumber))
            {
                var card = UpdateOrCreateRoomTileCard(item.Room, item.Type, item.Booking, item.Customer, now, item.IsUtilityOverdue, item.IsUtilityDueSoon, item.OverdueDays, item.DaysLeft, item.TotalUnpaid);
                floorFlow.Controls.Add(card);
            }
            floorFlow.ResumeLayout();

            _cardsContainer.Controls.Add(header);
            _cardsContainer.Controls.Add(floorFlow);
        }

        if (roomsToDisplay.Count == 0)
        {
            var lblEmpty = new Label
            {
                Text = "ไม่พบห้องพักตามเงื่อนไขที่ระบุ",
                Font = new Font("Segoe UI", 12F, FontStyle.Italic),
                ForeColor = Color.Gray,
                Location = new Point(20, 20),
                AutoSize = true
            };
            _cardsContainer.Controls.Add(lblEmpty);
        }

        _lblAvailableCount.Text = $"ว่าง: {avail}";
        _lblOccupiedCount.Text = $"มีคนพัก: {occ}";
        _lblCleaningCount.Text = $"รอทำสะอาด: {clean}";
        _lblReservedCount.Text = $"จอง: {res}";
        _lblNearCheckoutCount.Text = $"ใกล้ครบ: {nearCheckoutCount}";
        _lblOverdueCount.Text = $"เลยกำหนด: {overdueCount}";

        _cardsContainer.ResumeLayout(true);
    }

    private class RoomCardElements
    {
        public Room Room { get; set; } = null!;
        public RoomType? RoomType { get; set; }
        public Panel Card { get; set; } = null!;
        public Panel TopHeader { get; set; } = null!;
        public Label LblRoomNumHeader { get; set; } = null!;
        public Label LblStatusPill { get; set; } = null!;
        public Label LblRateBadge { get; set; } = null!;
        public Label LblGuest { get; set; } = null!;
        public Label LblTimeAlert { get; set; } = null!;
        public Button BtnMeter { get; set; } = null!;
    }

    private Control UpdateOrCreateRoomTileCard(
        Room room,
        RoomType? roomType,
        Booking? booking,
        Customer? customer,
        DateTime now,
        bool isUtilityOverdue = false,
        bool isUtilityDueSoon = false,
        int overdueDays = 0,
        int daysLeft = 0,
        decimal totalUnpaid = 0)
    {
        if (!_cardPool.TryGetValue(room.Id, out var card))
        {
            card = new Panel
            {
                Size = new Size(245, 168),
                Margin = new Padding(6),
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(0),
                Cursor = Cursors.Hand
            };

            var topHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 34,
                Padding = new Padding(8, 4, 8, 4)
            };

            var lblRoomNumHeader = new Label
            {
                Font = new Font("Segoe UI", 13.5F, FontStyle.Bold),
                ForeColor = Color.White,
                Dock = DockStyle.Left,
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleLeft
            };

            var lblStatusPill = new Label
            {
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.White,
                Dock = DockStyle.Right,
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleRight
            };

            topHeader.Controls.Add(lblRoomNumHeader);
            topHeader.Controls.Add(lblStatusPill);

            var lblRateBadge = new Label
            {
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                Location = new Point(8, 42),
                AutoSize = true,
                Padding = new Padding(6, 2, 6, 2),
                BorderStyle = BorderStyle.FixedSingle
            };

            var lblGuest = new Label
            {
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                Location = new Point(8, 74),
                Size = new Size(228, 22),
                AutoEllipsis = true
            };

            var lblTimeAlert = new Label
            {
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Location = new Point(8, 98),
                Size = new Size(228, 22),
                AutoEllipsis = true
            };

            var btnMeter = new Button
            {
                Text = "จอง/อ่านมิเตอร์",
                Location = new Point(8, 126),
                Size = new Size(228, 32),
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                BackColor = Color.FromArgb(14, 116, 144),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Visible = false
            };
            btnMeter.FlatAppearance.BorderSize = 0;

            card.Controls.Add(topHeader);
            card.Controls.Add(lblRateBadge);
            card.Controls.Add(lblGuest);
            card.Controls.Add(lblTimeAlert);
            card.Controls.Add(btnMeter);

            var elements = new RoomCardElements
            {
                Room = room,
                RoomType = roomType,
                Card = card,
                TopHeader = topHeader,
                LblRoomNumHeader = lblRoomNumHeader,
                LblStatusPill = lblStatusPill,
                LblRateBadge = lblRateBadge,
                LblGuest = lblGuest,
                LblTimeAlert = lblTimeAlert,
                BtnMeter = btnMeter
            };

            card.Tag = elements;

            // WinForms ไม่ bubble MouseClick จาก child ไป parent
            // ต้อง attach click handler ให้ทุก control บนการ์ด (Label, Panel ส่วนหัว ฯลฯ)
            void AttachClickToAllChildren(Control parent)
            {
                parent.MouseClick += async (s, e) =>
                {
                    if (e.Button == MouseButtons.Right || e.Button == MouseButtons.Left)
                    {
                        await OpenRoomActionDialogAsync((RoomCardElements)card.Tag);
                    }
                };
                foreach (Control child in parent.Controls)
                {
                    AttachClickToAllChildren(child);
                }
            }
            AttachClickToAllChildren(card);

            _cardPool[room.Id] = card;
        }

        var elems = (RoomCardElements)card.Tag!;
        elems.Room = room;
        elems.RoomType = roomType;

        Color headerColor;
        Color backColor;
        Color textColor;
        string statusText = "";

        bool isCheckoutOverdue = false;
        bool isNearCheckout = false;
        double minutesDiff = 0;

        if (room.Status == RoomStatus.Occupied && booking?.CheckOutPlanned.HasValue == true && booking.RatePlan != RatePlanType.Monthly)
        {
            var span = booking.CheckOutPlanned.Value - now;
            minutesDiff = span.TotalMinutes;
            if (minutesDiff <= 0) isCheckoutOverdue = true;
            else if (minutesDiff <= 30) isNearCheckout = true;
        }

        switch (room.Status)
        {
            case RoomStatus.Available:
                headerColor = Color.FromArgb(16, 185, 129); // Emerald Green
                backColor = Color.FromArgb(240, 253, 244);
                textColor = Color.FromArgb(6, 95, 70);
                statusText = "ว่างพร้อมใช้งาน";
                break;
            case RoomStatus.Occupied:
                if (isCheckoutOverdue || isUtilityOverdue)
                {
                    headerColor = Color.FromArgb(220, 38, 38); // Crimson Red (Alert)
                    backColor = Color.FromArgb(254, 242, 242);
                    textColor = Color.FromArgb(185, 28, 28);
                    statusText = isUtilityOverdue ? "เลยกำหนดค่าน้ำไฟ!" : "เลยกำหนดเวลาคืนห้อง!";
                }
                else if (isNearCheckout || isUtilityDueSoon)
                {
                    headerColor = Color.FromArgb(217, 119, 6); // Amber Gold (Warning)
                    backColor = Color.FromArgb(254, 243, 199);
                    textColor = Color.FromArgb(180, 83, 9);
                    statusText = isUtilityDueSoon ? "ใกล้กำหนดจ่ายค่าน้ำไฟ" : "ใกล้ครบเวลาคืนห้อง";
                }
                else
                {
                    headerColor = Color.FromArgb(37, 99, 235); // Royal Blue (Normal Occupied)
                    backColor = Color.FromArgb(239, 246, 255);
                    textColor = Color.FromArgb(30, 58, 138);
                    statusText = "มีผู้เข้าพัก (ปกติ)";
                }
                break;
            case RoomStatus.Cleaning:
                headerColor = Color.FromArgb(245, 158, 11); // Amber
                backColor = Color.FromArgb(254, 252, 232);
                textColor = Color.FromArgb(146, 64, 14);
                statusText = "รอทำความสะอาด";
                break;
            case RoomStatus.Reserved:
                headerColor = Color.FromArgb(139, 92, 246); // Purple
                backColor = Color.FromArgb(245, 243, 255);
                textColor = Color.FromArgb(91, 33, 182);
                statusText = "จองแล้ว";
                break;
            case RoomStatus.Maintenance:
            default:
                headerColor = Color.FromArgb(100, 116, 139); // Slate Gray
                backColor = Color.FromArgb(248, 250, 252);
                textColor = Color.FromArgb(51, 65, 85);
                statusText = "ปิดซ่อมบำรุง";
                break;
        }

        elems.Card.BackColor = backColor;
        elems.TopHeader.BackColor = headerColor;
        elems.LblRoomNumHeader.Text = $"ห้อง {room.RoomNumber}";
        elems.LblStatusPill.Text = statusText;

        string rateBadgeText;
        Color rateBadgeBg;
        Color rateBadgeFg;

        if (roomType != null && !string.IsNullOrEmpty(roomType.ColorHex))
        {
            try
            {
                rateBadgeBg = ColorTranslator.FromHtml(roomType.ColorHex);
                rateBadgeFg = Color.White;
            }
            catch
            {
                rateBadgeBg = Color.FromArgb(236, 253, 245);
                rateBadgeFg = Color.FromArgb(6, 95, 70);
            }
        }
        else
        {
            rateBadgeBg = Color.FromArgb(236, 253, 245);
            rateBadgeFg = Color.FromArgb(6, 95, 70);
        }

        string priceInfo = "";
        if (roomType != null)
        {
            if (roomType.DailyRate > 0) priceInfo = $"{roomType.DailyRate:N0} บ./วัน";
            else if (roomType.MonthlyRate > 0) priceInfo = $"{roomType.MonthlyRate:N0} บ./เดือน";
            else if (roomType.HourlyRate > 0) priceInfo = $"{roomType.HourlyRate:N0} บ./ชม.";
        }

        rateBadgeText = $"{roomType?.Name ?? "Standard"}{(string.IsNullOrEmpty(priceInfo) ? "" : $" ({priceInfo})")}";

        elems.LblRateBadge.Text = rateBadgeText;
        elems.LblRateBadge.BackColor = rateBadgeBg;
        elems.LblRateBadge.ForeColor = rateBadgeFg;

        string guestDetailText;
        if (room.Status == RoomStatus.Occupied && customer != null)
        {
            string planText = booking?.RatePlan == RatePlanType.Daily ? "รายวัน" : (booking?.RatePlan == RatePlanType.Hourly ? "รายชั่วโมง" : "รายเดือน");
            guestDetailText = $"ผู้พัก: {customer.FullName} ({planText})";
        }
        else if (room.Status == RoomStatus.Reserved && customer != null)
        {
            string planText = booking?.RatePlan == RatePlanType.Daily ? "รายวัน" : (booking?.RatePlan == RatePlanType.Hourly ? "รายชั่วโมง" : "รายเดือน");
            guestDetailText = $"ผู้จอง: {customer.FullName} ({planText})";
        }
        else if (room.Status == RoomStatus.Cleaning)
        {
            guestDetailText = "สถานะ: รอแม่บ้านทำความสะอาด";
        }
        else if (room.Status == RoomStatus.Available)
        {
            guestDetailText = "สถานะ: ห้องว่าง พร้อมเข้าพัก";
        }
        else
        {
            guestDetailText = "สถานะ: ปิดปรับปรุงซ่อมบำรุง";
        }

        elems.LblGuest.Text = guestDetailText;
        elems.LblGuest.ForeColor = textColor;

        string timeAlertText = "";
        Color alertColor = textColor;

        if (room.Status == RoomStatus.Occupied)
        {
            if (isUtilityOverdue)
            {
                timeAlertText = $"ค้างชำระ: {totalUnpaid:N2} บ. (เลย {overdueDays} วัน)";
                alertColor = Color.FromArgb(185, 28, 28);
            }
            else if (isUtilityDueSoon)
            {
                timeAlertText = $"ค่าน้ำไฟครบกำหนดใน {daysLeft} วัน ({totalUnpaid:N2} บ.)";
                alertColor = Color.DarkGoldenrod;
            }
            else if (booking?.CheckOutPlanned.HasValue == true && booking.RatePlan != RatePlanType.Monthly)
            {
                if (isCheckoutOverdue)
                {
                    timeAlertText = $"เลยกำหนด {Math.Abs((int)minutesDiff)} นาที ({booking.CheckOutPlanned.Value:HH:mm} น.)";
                    alertColor = Color.DarkRed;
                }
                else if (isNearCheckout)
                {
                    timeAlertText = $"เหลืออีก {Math.Ceiling(minutesDiff)} นาที ({booking.CheckOutPlanned.Value:HH:mm} น.)";
                    alertColor = Color.DarkOrange;
                }
                else
                {
                    timeAlertText = $"กำหนดออก: {booking.CheckOutPlanned.Value:dd/MM HH:mm} น.";
                }
            }
            else if (booking?.RatePlan == RatePlanType.Monthly)
            {
                timeAlertText = "ชำระค่าน้ำไฟเรียบร้อยแล้ว";
            }
        }
        else if (room.Status == RoomStatus.Reserved && booking != null)
        {
            timeAlertText = $"กำหนดเช็คอิน: {booking.CheckInPlanned:dd/MM HH:mm} น.";
        }

        elems.LblTimeAlert.Text = timeAlertText;
        elems.LblTimeAlert.ForeColor = alertColor;
        elems.LblTimeAlert.Font = new Font("Segoe UI", 10.5F, (isCheckoutOverdue || isUtilityOverdue || isNearCheckout || isUtilityDueSoon) ? FontStyle.Bold : FontStyle.Regular);

        if (room.Status == RoomStatus.Occupied && roomType != null && 
            (roomType.ElectricBillingMode == UtilityBillingMode.Meter || roomType.WaterBillingMode == UtilityBillingMode.Meter))
        {
            elems.BtnMeter.Visible = true;
            // Clear old click handlers
            elems.BtnMeter.Click -= BtnMeter_Click;
            elems.BtnMeter.Click += BtnMeter_Click;
            elems.BtnMeter.Tag = room.Id;
        }
        else
        {
            elems.BtnMeter.Visible = false;
        }

        return card;
    }

    private async void BtnMeter_Click(object? sender, EventArgs e)
    {
        if (sender is Button btn && btn.Tag is int roomId)
        {
            var room = _allRooms.FirstOrDefault(r => r.Id == roomId);
            if (room != null)
            {
                await OpenMeterReadingDialogFromRoomGridAsync(room);
            }
        }
    }

    private void ShowSharedContextMenu(RoomCardElements elems, Control card, Point location)
    {
        var room = elems.Room;
        var roomType = elems.RoomType;

        _sharedContextMenu.Items.Clear();

        if (room.Status == RoomStatus.Available)
        {
            var itemCheckIn = _sharedContextMenu.Items.Add("เช็คอินทันที (Walk-In)");
            itemCheckIn.Click += async (s, e) => await OpenCheckInDialogAsync(room, roomType);

            var itemReserve = _sharedContextMenu.Items.Add("จองห้องพักล่วงหน้า");
            itemReserve.Click += async (s, e) => await OpenBookingDialogAsync(room);

            var itemMaint = _sharedContextMenu.Items.Add("ปิดซ่อมบำรุง");
            itemMaint.Click += async (s, e) =>
            {
                await _roomService.UpdateRoomStatusAsync(room.Id, RoomStatus.Maintenance, "ปิดซ่อมบำรุง");
                await RefreshGridAsync();
            };
        }
        if (room.Status == RoomStatus.Occupied)
        {
            var itemMeter = _sharedContextMenu.Items.Add("จดมิเตอร์น้ำ-ไฟ");
            itemMeter.Click += async (s, e) => await OpenMeterReadingDialogFromRoomGridAsync(room);

            var itemCheckOut = _sharedContextMenu.Items.Add("คืนห้องพัก / เช็คเอาท์ & ออกบิล");
            itemCheckOut.Click += async (s, e) => await OpenCheckOutDialogAsync(room);

            var itemMinibar = _sharedContextMenu.Items.Add("สั่งมินิบาร์ / สั่งสินค้า (POS)");
            itemMinibar.Click += async (s, e) =>
            {
                var mainForm = this.FindForm() as MainForm;
                if (mainForm != null)
                {
                    await mainForm.NavigateToPOSWithRoomChargeAsync(room.RoomNumber);
                }
            };
        }
        if (room.Status == RoomStatus.Cleaning)
        {
            var itemCleanDone = _sharedContextMenu.Items.Add("ทำความสะอาดเสร็จแล้ว");
            itemCleanDone.Click += async (s, e) =>
            {
                await _roomService.UpdateRoomStatusAsync(room.Id, RoomStatus.Available);
                await RefreshGridAsync();
            };
        }
        if (room.Status == RoomStatus.Reserved)
        {
            var itemCheckInReserved = _sharedContextMenu.Items.Add("เช็คอิน (จากการจอง)");
            itemCheckInReserved.Click += async (s, e) => await CheckInReservedBookingAsync(room);

            var itemCancelReserve = _sharedContextMenu.Items.Add("ยกเลิกการจอง");
            itemCancelReserve.Click += async (s, e) => await CancelReservedBookingAsync(room);
        }
        if (room.Status == RoomStatus.Maintenance)
        {
            var itemMaintDone = _sharedContextMenu.Items.Add("ซ่อมเสร็จแล้ว (พร้อมใช้งาน)");
            itemMaintDone.Click += async (s, e) =>
            {
                await _roomService.UpdateRoomStatusAsync(room.Id, RoomStatus.Available);
                await RefreshGridAsync();
            };
        }

        if (_sharedContextMenu.Items.Count > 0)
        {
            _sharedContextMenu.Show(card, location);
        }
    }

    private async Task OpenRoomActionDialogAsync(RoomCardElements elems)
    {
        var room = elems.Room;
        var roomType = elems.RoomType;

        Booking? booking = null;
        Customer? customer = null;
        if (_cachedActiveBookingsMap.TryGetValue(room.Id, out var entry))
        {
            booking = entry.Booking;
            customer = entry.Customer;
        }

        bool isUtilityOverdue = false;
        bool isUtilityDueSoon = false;
        decimal totalUnpaid = 0;

        var unpaidBill = _cachedUnpaidBills.FirstOrDefault(b => b.RoomId == room.Id);
        if (unpaidBill != null)
        {
            isUtilityOverdue = true;
            totalUnpaid = unpaidBill.TotalAmount;
        }

        using var dlg = new RoomActionForm(room, roomType, booking, customer, isUtilityOverdue, isUtilityDueSoon, totalUnpaid);
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            switch (dlg.SelectedAction)
            {
                case RoomUserAction.CheckInWalkIn:
                    await OpenCheckInDialogAsync(room, roomType);
                    break;
                case RoomUserAction.Reserve:
                    await OpenBookingDialogAsync(room);
                    break;
                case RoomUserAction.CheckOut:
                    await OpenCheckOutDialogAsync(room);
                    break;
                case RoomUserAction.RecordMeter:
                    await OpenMeterReadingDialogFromRoomGridAsync(room);
                    break;
                case RoomUserAction.POS:
                    var mainForm = this.FindForm() as MainForm;
                    if (mainForm != null)
                    {
                        await mainForm.NavigateToPOSWithRoomChargeAsync(room.RoomNumber);
                    }
                    break;
                case RoomUserAction.CheckInReserved:
                    await CheckInReservedBookingAsync(room);
                    break;
                case RoomUserAction.CancelReserved:
                    await CancelReservedBookingAsync(room);
                    break;
                case RoomUserAction.CleaningDone:
                    await _roomService.UpdateRoomStatusAsync(room.Id, RoomStatus.Available);
                    await RefreshGridAsync();
                    break;
                case RoomUserAction.MaintenanceStart:
                    await _roomService.UpdateRoomStatusAsync(room.Id, RoomStatus.Maintenance, "ปิดซ่อมบำรุง");
                    await RefreshGridAsync();
                    break;
                case RoomUserAction.MaintenanceDone:
                    await _roomService.UpdateRoomStatusAsync(room.Id, RoomStatus.Available);
                    await RefreshGridAsync();
                    break;
                case RoomUserAction.AdminOverrideStatus:
                    await PromptAdminOverrideRoomStatusAsync(room);
                    break;
                case RoomUserAction.PayUtilityNow:
                    if (_utilityBillService != null)
                    {
                        var unpaidBillForRoom = _cachedUnpaidBills.FirstOrDefault(b => b.RoomId == room.Id);
                        if (unpaidBillForRoom != null)
                        {
                            var confirmMsg =
                                $"=== ทวนรายการรับชำระเงิน | ห้อง {room.RoomNumber} ===\n\n" +
                                $"• ผู้เช่า: {(customer?.FullName ?? "-")}\n" +
                                $"• ค่าเช่าห้องพัก: {unpaidBillForRoom.RoomCharge:N2} บาท\n" +
                                $"• ค่าไฟฟ้า: {unpaidBillForRoom.ElectricAmount:N2} บาท ({(unpaidBillForRoom.ElectricBillingMode == "FLAT" ? "เหมาจ่าย" : $"{unpaidBillForRoom.ElectricUnits:N0} หน่วย")})\n" +
                                $"• ค่าน้ำประปา: {unpaidBillForRoom.WaterAmount:N2} บาท ({(unpaidBillForRoom.WaterBillingMode == "FLAT" ? $"เหมาจ่าย {unpaidBillForRoom.WaterPersonCount} คน" : $"{unpaidBillForRoom.WaterUnits:N0} หน่วย")})\n" +
                                $"• ค่าส่วนกลาง/ขยะ: {unpaidBillForRoom.CommonAreaFee + unpaidBillForRoom.GarbageFee:N2} บาท\n" +
                                $"----------------------------------------\n" +
                                $"ยอดสุทธิที่ต้องรับชำระ = {unpaidBillForRoom.TotalAmount:N2} บาท\n\n" +
                                $"กด [Yes] เพื่อยืนยันบันทึกรับชำระเงินทันที";

                            if (MessageBox.Show(confirmMsg, "ยืนยันรับชำระเงิน", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                            {
                                await _utilityBillService.MarkBillAsPaidAsync(unpaidBillForRoom.Id, PaymentMethod.Cash);
                                unpaidBillForRoom.IsPaid = true;

                                var printConfirm = MessageBox.Show(
                                    "บันทึกรับชำระเงินสำเร็จเรียบร้อยแล้ว!\n\nต้องการพิมพ์ [ใบเสร็จรับเงิน] ทันทีหรือไม่?",
                                    "พิมพ์ใบเสร็จรับเงิน", MessageBoxButtons.YesNo, MessageBoxIcon.Information);

                                if (printConfirm == DialogResult.Yes)
                                {
                                    var settings = _settingsService != null ? await _settingsService.GetAllSettingsAsync() : new SystemSettingsDto();
                                    var receiptPrinter = new UtilityInvoicePrinter(unpaidBillForRoom, customer, settings);
                                    receiptPrinter.ShowPrintPreview();
                                }

                                await RefreshGridAsync();
                            }
                        }
                    }
                    break;
            }
        }
    }

    private async Task PromptAdminOverrideRoomStatusAsync(Room room)
    {
        using var frm = new Form
        {
            Width = 380,
            Height = 240,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            Text = $"Admin Override - เปลี่ยนสถานะห้อง {room.RoomNumber}",
            StartPosition = FormStartPosition.CenterParent,
            MaximizeBox = false,
            MinimizeBox = false,
            BackColor = Color.White
        };

        var lbl = new Label
        {
            Text = $"เลือกสถานะห้องพักใหม่สำหรับห้อง {room.RoomNumber}:",
            Location = new Point(20, 20),
            AutoSize = true,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold)
        };

        var cmbStatus = new ComboBox
        {
            Location = new Point(20, 55),
            Size = new Size(320, 30),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 10F)
        };

        cmbStatus.Items.Add(new { Text = "ว่างพร้อมใช้งาน (Available)", Status = RoomStatus.Available });
        cmbStatus.Items.Add(new { Text = "มีผู้เข้าพัก (Occupied)", Status = RoomStatus.Occupied });
        cmbStatus.Items.Add(new { Text = "รอทำความสะอาด (Cleaning)", Status = RoomStatus.Cleaning });
        cmbStatus.Items.Add(new { Text = "ปิดซ่อมบำรุง (Maintenance)", Status = RoomStatus.Maintenance });
        cmbStatus.Items.Add(new { Text = "จองแล้ว (Reserved)", Status = RoomStatus.Reserved });

        cmbStatus.DisplayMember = "Text";
        cmbStatus.ValueMember = "Status";
        cmbStatus.SelectedIndex = (int)room.Status;

        var btnSave = new Button
        {
            Text = "บันทึกเปลี่ยนสถานะ",
            Location = new Point(150, 120),
            Size = new Size(190, 40),
            BackColor = Color.FromArgb(217, 119, 6),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            DialogResult = DialogResult.OK,
            Cursor = Cursors.Hand
        };
        btnSave.FlatAppearance.BorderSize = 0;

        frm.Controls.Add(lbl);
        frm.Controls.Add(cmbStatus);
        frm.Controls.Add(btnSave);

        if (frm.ShowDialog() == DialogResult.OK && cmbStatus.SelectedItem != null)
        {
            dynamic selected = cmbStatus.SelectedItem;
            RoomStatus newStatus = (RoomStatus)selected.Status;
            await _roomService.UpdateRoomStatusAsync(room.Id, newStatus, "Super Admin Override Status");
            await RefreshGridAsync();
            MessageBox.Show($"อัปเดตสถานะห้อง {room.RoomNumber} เป็น [{selected.Text}] เรียบร้อยแล้ว", "สำเร็จ", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    #region Dialog Action Methods
    private async Task OpenCheckInDialogAsync(Room room, RoomType? roomType)
    {
        var rt = roomType ?? _allRoomTypes.FirstOrDefault(t => t.Id == room.RoomTypeId) ?? new RoomType { Name = "Standard", DailyRate = 500 };
        using var dlg = new CheckInForm(room, rt, _bookingService, _customerService);
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            await RefreshGridAsync();
        }
    }

    private async Task OpenBookingDialogAsync(Room room)
    {
        using var dlg = new BookingForm(_roomService, _bookingService, _customerService, room);
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            await RefreshGridAsync();
        }
    }

    private async Task OpenCheckOutDialogAsync(Room room)
    {
        var activeMap = await _bookingService.GetAllActiveBookingsWithCustomersAsync();
        if (activeMap.TryGetValue(room.Id, out var entry))
        {
            var folio = await _bookingService.GetFolioByBookingIdAsync(entry.Booking.Id);
            using var dlg = new CheckOutForm(room, entry.Booking, entry.Customer, folio, _bookingService, _utilityBillService, _settingsService);
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                await RefreshGridAsync();
            }
        }
        else
        {
            MessageBox.Show($"ไม่พบรายการจอง active สำหรับห้อง {room.RoomNumber}", "เตือน", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async Task OpenMeterReadingDialogFromRoomGridAsync(Room room)
    {
        if (_utilityBillService == null) return;
        try
        {
            string currentBillingMonth = DateTime.Now.ToString("yyyy-MM");
            var settings = await _settingsService.GetAllSettingsAsync();
            var activeMap = await _bookingService.GetAllActiveBookingsWithCustomersAsync();

            string tenantName = "ผู้เข้าพัก";
            decimal roomRate = 0;
            if (activeMap.TryGetValue(room.Id, out var entry))
            {
                if (entry.Customer != null) tenantName = entry.Customer.FullName;
                roomRate = entry.Booking.AgreedRate;
            }

            decimal elecPrev = await _utilityBillService.GetPreviousMeterValueAsync(room.Id, UtilityType.Electric, currentBillingMonth);
            decimal waterPrev = await _utilityBillService.GetPreviousMeterValueAsync(room.Id, UtilityType.Water, currentBillingMonth);

            var readings = (await _utilityBillService.GetMeterReadingsByMonthAsync(currentBillingMonth)).Where(r => r.RoomId == room.Id).ToList();
            var elecReading = readings.FirstOrDefault(r => r.UtilityType == UtilityType.Electric);
            var waterReading = readings.FirstOrDefault(r => r.UtilityType == UtilityType.Water);

            decimal elecCurr = elecReading?.ReadingCurr ?? elecPrev;
            decimal waterCurr = waterReading?.ReadingCurr ?? waterPrev;

            using var dlg = new MeterReadingInputDialog(
                room,
                tenantName,
                currentBillingMonth,
                roomRate,
                elecPrev,
                elecCurr,
                waterPrev,
                waterCurr,
                1,
                0,
                0,
                "",
                settings,
                _settingsService
            );

            if (dlg.ShowDialog() == DialogResult.OK)
            {
                await _utilityBillService.RecordMeterReadingAsync(room.Id, UtilityType.Electric, dlg.ElecPrev, dlg.ElecCurr, currentBillingMonth, dlg.Notes);
                if (settings.WaterBillingMode == "METER")
                {
                    await _utilityBillService.RecordMeterReadingAsync(room.Id, UtilityType.Water, dlg.WaterPrev, dlg.WaterCurr, currentBillingMonth, dlg.Notes);
                }
                var bill = await _utilityBillService.GenerateMonthlyBillAsync(room.Id, currentBillingMonth, dlg.WaterPersons);
                
                if (dlg.MarkAsPaidRequested)
                {
                    await _utilityBillService.MarkAllUnpaidBillsAsPaidForRoomAsync(room.Id, dlg.SelectedPaymentMethod);
                    bill.IsPaid = true;
                    _cachedUnpaidBills.RemoveAll(b => b.RoomId == room.Id);
                }

                if (dlg.PrintBillRequested)
                {
                    Customer? customer = null;
                    if (activeMap.TryGetValue(room.Id, out var custEntry)) customer = custEntry.Customer;

                    var printer = new HotelPOS.Printing.UtilityInvoicePrinter(bill, customer, settings);
                    printer.ShowPrintPreview();
                }
                await RefreshGridAsync();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"ไม่สามารถเปิดบันทึกค่าน้ำค่าไฟได้: {ex.Message}", "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task PayUtilitiesFromRoomGridAsync(Room room)
    {
        if (_utilityBillService == null) return;
        string currentBillingMonth = DateTime.Now.ToString("yyyy-MM");
        using var dlg = new UtilityBillHistoryForm(_utilityBillService, currentBillingMonth, _settingsService);
        dlg.ShowDialog();
        await RefreshGridAsync();
    }

    private async Task CheckInReservedBookingAsync(Room room)
    {
        var activeMap = await _bookingService.GetAllActiveBookingsWithCustomersAsync();
        if (activeMap.TryGetValue(room.Id, out var entry) && entry.Booking.Status == BookingStatus.Reserved)
        {
            await _bookingService.CheckInExistingBookingAsync(entry.Booking.Id);
            await RefreshGridAsync();
        }
    }

    private async Task CancelReservedBookingAsync(Room room)
    {
        var activeMap = await _bookingService.GetAllActiveBookingsWithCustomersAsync();
        if (activeMap.TryGetValue(room.Id, out var entry) && entry.Booking.Status == BookingStatus.Reserved)
        {
            if (MessageBox.Show($"ยืนยันการยกเลิกการจองห้อง {room.RoomNumber}?", "ยืนยัน", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                await _bookingService.CancelBookingAsync(entry.Booking.Id, "ยกเลิกจากผังห้องพัก");
                await RefreshGridAsync();
            }
        }
    }

    private async void BtnNewBooking_Click(object? sender, EventArgs e)
    {
        using var dlg = new BookingForm(_roomService, _bookingService, _customerService, null);
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            await RefreshGridAsync();
        }
    }
    #endregion
}
