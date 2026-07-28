using HotelPOS.Common;
using HotelPOS.Common.Models;
using HotelPOS.Core.Services;

namespace HotelPOS.UI;

/// <summary>
/// ผังห้องพักแสดงผลระดับพรีเมียม แบ่งสีประเภทราคา (รายเดือน / รายวัน / รายชั่วโมง) ชัดเจน ใช้ง่าย แข็งแกร่ง สะดวก
/// </summary>
public class RoomGridControl : UserControl
{
    private readonly IRoomService _roomService;
    private readonly IBookingService _bookingService;
    private readonly ICustomerService _customerService;

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

    private string? _selectedFilterMode = null; // null = ทั้งหมด, "Available", "Occupied", "Monthly", "Daily", "Hourly", "Cleaning", "Reserved", "Maintenance"
    private System.Windows.Forms.Timer _autoRefreshTimer = null!;
    private readonly ISettingsService _settingsService;
    private Label _lblUtilityRates = null!;

    public RoomGridControl(
        IRoomService roomService,
        IBookingService bookingService,
        ICustomerService customerService,
        ISettingsService settingsService)
    {
        _roomService = roomService;
        _bookingService = bookingService;
        _customerService = customerService;
        _settingsService = settingsService;

        InitializeUI();
        InitializeTimer();

        Load += async (s, e) => await RefreshGridAsync();
    }

    private void InitializeTimer()
    {
        _autoRefreshTimer = new System.Windows.Forms.Timer { Interval = 30000 };
        _autoRefreshTimer.Tick += async (s, e) =>
        {
            if (IsHandleCreated && !IsDisposed && Visible)
            {
                await ApplyFilterAsync();
            }
        };
        _autoRefreshTimer.Start();
    }

    private void InitializeUI()
    {
        Dock = DockStyle.Fill;
        Font = new Font("Segoe UI", 11F, FontStyle.Regular);
        BackColor = Color.FromArgb(241, 245, 249);

        // Header Panel — ใช้ FlowLayoutPanel เพื่อ Responsive (ไม่ hardcode pixel position)
        _headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 135,
            BackColor = Color.White,
            Padding = new Padding(15, 10, 15, 10)
        };

