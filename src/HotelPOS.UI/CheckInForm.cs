using HotelPOS.Common;
using HotelPOS.Common.Models;
using HotelPOS.Core.Services;

namespace HotelPOS.UI;

public class CheckInForm : Form
{
    private readonly Room _room;
    private readonly RoomType _roomType;
    private readonly IBookingService _bookingService;
    private readonly ICustomerService _customerService;

    private TextBox _txtFullName = null!;
    private TextBox _txtPhone = null!;
    private TextBox _txtIdCard = null!;
    private ComboBox _cboRatePlan = null!;
    private NumericUpDown _numAgreedRate = null!;
    private DateTimePicker _dtpCheckOut = null!;
    private TextBox _txtNotes = null!;
    private Button _btnSave = null!;
    private Button _btnCancel = null!;

    private ComboBox _cboCustomerSelect = null!;
    private List<Customer> _allCustomers = new();

    public CheckInForm(
        Room room,
        RoomType roomType,
        IBookingService bookingService,
        ICustomerService customerService)
    {
        _room = room;
        _roomType = roomType;
        _bookingService = bookingService;
        _customerService = customerService;

        InitializeUI();
        Load += CheckInForm_Load;
    }

    private void InitializeUI()
    {
        Text = $"ลงทะเบียนเข้าพัก (Walk-In Check-In) — ห้อง {_room.RoomNumber}";
        Size = new Size(560, 580);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Font = new Font("Segoe UI", 11F, FontStyle.Regular);

        var lblHeader = new Label
        {
            Text = $"เช็คอินห้อง {_room.RoomNumber} ({_roomType.Name})",
            Font = new Font("Segoe UI", 14F, FontStyle.Bold),
            ForeColor = Color.DarkBlue,
            Location = new Point(20, 15),
            AutoSize = true
        };

        var lblCustomerInfo = new Label { Text = "ข้อมูลผู้เข้าพัก:", Font = new Font("Segoe UI", 11.5F, FontStyle.Bold), Location = new Point(20, 55), AutoSize = true };

        var lblSelectCust = new Label { Text = "เลือกประวัติผู้เข้าพัก:", Location = new Point(20, 90), Font = new Font("Segoe UI", 11F), AutoSize = true };
        _cboCustomerSelect = new ComboBox { Location = new Point(170, 87), Width = 340, Font = new Font("Segoe UI", 11F), DropDownStyle = ComboBoxStyle.DropDownList };
        _cboCustomerSelect.SelectedIndexChanged += CboCustomerSelect_SelectedIndexChanged;

        var lblFullName = new Label { Text = "ชื่อ-นามสกุล *:", Location = new Point(20, 130), Font = new Font("Segoe UI", 11F, FontStyle.Bold), AutoSize = true };
        _txtFullName = new TextBox { Location = new Point(170, 127), Width = 340, Font = new Font("Segoe UI", 11F) };

        var lblPhone = new Label { Text = "เบอร์โทรศัพท์:", Location = new Point(20, 170), Font = new Font("Segoe UI", 11F), AutoSize = true };
        _txtPhone = new TextBox { Location = new Point(170, 167), Width = 340, Font = new Font("Segoe UI", 11F) };
        _txtPhone.Leave += TxtPhone_Leave;

        var lblIdCard = new Label { Text = "เลขบัตร/พาสปอร์ต:", Location = new Point(20, 210), Font = new Font("Segoe UI", 11F), AutoSize = true };
        _txtIdCard = new TextBox { Location = new Point(170, 207), Width = 340, Font = new Font("Segoe UI", 11F) };

        var lblBookingDetails = new Label { Text = "รายละเอียดการพัก:", Font = new Font("Segoe UI", 11.5F, FontStyle.Bold), Location = new Point(20, 250), AutoSize = true };

        var lblRatePlan = new Label { Text = "ประเภทราคา:", Location = new Point(20, 285), Font = new Font("Segoe UI", 11F), AutoSize = true };
        _cboRatePlan = new ComboBox { Location = new Point(170, 282), Width = 180, Font = new Font("Segoe UI", 11F), DropDownStyle = ComboBoxStyle.DropDownList };
        
        if (_roomType.MonthlyRate > 0)
        {
            _cboRatePlan.Items.Add(new RatePlanItem { RatePlan = RatePlanType.Monthly, DisplayName = "รายเดือน (Monthly)" });
        }
        if (_roomType.DailyRate > 0)
        {
            _cboRatePlan.Items.Add(new RatePlanItem { RatePlan = RatePlanType.Daily, DisplayName = "รายวัน (Daily)" });
        }
        if (_roomType.HourlyRate > 0)
        {
            _cboRatePlan.Items.Add(new RatePlanItem { RatePlan = RatePlanType.Hourly, DisplayName = "รายชั่วโมง (Hourly)" });
        }

        if (_cboRatePlan.Items.Count == 0)
        {
            _cboRatePlan.Items.Add(new RatePlanItem { RatePlan = RatePlanType.Daily, DisplayName = "รายวัน (Daily)" });
        }

        var lblRate = new Label { Text = "ราคาตกลง (บาท):", Location = new Point(20, 325), Font = new Font("Segoe UI", 11F), AutoSize = true };
        _numAgreedRate = new NumericUpDown
        {
            Location = new Point(170, 322),
            Width = 180,
            Font = new Font("Segoe UI", 11F),
            Maximum = 1000000,
            DecimalPlaces = 2,
            Value = 500
        };

        var lblCheckOut = new Label { Text = "คาดว่าจะเช็คเอาท์:", Location = new Point(20, 365), Font = new Font("Segoe UI", 11F), AutoSize = true };
        _dtpCheckOut = new DateTimePicker
        {
            Location = new Point(170, 362),
            Width = 230,
            Font = new Font("Segoe UI", 11F),
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "dd/MM/yyyy HH:mm",
            Value = DateTime.Now.AddDays(1)
        };

        _cboRatePlan.SelectedIndexChanged += CboRatePlan_SelectedIndexChanged;
        if (_cboRatePlan.Items.Count > 0)
        {
            _cboRatePlan.SelectedIndex = 0;
        }

        var lblNotes = new Label { Text = "หมายเหตุ:", Location = new Point(20, 405), Font = new Font("Segoe UI", 11F), AutoSize = true };
        _txtNotes = new TextBox { Location = new Point(170, 402), Width = 340, Font = new Font("Segoe UI", 11F), Multiline = true, Height = 55 };

        _btnSave = new Button
        {
            Text = "ยืนยันเช็คอิน",
            DialogResult = DialogResult.None,
            BackColor = Color.ForestGreen,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 11.5F, FontStyle.Bold),
            Location = new Point(230, 475),
            Size = new Size(160, 42)
        };
        _btnSave.Click += BtnSave_Click;

