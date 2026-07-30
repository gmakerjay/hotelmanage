using HotelPOS.Common;
using HotelPOS.Common.Models;
using HotelPOS.Core.Services;

namespace HotelPOS.UI;

/// <summary>
/// หน้าแสดงรายการจองห้องพัก พร้อมระบบค้นหาทันทีที่พิมพ์ (Instant Typing Search) ด้วยเบอร์โทร ชื่อ เลขห้อง หรือรหัสจอง
/// </summary>
public class BookingListControl : UserControl
{
    private readonly IBookingService _bookingService;
    private readonly IRoomService _roomService;
    private readonly ICustomerService _customerService;
    private readonly ISettingsService _settingsService;
    private readonly IUtilityBillService _utilityBillService;

    private DataGridView _dgvBookings = null!;
    private TextBox _txtSearch = null!;
    private ComboBox _cboStatusFilter = null!;
    private DateTimePicker _dtpStart = null!;
    private DateTimePicker _dtpEnd = null!;
    private Button _btnNewBooking = null!;

    private List<Booking> _bookingsList = new();
    private List<Room> _roomsList = new();
    private List<Customer> _customersList = new();
    private GridPaginationPanel _pgPanel = null!;

    public BookingListControl(
        IBookingService bookingService,
        IRoomService roomService,
        ICustomerService customerService,
        ISettingsService settingsService,
        IUtilityBillService utilityBillService)
    {
        _bookingService = bookingService;
        _roomService = roomService;
        _customerService = customerService;
        _settingsService = settingsService;
        _utilityBillService = utilityBillService;

        InitializeUI();
        Load += async (s, e) => await LoadBookingsAsync();
    }

