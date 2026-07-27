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

    private DataGridView _dgvBookings = null!;
    private TextBox _txtSearch = null!;
    private ComboBox _cboStatusFilter = null!;
    private DateTimePicker _dtpStart = null!;
    private DateTimePicker _dtpEnd = null!;
    private Button _btnNewBooking = null!;

    private List<Booking> _bookingsList = new();
    private List<Room> _roomsList = new();
    private List<Customer> _customersList = new();

    public BookingListControl(
        IBookingService bookingService,
        IRoomService roomService,
        ICustomerService customerService)
    {
        _bookingService = bookingService;
        _roomService = roomService;
        _customerService = customerService;

        InitializeUI();
        Load += async (s, e) => await LoadBookingsAsync();
    }

    private void InitializeUI()
    {
        Dock = DockStyle.Fill;
        Font = new Font("Segoe UI", 11F, FontStyle.Regular);

        var topPanel = new Panel { Dock = DockStyle.Top, Height = 65, Padding = new Padding(15, 12, 15, 12), BackColor = Color.White };
        var lblTitle = new Label { Text = "รายการจองห้องพัก", Font = new Font("Segoe UI", 14F, FontStyle.Bold), Location = new Point(15, 16), AutoSize = true };

        var lblSearch = new Label { Text = "ค้นหา:", Font = new Font("Segoe UI", 10.5F, FontStyle.Bold), Location = new Point(190, 18), AutoSize = true };
        _txtSearch = new TextBox
        {
            Location = new Point(245, 14),
            Width = 220,
            Font = new Font("Segoe UI", 11F),
            PlaceholderText = "พิมพ์เบอร์โทร / ชื่อ / เลขห้อง / รหัสจอง..."
        };
        _txtSearch.TextChanged += (s, e) => ApplyFilter();

        var lblStatus = new Label { Text = "สถานะ:", Location = new Point(480, 18), Font = new Font("Segoe UI", 10.5F, FontStyle.Bold), AutoSize = true };
        _cboStatusFilter = new ComboBox { Location = new Point(540, 14), Width = 140, Font = new Font("Segoe UI", 11F), DropDownStyle = ComboBoxStyle.DropDownList };
        _cboStatusFilter.Items.AddRange(new object[] { "ทั้งหมด", "จองไว้ล่วงหน้า", "เช็คอินแล้ว", "เช็คเอาท์แล้ว", "ยกเลิก" });
        _cboStatusFilter.SelectedIndex = 0;
        _cboStatusFilter.SelectedIndexChanged += async (s, e) => await LoadBookingsAsync();

        var lblDates = new Label { Text = "ตั้งแต่วันที่:", Location = new Point(695, 18), Font = new Font("Segoe UI", 10F), AutoSize = true };
        _dtpStart = new DateTimePicker { Location = new Point(770, 14), Width = 115, Font = new Font("Segoe UI", 10.5F), Format = DateTimePickerFormat.Short, Value = DateTime.Now.AddDays(-15) };
        _dtpStart.ValueChanged += async (s, e) => await LoadBookingsAsync();

        var lblTo = new Label { Text = "ถึง:", Location = new Point(895, 18), Font = new Font("Segoe UI", 10F), AutoSize = true };
        _dtpEnd = new DateTimePicker { Location = new Point(925, 14), Width = 115, Font = new Font("Segoe UI", 10.5F), Format = DateTimePickerFormat.Short, Value = DateTime.Now.AddDays(30) };
        _dtpEnd.ValueChanged += async (s, e) => await LoadBookingsAsync();

        _btnNewBooking = new Button
        {
            Text = "+ จองล่วงหน้า",
            Location = new Point(1055, 13),
            Size = new Size(130, 36),
            BackColor = Color.RoyalBlue,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnNewBooking.FlatAppearance.BorderSize = 0;
        _btnNewBooking.Click += BtnNewBooking_Click;

        topPanel.Controls.AddRange(new Control[]
        {
            lblTitle, lblSearch, _txtSearch, lblStatus, _cboStatusFilter, lblDates, _dtpStart, lblTo, _dtpEnd, _btnNewBooking
        });

        _dgvBookings = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AllowUserToAddRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            ColumnHeadersHeight = 38,
            RowTemplate = { Height = 35 },
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            GridColor = Color.FromArgb(226, 232, 240)
        };
        _dgvBookings.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            BackColor = Color.FromArgb(30, 41, 59),
            ForeColor = Color.White
        };
        _dgvBookings.EnableHeadersVisualStyles = false;
        _dgvBookings.DefaultCellStyle.Font = new Font("Segoe UI", 10.5F);

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

        Controls.Add(_dgvBookings);
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

        _dgvBookings.DataSource = filtered.Select(b =>
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

        using var form = new CheckOutForm(room ?? new Room { RoomNumber = "?" }, booking, customer, folio, _bookingService);
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

        var printer = new HotelPOS.Printing.ReceiptInvoicePrinter(
            "โรงแรม HotelPOS TH",
            "123/45 ถนนสุขุมวิท กรุงเทพฯ",
            "02-123-4567",
            "0105560000000",
            booking,
            room,
            customer,
            folio,
            "admin"
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