        _btnCancel = new Button
        {
            Text = "ยกเลิก",
            DialogResult = DialogResult.Cancel,
            Font = new Font("Segoe UI", 11F),
            Location = new Point(400, 475),
            Size = new Size(110, 42)
        };

        // ToolTips Onboarding Guide
        var tt = new AppToolTip();
        tt.SetToolTip(_cboCustomerSelect, "ค้นหาและเลือกผู้เข้าพักจากฐานข้อมูลทะเบียน");
        tt.SetToolTip(_txtFullName, "กรอกชื่อ-นามสกุลของผู้เข้าพัก");
        tt.SetToolTip(_txtPhone, "กรอกเบอร์โทรศัพท์ลูกค้า");
        tt.SetToolTip(_txtIdCard, "กรอกเลขบัตรประชาชน หรือ เลขพาสปอร์ต");
        tt.SetToolTip(_cboRatePlan, "เลือกรูปแบบการคิดราคา");
        tt.SetToolTip(_numAgreedRate, "กำหนดราคาค่าห้องพักตกลงต่อหน่วย");
        tt.SetToolTip(_dtpCheckOut, "เลือกระบุวันและเวลาที่คาดว่าจะเช็คเอาท์ออก");
        tt.SetToolTip(_txtNotes, "บันทึกหมายเหตุเพิ่มเติม");
        tt.SetToolTip(_btnSave, "ยืนยันการลงทะเบียนเข้าพัก");
        tt.SetToolTip(_btnCancel, "ยกเลิกและปิดหน้าต่าง");

        Controls.AddRange(new Control[]
        {
            lblHeader, lblCustomerInfo, lblSelectCust, _cboCustomerSelect, lblFullName, _txtFullName,
            lblPhone, _txtPhone, lblIdCard, _txtIdCard,
            lblBookingDetails, lblRatePlan, _cboRatePlan, lblRate, _numAgreedRate,
            lblCheckOut, _dtpCheckOut, lblNotes, _txtNotes,
            _btnSave, _btnCancel
        });

        AcceptButton = _btnSave;
        CancelButton = _btnCancel;
    }

    private async void CheckInForm_Load(object? sender, EventArgs e)
    {
        try
        {
            var customers = await _customerService.GetCustomersAsync();
            _allCustomers = customers.ToList();

            _cboCustomerSelect.Items.Clear();
            _cboCustomerSelect.Items.Add("[-- พิมพ์ข้อมูลเอง / ลงทะเบียนใหม่ --]");
            foreach (var c in _allCustomers)
            {
                _cboCustomerSelect.Items.Add($"{c.FullName} ({c.Phone ?? "ไม่มีเบอร์"})");
            }
            _cboCustomerSelect.SelectedIndex = 0;
        }
        catch { }
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

    private void CboRatePlan_SelectedIndexChanged(object? sender, EventArgs e)
    {
        var item = _cboRatePlan.SelectedItem as RatePlanItem;
        if (item == null) return;

        switch (item.RatePlan)
        {
            case RatePlanType.Daily:
                _numAgreedRate.Value = _roomType.DailyRate > 0 ? _roomType.DailyRate : 500;
                _dtpCheckOut.Value = DateTime.Now.AddDays(1);
                break;
            case RatePlanType.Hourly:
                _numAgreedRate.Value = _roomType.HourlyRate > 0 ? _roomType.HourlyRate : 150;
                _dtpCheckOut.Value = DateTime.Now.AddHours(3);
                break;
            case RatePlanType.Monthly:
                _numAgreedRate.Value = _roomType.MonthlyRate > 0 ? _roomType.MonthlyRate : 5000;
                _dtpCheckOut.Value = DateTime.Now.AddMonths(1);
                break;
        }
    }

    private async void BtnSave_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_txtFullName.Text))
        {
            MessageBox.Show("กรุณากรอกชื่อ-นามสกุลผู้เข้าพัก", "ข้อมูลไม่ครบถ้วน", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtFullName.Focus();
            return;
        }

        try
        {
            _btnSave.Enabled = false;
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

            await _bookingService.WalkInCheckInAsync(
                _room.Id,
                customer,
                ratePlan,
                _numAgreedRate.Value,
                _dtpCheckOut.Value,
                _txtNotes.Text
            );

            MessageBox.Show($"เช็คอินห้อง {_room.RoomNumber} เรียบร้อยแล้ว", "สำเร็จ", MessageBoxButtons.OK, MessageBoxIcon.Information);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"เกิดข้อผิดพลาดในการเช็คอิน: {ex.Message}", "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