        // Row 1: Title + Utility Rate + Search + Filters + Buttons (FlowLayoutPanel)
        var rowTopFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 40,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = false,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };

        var titleLabel = new Label
        {
            Text = "ผังห้องพักและสถานะปัจจุบัน (Room Floor Plan)",
            Font = new Font("Segoe UI", 15F, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59),
            AutoSize = true,
            Margin = new Padding(3, 4, 10, 0)
        };

        _lblUtilityRates = new Label
        {
            Text = "อัตราค่าไฟ: - /หน่วย",
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(234, 88, 12),
            AutoSize = true,
            Margin = new Padding(3, 8, 15, 0)
        };

        var lblSearch = new Label { Text = "ค้นหา:", Font = new Font("Segoe UI", 10F, FontStyle.Bold), AutoSize = true, Margin = new Padding(10, 8, 0, 0) };
        _txtSearch = new TextBox
        {
            Width = 160,
            Font = new Font("Segoe UI", 10.5F),
            PlaceholderText = "เลขห้อง / ผู้พัก / เบอร์โทร...",
            Margin = new Padding(3, 5, 8, 0)
        };
        _txtSearch.TextChanged += async (s, e) => await ApplyFilterAsync();

        var lblFloor = new Label { Text = "ชั้น:", Font = new Font("Segoe UI", 10F, FontStyle.Bold), AutoSize = true, Margin = new Padding(5, 8, 0, 0) };
        _cboFloorFilter = new ComboBox { Width = 85, Font = new Font("Segoe UI", 10F), DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(3, 5, 8, 0) };
        _cboFloorFilter.SelectedIndexChanged += async (s, e) => await ApplyFilterAsync();

        var lblType = new Label { Text = "ประเภท:", Font = new Font("Segoe UI", 10F, FontStyle.Bold), AutoSize = true, Margin = new Padding(5, 8, 0, 0) };
        _cboTypeFilter = new ComboBox { Width = 110, Font = new Font("Segoe UI", 10F), DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(3, 5, 8, 0) };
        _cboTypeFilter.SelectedIndexChanged += async (s, e) => await ApplyFilterAsync();

        _btnRefresh = new Button
        {
            Text = "รีเฟรช",
            Size = new Size(80, 34),
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Margin = new Padding(5, 3, 5, 0)
        };
        _btnRefresh.Click += async (s, e) => await RefreshGridAsync();

        _btnNewBooking = new Button
        {
            Text = "+ จองล่วงหน้า",
            Size = new Size(115, 34),
            BackColor = Color.FromArgb(37, 99, 235),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(5, 3, 0, 0)
        };
        _btnNewBooking.FlatAppearance.BorderSize = 0;
        _btnNewBooking.Click += BtnNewBooking_Click;

        rowTopFlow.Controls.AddRange(new Control[] {
            titleLabel, _lblUtilityRates,
            lblSearch, _txtSearch, lblFloor, _cboFloorFilter, lblType, _cboTypeFilter,
            _btnRefresh, _btnNewBooking
        });

        // Row 2: Summary Badges (FlowLayoutPanel — จะ wrap อัตโนมัติเมื่อหน้าจอแคบ)
        var rowBadgeFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 38,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Padding = new Padding(0),
            Margin = new Padding(0, 2, 0, 0)
        };

        _lblAvailableCount = CreateBadgeLabel("ว่าง: 0", Color.FromArgb(236, 253, 245), Color.FromArgb(6, 95, 70));
        _lblOccupiedCount = CreateBadgeLabel("มีคนพัก: 0", Color.FromArgb(254, 226, 226), Color.FromArgb(153, 27, 27));
        _lblCleaningCount = CreateBadgeLabel("รอทำความสะอาด: 0", Color.FromArgb(254, 243, 199), Color.FromArgb(146, 64, 14));
        _lblReservedCount = CreateBadgeLabel("จองล่วงหน้า: 0", Color.FromArgb(239, 246, 255), Color.FromArgb(30, 58, 138));
        _lblNearCheckoutCount = CreateBadgeLabel("ใกล้ครบกำหนด: 0", Color.FromArgb(254, 243, 199), Color.DarkOrange);
        _lblOverdueCount = CreateBadgeLabel("เลยกำหนด: 0", Color.FromArgb(254, 226, 226), Color.DarkRed);

        rowBadgeFlow.Controls.AddRange(new Control[] {
            _lblAvailableCount, _lblOccupiedCount, _lblCleaningCount,
            _lblReservedCount, _lblNearCheckoutCount, _lblOverdueCount
        });

        // Row 3: Quick Filter Pills
        _statusFilterPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 38,
            BackColor = Color.Transparent,
            WrapContents = false,
            AutoScroll = true
        };

        BuildStatusFilterButtons();

        // เพิ่ม Rows เข้า Header (Dock=Top จะเรียงจากบนลงล่าง ต้อง add กลับด้าน)
        _headerPanel.Controls.Add(_statusFilterPanel);
        _headerPanel.Controls.Add(rowBadgeFlow);
        _headerPanel.Controls.Add(rowTopFlow);

        // Cards Container
        _cardsContainer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(15),
            BackColor = Color.FromArgb(241, 245, 249),
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false
        };
        _cardsContainer.SizeChanged += (s, e) =>
        {
            int w = _cardsContainer.ClientSize.Width - 40;
            if (w < 200) w = 200;
            foreach (Control ctrl in _cardsContainer.Controls)
            {
                if (ctrl is FlowLayoutPanel or Panel)
                {
                    ctrl.Width = w;
                }
            }
        };

        Controls.Add(_cardsContainer);
        Controls.Add(_headerPanel);
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
                Height = 34,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Margin = new Padding(0, 0, 8, 0),
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

            btn.Click += async (s, e) =>
            {
                _selectedFilterMode = item.modeValue;
                BuildStatusFilterButtons();
                await ApplyFilterAsync();
            };

            _statusFilterPanel.Controls.Add(btn);
        }
    }

    private static Label CreateBadgeLabel(string text, Color backColor, Color foreColor, int x = 0, int y = 0)
    {
        return new Label
        {
            Text = text,
            BackColor = backColor,
            ForeColor = foreColor,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            Location = new Point(x, y),
            AutoSize = true,
            Padding = new Padding(8, 4, 8, 4),
            Margin = new Padding(3, 2, 3, 2),
            BorderStyle = BorderStyle.FixedSingle
        };
    }

    public async Task RefreshGridAsync()
    {
        try
        {
            _allRooms = (await _roomService.GetRoomsAsync()).ToList();
            _allRoomTypes = (await _roomService.GetRoomTypesAsync(true)).ToList();

            var currentFloor = _cboFloorFilter.SelectedItem?.ToString();
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

            await ApplyFilterAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"โหลดข้อมูลผังห้องพักไม่สำเร็จ: {ex.Message}", "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task ApplyFilterAsync()
    {
        try
        {
            var settings = await _settingsService.GetAllSettingsAsync();
            _lblUtilityRates.Text = $"ค่าไฟ: {settings.ElectricRatePerUnit:N2} บ./หน่วย | ค่าน้ำ: " +
                (settings.WaterBillingMode == "METER" ? $"{settings.WaterRatePerUnit:N2} บ./หน่วย" : $"เหมาจ่าย {settings.WaterFlatRatePerPerson:N2} บ./คน");
        }
        catch 
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
        var roomsToDisplay = new List<(Room Room, RoomType? Type, Booking? Booking, Customer? Customer)>();

        // Batch: ดึงการจอง active ทั้งหมดพร้อม Customer ใน query เดียว (แทน N+1 query ต่อห้อง)
        var activeBookingsMap = await _bookingService.GetAllActiveBookingsWithCustomersAsync();

        foreach (var room in _allRooms)
        {
            var roomType = _allRoomTypes.FirstOrDefault(t => t.Id == room.RoomTypeId);
            Booking? booking = null;
            Customer? customer = null;

            if (room.Status == RoomStatus.Occupied || room.Status == RoomStatus.Reserved)
            {
                if (activeBookingsMap.TryGetValue(room.Id, out var entry))
                {
                    booking = entry.Booking;
                    customer = entry.Customer;
                }

                if (room.Status == RoomStatus.Occupied && booking?.CheckOutPlanned.HasValue == true)
                {
                    var span = booking.CheckOutPlanned.Value - now;
                    if (span.TotalMinutes <= 0) overdueCount++;
                    else if (span.TotalMinutes <= 30) nearCheckoutCount++;
                }
            }

            // Filter Checking
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
                if (_selectedFilterMode == "Daily" && (roomType == null || (roomType.DailyRate == 0 && roomType.MonthlyRate == 0 && roomType.HourlyRate == 0))) continue;
            }

            // Search query check
            if (!string.IsNullOrWhiteSpace(query))
            {
                bool matchRoom = room.RoomNumber.Contains(query, StringComparison.OrdinalIgnoreCase);
                bool matchCust = customer != null && (customer.FullName.Contains(query, StringComparison.OrdinalIgnoreCase) || (!string.IsNullOrEmpty(customer.Phone) && customer.Phone.Contains(query, StringComparison.OrdinalIgnoreCase)));
                bool matchType = roomType != null && roomType.Name.Contains(query, StringComparison.OrdinalIgnoreCase);

                if (!matchRoom && !matchCust && !matchType) continue;
            }

            roomsToDisplay.Add((room, roomType, booking, customer));
        }

        // Group rooms by Floor
        var grouped = roomsToDisplay
            .GroupBy(x => x.Room.Floor ?? "1")
            .OrderBy(x => x.Key);

        int containerWidth = _cardsContainer.ClientSize.Width - 40;
        if (containerWidth < 200) containerWidth = 200;

        foreach (var group in grouped)
        {
            var floorName = group.Key;

            // Header bar for the floor
            var header = new Panel
            {
                Width = containerWidth,
                Height = 35,
                Margin = new Padding(0, 8, 0, 8),
                BackColor = Color.FromArgb(226, 232, 240) // Slate-200
            };
            var lblFloorHeader = new Label
            {
                Text = $"ชั้น {floorName} (Floor {floorName})",
                Font = new Font("Segoe UI", 11.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59),
                Location = new Point(10, 6),
                AutoSize = true
            };
            header.Controls.Add(lblFloorHeader);

            // Flow layout panel for cards on this floor
            var floorFlow = new FlowLayoutPanel
            {
                Width = containerWidth,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoSize = true,
                BackColor = Color.Transparent,
                Padding = new Padding(0),
                Margin = new Padding(0, 0, 0, 15)
            };

            foreach (var item in group.OrderBy(x => x.Room.RoomNumber))
            {
                var card = CreateRoomTileCard(item.Room, item.Type, item.Booking, item.Customer, now);
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
                Font = new Font("Segoe UI", 12F, FontStyle.Italic),
                ForeColor = Color.Gray,
                Location = new Point(20, 20),
                AutoSize = true
            };
            _cardsContainer.Controls.Add(lblEmpty);
        }

        _lblAvailableCount.Text = $"ว่าง: {avail}";
        _lblOccupiedCount.Text = $"มีคนพัก: {occ}";
        _lblCleaningCount.Text = $"รอทำความสะอาด: {clean}";
        _lblReservedCount.Text = $"จองล่วงหน้า: {res}";
        _lblNearCheckoutCount.Text = $"ใกล้ครบกำหนด: {nearCheckoutCount}";
        _lblOverdueCount.Text = $"เลยกำหนด: {overdueCount}";

        _cardsContainer.ResumeLayout();
    }

    private Control CreateRoomTileCard(Room room, RoomType? roomType, Booking? booking, Customer? customer, DateTime now)
    {
        var card = new Panel
        {
            Size = new Size(255, 205),
            Margin = new Padding(8),
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(0),
            Cursor = Cursors.Hand
        };

        Color headerColor;
        Color backColor;
        Color textColor;
        string statusText = "";

        bool isOverdue = false;
        bool isNearCheckout = false;
        double minutesDiff = 0;

        if (room.Status == RoomStatus.Occupied && booking?.CheckOutPlanned.HasValue == true)
        {
            var span = booking.CheckOutPlanned.Value - now;
            minutesDiff = span.TotalMinutes;
            if (minutesDiff <= 0) isOverdue = true;
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
                if (isOverdue)
                {
                    headerColor = Color.FromArgb(185, 28, 28);
                    backColor = Color.FromArgb(254, 226, 226);
                    textColor = Color.DarkRed;
                    statusText = "เลยกำหนด!";
                }
                else if (isNearCheckout)
                {
                    headerColor = Color.FromArgb(217, 119, 6);
                    backColor = Color.FromArgb(254, 243, 199);
                    textColor = Color.DarkGoldenrod;
                    statusText = "ใกล้ครบกำหนด";
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

        // Top Status Header Bar
        var topHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 36,
            BackColor = headerColor,
            Padding = new Padding(10, 4, 10, 4)
        };

        var lblRoomNumHeader = new Label
        {
            Text = $"ห้อง {room.RoomNumber}  (ชั้น {room.Floor ?? "1"})",
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            ForeColor = Color.White,
            Dock = DockStyle.Left,
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft
        };

        var lblStatusPill = new Label
        {
            Text = statusText,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = Color.White,
            Dock = DockStyle.Right,
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleRight
        };

        topHeader.Controls.Add(lblRoomNumHeader);
        topHeader.Controls.Add(lblStatusPill);

        // Rate Plan Color Badge
        string rateBadgeText;
        Color rateBadgeBg;
        Color rateBadgeFg;

        if (roomType != null && roomType.MonthlyRate > 0 && roomType.DailyRate == 0)
        {
            rateBadgeText = $"รายเดือน  ฿{roomType.MonthlyRate:N0}/เดือน";
            rateBadgeBg = Color.FromArgb(243, 232, 255); // Purple
            rateBadgeFg = Color.FromArgb(107, 33, 168);
        }
        else if (roomType != null && roomType.HourlyRate > 0 && roomType.DailyRate == 0)
        {
            rateBadgeText = $"รายชั่วโมง  ฿{roomType.HourlyRate:N0}/ชม.";
            rateBadgeBg = Color.FromArgb(254, 243, 199); // Amber
            rateBadgeFg = Color.FromArgb(146, 64, 14);
        }
        else if (roomType != null && roomType.MonthlyRate > 0 && roomType.DailyRate > 0)
        {
            rateBadgeText = $"รายเดือน ฿{roomType.MonthlyRate:N0} / รายวัน ฿{roomType.DailyRate:N0}";
            rateBadgeBg = Color.FromArgb(238, 242, 255); // Indigo
            rateBadgeFg = Color.FromArgb(55, 48, 163);
        }
        else
        {
            rateBadgeText = $"รายวัน  ฿{(roomType != null ? roomType.DailyRate.ToString("N0") : "0")}/วัน";
            rateBadgeBg = Color.FromArgb(236, 253, 245); // Emerald
            rateBadgeFg = Color.FromArgb(6, 95, 70);
        }

        var lblRateBadge = new Label
        {
            Text = rateBadgeText,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            BackColor = rateBadgeBg,
            ForeColor = rateBadgeFg,
            Location = new Point(10, 42),
            AutoSize = true,
            Padding = new Padding(6, 2, 6, 2),
            BorderStyle = BorderStyle.FixedSingle
        };

        // Guest Info
        string guestDetailText;
        if (room.Status == RoomStatus.Occupied && customer != null)
        {
            guestDetailText = $"ผู้พัก: {customer.FullName} ({customer.Phone ?? "-"})";
        }
        else if (room.Status == RoomStatus.Reserved && customer != null)
        {
            guestDetailText = $"ผู้จอง: {customer.FullName} ({customer.Phone ?? "-"})";
        }
        else if (room.Status == RoomStatus.Cleaning)
        {
            guestDetailText = "สถานะ: รอแม่บ้านทำความสะอาดห้อง";
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
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            ForeColor = textColor,
            Location = new Point(10, 72),
            Size = new Size(235, 20),
            AutoEllipsis = true
        };

        // Time Alert
        string timeAlertText = "";
        Color alertColor = textColor;

        if (room.Status == RoomStatus.Occupied && booking?.CheckOutPlanned.HasValue == true)
        {
            if (isOverdue)
            {
                timeAlertText = $"เลยกำหนด {Math.Abs((int)minutesDiff)} นาที (ออก: {booking.CheckOutPlanned.Value:HH:mm} น.)";
                alertColor = Color.DarkRed;
            }
            else if (isNearCheckout)
            {
                timeAlertText = $"เหลืออีก {Math.Ceiling(minutesDiff)} นาที (กำหนดออก: {booking.CheckOutPlanned.Value:HH:mm} น.)";
                alertColor = Color.DarkOrange;
            }
            else
            {
                timeAlertText = $"กำหนดออก: {booking.CheckOutPlanned.Value:dd/MM HH:mm} น.";
            }
        }
        else if (room.Status == RoomStatus.Reserved && booking != null)
        {
            timeAlertText = $"กำหนดเช็คอิน: {booking.CheckInPlanned:dd/MM HH:mm} น.";
        }

        var lblTimeAlert = new Label
        {
            Text = timeAlertText,
            Font = new Font("Segoe UI", 9F, (isOverdue || isNearCheckout) ? FontStyle.Bold : FontStyle.Regular),
            ForeColor = alertColor,
            Location = new Point(10, 94),
            Size = new Size(235, 20),
            AutoEllipsis = true
        };

        // Action Buttons Panel
        var actionPanel = new Panel
        {
            Location = new Point(8, 118),
            Size = new Size(235, 78),
            BackColor = Color.Transparent
        };

        // Context Menu
        var cms = new ContextMenuStrip { Font = new Font("Segoe UI", 10.5F) };
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
            var itemCheckOut = cms.Items.Add("คืนห้องพัก / เช็คเอาท์");
            itemCheckOut.Click += async (s, e) => await OpenCheckOutDialogAsync(room);
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
            var itemEnable = cms.Items.Add("เปิดใช้งานห้อง (เปลี่ยนเป็นห้องว่าง)");
            itemEnable.Click += async (s, e) =>
            {
                await _roomService.UpdateRoomStatusAsync(room.Id, RoomStatus.Available);
                await RefreshGridAsync();
            };
        }

        switch (room.Status)
        {
            case RoomStatus.Available:
                var btnCheckIn = new Button
                {
                    Text = "เช็คอิน",
                    Location = new Point(4, 10),
                    Size = new Size(110, 44),
                    BackColor = Color.FromArgb(16, 185, 129),
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                    FlatStyle = FlatStyle.Flat,
                    Cursor = Cursors.Hand
                };
                btnCheckIn.FlatAppearance.BorderSize = 0;
                btnCheckIn.Click += async (s, e) => await OpenCheckInDialogAsync(room, roomType);

                var btnReserve = new Button
                {
                    Text = "จอง",
                    Location = new Point(120, 10),
                    Size = new Size(110, 44),
                    BackColor = Color.FromArgb(37, 99, 235),
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                    FlatStyle = FlatStyle.Flat,
                    Cursor = Cursors.Hand
                };
                btnReserve.FlatAppearance.BorderSize = 0;
                btnReserve.Click += async (s, e) => await OpenBookingDialogAsync(room);

                actionPanel.Controls.Add(btnCheckIn);
                actionPanel.Controls.Add(btnReserve);
                break;

            case RoomStatus.Occupied:
                var btnCheckOut = new Button
                {
                    Text = isOverdue ? "เช็คเอาท์ (เลยกำหนด!)" : "คืนห้อง / เช็คเอาท์",
                    Location = new Point(4, 10),
                    Size = new Size(226, 44),
                    BackColor = isOverdue ? Color.FromArgb(185, 28, 28) : Color.FromArgb(225, 29, 72),
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                    FlatStyle = FlatStyle.Flat,
                    Cursor = Cursors.Hand
                };
                btnCheckOut.FlatAppearance.BorderSize = 0;
                btnCheckOut.Click += async (s, e) => await OpenCheckOutDialogAsync(room);
                actionPanel.Controls.Add(btnCheckOut);
                break;

            case RoomStatus.Cleaning:
                var btnCleanDone = new Button
                {
                    Text = "ทำความสะอาดเสร็จ",
                    Location = new Point(4, 10),
                    Size = new Size(226, 44),
                    BackColor = Color.FromArgb(217, 119, 6),
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                    FlatStyle = FlatStyle.Flat,
                    Cursor = Cursors.Hand
                };
                btnCleanDone.FlatAppearance.BorderSize = 0;
                btnCleanDone.Click += async (s, e) =>
                {
                    await _roomService.UpdateRoomStatusAsync(room.Id, RoomStatus.Available);
                    await RefreshGridAsync();
                };
                actionPanel.Controls.Add(btnCleanDone);
                break;

            case RoomStatus.Reserved:
                var btnCheckInReserved = new Button
                {
                    Text = "เช็คอินการจอง",
                    Location = new Point(4, 10),
                    Size = new Size(226, 44),
                    BackColor = Color.FromArgb(37, 99, 235),
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                    FlatStyle = FlatStyle.Flat,
                    Cursor = Cursors.Hand
                };
                btnCheckInReserved.FlatAppearance.BorderSize = 0;
                btnCheckInReserved.Click += async (s, e) => await CheckInReservedBookingAsync(room);
                actionPanel.Controls.Add(btnCheckInReserved);
                break;

            case RoomStatus.Maintenance:
            default:
                var btnEnable = new Button
                {
                    Text = "เปิดใช้งานห้องพัก",
                    Location = new Point(4, 10),
                    Size = new Size(226, 44),
                    BackColor = Color.FromArgb(100, 116, 139),
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                    FlatStyle = FlatStyle.Flat,
                    Cursor = Cursors.Hand
                };
                btnEnable.FlatAppearance.BorderSize = 0;
                btnEnable.Click += async (s, e) =>
                {
                    await _roomService.UpdateRoomStatusAsync(room.Id, RoomStatus.Available);
                    await RefreshGridAsync();
                };
                actionPanel.Controls.Add(btnEnable);
                break;
        }

        card.Controls.Add(topHeader);
        card.Controls.Add(lblRateBadge);
        card.Controls.Add(lblGuest);
        card.Controls.Add(lblTimeAlert);
        card.Controls.Add(actionPanel);

        // ToolTip Guide for Room Card
        AppToolTip.Attach(card, $"ห้อง {room.RoomNumber} - คลิกขวาเพื่อเปิดเมนูด่วน");

        return card;
    }

    private async Task OpenCheckInDialogAsync(Room room, RoomType? roomType)
    {
        using var form = new CheckInForm(room, roomType ?? new RoomType(), _bookingService, _customerService);
        if (form.ShowDialog() == DialogResult.OK)
        {
            await RefreshGridAsync();
        }
    }

    private async Task OpenBookingDialogAsync(Room room)
    {
        using var form = new BookingForm(_roomService, _bookingService, _customerService, room);
        if (form.ShowDialog() == DialogResult.OK)
        {
            await RefreshGridAsync();
        }
    }

    private async Task OpenCheckOutDialogAsync(Room room)
    {
        var booking = await _bookingService.GetActiveBookingByRoomIdAsync(room.Id);
        if (booking == null)
        {
            MessageBox.Show("ไม่พบข้อมูลการเช็คอินปัจจุบันของห้องนี้", "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        var customer = await _customerService.GetCustomerByIdAsync(booking.CustomerId);
        var folio = await _bookingService.GetFolioByBookingIdAsync(booking.Id);

        using var form = new CheckOutForm(room, booking, customer, folio, _bookingService);
        if (form.ShowDialog() == DialogResult.OK)
        {
            await RefreshGridAsync();
        }
    }

    private async Task CheckInReservedBookingAsync(Room room)
    {
        var booking = await _bookingService.GetActiveBookingByRoomIdAsync(room.Id);
        if (booking == null)
        {
            MessageBox.Show("ไม่พบรายการจองล่วงหน้า", "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        await _bookingService.CheckInExistingBookingAsync(booking.Id);
        MessageBox.Show($"เช็คอินห้อง {room.RoomNumber} เรียบร้อยแล้ว", "สำเร็จ", MessageBoxButtons.OK, MessageBoxIcon.Information);
        await RefreshGridAsync();
    }

    private async Task CancelReservedBookingAsync(Room room)
    {
        var booking = await _bookingService.GetActiveBookingByRoomIdAsync(room.Id);
        if (booking != null)
        {
            if (MessageBox.Show($"คุณแน่ใจหรือไม่ที่จะยกเลิกรายการจองห้อง {room.RoomNumber}?", "ยืนยันการยกเลิก", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                await _bookingService.CancelBookingAsync(booking.Id, "ผู้ใช้ยกเลิกการจอง");
                await RefreshGridAsync();
            }
        }
    }

    private async void BtnNewBooking_Click(object? sender, EventArgs e)
    {
        using var form = new BookingForm(_roomService, _bookingService, _customerService);
        if (form.ShowDialog() == DialogResult.OK)
        {
            await RefreshGridAsync();
        }
    }
}
