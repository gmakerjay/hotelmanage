using HotelPOS.Common;
using HotelPOS.Common.Models;
using HotelPOS.Core.Services;

namespace HotelPOS.UI;

public class BookingForm : Form
{
    private readonly IRoomService _roomService;
    private readonly IBookingService _bookingService;
    private readonly ICustomerService _customerService;
    private readonly Room? _selectedRoom;

    private ComboBox _cboRooms = null!;
    private TextBox _txtFullName = null!;
    private TextBox _txtPhone = null!;
    private TextBox _txtIdCard = null!;
    private ComboBox _cboRatePlan = null!;
    private NumericUpDown _numAgreedRate = null!;
    private DateTimePicker _dtpCheckIn = null!;
    private DateTimePicker _dtpCheckOut = null!;
    private TextBox _txtNotes = null!;
    private Button _btnSave = null!;
    private Button _btnCancel = null!;

    private ComboBox _cboCustomerSelect = null!;
    private List<Customer> _allCustomers = new();

    private List<Room> _roomsList = new();
    private List<RoomType> _roomTypesList = new();

    public BookingForm(
        IRoomService roomService,
        IBookingService bookingService,
        ICustomerService customerService,
        Room? selectedRoom = null)
    {
        _roomService = roomService;
        _bookingService = bookingService;
        _customerService = customerService;
        _selectedRoom = selectedRoom;

        InitializeUI();
        Load += BookingForm_Load;
    }

    private void InitializeUI()
    {
        Text = "จองห้องพักล่วงหน้า (Advance Reservation)";
        Size = new Size(580, 620);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Font = new Font("Segoe UI", 11F, FontStyle.Regular);

        var lblHeader = new Label
        {
            Text = "สร้างรายการจองห้องพักล่วงหน้า",
            Font = new Font("Segoe UI", 14F, FontStyle.Bold),
            ForeColor = Color.DarkSlateBlue,
            Location = new Point(20, 15),
            AutoSize = true
        };

        var lblRoom = new Label { Text = "เลือกห้องพัก *:", Location = new Point(20, 55), Font = new Font("Segoe UI", 11F, FontStyle.Bold), AutoSize = true };
        _cboRooms = new ComboBox { Location = new Point(170, 52), Width = 360, Font = new Font("Segoe UI", 11F), DropDownStyle = ComboBoxStyle.DropDownList };
        _cboRooms.SelectedIndexChanged += CboRooms_SelectedIndexChanged;

        var lblCustomerInfo = new Label { Text = "ข้อมูลผู้จอง:", Font = new Font("Segoe UI", 11.5F, FontStyle.Bold), Location = new Point(20, 95), AutoSize = true };

        var lblSelectCust = new Label { Text = "เลือกประวัติผู้เข้าพัก:", Location = new Point(20, 130), Font = new Font("Segoe UI", 11F), AutoSize = true };
        _cboCustomerSelect = new ComboBox { Location = new Point(170, 127), Width = 360, Font = new Font("Segoe UI", 11F), DropDownStyle = ComboBoxStyle.DropDownList };
        _cboCustomerSelect.SelectedIndexChanged += CboCustomerSelect_SelectedIndexChanged;

        var lblFullName = new Label { Text = "ชื่อ-นามสกุล *:", Location = new Point(20, 170), Font = new Font("Segoe UI", 11F, FontStyle.Bold), AutoSize = true };
        _txtFullName = new TextBox { Location = new Point(170, 167), Width = 360, Font = new Font("Segoe UI", 11F) };

        var lblPhone = new Label { Text = "เบอร์โทรศัพท์:", Location = new Point(20, 210), Font = new Font("Segoe UI", 11F), AutoSize = true };
        _txtPhone = new TextBox { Location = new Point(170, 207), Width = 360, Font = new Font("Segoe UI", 11F) };
        _txtPhone.Leave += TxtPhone_Leave;

        var lblIdCard = new Label { Text = "เลขบัตร/พาสปอร์ต:", Location = new Point(20, 250), Font = new Font("Segoe UI", 11F), AutoSize = true };
        _txtIdCard = new TextBox { Location = new Point(170, 247), Width = 360, Font = new Font("Segoe UI", 11F) };

        var lblBookingDetails = new Label { Text = "กำหนดการเข้าพัก:", Font = new Font("Segoe UI", 11.5F, FontStyle.Bold), Location = new Point(20, 290), AutoSize = true };

        var lblCheckIn = new Label { Text = "วันที่/เวลา เช็คอิน:", Location = new Point(20, 325), Font = new Font("Segoe UI", 11F), AutoSize = true };
        _dtpCheckIn = new DateTimePicker
        {
            Location = new Point(170, 322),
            Width = 230,
            Font = new Font("Segoe UI", 11F),
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "dd/MM/yyyy HH:mm",
            Value = DateTime.Now.AddDays(1)
        };

        var lblCheckOut = new Label { Text = "วันที่/เวลา เช็คเอาท์:", Location = new Point(20, 365), Font = new Font("Segoe UI", 11F), AutoSize = true };
        _dtpCheckOut = new DateTimePicker
        {
            Location = new Point(170, 362),
            Width = 230,
            Font = new Font("Segoe UI", 11F),
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "dd/MM/yyyy HH:mm",
            Value = DateTime.Now.AddDays(2)
        };

        var lblRatePlan = new Label { Text = "ประเภทราคา:", Location = new Point(20, 405), Font = new Font("Segoe UI", 11F), AutoSize = true };
        _cboRatePlan = new ComboBox { Location = new Point(170, 402), Width = 180, Font = new Font("Segoe UI", 11F), DropDownStyle = ComboBoxStyle.DropDownList };
        _cboRatePlan.SelectedIndexChanged += CboRatePlan_SelectedIndexChanged;

        var lblRate = new Label { Text = "ราคาตกลง (บาท):", Location = new Point(20, 445), Font = new Font("Segoe UI", 11F), AutoSize = true };
        _numAgreedRate = new NumericUpDown
        {
            Location = new Point(170, 442),
            Width = 180,
            Font = new Font("Segoe UI", 11F),
            Maximum = 1000000,
            DecimalPlaces = 2,
            Value = 500
        };

        var lblNotes = new Label { Text = "หมายเหตุ:", Location = new Point(20, 485), Font = new Font("Segoe UI", 11F), AutoSize = true };
        _txtNotes = new TextBox { Location = new Point(170, 482), Width = 360, Font = new Font("Segoe UI", 11F), Multiline = true, Height = 45 };

        _btnSave = new Button
        {
            Text = "บันทึกการจอง",
            BackColor = Color.RoyalBlue,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 11.5F, FontStyle.Bold),
            Location = new Point(250, 535),
            Size = new Size(160, 42)
        };
        _btnSave.Click += BtnSave_Click;

