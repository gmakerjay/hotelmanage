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
        Size = new Size(580, 580);
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

        var lblFullName = new Label { Text = "ชื่อ-นามสกุล *:", Location = new Point(20, 130), Font = new Font("Segoe UI", 11F, FontStyle.Bold), AutoSize = true };
        _txtFullName = new TextBox { Location = new Point(170, 127), Width = 360, Font = new Font("Segoe UI", 11F) };

        var lblPhone = new Label { Text = "เบอร์โทรศัพท์:", Location = new Point(20, 170), Font = new Font("Segoe UI", 11F), AutoSize = true };
        _txtPhone = new TextBox { Location = new Point(170, 167), Width = 360, Font = new Font("Segoe UI", 11F) };
        _txtPhone.Leave += TxtPhone_Leave;

        var lblIdCard = new Label { Text = "เลขบัตร/พาสปอร์ต:", Location = new Point(20, 210), Font = new Font("Segoe UI", 11F), AutoSize = true };
        _txtIdCard = new TextBox { Location = new Point(170, 207), Width = 360, Font = new Font("Segoe UI", 11F) };

        var lblBookingDetails = new Label { Text = "กำหนดการเข้าพัก:", Font = new Font("Segoe UI", 11.5F, FontStyle.Bold), Location = new Point(20, 250), AutoSize = true };

        var lblCheckIn = new Label { Text = "วันที่/เวลา เช็คอิน:", Location = new Point(20, 285), Font = new Font("Segoe UI", 11F), AutoSize = true };
        _dtpCheckIn = new DateTimePicker
        {
            Location = new Point(170, 282),
            Width = 230,
            Font = new Font("Segoe UI", 11F),
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "dd/MM/yyyy HH:mm",
            Value = DateTime.Now.AddDays(1)
        };

        var lblCheckOut = new Label { Text = "วันที่/เวลา เช็คเอาท์:", Location = new Point(20, 325), Font = new Font("Segoe UI", 11F), AutoSize = true };
        _dtpCheckOut = new DateTimePicker
        {
            Location = new Point(170, 322),
            Width = 230,
            Font = new Font("Segoe UI", 11F),
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "dd/MM/yyyy HH:mm",
            Value = DateTime.Now.AddDays(2)
        };

        var lblRatePlan = new Label { Text = "ประเภทราคา:", Location = new Point(20, 365), Font = new Font("Segoe UI", 11F), AutoSize = true };
        _cboRatePlan = new ComboBox { Location = new Point(170, 362), Width = 180, Font = new Font("Segoe UI", 11F), DropDownStyle = ComboBoxStyle.DropDownList };
        _cboRatePlan.Items.AddRange(new object[] { "รายวัน (Daily)", "รายชั่วโมง (Hourly)", "รายเดือน (Monthly)" });
        _cboRatePlan.SelectedIndex = 0;

        var lblRate = new Label { Text = "ราคาตกลง (บาท):", Location = new Point(20, 405), Font = new Font("Segoe UI", 11F), AutoSize = true };
        _numAgreedRate = new NumericUpDown
        {
            Location = new Point(170, 402),
            Width = 180,
            Font = new Font("Segoe UI", 11F),
            Maximum = 1000000,
            DecimalPlaces = 2,
            Value = 500
        };

        var lblNotes = new Label { Text = "หมายเหตุ:", Location = new Point(20, 445), Font = new Font("Segoe UI", 11F), AutoSize = true };
        _txtNotes = new TextBox { Location = new Point(170, 442), Width = 360, Font = new Font("Segoe UI", 11F), Multiline = true, Height = 45 };

        _btnSave = new Button
        {
            Text = "📅 บันทึกการจอง",
            BackColor = Color.RoyalBlue,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 11.5F, FontStyle.Bold),
            Location = new Point(250, 495),
            Size = new Size(160, 42)
        };
        _btnSave.Click += BtnSave_Click;

        _btnCancel = new Button
        {
            Text = "ยกเลิก",
            DialogResult = DialogResult.Cancel,
            Font = new Font("Segoe UI", 11F),
            Location = new Point(420, 495),
            Size = new Size(110, 42)
        };

        // ToolTips Onboarding Guide (Large readable font & clipping safety)
        var tt = new AppToolTip();
        tt.SetToolTip(_cboRooms, "เลือกห้องพักที่ต้องการจองล่วงหน้า");
        tt.SetToolTip(_txtFullName, "ชื่อ-นามสกุลของผู้จองห้องพัก");
        tt.SetToolTip(_txtPhone, "เบอร์โทรศัพท์ติดต่อของผู้จอง");
        tt.SetToolTip(_dtpCheckIn, "กำหนดวันเวลาที่คาดว่าจะมาเช็คอินเข้าพัก");
        tt.SetToolTip(_dtpCheckOut, "กำหนดวันเวลาที่คาดว่าจะเช็คเอาท์คืนห้อง");
        tt.SetToolTip(_cboRatePlan, "ประเภทรูปแบบราคา: รายวัน, รายชั่วโมง, หรือ รายเดือน");
        tt.SetToolTip(_numAgreedRate, "ราคาค่าห้องตกลงล่วงหน้าต่อหน่วย (บาท)");
        tt.SetToolTip(_btnSave, "บันทึกสร้างรายการจองห้องพักล่วงหน้าลงระบบ");
        tt.SetToolTip(_btnCancel, "ยกเลิกการจองและปิดหน้าต่าง");

        Controls.AddRange(new Control[]
        {
            lblHeader, lblRoom, _cboRooms,
            lblCustomerInfo, lblFullName, _txtFullName, lblPhone, _txtPhone, lblIdCard, _txtIdCard,
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

            _roomsList = rooms.ToList();
            _roomTypesList = types.ToList();

            _cboRooms.Items.Clear();
            int selectedIndex = 0;

            for (int i = 0; i < _roomsList.Count; i++)
            {
                var r = _roomsList[i];
                var t = _roomTypesList.FirstOrDefault(x => x.Id == r.RoomTypeId);
                _cboRooms.Items.Add($"ห้อง {r.RoomNumber} - {t?.Name ?? "ทั่วไป"} (ชั้น {r.Floor ?? "-"})");
                if (_selectedRoom != null && r.Id == _selectedRoom.Id)
                {
                    selectedIndex = i;
                }
            }

            if (_cboRooms.Items.Count > 0)
            {
                _cboRooms.SelectedIndex = selectedIndex;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"โหลดข้อมูลห้องพักไม่สำเร็จ: {ex.Message}", "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void CboRooms_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_cboRooms.SelectedIndex < 0 || _cboRooms.SelectedIndex >= _roomsList.Count) return;
        var r = _roomsList[_cboRooms.SelectedIndex];
        var t = _roomTypesList.FirstOrDefault(x => x.Id == r.RoomTypeId);
        if (t != null && t.DailyRate > 0)
        {
            _numAgreedRate.Value = t.DailyRate;
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

            var ratePlan = (RatePlanType)_cboRatePlan.SelectedIndex;
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
}
