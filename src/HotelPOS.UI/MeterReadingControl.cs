using HotelPOS.Common;
using HotelPOS.Common.Models;
using HotelPOS.Core.Services;
using HotelPOS.Printing;

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

    private ComboBox _cmbBillingMonth = null!;
    private DataGridView _dgvMeterReadings = null!;
    private Label _lblSummary = null!;
    private Button _btnOneClickProcess = null!;
    private Button _btnBatchPrint = null!;
    private Button _btnViewHistory = null!;
    private Button _btnConfigureRates = null!;
    private Label _lblWaterMode = null!;

    private List<Room> _rooms = new();
    private SystemSettingsDto _settings = null!;

    public MeterReadingControl(
        IUtilityBillService utilityBillService,
        IRoomService roomService,
        ISettingsService settingsService,
        IBookingService? bookingService = null,
        ICustomerService? customerService = null)
    {
        _utilityBillService = utilityBillService;
        _roomService = roomService;
        _settingsService = settingsService;
        _bookingService = bookingService;
        _customerService = customerService;

        InitializeComponents();
    }

    private void InitializeComponents()
    {
        BackColor = Color.FromArgb(245, 247, 250);
        Padding = new Padding(20);

        // === Header Section ===
        var headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 115,
            Padding = new Padding(5)
        };

        var lblTitle = new Label
        {
            Text = "ระบบคำนวณค่าน้ำค่าไฟ & ออกใบแจ้งหนี้ (บิลเดียว)",
            Font = new Font("Segoe UI", 16F, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Location = new Point(5, 5),
            AutoSize = true
        };

        var lblSubtitle = new Label
        {
            Text = "กรอกเลขมิเตอร์ -> ระบบคำนวณให้อัตโนมัติ -> พิมพ์ใบแจ้งหนี้รวม (ค่าห้อง + ค่าไฟ + ค่าน้ำ + ค่าขยะ) ในใบเดียว",
            Font = new Font("Segoe UI", 10F, FontStyle.Regular),
            ForeColor = Color.FromArgb(100, 116, 139),
            Location = new Point(5, 38),
            AutoSize = true
        };

        // === Billing Month Selector ===
        var lblMonth = new Label
        {
            Text = "รอบบิลเดือน:",
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59),
            Location = new Point(5, 74),
            AutoSize = true
        };

        _cmbBillingMonth = new ComboBox
        {
            Location = new Point(110, 70),
            Width = 190,
            Font = new Font("Segoe UI", 11F),
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Color.White
        };

        // Water billing mode indicator
        _lblWaterMode = new Label
        {
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(37, 99, 235),
            Location = new Point(315, 74),
            AutoSize = true
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

        headerPanel.Controls.AddRange(new Control[] { lblTitle, lblSubtitle, lblMonth, _cmbBillingMonth, _lblWaterMode });

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
                Padding = new Padding(6, 8, 6, 8),
                Alignment = DataGridViewContentAlignment.MiddleCenter
            },
            EnableHeadersVisualStyles = false,
            ColumnHeadersHeight = 44,
            RowTemplate = { Height = 42 },
            GridColor = Color.FromArgb(226, 232, 240)
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
            new DataGridViewTextBoxColumn { Name = "ElecPrev", HeaderText = "ไฟ-ก่อน", FillWeight = 65,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N0" } },
            new DataGridViewTextBoxColumn { Name = "ElecCurr", HeaderText = "ไฟ-หลัง", FillWeight = 65,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N0", BackColor = Color.FromArgb(255, 251, 235) } },
            new DataGridViewTextBoxColumn { Name = "ElecUnits", HeaderText = "หน่วยไฟ", ReadOnly = true, FillWeight = 55,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N0", ForeColor = Color.FromArgb(234, 88, 12) } },
            new DataGridViewTextBoxColumn { Name = "ElecAmount", HeaderText = "ค่าไฟ (บาท)", ReadOnly = true, FillWeight = 70,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N2", Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = Color.FromArgb(234, 88, 12) } },
            
            // ประปา
            new DataGridViewTextBoxColumn { Name = "WaterPrev", HeaderText = "น้ำ-ก่อน", FillWeight = 65,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N0" } },
            new DataGridViewTextBoxColumn { Name = "WaterCurr", HeaderText = "น้ำ-หลัง", FillWeight = 65,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N0", BackColor = Color.FromArgb(236, 253, 245) } },
            new DataGridViewTextBoxColumn { Name = "WaterUnits", HeaderText = "หน่วยน้ำ", ReadOnly = true, FillWeight = 55,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N0", ForeColor = Color.FromArgb(14, 116, 144) } },
            new DataGridViewTextBoxColumn { Name = "WaterAmount", HeaderText = "ค่าน้ำ (บาท)", ReadOnly = true, FillWeight = 70,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N2", Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = Color.FromArgb(14, 116, 144) } },
            new DataGridViewTextBoxColumn { Name = "WaterPersons", HeaderText = "จำนวนคน", FillWeight = 55,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter, Format = "N0" } },
            
            // ยอดบิลรวมสุทธิ
            new DataGridViewTextBoxColumn { Name = "TotalBillAmount", HeaderText = "รวมสุทธิ (บาท)", ReadOnly = true, FillWeight = 85,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N2", Font = new Font("Segoe UI", 11F, FontStyle.Bold), ForeColor = Color.FromArgb(15, 23, 42) } },
            
            // ปุ่มดำเนินการตรงในตาราง
            new DataGridViewButtonColumn { Name = "BtnStatus", HeaderText = "สถานะชำระ", FillWeight = 80,
                Text = "ชำระเงิน", UseColumnTextForButtonValue = false },
            new DataGridViewButtonColumn { Name = "BtnPrint", HeaderText = "ออกบิล", FillWeight = 85,
                Text = "พิมพ์บิลเดียว", UseColumnTextForButtonValue = true },
            
            new DataGridViewTextBoxColumn { Name = "Notes", HeaderText = "หมายเหตุ", FillWeight = 80 }
        });

        _dgvMeterReadings.CellValueChanged += DgvMeterReadings_CellValueChanged;
        _dgvMeterReadings.CellContentClick += async (s, e) => await DgvMeterReadings_CellContentClick(s, e);
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

        // === Footer Section ===
        var footerPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 65,
            Padding = new Padding(5, 10, 5, 5)
        };

        _lblSummary = new Label
        {
            Text = "รวม: 0 ห้อง | ค่าไฟรวม: 0.00 บาท | ค่าน้ำรวม: 0.00 บาท | รวมบิลทั้งหมด: 0.00 บาท",
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59),
            Location = new Point(5, 18),
            AutoSize = true
        };

        // Main action buttons (No emojis)
        _btnOneClickProcess = new Button
        {
            Text = "บันทึกและออกบิลรวมทั้งหมด",
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Color.FromArgb(22, 163, 74),
            FlatStyle = FlatStyle.Flat,
            Size = new Size(240, 44),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Cursor = Cursors.Hand
        };
        _btnOneClickProcess.FlatAppearance.BorderSize = 0;
        _btnOneClickProcess.Click += async (s, e) => await ProcessOneClickSaveAndGenerateAsync();

        _btnBatchPrint = new Button
        {
            Text = "พิมพ์บิลทุกห้อง",
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Color.FromArgb(37, 99, 235),
            FlatStyle = FlatStyle.Flat,
            Size = new Size(150, 44),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Cursor = Cursors.Hand
        };
        _btnBatchPrint.FlatAppearance.BorderSize = 0;
        _btnBatchPrint.Click += async (s, e) => await PrintBatchInvoicesAsync();

        _btnViewHistory = new Button
        {
            Text = "ดูประวัติ / รายงาน",
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Color.FromArgb(100, 116, 139),
            FlatStyle = FlatStyle.Flat,
            Size = new Size(150, 44),
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
            Size = new Size(150, 44),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Cursor = Cursors.Hand
        };
        _btnConfigureRates.FlatAppearance.BorderSize = 0;
        _btnConfigureRates.Click += async (s, e) => await ConfigureRatesWithAdminAuthAsync();

        footerPanel.Controls.Add(_lblSummary);
        footerPanel.Controls.Add(_btnConfigureRates);
        footerPanel.Controls.Add(_btnViewHistory);
        footerPanel.Controls.Add(_btnBatchPrint);
        footerPanel.Controls.Add(_btnOneClickProcess);

        footerPanel.Resize += (s, e) =>
        {
            _btnOneClickProcess.Location = new Point(footerPanel.Width - _btnOneClickProcess.Width - 10, 10);
            _btnBatchPrint.Location = new Point(_btnOneClickProcess.Left - _btnBatchPrint.Width - 10, 10);
            _btnViewHistory.Location = new Point(_btnBatchPrint.Left - _btnViewHistory.Width - 10, 10);
            _btnConfigureRates.Location = new Point(_btnViewHistory.Left - _btnConfigureRates.Width - 10, 10);
        };

        Controls.Add(_dgvMeterReadings);
        Controls.Add(footerPanel);
        Controls.Add(headerPanel);
    }

    public async Task LoadMeterDataAsync()
    {
        if (_cmbBillingMonth.SelectedItem == null) return;
        string billingMonth = ((MonthItem)_cmbBillingMonth.SelectedItem).Value;

        _settings = await _settingsService.GetAllSettingsAsync();
        _rooms = (await _roomService.GetRoomsAsync()).ToList();

        bool isWaterMeter = _settings.WaterBillingMode == "METER";
        _lblWaterMode.Text = isWaterMeter
            ? $"น้ำ: ตามมิเตอร์ ({_settings.WaterRatePerUnit:N2} บาท/หน่วย) | ไฟ: {_settings.ElectricRatePerUnit:N2} บาท/หน่วย | ขยะ: {_settings.GarbageFee:N0} บาท"
            : $"น้ำ: เหมาจ่าย ({_settings.WaterFlatRatePerPerson:N2} บาท/คน) | ไฟ: {_settings.ElectricRatePerUnit:N2} บาท/หน่วย | ขยะ: {_settings.GarbageFee:N0} บาท";

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
            string tenantName = "ผู้เช่าทั่วไป";
            if (_bookingService != null && _customerService != null)
            {
                try
                {
                    var activeBooking = await _bookingService.GetActiveBookingByRoomIdAsync(room.Id);
                    if (activeBooking != null)
                    {
                        var cust = await _customerService.GetCustomerByIdAsync(activeBooking.CustomerId);
                        if (cust != null) tenantName = cust.FullName;
                    }
                }
                catch { }
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
            decimal elecAmount = elecReading?.TotalAmount ?? 0;
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
                isPaid ? "ชำระแล้ว" : "ชำระเงิน",
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

        if (colName == "BtnPrint")
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

        if (colName == "BtnStatus")
        {
            await SaveSingleRoomReadingAsync(row, roomId, billingMonth);
            
            int waterPersons = 1;
            if (int.TryParse(row.Cells["WaterPersons"].Value?.ToString(), out int p)) waterPersons = p;

            var bill = await _utilityBillService.GenerateMonthlyBillAsync(roomId, billingMonth, waterPersons);

            if (!bill.IsPaid)
            {
                if (MessageBox.Show($"บันทึกว่าห้อง {roomNumber} ชำระเงินเรียบร้อยแล้ว ยอด {bill.TotalAmount:N2} บาท?",
                    "ยืนยันการชำระเงิน", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    await _utilityBillService.MarkBillAsPaidAsync(bill.Id, PaymentMethod.Cash);
                    row.Cells["BtnStatus"].Value = "ชำระแล้ว";
                    row.Cells["BtnStatus"].Style.BackColor = Color.FromArgb(220, 252, 231);
                    row.Cells["BtnStatus"].Style.ForeColor = Color.FromArgb(22, 101, 52);
                }
            }
            else
            {
                MessageBox.Show($"ห้อง {roomNumber} ชำระเงินเรียบร้อยแล้ว", "ข้อมูลการชำระ", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            UpdateSummary();
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

        var bills = (await _utilityBillService.GetBillsByMonthAsync(billingMonth)).ToList();
        if (bills.Count == 0)
        {
            MessageBox.Show("ยังไม่มีใบแจ้งหนี้ในระบบ กรุณากดปุ่ม [บันทึกและออกบิลรวมทั้งหมด] ก่อน", "ไม่พบใบแจ้งหนี้", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        foreach (var bill in bills)
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
        using var authForm = new AdminAuthForm();
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