        _btnCancel = new Button
        {
            Text = "ยกเลิก",
            DialogResult = DialogResult.Cancel,
            Font = new Font("Segoe UI", 11F),
            Location = new Point(420, 535),
            Size = new Size(110, 42)
        };

        // ToolTips Onboarding Guide
        var tt = new AppToolTip();
        tt.SetToolTip(_cboRooms, "เลือกห้องพักที่ต้องการจองล่วงหน้า");
        tt.SetToolTip(_cboCustomerSelect, "ค้นหาและเลือกผู้เข้าพักจากฐานข้อมูลทะเบียน");
        tt.SetToolTip(_txtFullName, "ชื่อ-นามสกุลของผู้จองห้องพัก");
        tt.SetToolTip(_txtPhone, "เบอร์โทรศัพท์ติดต่อของผู้จอง");
        tt.SetToolTip(_dtpCheckIn, "กำหนดวันเวลาที่คาดว่าจะมาเช็คอินเข้าพัก");
        tt.SetToolTip(_dtpCheckOut, "กำหนดวันเวลาที่คาดว่าจะเช็คเอาท์คืนห้อง");
        tt.SetToolTip(_cboRatePlan, "ประเภทรูปแบบราคา");
        tt.SetToolTip(_numAgreedRate, "ราคาค่าห้องตกลงล่วงหน้าต่อหน่วย");
        tt.SetToolTip(_btnSave, "บันทึกสร้างรายการจองห้องพักล่วงหน้าลงระบบ");
        tt.SetToolTip(_btnCancel, "ยกเลิกการจองและปิดหน้าต่าง");

        Controls.AddRange(new Control[]
        {
            lblHeader, lblRoom, _cboRooms,
            lblCustomerInfo, lblSelectCust, _cboCustomerSelect, lblFullName, _txtFullName, lblPhone, _txtPhone, lblIdCard, _txtIdCard,
            lblBookingDetails, lblCheckIn, _dtpCheckIn, lblCheckOut, _dtpCheckOut,
            lblRatePlan, _cboRatePlan, lblRate, _numAgreedRate,
            lblNotes, _txtNotes, _btnSave, _btnCancel
        });

