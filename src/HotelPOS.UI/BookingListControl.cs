using HotelPOS.Common;
using HotelPOS.Common.Models;
using HotelPOS.Core.Services;

namespace HotelPOS.UI;

public class BookingListControl : UserControl
{
    private readonly IBookingService _bookingService;
    private readonly IRoomService _roomService;
    private readonly ICustomerService _customerService;

    private DataGridView _dgvBookings = null!;
    private ComboBox _cboStatusFilter = null!;
    private DateTimePicker _dtpStart = null!;
    private DateTimePicker _dtpEnd = null!;
    private Button _btnFilter = null!;
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
        var lblTitle = new Label { Text = "📅 รายการจองห้องพัก", Font = new Font("Segoe UI", 14F, FontStyle.Bold), Location = new Point(15, 16), AutoSize = true };

        var lblStatus = new Label { Text = "สถานะ:", Location = new Point(230, 18), Font = new Font("Segoe UI", 11F, FontStyle.Bold), AutoSize = true };
        _cboStatusFilter = new ComboBox { Location = new Point(290, 15), Width = 150, Font = new Font("Segoe UI", 11F), DropDownStyle = ComboBoxStyle.DropDownList };
        _cboStatusFilter.Items.AddRange(new object[] { "ทั้งหมด", "จองไว้ล่วงหน้า", "เช็คอินแล้ว", "เช็คเอาท์แล้ว", "ยกเลิก" });
        _cboStatusFilter.SelectedIndex = 0;

        var lblDates = new Label { Text = "ตั้งแต่วันที่:", Location = new Point(455, 18), Font = new Font("Segoe UI", 11F), AutoSize = true };
        _dtpStart = new DateTimePicker { Location = new Point(540, 15), Width = 130, Font = new Font("Segoe UI", 11F), Format = DateTimePickerFormat.Short, Value = DateTime.Now.AddDays(-7) };

        var lblTo = new Label { Text = "ถึง:", Location = new Point(680, 18), Font = new Font("Segoe UI", 11F), AutoSize = true };
        _dtpEnd = new DateTimePicker { Location = new Point(715, 15), Width = 130, Font = new Font("Segoe UI", 11F), Format = DateTimePickerFormat.Short, Value = DateTime.Now.AddDays(30) };

        _btnFilter = new Button { Text = "🔍 ค้นหา", Location = new Point(855, 13), Size = new Size(100, 36), Font = new Font("Segoe UI", 10.5F, FontStyle.Bold) };
        _btnFilter.Click += async (s, e) => await LoadBookingsAsync();

        _btnNewBooking = new Button
        {
            Text = "+ จองล่วงหน้า",
            Location = new Point(965, 13),
            Size = new Size(140, 36),
            BackColor = Color.RoyalBlue,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold)
        };
        _btnNewBooking.Click += BtnNewBooking_Click;

        // ToolTips Guide
        var tt = new ToolTip();
        tt.SetToolTip(_cboStatusFilter, "กรองดูรายการจองตามสถานะ (จองไว้ล่วงหน้า, เช็คอินแล้ว, เช็คเอาท์แล้ว, ยกเลิก)");
        tt.SetToolTip(_dtpStart, "เลือกวันที่เริ่มต้นของช่วงเวลาการจองที่ต้องการดู");
        tt.SetToolTip(_dtpEnd, "เลือกวันที่สิ้นสุดของช่วงเวลาการจองที่ต้องการดู");
        tt.SetToolTip(_btnFilter, "ค้นหารายการจองตามเงื่อนไขที่เลือก");
        tt.SetToolTip(_btnNewBooking, "เปิดหน้าต่างสร้างรายการจองห้องพักล่วงหน้าใหม่");

        topPanel.Controls.AddRange(new Control[]
        {
            lblTitle, lblStatus, _cboStatusFilter, lblDates, _dtpStart, lblTo, _dtpEnd, _btnFilter, _btnNewBooking
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
            RowTemplate = { Height = 35 }
        };
        _dgvBookings.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
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

            _dgvBookings.DataSource = _bookingsList.Select(b =>
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
        catch (Exception ex)
        {
            MessageBox.Show($"โหลดรายการจองไม่สำเร็จ: {ex.Message}", "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static string GetBookingStatusText(BookingStatus status)
    {
        return status switch
        {
            BookingStatus.Reserved => "🔵 จองไว้ล่วงหน้า",
            BookingStatus.CheckedIn => "🔴 เช็คอินแล้ว",
            BookingStatus.CheckedOut => "🟢 เช็คเอาท์แล้ว",
            BookingStatus.Cancelled => "⚪ ยกเลิก",
            BookingStatus.NoShow => "❌ ไม่มาตามนัด",
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
