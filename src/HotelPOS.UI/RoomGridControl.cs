using HotelPOS.Common;
using HotelPOS.Common.Models;
using HotelPOS.Core.Services;

namespace HotelPOS.UI;

public class RoomGridControl : UserControl
{
    private readonly IRoomService _roomService;
    private readonly IBookingService _bookingService;
    private readonly ICustomerService _customerService;

    private Panel _headerPanel = null!;
    private ComboBox _cboFloorFilter = null!;
    private ComboBox _cboTypeFilter = null!;
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

    private string? _selectedStatusFilter = null; // null = ทั้งหมด
    private System.Windows.Forms.Timer _autoRefreshTimer = null!;

    public RoomGridControl(
        IRoomService roomService,
        IBookingService bookingService,
        ICustomerService customerService)
    {
        _roomService = roomService;
        _bookingService = bookingService;
        _customerService = customerService;

        InitializeUI();
        InitializeTimer();

        Load += async (s, e) => await RefreshGridAsync();
    }

    private void InitializeTimer()
    {
        _autoRefreshTimer = new System.Windows.Forms.Timer { Interval = 30000 }; // Auto refresh check-out status every 30s
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

        // Header Panel
        _headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 130,
            BackColor = Color.White,
            Padding = new Padding(15, 10, 15, 10)
        };

        var titleLabel = new Label
        {
            Text = "ผังห้องพัก",
            Font = new Font("Segoe UI", 15F, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59),
            Location = new Point(15, 8),
            AutoSize = true
        };

        // Summary Badges
        _lblAvailableCount = CreateBadgeLabel("ว่าง: 0", Color.Honeydew, Color.ForestGreen, 15, 42);
        _lblOccupiedCount = CreateBadgeLabel("มีคนพัก: 0", Color.MistyRose, Color.Crimson, 115, 42);
        _lblCleaningCount = CreateBadgeLabel("รอทำความสะอาด: 0", Color.LightYellow, Color.DarkGoldenrod, 225, 42);
        _lblReservedCount = CreateBadgeLabel("จองล่วงหน้า: 0", Color.LightCyan, Color.DarkBlue, 380, 42);
        _lblNearCheckoutCount = CreateBadgeLabel("ใกล้ครบกำหนด: 0", Color.FromArgb(254, 243, 199), Color.DarkOrange, 520, 42);
        _lblOverdueCount = CreateBadgeLabel("เลยกำหนด: 0", Color.FromArgb(254, 226, 226), Color.DarkRed, 675, 42);

        // Filter Controls
        var lblFloor = new Label { Text = "ชั้น:", Location = new Point(810, 12), Font = new Font("Segoe UI", 10.5F, FontStyle.Bold), AutoSize = true };
        _cboFloorFilter = new ComboBox { Location = new Point(850, 8), Width = 90, Font = new Font("Segoe UI", 10.5F), DropDownStyle = ComboBoxStyle.DropDownList };
        _cboFloorFilter.SelectedIndexChanged += async (s, e) => await ApplyFilterAsync();

        var lblType = new Label { Text = "ประเภท:", Location = new Point(950, 12), Font = new Font("Segoe UI", 10.5F, FontStyle.Bold), AutoSize = true };
        _cboTypeFilter = new ComboBox { Location = new Point(1015, 8), Width = 110, Font = new Font("Segoe UI", 10.5F), DropDownStyle = ComboBoxStyle.DropDownList };
        _cboTypeFilter.SelectedIndexChanged += async (s, e) => await ApplyFilterAsync();

        _btnRefresh = new Button
        {
            Text = "รีเฟรช",
            Location = new Point(1135, 6),
            Size = new Size(85, 36),
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat
        };
        _btnRefresh.Click += async (s, e) => await RefreshGridAsync();