        AcceptButton = _btnSave;
        CancelButton = _btnCancel;
    }

    private async void BookingForm_Load(object? sender, EventArgs e)
    {
        try
        {
            var rooms = await _roomService.GetRoomsAsync();
            var types = await _roomService.GetRoomTypesAsync();
            var customers = await _customerService.GetCustomersAsync();

            // แสดงเฉพาะห้องพักที่อยู่ในสถานะว่างพร้อมใช้งาน (Available) หรือห้องที่เลือกมาจากผังห้อง
            var availableRooms = rooms
                .Where(r => r.Status == RoomStatus.Available || (_selectedRoom != null && r.Id == _selectedRoom.Id && r.Status == RoomStatus.Available))
                .OrderBy(r => r.RoomNumber)
                .ToList();

            _roomsList = availableRooms;
            _roomTypesList = types.ToList();
            _allCustomers = customers.ToList();

            _cboRooms.Items.Clear();
            int selectedIndex = 0;

            if (_roomsList.Count == 0)
            {
                _cboRooms.Items.Add("(ไม่มีห้องว่างที่พร้อมสำหรับการจองในขณะนี้)");
                _btnSave.Enabled = false;
            }
            else
            {
                _btnSave.Enabled = true;
                for (int i = 0; i < _roomsList.Count; i++)
                {
                    var r = _roomsList[i];
                    var t = _roomTypesList.FirstOrDefault(x => x.Id == r.RoomTypeId);
                    _cboRooms.Items.Add($"ห้อง {r.RoomNumber} - {t?.Name ?? "ทั่วไป"} (ชั้น {r.Floor ?? "-"}) [ว่างพร้อมใช้งาน]");
                    if (_selectedRoom != null && r.Id == _selectedRoom.Id)
                    {
                        selectedIndex = i;
                    }
                }
            }

            if (_cboRooms.Items.Count > 0)
            {
                _cboRooms.SelectedIndex = selectedIndex;
            }

            _cboCustomerSelect.Items.Clear();
            _cboCustomerSelect.Items.Add("[-- พิมพ์ข้อมูลเอง / ลงทะเบียนใหม่ --]");
            foreach (var c in _allCustomers)
            {
                _cboCustomerSelect.Items.Add($"{c.FullName} ({c.Phone ?? "ไม่มีเบอร์"})");
            }
            _cboCustomerSelect.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"โหลดข้อมูลไม่สำเร็จ: {ex.Message}", "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void CboCustomerSelect_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_cboCustomerSelect.SelectedIndex <= 0)
        {
            _txtFullName.Text = "";
            _txtPhone.Text = "";
            _txtIdCard.Text = "";
            _txtFullName.ReadOnly = false;
            _txtPhone.ReadOnly = false;
            _txtIdCard.ReadOnly = false;
        }
        else
        {
            var c = _allCustomers[_cboCustomerSelect.SelectedIndex - 1];
            _txtFullName.Text = c.FullName;
            _txtPhone.Text = c.Phone ?? "";
            _txtIdCard.Text = c.IdCardOrPassport ?? "";
            _txtFullName.ReadOnly = true;
            _txtPhone.ReadOnly = true;
            _txtIdCard.ReadOnly = true;
        }
    }

    private void CboRooms_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_cboRooms.SelectedIndex < 0 || _cboRooms.SelectedIndex >= _roomsList.Count) return;
        var r = _roomsList[_cboRooms.SelectedIndex];
        var t = _roomTypesList.FirstOrDefault(x => x.Id == r.RoomTypeId);
        if (t != null)
        {
            _cboRatePlan.SelectedIndexChanged -= CboRatePlan_SelectedIndexChanged;
            _cboRatePlan.Items.Clear();

            if (t.MonthlyRate > 0)
            {
                _cboRatePlan.Items.Add(new RatePlanItem { RatePlan = RatePlanType.Monthly, DisplayName = "รายเดือน (Monthly)" });
            }
            if (t.DailyRate > 0)
            {
                _cboRatePlan.Items.Add(new RatePlanItem { RatePlan = RatePlanType.Daily, DisplayName = "รายวัน (Daily)" });
            }
            if (t.HourlyRate > 0)
            {
                _cboRatePlan.Items.Add(new RatePlanItem { RatePlan = RatePlanType.Hourly, DisplayName = "รายชั่วโมง (Hourly)" });
            }

            if (_cboRatePlan.Items.Count == 0)
            {
                _cboRatePlan.Items.Add(new RatePlanItem { RatePlan = RatePlanType.Daily, DisplayName = "รายวัน (Daily)" });
            }

            if (_cboRatePlan.Items.Count > 0)
            {
                _cboRatePlan.SelectedIndex = 0;
            }
            _cboRatePlan.SelectedIndexChanged += CboRatePlan_SelectedIndexChanged;

            // Trigger update calculations
            CboRatePlan_SelectedIndexChanged(null, EventArgs.Empty);
        }
    }

    private void CboRatePlan_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_cboRooms.SelectedIndex < 0 || _cboRooms.SelectedIndex >= _roomsList.Count) return;
        var r = _roomsList[_cboRooms.SelectedIndex];
        var t = _roomTypesList.FirstOrDefault(x => x.Id == r.RoomTypeId);
        if (t == null) return;

        var item = _cboRatePlan.SelectedItem as RatePlanItem;
        if (item == null) return;

        switch (item.RatePlan)
        {
            case RatePlanType.Daily:
                _numAgreedRate.Value = t.DailyRate > 0 ? t.DailyRate : 500;
                _dtpCheckOut.Value = _dtpCheckIn.Value.AddDays(1);
                break;
            case RatePlanType.Hourly:
                _numAgreedRate.Value = t.HourlyRate > 0 ? t.HourlyRate : 150;
                _dtpCheckOut.Value = _dtpCheckIn.Value.AddHours(3);
                break;
            case RatePlanType.Monthly:
                _numAgreedRate.Value = t.MonthlyRate > 0 ? t.MonthlyRate : 5000;
                _dtpCheckOut.Value = _dtpCheckIn.Value.AddMonths(1);
                break;
        }
    }

    private async void TxtPhone_Leave(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_txtPhone.Text)) return;
        try
        {
            var customer = await _customerService.GetCustomerByPhoneOrIdCardAsync(_txtPhone.Text);
            if (customer != null && string.IsNullOrWhiteSpace(_txtFullName.Text))
            {
                _txtFullName.Text = customer.FullName;
                if (!string.IsNullOrWhiteSpace(customer.IdCardOrPassport))
                {
                    _txtIdCard.Text = customer.IdCardOrPassport;
                }
            }
        }
        catch { }
    }

    private async void BtnSave_Click(object? sender, EventArgs e)
    {
        if (_cboRooms.SelectedIndex < 0 || _cboRooms.SelectedIndex >= _roomsList.Count)
        {
            MessageBox.Show("กรุณาเลือกห้องพัก", "ข้อมูลไม่ครบถ้วน", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(_txtFullName.Text))
        {
            MessageBox.Show("กรุณากรอกชื่อ-นามสกุลผู้จอง", "ข้อมูลไม่ครบถ้วน", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtFullName.Focus();
            return;
        }
        if (_dtpCheckOut.Value <= _dtpCheckIn.Value)
        {
            MessageBox.Show("วันที่เช็คเอาท์ต้องมากกว่าวันที่เช็คอิน", "ข้อมูลไม่ถูกต้อง", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            _btnSave.Enabled = false;
            var room = _roomsList[_cboRooms.SelectedIndex];

            var customer = new Customer
            {
                FullName = _txtFullName.Text.Trim(),
                Phone = _txtPhone.Text.Trim(),
                IdCardOrPassport = _txtIdCard.Text.Trim()
            };

            var existingCust = await _customerService.GetCustomerByPhoneOrIdCardAsync(customer.Phone);
            if (existingCust != null)
            {
                customer.Id = existingCust.Id;
            }

            var selectedRateItem = _cboRatePlan.SelectedItem as RatePlanItem;
            var ratePlan = selectedRateItem?.RatePlan ?? RatePlanType.Daily;

            await _bookingService.CreateReservationAsync(
                room.Id,
                customer,
                ratePlan,
                _numAgreedRate.Value,
                _dtpCheckIn.Value,
                _dtpCheckOut.Value,
                _txtNotes.Text
            );

            MessageBox.Show($"บันทึกการจองห้อง {room.RoomNumber} เรียบร้อยแล้ว", "สำเร็จ", MessageBoxButtons.OK, MessageBoxIcon.Information);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"เกิดข้อผิดพลาดในการสร้างการจอง: {ex.Message}", "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _btnSave.Enabled = true;
        }
    }

    private class RatePlanItem
    {
        public RatePlanType RatePlan { get; set; }
        public string DisplayName { get; set; } = "";
        public override string ToString() => DisplayName;
    }
}
