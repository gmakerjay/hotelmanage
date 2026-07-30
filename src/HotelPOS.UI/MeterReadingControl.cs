using HotelPOS.Common;
using HotelPOS.Common.Models;
using HotelPOS.Core.Services;
using HotelPOS.Printing;
using HotelPOS.Logging;

namespace HotelPOS.UI;

/// <summary>
/// หน้าจอบันทึกเลขมิเตอร์ค่าน้ำค่าไฟและออกใบแจ้งหนี้รายเดือน (บิลรวมใบเดียว)
/// </summary>
public class MeterReadingControl : UserControl
{
    private readonly IUtilityBillService _utilityBillService;
    private readonly IRoomService _roomService;
    private readonly ISettingsService _settingsService;
    private readonly IBookingService? _bookingService;
    private readonly ICustomerService? _customerService;
    private readonly IAppLogger _logger;

    private ComboBox _cmbBillingMonth = null!;
    private DataGridView _dgvMeterReadings = null!;
    private TextBox _txtSearch = null!;
    private Label _lblSummary = null!;
    private Button _btnOneClickProcess = null!;
    private Button _btnBatchPrint = null!;
    private Button _btnViewHistory = null!;
    private Button _btnConfigureRates = null!;
    private Label _lblWaterMode = null!;
    private CheckBox _chkShowOccupiedOnly = null!;

    private List<Room> _rooms = new();
    private SystemSettingsDto _settings = null!;

    public MeterReadingControl(
        IUtilityBillService utilityBillService,
        IRoomService roomService,
        ISettingsService settingsService,
        IAppLogger logger,
        IBookingService? bookingService = null,
        ICustomerService? customerService = null)
    {
        _utilityBillService = utilityBillService;
        _roomService = roomService;
        _settingsService = settingsService;
        _logger = logger;
        _bookingService = bookingService;
        _customerService = customerService;

        InitializeComponents();
    }

    private void InitializeComponents()
    {
        BackColor = Color.FromArgb(245, 247, 250);
        Padding = new Padding(20);

        // === Header Section ===
        var headerPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            Padding = new Padding(5),
            BackColor = Color.Transparent
        };

        var lblTitle = new Label
        {
            Text = "ระบบบิลค่าไฟ/ค่าน้ำรวมรายห้อง (ค่าห้อง + ค่าไฟ + ค่าน้ำ + ค่าขยะ + จิปาถะ)",
            Font = new Font("Segoe UI", 15F, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            AutoSize = true,
            Margin = new Padding(5, 5, 5, 2)
        };

        var lblSubtitle = new Label
        {
            Text = "กรอกเลขมิเตอร์ -> บันทึกข้อมูล -> ระบบออกใบแจ้งหนี้รวมประจำห้อง (บิลใบเดียวแยกรายห้อง รวมค่าเช่า + มิเตอร์ + ค่าบริการ)",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
            ForeColor = Color.FromArgb(100, 116, 139),
            AutoSize = true,
            Margin = new Padding(5, 0, 5, 10)
        };

        var inputFlowPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoSize = true,
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };

        var lblMonth = new Label
        {
            Text = "รอบบิลเดือน:",
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59),
            AutoSize = true,
            Margin = new Padding(5, 10, 5, 5)
        };

        _cmbBillingMonth = new ComboBox
        {
            Width = 190,
            Font = new Font("Segoe UI", 11F),
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Color.White,
            Margin = new Padding(5, 6, 5, 5)
        };

        // Water billing mode indicator
        _lblWaterMode = new Label
        {
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(37, 99, 235),
            AutoSize = true,
            Margin = new Padding(15, 10, 5, 5)
        };

        // Populate months (current ± 6 months)
        for (int i = -6; i <= 2; i++)
        {
            var date = DateTime.Now.AddMonths(i);
            string monthKey = date.ToString("yyyy-MM");
            string monthDisplay = date.ToString("MMMM yyyy", new System.Globalization.CultureInfo("th-TH"));
            _cmbBillingMonth.Items.Add(new MonthItem(monthKey, monthDisplay));
        }
        for (int i = 0; i < _cmbBillingMonth.Items.Count; i++)
        {
            if (((MonthItem)_cmbBillingMonth.Items[i]).Value == DateTime.Now.ToString("yyyy-MM"))
            {
                _cmbBillingMonth.SelectedIndex = i;
                break;
            }
        }
        _cmbBillingMonth.SelectedIndexChanged += async (s, e) => await LoadMeterDataAsync();

        _chkShowOccupiedOnly = new CheckBox
        {
            Text = "แสดงเฉพาะห้องที่มีผู้เช่ารายเดือน",
            Width = 320,
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59),
            Checked = true,
            Margin = new Padding(15, 8, 5, 5)
        };
        _chkShowOccupiedOnly.CheckedChanged += async (s, e) => await LoadMeterDataAsync();

        // Instant Search Box
        var lblSearch = new Label
        {
            Text = "ค้นหาห้อง/ผู้เช่า:",
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59),
            AutoSize = true,
            Margin = new Padding(15, 10, 5, 5)
        };

        _txtSearch = new TextBox
        {
            Width = 260,
            Font = new Font("Segoe UI", 10.5F),
            PlaceholderText = "พิมพ์เบอร์โทร / ชื่อ / เลขห้อง...",
            Margin = new Padding(5, 6, 5, 5)
        };
        _txtSearch.TextChanged += (s, e) => FilterMeterGrid();

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
            _chkShowOccupiedOnly.Checked = true;
            await LoadMeterDataAsync();
        };

        inputFlowPanel.Controls.AddRange(new Control[] { lblMonth, _cmbBillingMonth, _lblWaterMode, _chkShowOccupiedOnly, lblSearch, _txtSearch, btnRefresh });
        headerPanel.Controls.AddRange(new Control[] { lblTitle, lblSubtitle, inputFlowPanel });

        // === DataGridView ===
        _dgvMeterReadings = new DataGridView
        {
            Dock = DockStyle.Fill,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            SelectionMode = DataGridViewSelectionMode.CellSelect,
            DefaultCellStyle = new DataGridViewCellStyle
            {
                Font = new Font("Segoe UI", 10.5F),
                Padding = new Padding(4, 2, 4, 2),
                BackColor = Color.White,
                ForeColor = Color.FromArgb(15, 23, 42),
                SelectionBackColor = Color.FromArgb(224, 231, 255),
                SelectionForeColor = Color.FromArgb(15, 23, 42)
            },
            ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                BackColor = Color.FromArgb(30, 41, 59),
                ForeColor = Color.White,
                SelectionBackColor = Color.FromArgb(30, 41, 59),
                SelectionForeColor = Color.White,
                Padding = new Padding(6, 8, 6, 8),
                Alignment = DataGridViewContentAlignment.MiddleCenter,
                WrapMode = DataGridViewTriState.True
            },
            EnableHeadersVisualStyles = false,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
            RowTemplate = { Height = 42 },
            GridColor = Color.FromArgb(226, 232, 240)
        };

        _dgvMeterReadings.DataBindingComplete += (s, e) =>
        {
            foreach (DataGridViewColumn col in _dgvMeterReadings.Columns)
            {
                col.MinimumWidth = 85;
            }
        };

        // Define columns
        _dgvMeterReadings.Columns.AddRange(new DataGridViewColumn[]
        {
            new DataGridViewTextBoxColumn { Name = "RoomId", HeaderText = "ID", Visible = false },
            new DataGridViewTextBoxColumn { Name = "RoomNumber", HeaderText = "ห้อง", ReadOnly = true, FillWeight = 50,
                DefaultCellStyle = new DataGridViewCellStyle { Font = new Font("Segoe UI", 11F, FontStyle.Bold), Alignment = DataGridViewContentAlignment.MiddleCenter } },
            new DataGridViewTextBoxColumn { Name = "TenantName", HeaderText = "ผู้เช่า", ReadOnly = true, FillWeight = 85,
                DefaultCellStyle = new DataGridViewCellStyle { Font = new Font("Segoe UI", 10F), ForeColor = Color.FromArgb(71, 85, 105) } },
            
            // ไฟฟ้า
            new DataGridViewTextBoxColumn { Name = "ElecPrev", HeaderText = "ไฟ-ก่อน", ReadOnly = true, FillWeight = 65,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N0", BackColor = Color.FromArgb(241, 245, 249) } },
            new DataGridViewTextBoxColumn { Name = "ElecCurr", HeaderText = "ไฟ-หลัง", ReadOnly = true, FillWeight = 65,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N0", BackColor = Color.FromArgb(241, 245, 249) } },
            new DataGridViewTextBoxColumn { Name = "ElecUnits", HeaderText = "หน่วยไฟ", ReadOnly = true, FillWeight = 55,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N0", ForeColor = Color.FromArgb(234, 88, 12) } },
            new DataGridViewTextBoxColumn { Name = "ElecAmount", HeaderText = "ค่าไฟ (บาท)", ReadOnly = true, FillWeight = 70,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N2", Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = Color.FromArgb(234, 88, 12) } },
            
            // ประปา
            new DataGridViewTextBoxColumn { Name = "WaterPrev", HeaderText = "น้ำ-ก่อน", ReadOnly = true, FillWeight = 65,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N0", BackColor = Color.FromArgb(241, 245, 249) } },
            new DataGridViewTextBoxColumn { Name = "WaterCurr", HeaderText = "น้ำ-หลัง", ReadOnly = true, FillWeight = 65,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N0", BackColor = Color.FromArgb(241, 245, 249) } },
            new DataGridViewTextBoxColumn { Name = "WaterUnits", HeaderText = "หน่วยน้ำ", ReadOnly = true, FillWeight = 55,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N0", ForeColor = Color.FromArgb(14, 116, 144) } },
            new DataGridViewTextBoxColumn { Name = "WaterAmount", HeaderText = "ค่าน้ำ (บาท)", ReadOnly = true, FillWeight = 70,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N2", Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = Color.FromArgb(14, 116, 144) } },
            new DataGridViewTextBoxColumn { Name = "WaterPersons", HeaderText = "จำนวนคน", ReadOnly = true, FillWeight = 55,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter, Format = "N0", BackColor = Color.FromArgb(241, 245, 249) } },
            
            // ยอดบิลรวมสุทธิ
            new DataGridViewTextBoxColumn { Name = "TotalBillAmount", HeaderText = "รวมสุทธิ (บาท)", ReadOnly = true, FillWeight = 85,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N2", Font = new Font("Segoe UI", 11F, FontStyle.Bold), ForeColor = Color.FromArgb(15, 23, 42) } },
            
            // ปุ่มดำเนินการตรงในตาราง
            new DataGridViewButtonColumn { Name = "BtnEdit", HeaderText = "บันทึก/แก้ไข", FillWeight = 85,
                Text = "กรอก/แก้ไข", UseColumnTextForButtonValue = true, Visible = false },
            new DataGridViewTextBoxColumn { Name = "BtnStatus", HeaderText = "สถานะชำระ", ReadOnly = true, FillWeight = 80,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 10F, FontStyle.Bold) } },
            new DataGridViewButtonColumn { Name = "BtnPrint", HeaderText = "ออกบิล", FillWeight = 85,
                Text = "พิมพ์บิลเดียว", UseColumnTextForButtonValue = true },
            
            new DataGridViewTextBoxColumn { Name = "Notes", HeaderText = "หมายเหตุ", ReadOnly = true, FillWeight = 80 }
        });

        // Set explicit and minimum widths to prevent squeezing on smaller resolutions
        foreach (DataGridViewColumn col in _dgvMeterReadings.Columns)
        {
            if (col.Name == "RoomNumber") { col.Width = 60; col.MinimumWidth = 55; }
            else if (col.Name == "TenantName") { col.Width = 120; col.MinimumWidth = 100; }
            else if (col.Name == "ElecPrev" || col.Name == "ElecCurr" || col.Name == "ElecUnits") { col.Width = 75; col.MinimumWidth = 70; }
            else if (col.Name == "ElecAmount") { col.Width = 90; col.MinimumWidth = 80; }
            else if (col.Name == "WaterPrev" || col.Name == "WaterCurr" || col.Name == "WaterUnits") { col.Width = 75; col.MinimumWidth = 70; }
            else if (col.Name == "WaterAmount") { col.Width = 90; col.MinimumWidth = 80; }
            else if (col.Name == "WaterPersons") { col.Width = 80; col.MinimumWidth = 70; }
            else if (col.Name == "TotalBillAmount") { col.Width = 115; col.MinimumWidth = 100; }
            else if (col.Name == "BtnEdit" || col.Name == "BtnStatus" || col.Name == "BtnPrint") { col.Width = 105; col.MinimumWidth = 95; }
            else { col.Width = 110; col.MinimumWidth = 90; }
        }

        _dgvMeterReadings.CellValueChanged += DgvMeterReadings_CellValueChanged;
        _dgvMeterReadings.CellContentClick += async (s, e) => await DgvMeterReadings_CellContentClick(s, e);
        _dgvMeterReadings.CellDoubleClick += async (s, e) =>
        {
            if (e.RowIndex >= 0)
            {
                int colPrintIndex = _dgvMeterReadings.Columns["BtnPrint"].Index;
                var args = new DataGridViewCellEventArgs(colPrintIndex, e.RowIndex);
                await DgvMeterReadings_CellContentClick(s, args);
            }
        };
        _dgvMeterReadings.CellEndEdit += (s, e) => _dgvMeterReadings.InvalidateRow(e.RowIndex);

        // Alternating row color
        _dgvMeterReadings.RowsAdded += (s, e) =>
        {
            for (int i = e.RowIndex; i < e.RowIndex + e.RowCount; i++)
            {
                if (i % 2 == 1)
                    _dgvMeterReadings.Rows[i].DefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252);
            }
        };

        // === Summary Section (Row 1) ===
        var pnlSummary = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 40,
            BackColor = Color.FromArgb(30, 41, 59),
            Padding = new Padding(15, 8, 15, 8)
        };

        _lblSummary = new Label
        {
            Text = "รวม: 0 ห้อง | ค่าไฟรวม: 0.00 บาท | ค่าน้ำรวม: 0.00 บาท | รวมบิลสุทธิทุกห้อง: 0.00 บาท",
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor = Color.White,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
        pnlSummary.Controls.Add(_lblSummary);

        // === Actions Section (Row 2) ===
        var pnlActions = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 55,
            BackColor = Color.White,
            Padding = new Padding(10, 5, 10, 5)
        };

        // Main action buttons (No emojis)
        _btnOneClickProcess = new Button
        {
            Text = "บันทึกและสร้างบิลรวมรายห้อง",
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Color.FromArgb(22, 163, 74),
            FlatStyle = FlatStyle.Flat,
            Size = new Size(240, 42),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Cursor = Cursors.Hand
        };
        _btnOneClickProcess.FlatAppearance.BorderSize = 0;
        _btnOneClickProcess.Click += async (s, e) => await ProcessOneClickSaveAndGenerateAsync();

        _btnBatchPrint = new Button
        {
            Text = "พิมพ์บิลแยกรายห้อง (ทุกห้อง)",
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Color.FromArgb(37, 99, 235),
            FlatStyle = FlatStyle.Flat,
            Size = new Size(200, 42),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Cursor = Cursors.Hand
        };
        _btnBatchPrint.FlatAppearance.BorderSize = 0;
        _btnBatchPrint.Click += async (s, e) => await PrintBatchInvoicesAsync();

        _btnViewHistory = new Button
        {
            Text = "ดูประวัติ / รายงาน",
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Color.FromArgb(100, 116, 139),
            FlatStyle = FlatStyle.Flat,
            Size = new Size(140, 42),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Cursor = Cursors.Hand
        };
        _btnViewHistory.FlatAppearance.BorderSize = 0;
        _btnViewHistory.Click += (s, e) => ViewBillHistory();

        _btnConfigureRates = new Button
        {
            Text = "ตั้งค่าอัตราค่าหน่วย",
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Color.FromArgb(71, 85, 105),
            FlatStyle = FlatStyle.Flat,
            Size = new Size(150, 42),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Cursor = Cursors.Hand
        };
        _btnConfigureRates.FlatAppearance.BorderSize = 0;
        _btnConfigureRates.Click += async (s, e) => await ConfigureRatesWithAdminAuthAsync();

        pnlActions.Controls.Add(_btnConfigureRates);
        pnlActions.Controls.Add(_btnViewHistory);
        pnlActions.Controls.Add(_btnBatchPrint);
        pnlActions.Controls.Add(_btnOneClickProcess);

        pnlActions.Resize += (s, e) =>
        {
            _btnOneClickProcess.Location = new Point(pnlActions.Width - _btnOneClickProcess.Width - 10, 6);
            _btnBatchPrint.Location = new Point(_btnOneClickProcess.Left - _btnBatchPrint.Width - 10, 6);
            _btnViewHistory.Location = new Point(_btnBatchPrint.Left - _btnViewHistory.Width - 10, 6);
            _btnConfigureRates.Location = new Point(_btnViewHistory.Left - _btnConfigureRates.Width - 10, 6);
        };

        Controls.Add(_dgvMeterReadings);
        Controls.Add(pnlActions);
        Controls.Add(pnlSummary);
        Controls.Add(headerPanel);
    }

    public async Task LoadMeterDataAsync()
    {
        if (_cmbBillingMonth.SelectedItem == null) return;
        string billingMonth = ((MonthItem)_cmbBillingMonth.SelectedItem).Value;

        _settings = await _settingsService.GetAllSettingsAsync();
        _rooms = (await _roomService.GetRoomsAsync()).ToList();

        bool isWaterMeter = _settings.WaterBillingMode == "METER";
        bool isElecMeter = _settings.ElectricBillingMode == "METER";

        string elecLabel = isElecMeter ? $"ไฟ: ตามมิเตอร์ ({_settings.ElectricRatePerUnit:N2} บาท/หน่วย)" : $"ไฟ: เหมาจ่าย ({_settings.ElectricFlatRate:N2} บาท/เดือน)";
        string waterLabel = isWaterMeter ? $"น้ำ: ตามมิเตอร์ ({_settings.WaterRatePerUnit:N2} บาท/หน่วย)" : $"น้ำ: เหมาจ่าย ({_settings.WaterFlatRatePerPerson:N2} บาท/คน)";
        _lblWaterMode.Text = $"{elecLabel} | {waterLabel} | ขยะ: {_settings.GarbageFee:N0} บาท";

        _dgvMeterReadings.Columns["ElecPrev"]!.Visible = isElecMeter;
        _dgvMeterReadings.Columns["ElecCurr"]!.Visible = isElecMeter;
        _dgvMeterReadings.Columns["ElecUnits"]!.Visible = isElecMeter;

        _dgvMeterReadings.Columns["WaterPrev"]!.Visible = isWaterMeter;
        _dgvMeterReadings.Columns["WaterCurr"]!.Visible = isWaterMeter;
        _dgvMeterReadings.Columns["WaterUnits"]!.Visible = isWaterMeter;
        _dgvMeterReadings.Columns["WaterPersons"]!.Visible = !isWaterMeter;

        _dgvMeterReadings.Rows.Clear();

        var existingBills = (await _utilityBillService.GetBillsByMonthAsync(billingMonth)).ToDictionary(b => b.RoomId);
        var readingsMap = (await _utilityBillService.GetMeterReadingsByMonthAsync(billingMonth))
            .GroupBy(r => r.RoomId).ToDictionary(g => g.Key, g => g.ToList());

        foreach (var room in _rooms)
        {
            Booking? activeBooking = null;
            Customer? cust = null;
            string tenantName = "-";
            if (_bookingService != null && _customerService != null)
            {
                try
                {
                    activeBooking = await _bookingService.GetActiveBookingByRoomIdAsync(room.Id);
                    if (activeBooking != null)
                    {
                        cust = await _customerService.GetCustomerByIdAsync(activeBooking.CustomerId);
                        if (cust != null) tenantName = cust.FullName;
                    }
                }
                catch { }
            }

            if (_chkShowOccupiedOnly.Checked)
            {
                if (room.Status != RoomStatus.Occupied) continue;
                if (activeBooking == null || activeBooking.RatePlan != RatePlanType.Monthly) continue;
            }

            var roomType = await _roomService.GetRoomTypeByIdAsync(room.RoomTypeId);
            decimal roomRate = roomType?.MonthlyRate ?? 3500m;

            decimal elecPrev = await _utilityBillService.GetPreviousMeterValueAsync(room.Id, UtilityType.Electric, billingMonth);
            decimal waterPrev = await _utilityBillService.GetPreviousMeterValueAsync(room.Id, UtilityType.Water, billingMonth);

            readingsMap.TryGetValue(room.Id, out var readings);
            readings ??= new List<MeterReading>();

            var elecReading = readings.FirstOrDefault(r => r.UtilityType == UtilityType.Electric);
            var waterReading = readings.FirstOrDefault(r => r.UtilityType == UtilityType.Water);

            decimal elecCurr = elecReading?.ReadingCurr ?? 0;
            decimal waterCurr = waterReading?.ReadingCurr ?? 0;
            decimal elecUnits = elecReading?.UnitsUsed ?? 0;
            decimal waterUnits = waterReading?.UnitsUsed ?? 0;
            decimal elecAmount = isElecMeter ? (elecReading?.TotalAmount ?? 0) : _settings.ElectricFlatRate;
            decimal waterAmount = isWaterMeter ? (waterReading?.TotalAmount ?? 0) : _settings.WaterFlatRatePerPerson;

            if (elecReading != null) elecPrev = elecReading.ReadingPrev;
            if (waterReading != null) waterPrev = waterReading.ReadingPrev;

            decimal totalBill = roomRate + elecAmount + waterAmount + _settings.CommonAreaFee + _settings.GarbageFee;

            bool isPaid = false;
            if (existingBills.TryGetValue(room.Id, out var bill))
            {
                isPaid = bill.IsPaid;
                totalBill = bill.TotalAmount;
            }

            int rowIndex = _dgvMeterReadings.Rows.Add(
                room.Id,
                room.RoomNumber,
                tenantName,
                elecPrev,
                elecCurr == 0 ? (object)"" : elecCurr,
                elecUnits,
                elecAmount,
                waterPrev,
                waterCurr == 0 ? (object)"" : waterCurr,
                waterUnits,
                waterAmount,
                1,
                totalBill,
                "กรอก/แก้ไข",
                isPaid ? "ชำระแล้ว" : "ค้างชำระ",
                "พิมพ์บิลเดียว",
                elecReading?.Notes ?? ""
            );

            _dgvMeterReadings.Rows[rowIndex].Cells["ElecPrev"].ReadOnly = true;
            _dgvMeterReadings.Rows[rowIndex].Cells["WaterPrev"].ReadOnly = true;

            var statusCell = _dgvMeterReadings.Rows[rowIndex].Cells["BtnStatus"];
            if (isPaid)
            {
                statusCell.Style.BackColor = Color.FromArgb(220, 252, 231);
                statusCell.Style.ForeColor = Color.FromArgb(22, 101, 52);
            }
            else
            {
                statusCell.Style.BackColor = Color.FromArgb(254, 226, 226);
                statusCell.Style.ForeColor = Color.FromArgb(153, 27, 27);
            }
        }

        FilterMeterGrid();
    }

    private void FilterMeterGrid()
    {
        string query = _txtSearch.Text.Trim();
        _dgvMeterReadings.CurrentCell = null;

        foreach (DataGridViewRow row in _dgvMeterReadings.Rows)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                row.Visible = true;
                continue;
            }

            string roomNo = row.Cells["RoomNumber"].Value?.ToString() ?? "";
            string tenant = row.Cells["TenantName"].Value?.ToString() ?? "";
            string notes = row.Cells["Notes"].Value?.ToString() ?? "";

            bool matchRoom = roomNo.Contains(query, StringComparison.OrdinalIgnoreCase);
            bool matchTenant = tenant.Contains(query, StringComparison.OrdinalIgnoreCase);
            bool matchNotes = notes.Contains(query, StringComparison.OrdinalIgnoreCase);

            row.Visible = matchRoom || matchTenant || matchNotes;
        }

        UpdateSummary();
    }

    private void DgvMeterReadings_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0) return;

        var row = _dgvMeterReadings.Rows[e.RowIndex];
        string colName = _dgvMeterReadings.Columns[e.ColumnIndex].Name;

        if (colName == "ElecCurr")
        {
            if (decimal.TryParse(row.Cells["ElecPrev"].Value?.ToString(), out decimal prev) &&
                decimal.TryParse(row.Cells["ElecCurr"].Value?.ToString(), out decimal curr))
            {
                decimal units = Math.Max(0, curr - prev);
                decimal amount = units * _settings.ElectricRatePerUnit;
                row.Cells["ElecUnits"].Value = units;
                row.Cells["ElecAmount"].Value = amount;
            }
        }

        if (colName == "WaterCurr" && _settings.WaterBillingMode == "METER")
        {
            if (decimal.TryParse(row.Cells["WaterPrev"].Value?.ToString(), out decimal prev) &&
                decimal.TryParse(row.Cells["WaterCurr"].Value?.ToString(), out decimal curr))
            {
                decimal units = Math.Max(0, curr - prev);
                decimal amount = units * _settings.WaterRatePerUnit;
                row.Cells["WaterUnits"].Value = units;
                row.Cells["WaterAmount"].Value = amount;
            }
        }

        if (colName == "WaterPersons" && _settings.WaterBillingMode == "FLAT")
        {
            if (int.TryParse(row.Cells["WaterPersons"].Value?.ToString(), out int persons))
            {
                decimal amount = _settings.WaterFlatRatePerPerson * persons;
                row.Cells["WaterAmount"].Value = amount;
            }
        }

        int roomId = Convert.ToInt32(row.Cells["RoomId"].Value);
        decimal roomRate = 3500m;

        decimal.TryParse(row.Cells["ElecAmount"].Value?.ToString(), out decimal elecAmt);
        decimal.TryParse(row.Cells["WaterAmount"].Value?.ToString(), out decimal waterAmt);
        decimal total = roomRate + elecAmt + waterAmt + _settings.CommonAreaFee + _settings.GarbageFee;
        row.Cells["TotalBillAmount"].Value = total;

        UpdateSummary();
    }

    private async Task DgvMeterReadings_CellContentClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0) return;

        string colName = _dgvMeterReadings.Columns[e.ColumnIndex].Name;
        var row = _dgvMeterReadings.Rows[e.RowIndex];
        int roomId = Convert.ToInt32(row.Cells["RoomId"].Value);
        string roomNumber = row.Cells["RoomNumber"].Value?.ToString() ?? "";
        if (_cmbBillingMonth.SelectedItem == null) return;
        string billingMonth = ((MonthItem)_cmbBillingMonth.SelectedItem).Value;

        if (colName == "BtnEdit")
        {
            await OpenMeterInputDialogAsync(e.RowIndex);
            return;
        }

        if (colName == "BtnPrint")
        {
            try
            {
                await SaveSingleRoomReadingAsync(row, roomId, billingMonth);
                
                int waterPersons = 1;
                if (int.TryParse(row.Cells["WaterPersons"].Value?.ToString(), out int p)) waterPersons = p;

                var bill = await _utilityBillService.GenerateMonthlyBillAsync(roomId, billingMonth, waterPersons);

                Customer? customer = null;
                if (_bookingService != null && _customerService != null)
                {
                    try
                    {
                        var activeBooking = await _bookingService.GetActiveBookingByRoomIdAsync(roomId);
                        if (activeBooking != null)
                        {
                            customer = await _customerService.GetCustomerByIdAsync(activeBooking.CustomerId);
                        }
                    }
                    catch { }
                }

                var printer = new UtilityInvoicePrinter(bill, customer, _settings);
                printer.ShowPrintPreview();
            }
            catch (Exception ex)
            {
                _logger.Error(LogCategory.Utility, $"พิมพ์ใบแจ้งหนี้ของห้อง {roomNumber} เดือน {billingMonth} ล้มเหลว", ex);
                Program.ShowDetailedErrorPopup(ex, $"ไม่สามารถออกใบแจ้งหนี้และพิมพ์บิลสำหรับห้อง {roomNumber} ได้");
            }
        }


    }

    private async Task SaveSingleRoomReadingAsync(DataGridViewRow row, int roomId, string billingMonth)
    {
        if (decimal.TryParse(row.Cells["ElecPrev"].Value?.ToString(), out decimal elecPrev) &&
            decimal.TryParse(row.Cells["ElecCurr"].Value?.ToString(), out decimal elecCurr) && elecCurr > 0)
        {
            await _utilityBillService.RecordMeterReadingAsync(
                roomId, UtilityType.Electric, elecPrev, elecCurr, billingMonth, row.Cells["Notes"].Value?.ToString());
        }

        if (_settings.WaterBillingMode == "METER")
        {
            if (decimal.TryParse(row.Cells["WaterPrev"].Value?.ToString(), out decimal waterPrev) &&
                decimal.TryParse(row.Cells["WaterCurr"].Value?.ToString(), out decimal waterCurr) && waterCurr > 0)
            {
                await _utilityBillService.RecordMeterReadingAsync(
                    roomId, UtilityType.Water, waterPrev, waterCurr, billingMonth, row.Cells["Notes"].Value?.ToString());
            }
        }
    }

    private void UpdateSummary()
    {
        decimal totalElec = 0, totalWater = 0, grandTotal = 0;
        int roomCount = _dgvMeterReadings.Rows.Count;

        foreach (DataGridViewRow row in _dgvMeterReadings.Rows)
        {
            if (decimal.TryParse(row.Cells["ElecAmount"].Value?.ToString(), out decimal e)) totalElec += e;
            if (decimal.TryParse(row.Cells["WaterAmount"].Value?.ToString(), out decimal w)) totalWater += w;
            if (decimal.TryParse(row.Cells["TotalBillAmount"].Value?.ToString(), out decimal t)) grandTotal += t;
        }

        _lblSummary.Text = $"รวม: {roomCount} ห้อง | ไฟ: {totalElec:N2} บาท | น้ำ: {totalWater:N2} บาท | รวมบิลสุทธิทุกห้อง: {grandTotal:N2} บาท";
    }

    private async Task ProcessOneClickSaveAndGenerateAsync()
    {
        if (_cmbBillingMonth.SelectedItem == null) return;
        string billingMonth = ((MonthItem)_cmbBillingMonth.SelectedItem).Value;

        var confirm = MessageBox.Show(
            $"ต้องการ [บันทึกเลขมิเตอร์ + ออกใบแจ้งหนี้รวม] ทุกห้อง สำหรับเดือน {((MonthItem)_cmbBillingMonth.SelectedItem).Display} ใช่หรือไม่?\n\n" +
            "ระบบจะคำนวณและสรุปยอดบิล (ค่าห้อง + ค่าไฟ + ค่าน้ำ + ค่าบริการ + ค่าขยะ)",
            "ยืนยันการทำรายการ", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

        if (confirm != DialogResult.Yes) return;

        int processed = 0;
        var errors = new List<string>();

        foreach (DataGridViewRow row in _dgvMeterReadings.Rows)
        {
            int roomId = Convert.ToInt32(row.Cells["RoomId"].Value);
            string roomNumber = row.Cells["RoomNumber"].Value?.ToString() ?? "";

            try
            {
                await SaveSingleRoomReadingAsync(row, roomId, billingMonth);

                int waterPersons = 1;
                if (int.TryParse(row.Cells["WaterPersons"].Value?.ToString(), out int p)) waterPersons = p;

                await _utilityBillService.GenerateMonthlyBillAsync(roomId, billingMonth, waterPersons);
                processed++;
            }
            catch (Exception ex)
            {
                _logger.Error(LogCategory.Utility, $"บันทึกเลขมิเตอร์หรืออกบิลรวมห้อง {roomNumber} เดือน {billingMonth} ล้มเหลว", ex);
                errors.Add($"ห้อง {roomNumber}: {ex.Message}");
            }
        }

        await LoadMeterDataAsync();

        if (errors.Count > 0)
        {
            MessageBox.Show($"ดำเนินการสำเร็จ {processed} ห้อง\n\nข้อผิดพลาด:\n{string.Join("\n", errors)}",
                "ข้อผิดพลาดในการทำรายการ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        else
        {
            MessageBox.Show($"บันทึกเลขมิเตอร์และออกใบแจ้งหนี้รวมสำเร็จ {processed} ห้องเรียบร้อยแล้ว\n\nท่านสามารถกดปุ่ม [พิมพ์บิลเดียว] ในแต่ละแถวเพื่อพิมพ์ใบแจ้งหนี้ให้ผู้เช่าได้ทันที",
                "ทำรายการสำเร็จ", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private async Task PrintBatchInvoicesAsync()
    {
        if (_cmbBillingMonth.SelectedItem == null) return;
        string billingMonth = ((MonthItem)_cmbBillingMonth.SelectedItem).Value;

        var confirm = MessageBox.Show(
            $"ต้องการพิมพ์ใบแจ้งหนี้ของทุกห้องสำหรับเดือน {((MonthItem)_cmbBillingMonth.SelectedItem).Display} ใช่หรือไม่?",
            "พิมพ์บิลทุกห้อง", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

        if (confirm != DialogResult.Yes) return;

        try
        {
            var bills = (await _utilityBillService.GetBillsByMonthAsync(billingMonth)).ToList();
            if (bills.Count == 0)
            {
                MessageBox.Show("ยังไม่มีใบแจ้งหนี้ในระบบ กรุณากดปุ่ม [บันทึกและออกบิลรวมทั้งหมด] ก่อน", "ไม่พบใบแจ้งหนี้", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            foreach (var bill in bills)
            {
                try
                {
                    Customer? customer = null;
                    if (_bookingService != null && _customerService != null)
                    {
                        try
                        {
                            var activeBooking = await _bookingService.GetActiveBookingByRoomIdAsync(bill.RoomId);
                            if (activeBooking != null)
                            {
                                customer = await _customerService.GetCustomerByIdAsync(activeBooking.CustomerId);
                            }
                        }
                        catch { }
                    }

                    var printer = new UtilityInvoicePrinter(bill, customer, _settings);
                    printer.ShowPrintPreview();
                }
                catch (Exception ex)
                {
                    _logger.Error(LogCategory.Printing, $"พิมพ์ใบแจ้งหนี้แบบกลุ่ม (Batch) สำหรับบิล {bill.BillCode} ล้มเหลว", ex);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Printing, $"โหลดรายการใบแจ้งหนี้สำหรับพิมพ์กลุ่มประจำเดือน {billingMonth} ล้มเหลว", ex);
            Program.ShowDetailedErrorPopup(ex, "ไม่สามารถดึงข้อมูลรายการใบแจ้งหนี้เพื่อพิมพ์กลุ่มได้");
        }
    }

    private async Task OpenMeterInputDialogAsync(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= _dgvMeterReadings.Rows.Count) return;
        var row = _dgvMeterReadings.Rows[rowIndex];

        int roomId = Convert.ToInt32(row.Cells["RoomId"].Value);
        string roomNumber = row.Cells["RoomNumber"].Value?.ToString() ?? "";
        string tenantName = row.Cells["TenantName"].Value?.ToString() ?? "-";

        if (_cmbBillingMonth.SelectedItem == null) return;
        string billingMonth = ((MonthItem)_cmbBillingMonth.SelectedItem).Value;

        var room = _rooms.FirstOrDefault(r => r.Id == roomId);
        if (room == null) return;

        var roomType = await _roomService.GetRoomTypeByIdAsync(room.RoomTypeId);
        decimal roomRate = roomType?.MonthlyRate ?? 3500m;

        decimal.TryParse(row.Cells["ElecPrev"].Value?.ToString(), out decimal elecPrev);
        decimal.TryParse(row.Cells["ElecCurr"].Value?.ToString(), out decimal elecCurr);
        decimal.TryParse(row.Cells["WaterPrev"].Value?.ToString(), out decimal waterPrev);
        decimal.TryParse(row.Cells["WaterCurr"].Value?.ToString(), out decimal waterCurr);

        int waterPersons = 1;
        if (int.TryParse(row.Cells["WaterPersons"].Value?.ToString(), out int p)) waterPersons = p;

        string notes = row.Cells["Notes"].Value?.ToString() ?? "";

        decimal extraCharges = 0;
        decimal discountAmount = 0;
        bool isEdit = false;
        try
        {
            var bills = await _utilityBillService.GetBillsByMonthAsync(billingMonth);
            var existingBill = bills.FirstOrDefault(b => b.RoomId == roomId);
            if (existingBill != null)
            {
                extraCharges = existingBill.ExtraCharges;
                discountAmount = existingBill.DiscountAmount;
                if (!string.IsNullOrEmpty(existingBill.Notes)) notes = existingBill.Notes;
                isEdit = true;
            }
            else if (elecCurr > 0 || waterCurr > 0)
            {
                isEdit = true;
            }
        }
        catch { }

        if (isEdit)
        {
            using var authForm = new AdminAuthForm(_settingsService);
            if (authForm.ShowDialog() != DialogResult.OK)
            {
                return;
            }
        }

        using var dlg = new MeterReadingInputDialog(
            room, tenantName, billingMonth, roomRate,
            elecPrev, elecCurr, waterPrev, waterCurr, waterPersons,
            extraCharges, discountAmount, notes, _settings);

        if (dlg.ShowDialog() == DialogResult.OK)
        {
            try
            {
                // 1. Save Electric Reading
                if (dlg.ElecCurr > 0 || dlg.ElecPrev > 0)
                {
                    await _utilityBillService.RecordMeterReadingAsync(
                        roomId, UtilityType.Electric, dlg.ElecPrev, dlg.ElecCurr, billingMonth, dlg.Notes);
                }

                // 2. Save Water Reading
                if (_settings.WaterBillingMode == "METER" && (dlg.WaterCurr > 0 || dlg.WaterPrev > 0))
                {
                    await _utilityBillService.RecordMeterReadingAsync(
                        roomId, UtilityType.Water, dlg.WaterPrev, dlg.WaterCurr, billingMonth, dlg.Notes);
                }

                // 3. Generate Monthly Bill
                var bill = await _utilityBillService.GenerateMonthlyBillAsync(
                    roomId, billingMonth, dlg.WaterPersons, dlg.ExtraCharges, dlg.DiscountAmount, dlg.Notes);

                // 4. Update UI Grid Row
                row.Cells["ElecPrev"].Value = dlg.ElecPrev;
                row.Cells["ElecCurr"].Value = dlg.ElecCurr == 0 ? (object)"" : dlg.ElecCurr;
                row.Cells["ElecUnits"].Value = dlg.ElecUnits;
                row.Cells["ElecAmount"].Value = dlg.ElecAmount;

                if (_settings.WaterBillingMode == "METER")
                {
                    row.Cells["WaterPrev"].Value = dlg.WaterPrev;
                    row.Cells["WaterCurr"].Value = dlg.WaterCurr == 0 ? (object)"" : dlg.WaterCurr;
                    row.Cells["WaterUnits"].Value = dlg.WaterUnits;
                    row.Cells["WaterAmount"].Value = dlg.WaterAmount;
                }
                else
                {
                    row.Cells["WaterPersons"].Value = dlg.WaterPersons;
                    row.Cells["WaterAmount"].Value = dlg.WaterAmount;
                }

                row.Cells["TotalBillAmount"].Value = bill.TotalAmount;
                row.Cells["Notes"].Value = dlg.Notes;

                UpdateSummary();

                // 5. If Print requested
                if (dlg.PrintBillRequested)
                {
                    Customer? customer = null;
                    if (_bookingService != null && _customerService != null)
                    {
                        try
                        {
                            var activeBooking = await _bookingService.GetActiveBookingByRoomIdAsync(roomId);
                            if (activeBooking != null)
                            {
                                customer = await _customerService.GetCustomerByIdAsync(activeBooking.CustomerId);
                            }
                        }
                        catch { }
                    }

                    var printer = new UtilityInvoicePrinter(bill, customer, _settings);
                    printer.ShowPrintPreview();
                }
            }
            catch (Exception ex)
            {
                _logger.Error(LogCategory.Utility, $"บันทึกข้อมูลค่าน้ำ-ค่าไฟ ห้อง {roomNumber} ผ่าน Pop-up ล้มเหลว", ex);
                Program.ShowDetailedErrorPopup(ex, $"ไม่สามารถบันทึกข้อมูลค่าน้ำ-ค่าไฟห้อง {roomNumber} ได้");
            }
        }
    }

    private void ViewBillHistory()
    {
        if (_cmbBillingMonth.SelectedItem == null) return;
        string billingMonth = ((MonthItem)_cmbBillingMonth.SelectedItem).Value;

        using var historyForm = new UtilityBillHistoryForm(_utilityBillService, billingMonth, _settingsService);
        historyForm.ShowDialog();
    }

    private async Task ConfigureRatesWithAdminAuthAsync()
    {
        using var authForm = new AdminAuthForm(_settingsService);
        if (authForm.ShowDialog() == DialogResult.OK)
        {
            using var rateForm = new UtilityRateSettingsForm(_settingsService);
            if (rateForm.ShowDialog() == DialogResult.OK)
            {
                await LoadMeterDataAsync();
            }
        }
    }
}

/// <summary>Helper class สำหรับ ComboBox เดือน</summary>
internal class MonthItem
{
    public string Value { get; } // "YYYY-MM"
    public string Display { get; }

    public MonthItem(string value, string display)
    {
        Value = value;
        Display = display;
    }

    public override string ToString() => Display;
}