        _btnNewBooking = new Button
        {
            Text = "+ จองล่วงหน้า",
            Location = new Point(1135, 44),
            Size = new Size(115, 36),
            BackColor = Color.FromArgb(37, 99, 235),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold)
        };
        _btnNewBooking.Click += BtnNewBooking_Click;

        // Quick Status Filter Tabs Panel (Floor Plan Filter Buttons)
        _statusFilterPanel = new FlowLayoutPanel
        {
            Location = new Point(15, 84),
            Size = new Size(1100, 38),
            BackColor = Color.Transparent,
            WrapContents = false
        };

        BuildStatusFilterButtons();

        _headerPanel.Controls.AddRange(new Control[]
        {
            titleLabel,
            _lblAvailableCount, _lblOccupiedCount, _lblCleaningCount, _lblReservedCount,
            _lblNearCheckoutCount, _lblOverdueCount,
            lblFloor, _cboFloorFilter, lblType, _cboTypeFilter,
            _btnRefresh, _btnNewBooking, _statusFilterPanel
        });

        // Cards Container
        _cardsContainer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(15),
            BackColor = Color.FromArgb(241, 245, 249)
        };

        Controls.Add(_cardsContainer);
        Controls.Add(_headerPanel);
    }

    private void BuildStatusFilterButtons()
    {
        _statusFilterPanel.Controls.Clear();
        var filters = new (string label, string? statusValue)[]
        {
            ("ทั้งหมด", null),
            ("ห้องว่าง", "Available"),
            ("มีผู้เข้าพัก", "Occupied"),
            ("รอทำความสะอาด", "Cleaning"),
            ("จองล่วงหน้า", "Reserved"),
            ("ปิดซ่อม", "Maintenance")
        };

        foreach (var item in filters)
        {
            var btn = new Button
            {
                Text = item.label,
                AutoSize = true,
                Height = 32,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Margin = new Padding(0, 0, 8, 0),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };

            bool isSelected = _selectedStatusFilter == item.statusValue;
            btn.BackColor = isSelected ? Color.FromArgb(30, 41, 59) : Color.White;
            btn.ForeColor = isSelected ? Color.White : Color.FromArgb(60, 60, 60);

            btn.Click += async (s, e) =>
            {
                _selectedStatusFilter = item.statusValue;
                BuildStatusFilterButtons();
                await ApplyFilterAsync();
            };

            _statusFilterPanel.Controls.Add(btn);
        }
    }

    private static Label CreateBadgeLabel(string text, Color backColor, Color foreColor, int x, int y)
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
            BorderStyle = BorderStyle.FixedSingle
        };
    }

    public async Task RefreshGridAsync()
    {
        try
        {
            _allRooms = (await _roomService.GetRoomsAsync()).ToList();
            _allRoomTypes = (await _roomService.GetRoomTypesAsync(true)).ToList();

            // Populate Floor Combobox
            var currentFloor = _cboFloorFilter.SelectedItem?.ToString();
            var floors = await _roomService.GetFloorsAsync();
            _cboFloorFilter.Items.Clear();
            _cboFloorFilter.Items.Add("ทุกชั้น");
            foreach (var f in floors)
            {
                _cboFloorFilter.Items.Add($"ชั้น {f}");
            }
            _cboFloorFilter.SelectedIndex = 0;

            // Populate Room Type Combobox
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
        _cardsContainer.SuspendLayout();
        _cardsContainer.Controls.Clear();

        string? selectedFloor = _cboFloorFilter.SelectedIndex > 0
            ? _cboFloorFilter.SelectedItem?.ToString()?.Replace("ชั้น ", "")
            : null;

        int? selectedTypeId = null;
        if (_cboTypeFilter.SelectedIndex > 0 && _cboTypeFilter.SelectedIndex - 1 < _allRoomTypes.Count)
        {
            selectedTypeId = _allRoomTypes[_cboTypeFilter.SelectedIndex - 1].Id;
        }

        var filteredRooms = _allRooms.Where(r =>
            (selectedFloor == null || r.Floor == selectedFloor) &&
            (selectedTypeId == null || r.RoomTypeId == selectedTypeId) &&
            (_selectedStatusFilter == null || r.Status.ToString() == _selectedStatusFilter)
        ).ToList();

        int avail = _allRooms.Count(r => r.Status == RoomStatus.Available);
        int occ = _allRooms.Count(r => r.Status == RoomStatus.Occupied);
        int clean = _allRooms.Count(r => r.Status == RoomStatus.Cleaning);
        int res = _allRooms.Count(r => r.Status == RoomStatus.Reserved);
        int nearCheckoutCount = 0;
        int overdueCount = 0;

        var now = DateTime.Now;

        foreach (var room in filteredRooms)
        {
            var roomType = _allRoomTypes.FirstOrDefault(t => t.Id == room.RoomTypeId);
            Booking? booking = null;
            Customer? customer = null;

            if (room.Status == RoomStatus.Occupied || room.Status == RoomStatus.Reserved)
            {
                booking = await _bookingService.GetActiveBookingByRoomIdAsync(room.Id);
                if (booking != null && booking.CustomerId > 0)
                {
                    customer = await _customerService.GetCustomerByIdAsync(booking.CustomerId);
                }

                if (room.Status == RoomStatus.Occupied && booking?.CheckOutPlanned.HasValue == true)
                {
                    var span = booking.CheckOutPlanned.Value - now;
                    if (span.TotalMinutes <= 0)
                    {
                        overdueCount++;
                    }
                    else if (span.TotalMinutes <= 30)
                    {
                        nearCheckoutCount++;
                    }
                }
            }

            var card = CreateRoomTileCard(room, roomType, booking, customer, now);
            _cardsContainer.Controls.Add(card);
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
            Size = new Size(245, 195),
            Margin = new Padding(8),
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(0)
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
            if (minutesDiff <= 0)
            {
                isOverdue = true;
            }
            else if (minutesDiff <= 30)
            {
                isNearCheckout = true;
            }
        }

        switch (room.Status)
        {
            case RoomStatus.Available:
                headerColor = Color.ForestGreen;
                backColor = Color.FromArgb(242, 251, 245);
                textColor = Color.DarkGreen;
                statusText = "ว่าง";
                break;
            case RoomStatus.Occupied:
                if (isOverdue)
                {
                    headerColor = Color.FromArgb(185, 28, 28); // Bright Crimson Red
                    backColor = Color.FromArgb(254, 226, 226); // Light Red Alert
                    textColor = Color.DarkRed;
                    statusText = "🚨 เลยกำหนด!";
                }
                else if (isNearCheckout)
                {
                    headerColor = Color.FromArgb(217, 119, 6); // Amber Warning
                    backColor = Color.FromArgb(254, 243, 199); // Soft Amber Tint
                    textColor = Color.DarkGoldenrod;
                    statusText = "⚠️ ใกล้ครบกำหนด";
                }
                else
                {
                    headerColor = Color.Crimson;
                    backColor = Color.FromArgb(255, 240, 240);
                    textColor = Color.DarkRed;
                    statusText = "มีผู้เข้าพัก";
                }
                break;
            case RoomStatus.Cleaning:
                headerColor = Color.DarkGoldenrod;
                backColor = Color.FromArgb(255, 253, 230);
                textColor = Color.SaddleBrown;
                statusText = "รอทำความสะอาด";
                break;
            case RoomStatus.Reserved:
                headerColor = Color.RoyalBlue;
                backColor = Color.FromArgb(240, 244, 255);
                textColor = Color.DarkBlue;
                statusText = "จองล่วงหน้า";
                break;
            case RoomStatus.Maintenance:
            default:
                headerColor = Color.Gray;
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
            Height = 32,
            BackColor = headerColor,
            Padding = new Padding(8, 4, 8, 4)
        };

        var lblRoomNumHeader = new Label
        {
            Text = $"ห้อง {room.RoomNumber} (ชั้น {room.Floor ?? "1"}) - {statusText}",
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
            ForeColor = Color.White,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
        topHeader.Controls.Add(lblRoomNumHeader);

        // Line 1: Type & Price
        var lblTypeName = new Label
        {
            Text = $"{roomType?.Name ?? "ทั่วไป"} (฿{(roomType != null ? roomType.DailyRate.ToString("N0") : "0")}/วัน)",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(50, 50, 50),
            Location = new Point(10, 38),
            AutoSize = true
        };

        // Line 2: Guest Details (Full Details)
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
            guestDetailText = "สถานะ: รอแม่บ้านทำความสะอาด";
        }
        else if (room.Status == RoomStatus.Available)
        {
            guestDetailText = "สถานะ: ห้องว่าง พร้อมลงทะเบียนเข้าพัก";
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
            Location = new Point(10, 60),
            Size = new Size(225, 20),
            AutoEllipsis = true
        };

        // Line 3: Time Remaining / Overdue Warning Alert
        string timeAlertText = "";
        Color alertColor = textColor;

        if (room.Status == RoomStatus.Occupied && booking?.CheckOutPlanned.HasValue == true)
        {
            if (isOverdue)
            {
                timeAlertText = $"🚨 เลยกำหนดแล้ว {Math.Abs((int)minutesDiff)} นาที (ออก: {booking.CheckOutPlanned.Value:HH:mm} น.)";
                alertColor = Color.DarkRed;
            }
            else if (isNearCheckout)
            {
                timeAlertText = $"⚠️ เหลืออีก {Math.Ceiling(minutesDiff)} นาที (กำหนดออก: {booking.CheckOutPlanned.Value:HH:mm} น.)";
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
            Location = new Point(10, 82),
            Size = new Size(225, 20),
            AutoEllipsis = true
        };

        // Panel Action Buttons (Direct Interactive Buttons on Card)
        var actionPanel = new Panel
        {
            Location = new Point(8, 108),
            Size = new Size(225, 78),
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
                    Size = new Size(105, 44),
                    BackColor = Color.ForestGreen,
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                    FlatStyle = FlatStyle.Flat,
                    Cursor = Cursors.Hand
                };
                btnCheckIn.Click += async (s, e) => await OpenCheckInDialogAsync(room, roomType);

                var btnReserve = new Button
                {
                    Text = "จอง",
                    Location = new Point(114, 10),
                    Size = new Size(105, 44),
                    BackColor = Color.FromArgb(37, 99, 235),
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                    FlatStyle = FlatStyle.Flat,
                    Cursor = Cursors.Hand
                };
                btnReserve.Click += async (s, e) => await OpenBookingDialogAsync(room);

                actionPanel.Controls.Add(btnCheckIn);
                actionPanel.Controls.Add(btnReserve);
                break;

            case RoomStatus.Occupied:
                var btnCheckOut = new Button
                {
                    Text = isOverdue ? "เช็คเอาท์ (เลยกำหนด!)" : "คืนห้อง / เช็คเอาท์",
                    Location = new Point(4, 10),
                    Size = new Size(215, 44),
                    BackColor = isOverdue ? Color.FromArgb(185, 28, 28) : Color.Crimson,
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                    FlatStyle = FlatStyle.Flat,
                    Cursor = Cursors.Hand
                };
                btnCheckOut.Click += async (s, e) => await OpenCheckOutDialogAsync(room);
                actionPanel.Controls.Add(btnCheckOut);
                break;

            case RoomStatus.Cleaning:
                var btnCleanDone = new Button
                {
                    Text = "ทำความสะอาดเสร็จ",
                    Location = new Point(4, 10),
                    Size = new Size(215, 44),
                    BackColor = Color.DarkGoldenrod,
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                    FlatStyle = FlatStyle.Flat,
                    Cursor = Cursors.Hand
                };
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
                    Size = new Size(215, 44),
                    BackColor = Color.FromArgb(37, 99, 235),
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                    FlatStyle = FlatStyle.Flat,
                    Cursor = Cursors.Hand
                };
                btnCheckInReserved.Click += async (s, e) => await CheckInReservedBookingAsync(room);
                actionPanel.Controls.Add(btnCheckInReserved);
                break;

            case RoomStatus.Maintenance:
            default:
                var btnEnable = new Button
                {
                    Text = "เปิดใช้งานห้องพัก",
                    Location = new Point(4, 10),
                    Size = new Size(215, 44),
                    BackColor = Color.SteelBlue,
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                    FlatStyle = FlatStyle.Flat,
                    Cursor = Cursors.Hand
                };
                btnEnable.Click += async (s, e) =>
                {
                    await _roomService.UpdateRoomStatusAsync(room.Id, RoomStatus.Available);
                    await RefreshGridAsync();
                };
                actionPanel.Controls.Add(btnEnable);
                break;
        }

        card.Controls.Add(topHeader);
        card.Controls.Add(lblTypeName);
        card.Controls.Add(lblGuest);
        card.Controls.Add(lblTimeAlert);
        card.Controls.Add(actionPanel);

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
