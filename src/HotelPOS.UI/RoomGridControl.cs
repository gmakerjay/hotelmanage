using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using HotelPOS.Common;
using HotelPOS.Common.Models;
using HotelPOS.Core.Services;

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
        InitializeTimer();

        Load += async (s, e) => await RefreshGridAsync();
        VisibleChanged += async (s, e) =>
        {
            if (Visible && _isDataLoaded)
            {
                // Instant draw from cache (0ms lag when switching tabs!)
                ApplyFilter();
                // Silent background update in case data changed
                await LoadDataCachesAsync(silent: true);
            }
        };
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
        Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);
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
            Width = 540,
            Height = 36,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent
        };

        var lblSearch = new Label { Text = "ค้นหา:", Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), AutoSize = true, Margin = new Padding(0, 7, 2, 0) };
        _txtSearch = new TextBox
        {
            Width = 140,
            Font = new Font("Segoe UI", 9.5F),
            PlaceholderText = "เลขห้อง / ชื่อ...",
            Margin = new Padding(0, 4, 6, 0)
        };
        _txtSearch.TextChanged += (s, e) => ApplyFilter();

        var lblFloor = new Label { Text = "ชั้น:", Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), AutoSize = true, Margin = new Padding(2, 7, 2, 0) };
        _cboFloorFilter = new ComboBox { Width = 75, Font = new Font("Segoe UI", 9.5F), DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 4, 6, 0) };
        _cboFloorFilter.SelectedIndexChanged += (s, e) => ApplyFilter();

        var lblType = new Label { Text = "ประเภท:", Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), AutoSize = true, Margin = new Padding(2, 7, 2, 0) };
        _cboTypeFilter = new ComboBox { Width = 95, Font = new Font("Segoe UI", 9.5F), DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 4, 6, 0) };
        _cboTypeFilter.SelectedIndexChanged += (s, e) => ApplyFilter();

        _btnRefresh = new Button
        {
            Text = "รีเฟรช",
            Size = new Size(68, 28),
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 3, 4, 0)
        };
        _btnRefresh.Click += async (s, e) => await RefreshGridAsync();

        _btnNewBooking = new Button
        {
            Text = "จองล่วงหน้า",
            Size = new Size(92, 28),
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
            Width = 840,
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
            ("มีผู้เข้าพัก", "Occupied", Color.FromArgb(153, 27, 27)),
            ("รายเดือน", "Monthly", Color.FromArgb(107, 33, 168)),
            ("รายวัน", "Daily", Color.FromArgb(37, 99, 235)),
            ("รายชั่วโมง", "Hourly", Color.FromArgb(146, 64, 14)),
            ("รอทำความสะอาด", "Cleaning", Color.FromArgb(180, 83, 9)),
            ("จองล่วงหน้า", "Reserved", Color.FromArgb(30, 58, 138)),
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
                var curMonth = now.ToString("yyyy-MM");
                var prevMonth = now.AddMonths(-1).ToString("yyyy-MM");
                var curBills = await _utilityBillService.GetBillsByMonthAsync(curMonth);
                var prevBills = await _utilityBillService.GetBillsByMonthAsync(prevMonth);
                _cachedUnpaidBills = curBills.Concat(prevBills).Where(b => !b.IsPaid).ToList();
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

            var header = new Panel
            {
                Width = containerWidth,
                Height = 26,
                Margin = new Padding(0, 4, 0, 4),
                BackColor = Color.FromArgb(226, 232, 240)
            };
            var lblFloorHeader = new Label
            {
                Text = $"ชั้น {floorName} (Floor {floorName})",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59),
                Location = new Point(8, 3),
                AutoSize = true
            };
            header.Controls.Add(lblFloorHeader);

            var floorFlow = new FlowLayoutPanel
            {
                Width = containerWidth,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoSize = true,
                BackColor = Color.Transparent,
                Padding = new Padding(0),
                Margin = new Padding(0, 0, 0, 8)
            };

            foreach (var item in group.OrderBy(x => x.Room.RoomNumber))
            {
                var card = CreateRoomTileCard(item.Room, item.Type, item.Booking, item.Customer, now, item.IsUtilityOverdue, item.IsUtilityDueSoon, item.OverdueDays, item.DaysLeft, item.TotalUnpaid);
                floorFlow.Controls.Add(card);
            }

            _cardsContainer.Controls.Add(header);
            _cardsContainer.Controls.Add(floorFlow);
        }

        if (roomsToDisplay.Count == 0)
        {
            var lblEmpty = new Label
            {
                Text = "ไม่พบห้องพักตามเงื่อนไขที่ระบุ",
                Font = new Font("Segoe UI", 11F, FontStyle.Italic),
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

        _cardsContainer.ResumeLayout();
    }

    private Control CreateRoomTileCard(
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
        // Sleek & Compact Tile Card (210 x 142)
        var card = new Panel
        {
            Size = new Size(210, 142),
            Margin = new Padding(5),
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(0),
            Cursor = Cursors.Hand
        };

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
                headerColor = Color.FromArgb(16, 185, 129); // Emerald
                backColor = Color.FromArgb(242, 251, 245);
                textColor = Color.FromArgb(6, 95, 70);
                statusText = "ว่าง";
                break;
            case RoomStatus.Occupied:
                if (isCheckoutOverdue || isUtilityOverdue)
                {
                    headerColor = Color.FromArgb(185, 28, 28);
                    backColor = Color.FromArgb(254, 226, 226);
                    textColor = Color.DarkRed;
                    statusText = isUtilityOverdue ? "เลยกำหนดค่าน้ำไฟ!" : "เลยกำหนดเวลาคืนห้อง!";
                }
                else if (isNearCheckout || isUtilityDueSoon)
                {
                    headerColor = Color.FromArgb(217, 119, 6);
                    backColor = Color.FromArgb(254, 243, 199);
                    textColor = Color.DarkGoldenrod;
                    statusText = isUtilityDueSoon ? "ใกล้กำหนดจ่ายค่าน้ำไฟ" : "ใกล้ครบเวลาคืนห้อง";
                }
                else
                {
                    headerColor = Color.FromArgb(225, 29, 72); // Rose Red
                    backColor = Color.FromArgb(255, 241, 242);
                    textColor = Color.FromArgb(159, 18, 57);
                    statusText = "มีผู้เข้าพัก";
                }
                break;
            case RoomStatus.Cleaning:
                headerColor = Color.FromArgb(217, 119, 6);
                backColor = Color.FromArgb(255, 253, 230);
                textColor = Color.SaddleBrown;
                statusText = "รอทำความสะอาด";
                break;
            case RoomStatus.Reserved:
                headerColor = Color.FromArgb(37, 99, 235);
                backColor = Color.FromArgb(239, 246, 255);
                textColor = Color.FromArgb(30, 58, 138);
                statusText = "จองล่วงหน้า";
                break;
            case RoomStatus.Maintenance:
            default:
                headerColor = Color.FromArgb(100, 116, 139);
                backColor = Color.FromArgb(245, 245, 245);
                textColor = Color.DimGray;
                statusText = "ปิดซ่อม";
                break;
        }

        card.BackColor = backColor;

        // Compact Tile Header Bar (28px height)
        var topHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 28,
            BackColor = headerColor,
            Padding = new Padding(6, 2, 6, 2)
        };

        var lblRoomNumHeader = new Label
        {
            Text = $"ห้อง {room.RoomNumber}",
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
            ForeColor = Color.White,
            Dock = DockStyle.Left,
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft
        };

        var lblStatusPill = new Label
        {
            Text = statusText,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            ForeColor = Color.White,
            Dock = DockStyle.Right,
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleRight
        };

        topHeader.Controls.Add(lblRoomNumHeader);
        topHeader.Controls.Add(lblStatusPill);

        string rateBadgeText;
        Color rateBadgeBg;
        Color rateBadgeFg;

        if (roomType != null && roomType.MonthlyRate > 0 && roomType.DailyRate == 0)
        {
            rateBadgeText = $"รายเดือน {roomType.MonthlyRate:N0} บ.";
            rateBadgeBg = Color.FromArgb(243, 232, 255);
            rateBadgeFg = Color.FromArgb(107, 33, 168);
        }
        else if (roomType != null && roomType.HourlyRate > 0 && roomType.DailyRate == 0)
        {
            rateBadgeText = $"รายชั่วโมง {roomType.HourlyRate:N0} บ./ชม.";
            rateBadgeBg = Color.FromArgb(254, 243, 199);
            rateBadgeFg = Color.FromArgb(146, 64, 14);
        }
        else if (roomType != null && roomType.MonthlyRate > 0 && roomType.DailyRate > 0)
        {
            rateBadgeText = $"รายเดือน {roomType.MonthlyRate:N0} / รายวัน {roomType.DailyRate:N0}";
            rateBadgeBg = Color.FromArgb(238, 242, 255);
            rateBadgeFg = Color.FromArgb(55, 48, 163);
        }
        else
        {
            rateBadgeText = $"รายวัน {(roomType != null ? roomType.DailyRate.ToString("N0") : "0")} บาท";
            rateBadgeBg = Color.FromArgb(236, 253, 245);
            rateBadgeFg = Color.FromArgb(6, 95, 70);
        }

        var lblRateBadge = new Label
        {
            Text = rateBadgeText,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            BackColor = rateBadgeBg,
            ForeColor = rateBadgeFg,
            Location = new Point(6, 33),
            AutoSize = true,
            Padding = new Padding(4, 1, 4, 1),
            BorderStyle = BorderStyle.FixedSingle
        };

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

        var lblGuest = new Label
        {
            Text = guestDetailText,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
            ForeColor = textColor,
            Location = new Point(6, 62),
            Size = new Size(196, 18),
            AutoEllipsis = true
        };

        string timeAlertText = "";
        Color alertColor = textColor;

        if (room.Status == RoomStatus.Occupied)
        {
            if (isUtilityOverdue)
            {
                timeAlertText = $"ค้างชำระ: {totalUnpaid:N2} บ. (เลย {overdueDays} วัน)";
                alertColor = Color.DarkRed;
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

        var lblTimeAlert = new Label
        {
            Text = timeAlertText,
            Font = new Font("Segoe UI", 8.5F, (isCheckoutOverdue || isUtilityOverdue || isNearCheckout || isUtilityDueSoon) ? FontStyle.Bold : FontStyle.Regular),
            ForeColor = alertColor,
            Location = new Point(6, 82),
            Size = new Size(196, 18),
            AutoEllipsis = true
        };

        var cms = new ContextMenuStrip { Font = new Font("Segoe UI", 10F) };
        card.ContextMenuStrip = cms;

        if (room.Status == RoomStatus.Available || room.Status == RoomStatus.Cleaning)
        {
            var itemCheckIn = cms.Items.Add("เช็คอินทันที (Walk-In)");
            itemCheckIn.Click += async (s, e) => await OpenCheckInDialogAsync(room, roomType);
        }
        if (room.Status == RoomStatus.Available)
        {
            var itemReserve = cms.Items.Add("จองห้องพักล่วงหน้า");
            itemReserve.Click += async (s, e) => await OpenBookingDialogAsync(room);

            var itemMaint = cms.Items.Add("ปิดซ่อมบำรุง");
            itemMaint.Click += async (s, e) =>
            {
                await _roomService.UpdateRoomStatusAsync(room.Id, RoomStatus.Maintenance, "ปิดซ่อมบำรุง");
                await RefreshGridAsync();
            };
        }
        if (room.Status == RoomStatus.Occupied)
        {
            var itemMeter = cms.Items.Add("กรอก/คำนวณค่าน้ำ-ค่าไฟ");
            itemMeter.Click += async (s, e) => await OpenMeterReadingDialogFromRoomGridAsync(room);

            var itemPayUtilities = cms.Items.Add("บันทึกรับเงินค่าน้ำ-ค่าไฟ (Pay Utilities)");
            itemPayUtilities.Click += async (s, e) => await PayUtilitiesFromRoomGridAsync(room);

            var itemCheckOut = cms.Items.Add("คืนห้องพัก / เช็คเอาท์ & ออกบิล");
            itemCheckOut.Click += async (s, e) => await OpenCheckOutDialogAsync(room);

            var itemMinibar = cms.Items.Add("สั่งมินิบาร์ / สั่งสินค้า (POS)");
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
            var itemCleanDone = cms.Items.Add("ทำความสะอาดเสร็จแล้ว");
            itemCleanDone.Click += async (s, e) =>
            {
                await _roomService.UpdateRoomStatusAsync(room.Id, RoomStatus.Available);
                await RefreshGridAsync();
            };
        }
        if (room.Status == RoomStatus.Reserved)
        {
            var itemCheckInReserved = cms.Items.Add("เช็คอิน (จากการจอง)");
            itemCheckInReserved.Click += async (s, e) => await CheckInReservedBookingAsync(room);

            var itemCancelReserve = cms.Items.Add("ยกเลิกการจอง");
            itemCancelReserve.Click += async (s, e) => await CancelReservedBookingAsync(room);
        }
        if (room.Status == RoomStatus.Maintenance)
        {
            var itemMaintDone = cms.Items.Add("ซ่อมเสร็จแล้ว (พร้อมใช้งาน)");
            itemMaintDone.Click += async (s, e) =>
            {
                await _roomService.UpdateRoomStatusAsync(room.Id, RoomStatus.Available);
                await RefreshGridAsync();
            };
        }

        card.Controls.Add(topHeader);
        card.Controls.Add(lblRateBadge);
        card.Controls.Add(lblGuest);
        card.Controls.Add(lblTimeAlert);

        card.Click += (s, e) => cms.Show(card, new Point(10, 10));

        return card;
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
                settings
            );

            if (dlg.ShowDialog() == DialogResult.OK)
            {
                await _utilityBillService.RecordMeterReadingAsync(room.Id, UtilityType.Electric, dlg.ElecPrev, dlg.ElecCurr, currentBillingMonth, dlg.Notes);
                if (settings.WaterBillingMode == "METER")
                {
                    await _utilityBillService.RecordMeterReadingAsync(room.Id, UtilityType.Water, dlg.WaterPrev, dlg.WaterCurr, currentBillingMonth, dlg.Notes);
                }
                var bill = await _utilityBillService.GenerateMonthlyBillAsync(room.Id, currentBillingMonth, dlg.WaterPersons);
                if (dlg.PrintBillRequested)
                {
                    var printer = new HotelPOS.Printing.UtilityInvoicePrinter(bill, null, settings);
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