    private void InitializeUI()
    {
        Dock = DockStyle.Fill;
        Font = new Font("Segoe UI", 11F, FontStyle.Regular);

        var topPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Padding = new Padding(15, 12, 15, 12),
            BackColor = Color.White,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        var lblTitle = new Label { Text = "รายการจองห้องพัก", Font = new Font("Segoe UI", 14F, FontStyle.Bold), AutoSize = true, Margin = new Padding(0, 5, 20, 5) };

        var lblSearch = new Label { Text = "ค้นหา:", Font = new Font("Segoe UI", 10.5F, FontStyle.Bold), AutoSize = true, Margin = new Padding(5, 10, 5, 5) };
        _txtSearch = new TextBox
        {
            Width = 220,
            Font = new Font("Segoe UI", 11F),
            PlaceholderText = "พิมพ์เบอร์โทร / ชื่อ / เลขห้อง / รหัสจอง...",
            Margin = new Padding(5, 6, 5, 5)
        };
        _txtSearch.TextChanged += (s, e) => ApplyFilter();

        var lblStatus = new Label { Text = "สถานะ:", Font = new Font("Segoe UI", 10.5F, FontStyle.Bold), AutoSize = true, Margin = new Padding(15, 10, 5, 5) };
        _cboStatusFilter = new ComboBox { Width = 140, Font = new Font("Segoe UI", 11F), DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(5, 6, 5, 5) };
        _cboStatusFilter.Items.AddRange(new object[] { "ทั้งหมด", "จองไว้ล่วงหน้า", "เช็คอินแล้ว", "เช็คเอาท์แล้ว", "ยกเลิก" });
        _cboStatusFilter.SelectedIndex = 0;
        _cboStatusFilter.SelectedIndexChanged += async (s, e) => await LoadBookingsAsync();

        var lblDates = new Label { Text = "ตั้งแต่วันที่:", Font = new Font("Segoe UI", 10F), AutoSize = true, Margin = new Padding(15, 10, 5, 5) };
        _dtpStart = new DateTimePicker { Width = 115, Font = new Font("Segoe UI", 10.5F), Format = DateTimePickerFormat.Short, Value = DateTime.Now.AddDays(-15), Margin = new Padding(5, 6, 5, 5) };
        _dtpStart.ValueChanged += async (s, e) => await LoadBookingsAsync();

        var lblTo = new Label { Text = "ถึง:", Font = new Font("Segoe UI", 10F), AutoSize = true, Margin = new Padding(10, 10, 5, 5) };
        _dtpEnd = new DateTimePicker { Width = 115, Font = new Font("Segoe UI", 10.5F), Format = DateTimePickerFormat.Short, Value = DateTime.Now.AddDays(30), Margin = new Padding(5, 6, 5, 5) };
        _dtpEnd.ValueChanged += async (s, e) => await LoadBookingsAsync();

        var btnRefresh = new Button
        {
            Text = "รีเฟรช",
            Size = new Size(100, 36),
            BackColor = Color.FromArgb(241, 245, 249),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(15, 4, 5, 5)
        };
        btnRefresh.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        btnRefresh.Click += async (s, e) => {
            _txtSearch.Clear();
            _cboStatusFilter.SelectedIndex = 0;
            _dtpStart.Value = DateTime.Now.AddDays(-15);
            _dtpEnd.Value = DateTime.Now.AddDays(30);
            await LoadBookingsAsync();
        };

        _btnNewBooking = new Button
        {
            Text = "+ จองล่วงหน้า",
            Size = new Size(130, 36),
            BackColor = Color.RoyalBlue,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(5, 4, 5, 5)
        };
        _btnNewBooking.FlatAppearance.BorderSize = 0;
        _btnNewBooking.Click += BtnNewBooking_Click;

        topPanel.Controls.AddRange(new Control[]
        {
            lblTitle, lblSearch, _txtSearch, lblStatus, _cboStatusFilter, lblDates, _dtpStart, lblTo, _dtpEnd, btnRefresh, _btnNewBooking
        });

        _dgvBookings = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AllowUserToAddRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
            RowTemplate = { Height = 35 },
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            GridColor = Color.FromArgb(226, 232, 240)
        };
        _dgvBookings.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            BackColor = Color.FromArgb(30, 41, 59),
            ForeColor = Color.White,
            SelectionBackColor = Color.FromArgb(30, 41, 59),
            SelectionForeColor = Color.White,
            WrapMode = DataGridViewTriState.True
        };
        _dgvBookings.EnableHeadersVisualStyles = false;
        _dgvBookings.DefaultCellStyle.Font = new Font("Segoe UI", 10.5F);
        _dgvBookings.DataBindingComplete += (s, e) =>
        {
            foreach (DataGridViewColumn col in _dgvBookings.Columns)
            {
                col.MinimumWidth = 90;
            }
        };
        _dgvBookings.CellDoubleClick += (s, e) =>
        {
            if (e.RowIndex >= 0)
            {
                BtnPrintReceipt_Click(s, e);
            }
        };

        var cms = new ContextMenuStrip { Font = new Font("Segoe UI", 11F) };
        var itemCheckIn = cms.Items.Add("เช็คอินเข้าพัก");
        itemCheckIn.Click += BtnCheckIn_Click;
        var itemCheckOut = cms.Items.Add("เช็คเอาท์ / คืนห้อง");
        itemCheckOut.Click += BtnCheckOut_Click;
        var itemPrint = cms.Items.Add("พิมพ์ใบเสร็จ (Receipt & Invoice)");
        itemPrint.Click += BtnPrintReceipt_Click;
        var itemCancel = cms.Items.Add("ยกเลิกการจอง");
        itemCancel.Click += BtnCancel_Click;
        _dgvBookings.ContextMenuStrip = cms;

        _pgPanel = new GridPaginationPanel(() => ApplyFilter());
        Controls.Add(_pgPanel);
        Controls.Add(_dgvBookings);
        _dgvBookings.BringToFront();
        Controls.Add(topPanel);
    }

    public async Task LoadBookingsAsync()
    {
        try
        {
            BookingStatus? status = _cboStatusFilter.SelectedIndex switch
            {
                1 => BookingStatus.Reserved,
                2 => BookingStatus.CheckedIn,
                3 => BookingStatus.CheckedOut,
                4 => BookingStatus.Cancelled,
                _ => null
            };

            _roomsList = (await _roomService.GetRoomsAsync()).ToList();
            _customersList = (await _customerService.GetCustomersAsync()).ToList();
            _bookingsList = (await _bookingService.GetBookingsAsync(_dtpStart.Value.Date, _dtpEnd.Value.Date.AddDays(1).AddSeconds(-1), status)).ToList();

            _pgPanel.Reset();
            ApplyFilter();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"โหลดรายการจองไม่สำเร็จ: {ex.Message}", "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ApplyFilter()
    {
        string query = _txtSearch.Text.Trim();

        var filtered = _bookingsList.Where(b =>
        {
            if (string.IsNullOrWhiteSpace(query)) return true;

            var room = _roomsList.FirstOrDefault(r => r.Id == b.RoomId);
            var cust = _customersList.FirstOrDefault(c => c.Id == b.CustomerId);

            bool matchCode = b.BookingCode.Contains(query, StringComparison.OrdinalIgnoreCase);
            bool matchRoom = room != null && room.RoomNumber.Contains(query, StringComparison.OrdinalIgnoreCase);
            bool matchName = cust != null && cust.FullName.Contains(query, StringComparison.OrdinalIgnoreCase);
            bool matchPhone = cust != null && !string.IsNullOrEmpty(cust.Phone) && cust.Phone.Contains(query, StringComparison.OrdinalIgnoreCase);

            return matchCode || matchRoom || matchName || matchPhone;
        }).ToList();

        _pgPanel.UpdateState(filtered.Count);
        var pageData = _pgPanel.GetPageData(filtered).ToList();

        _dgvBookings.DataSource = pageData.Select(b =>
        {
            var room = _roomsList.FirstOrDefault(r => r.Id == b.RoomId);
            var cust = _customersList.FirstOrDefault(c => c.Id == b.CustomerId);
            return new
            {
                b.Id,
                รหัสการจอง = b.BookingCode,
                ห้องพัก = room != null ? $"ห้อง {room.RoomNumber}" : "-",
                ผู้เข้าพัก = cust?.FullName ?? "-",
                เบอร์โทร = cust?.Phone ?? "-",
                กำหนดเช็คอิน = b.CheckInPlanned.ToString("dd/MM/yyyy HH:mm"),
                กำหนดเช็คเอาท์ = b.CheckOutPlanned?.ToString("dd/MM/yyyy HH:mm") ?? "-",
                ราคาตกลง = b.AgreedRate,
                สถานะ = GetBookingStatusText(b.Status)
            };
        }).ToList();
    }

    private static string GetBookingStatusText(BookingStatus status)
    {
        return status switch
        {
            BookingStatus.Reserved => "จองไว้ล่วงหน้า",
            BookingStatus.CheckedIn => "เช็คอินแล้ว",
            BookingStatus.CheckedOut => "เช็คเอาท์แล้ว",
            BookingStatus.Cancelled => "ยกเลิก",
            BookingStatus.NoShow => "ไม่มาตามนัด",
            _ => "-"
        };
    }

    private async void BtnNewBooking_Click(object? sender, EventArgs e)
    {
        using var form = new BookingForm(_roomService, _bookingService, _customerService);
        if (form.ShowDialog() == DialogResult.OK)
        {
            await LoadBookingsAsync();
        }
    }

    private async void BtnCheckIn_Click(object? sender, EventArgs e)
    {
        if (_dgvBookings.SelectedRows.Count == 0) return;
        var bookingId = Convert.ToInt32(_dgvBookings.SelectedRows[0].Cells["Id"].Value);
        var booking = _bookingsList.FirstOrDefault(b => b.Id == bookingId);
        if (booking == null) return;

        if (booking.Status != BookingStatus.Reserved)
        {
            MessageBox.Show("สามารถเช็คอินได้เฉพาะรายการที่อยู่ในสถานะ 'จองไว้ล่วงหน้า' เท่านั้น", "แจ้งเตือน", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            await _bookingService.CheckInExistingBookingAsync(booking.Id);
            MessageBox.Show("เช็คอินเรียบร้อยแล้ว", "สำเร็จ", MessageBoxButtons.OK, MessageBoxIcon.Information);
            await LoadBookingsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"เช็คอินไม่สำเร็จ: {ex.Message}", "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void BtnCheckOut_Click(object? sender, EventArgs e)
    {
        if (_dgvBookings.SelectedRows.Count == 0) return;
        var bookingId = Convert.ToInt32(_dgvBookings.SelectedRows[0].Cells["Id"].Value);
        var booking = _bookingsList.FirstOrDefault(b => b.Id == bookingId);
        if (booking == null) return;

        if (booking.Status != BookingStatus.CheckedIn)
        {
            MessageBox.Show("สามารถเช็คเอาท์ได้เฉพาะรายการที่อยู่ในสถานะ 'เช็คอินแล้ว' เท่านั้น", "แจ้งเตือน", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var room = _roomsList.FirstOrDefault(r => r.Id == booking.RoomId);
        var customer = _customersList.FirstOrDefault(c => c.Id == booking.CustomerId);
        var folio = await _bookingService.GetFolioByBookingIdAsync(booking.Id);

        using var form = new CheckOutForm(room ?? new Room { RoomNumber = "?" }, booking, customer, folio, _bookingService, _utilityBillService, _settingsService);
        if (form.ShowDialog() == DialogResult.OK)
        {
            await LoadBookingsAsync();
        }
    }

    private async void BtnPrintReceipt_Click(object? sender, EventArgs e)
    {
        if (_dgvBookings.SelectedRows.Count == 0) return;
        var bookingId = Convert.ToInt32(_dgvBookings.SelectedRows[0].Cells["Id"].Value);
        var booking = _bookingsList.FirstOrDefault(b => b.Id == bookingId);
        if (booking == null) return;

        var room = _roomsList.FirstOrDefault(r => r.Id == booking.RoomId) ?? new Room { RoomNumber = "?" };
        var customer = _customersList.FirstOrDefault(c => c.Id == booking.CustomerId);
        var folio = await _bookingService.GetFolioByBookingIdAsync(booking.Id);

        SystemSettingsDto? settings = null;
        if (_settingsService != null)
        {
            try
            {
                settings = await _settingsService.GetAllSettingsAsync();
            }
            catch { }
        }

        UtilityBill? utilityBill = null;
        if (_utilityBillService != null && booking.RatePlan == RatePlanType.Monthly)
        {
            try
            {
                var checkoutDate = booking.CheckOutActual ?? DateTime.Now;
                string billingMonth = checkoutDate.ToString("yyyy-MM");
                var bills = await _utilityBillService.GetBillsByMonthAsync(billingMonth);
                utilityBill = bills.FirstOrDefault(b => b.RoomId == booking.RoomId);
            }
            catch { }
        }

        var printer = new HotelPOS.Printing.ReceiptInvoicePrinter(
            settings?.ShopName ?? "ชื่อร้าน/ที่พักของคุณ",
            settings?.ShopAddress ?? "123/45 ถนนสุขุมวิท กรุงเทพฯ",
            settings?.ShopPhone ?? "02-123-4567",
            settings?.ShopTaxId ?? "0105560000000",
            booking,
            room,
            customer,
            folio,
            "admin",
            settings,
            utilityBill
        );
        printer.ShowPrintPreview();
    }

    private async void BtnCancel_Click(object? sender, EventArgs e)
    {
        if (_dgvBookings.SelectedRows.Count == 0) return;
        var bookingId = Convert.ToInt32(_dgvBookings.SelectedRows[0].Cells["Id"].Value);
        var booking = _bookingsList.FirstOrDefault(b => b.Id == bookingId);
        if (booking == null) return;

        if (MessageBox.Show($"ยืนยันการยกเลิกการจอง {booking.BookingCode}?", "ยืนยัน", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            await _bookingService.CancelBookingAsync(booking.Id, "ผู้ใช้ยกเลิกการจอง");
            await LoadBookingsAsync();
        }
    }
}
