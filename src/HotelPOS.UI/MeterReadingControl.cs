using HotelPOS.Common;
using HotelPOS.Common.Models;
using HotelPOS.Core.Services;

namespace HotelPOS.UI;

/// <summary>
/// หน้าจอบันทึกเลขมิเตอร์ค่าน้ำค่าไฟ รายห้อง รายเดือน
/// เจ้าของหอพักเดินเช็คมิเตอร์ → กลับมากรอกเลข → ระบบคำนวณให้อัตโนมัติ
/// </summary>
public class MeterReadingControl : UserControl
{
    private readonly IUtilityBillService _utilityBillService;
    private readonly IRoomService _roomService;
    private readonly ISettingsService _settingsService;

    private ComboBox _cmbBillingMonth = null!;
    private DataGridView _dgvMeterReadings = null!;
    private Label _lblSummary = null!;
    private Button _btnSave = null!;
    private Button _btnGenerateBills = null!;
    private Button _btnViewHistory = null!;
    private Label _lblWaterMode = null!;

    private List<Room> _rooms = new();
    private SystemSettingsDto _settings = null!;

    public MeterReadingControl(
        IUtilityBillService utilityBillService,
        IRoomService roomService,
        ISettingsService settingsService)
    {
        _utilityBillService = utilityBillService;
        _roomService = roomService;
        _settingsService = settingsService;

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
            Height = 110,
            Padding = new Padding(5)
        };

        var lblTitle = new Label
        {
            Text = "📊 บันทึกเลขมิเตอร์ค่าน้ำค่าไฟ",
            Font = new Font("Segoe UI", 16F, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Location = new Point(5, 5),
            AutoSize = true
        };

        var lblSubtitle = new Label
        {
            Text = "เดินเช็คมิเตอร์รายเดือน → กรอกเลข → ระบบคำนวณอัตโนมัติ",
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
            Location = new Point(5, 72),
            AutoSize = true
        };

        _cmbBillingMonth = new ComboBox
        {
            Location = new Point(130, 68),
            Width = 200,
            Font = new Font("Segoe UI", 11F),
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Color.White
        };

        // Water billing mode indicator
        _lblWaterMode = new Label
        {
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(37, 99, 235),
            Location = new Point(350, 72),
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
        // Select current month
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
                Font = new Font("Segoe UI", 11F),
                Padding = new Padding(6, 4, 6, 4),
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
            new DataGridViewTextBoxColumn { Name = "RoomNumber", HeaderText = "ห้อง", ReadOnly = true, FillWeight = 60,
                DefaultCellStyle = new DataGridViewCellStyle { Font = new Font("Segoe UI", 11F, FontStyle.Bold), Alignment = DataGridViewContentAlignment.MiddleCenter } },
            new DataGridViewTextBoxColumn { Name = "ElecPrev", HeaderText = "⚡ ไฟ-ก่อน", FillWeight = 80,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N0" } },
            new DataGridViewTextBoxColumn { Name = "ElecCurr", HeaderText = "⚡ ไฟ-หลัง", FillWeight = 80,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N0", BackColor = Color.FromArgb(255, 251, 235) } },
            new DataGridViewTextBoxColumn { Name = "ElecUnits", HeaderText = "หน่วยไฟ", ReadOnly = true, FillWeight = 70,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N0", ForeColor = Color.FromArgb(234, 88, 12) } },
            new DataGridViewTextBoxColumn { Name = "ElecAmount", HeaderText = "ค่าไฟ (฿)", ReadOnly = true, FillWeight = 85,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N2", Font = new Font("Segoe UI", 11F, FontStyle.Bold), ForeColor = Color.FromArgb(234, 88, 12) } },
            new DataGridViewTextBoxColumn { Name = "WaterPrev", HeaderText = "💧 น้ำ-ก่อน", FillWeight = 80,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N0" } },
            new DataGridViewTextBoxColumn { Name = "WaterCurr", HeaderText = "💧 น้ำ-หลัง", FillWeight = 80,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N0", BackColor = Color.FromArgb(236, 253, 245) } },
            new DataGridViewTextBoxColumn { Name = "WaterUnits", HeaderText = "หน่วยน้ำ", ReadOnly = true, FillWeight = 70,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N0", ForeColor = Color.FromArgb(14, 116, 144) } },
            new DataGridViewTextBoxColumn { Name = "WaterAmount", HeaderText = "ค่าน้ำ (฿)", ReadOnly = true, FillWeight = 85,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N2", Font = new Font("Segoe UI", 11F, FontStyle.Bold), ForeColor = Color.FromArgb(14, 116, 144) } },
            new DataGridViewTextBoxColumn { Name = "WaterPersons", HeaderText = "จำนวนคน", FillWeight = 65,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter, Format = "N0" } },
            new DataGridViewTextBoxColumn { Name = "Notes", HeaderText = "หมายเหตุ", FillWeight = 100 }
        });

        _dgvMeterReadings.CellValueChanged += DgvMeterReadings_CellValueChanged;
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
            Height = 60,
            Padding = new Padding(5, 10, 5, 5)
        };

        _lblSummary = new Label
        {
            Text = "รวม: 0 ห้อง | ค่าไฟรวม: ฿0 | ค่าน้ำรวม: ฿0",
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59),
            Location = new Point(5, 16),
            AutoSize = true
        };

        _btnSave = new Button
        {
            Text = "💾 บันทึกทั้งหมด",
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Color.FromArgb(22, 163, 74),
            FlatStyle = FlatStyle.Flat,
            Size = new Size(180, 40),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Cursor = Cursors.Hand
        };
        _btnSave.FlatAppearance.BorderSize = 0;
        _btnSave.Click += async (s, e) => await SaveAllReadingsAsync();

        _btnGenerateBills = new Button
        {
            Text = "📄 สร้างใบแจ้งหนี้",
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Color.FromArgb(37, 99, 235),
            FlatStyle = FlatStyle.Flat,
            Size = new Size(180, 40),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Cursor = Cursors.Hand
        };
        _btnGenerateBills.FlatAppearance.BorderSize = 0;
        _btnGenerateBills.Click += async (s, e) => await GenerateAllBillsAsync();

        _btnViewHistory = new Button
        {
            Text = "📋 ดูย้อนหลัง",
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Color.FromArgb(100, 116, 139),
            FlatStyle = FlatStyle.Flat,
            Size = new Size(150, 40),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Cursor = Cursors.Hand
        };
        _btnViewHistory.FlatAppearance.BorderSize = 0;
        _btnViewHistory.Click += (s, e) => ViewBillHistory();

        footerPanel.Controls.Add(_lblSummary);
        footerPanel.Controls.Add(_btnViewHistory);
        footerPanel.Controls.Add(_btnGenerateBills);
        footerPanel.Controls.Add(_btnSave);

        // Position buttons (right-aligned)
        footerPanel.Resize += (s, e) =>
        {
            _btnSave.Location = new Point(footerPanel.Width - _btnSave.Width - 10, 10);
            _btnGenerateBills.Location = new Point(_btnSave.Left - _btnGenerateBills.Width - 10, 10);
            _btnViewHistory.Location = new Point(_btnGenerateBills.Left - _btnViewHistory.Width - 10, 10);
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

        // Update water mode indicator
        bool isWaterMeter = _settings.WaterBillingMode == "METER";
        _lblWaterMode.Text = isWaterMeter
            ? $"💧 น้ำ: ตามมิเตอร์ ({_settings.WaterRatePerUnit:N2} ฿/หน่วย)"
            : $"💧 น้ำ: เหมาจ่าย ({_settings.WaterFlatRatePerPerson:N2} ฿/คน)";

        // Show/hide water meter columns based on mode
        _dgvMeterReadings.Columns["WaterPrev"]!.Visible = isWaterMeter;
        _dgvMeterReadings.Columns["WaterCurr"]!.Visible = isWaterMeter;
        _dgvMeterReadings.Columns["WaterUnits"]!.Visible = isWaterMeter;
        _dgvMeterReadings.Columns["WaterPersons"]!.Visible = !isWaterMeter;

        _dgvMeterReadings.Rows.Clear();

        foreach (var room in _rooms)
        {
            // ดึงเลขมิเตอร์เดือนก่อนหน้าอัตโนมัติ
            decimal elecPrev = await _utilityBillService.GetPreviousMeterValueAsync(room.Id, UtilityType.Electric, billingMonth);
            decimal waterPrev = await _utilityBillService.GetPreviousMeterValueAsync(room.Id, UtilityType.Water, billingMonth);

            // ดึงข้อมูลที่เคยบันทึกแล้ว (ถ้ามี)
            var readings = (await _utilityBillService.GetMeterReadingsByMonthAsync(billingMonth))
                .Where(r => r.RoomId == room.Id).ToList();
            var elecReading = readings.FirstOrDefault(r => r.UtilityType == UtilityType.Electric);
            var waterReading = readings.FirstOrDefault(r => r.UtilityType == UtilityType.Water);

            decimal elecCurr = elecReading?.ReadingCurr ?? 0;
            decimal waterCurr = waterReading?.ReadingCurr ?? 0;
            decimal elecUnits = elecReading?.UnitsUsed ?? 0;
            decimal waterUnits = waterReading?.UnitsUsed ?? 0;
            decimal elecAmount = elecReading?.TotalAmount ?? 0;
            decimal waterAmount;

            if (isWaterMeter)
            {
                waterAmount = waterReading?.TotalAmount ?? 0;
            }
            else
            {
                waterAmount = _settings.WaterFlatRatePerPerson; // default 1 person
            }

            // ถ้ามีข้อมูลเดิม ใช้ค่าเดิม, ถ้าไม่มีใช้ค่า prev ที่ดึงมา
            if (elecReading != null) elecPrev = elecReading.ReadingPrev;
            if (waterReading != null) waterPrev = waterReading.ReadingPrev;

            int rowIndex = _dgvMeterReadings.Rows.Add(
                room.Id,
                room.RoomNumber,
                elecPrev,
                elecCurr == 0 ? (object)"" : elecCurr,
                elecUnits,
                elecAmount,
                waterPrev,
                waterCurr == 0 ? (object)"" : waterCurr,
                waterUnits,
                waterAmount,
                1, // default 1 person
                elecReading?.Notes ?? ""
            );

            // Mark prev columns as read-only
            _dgvMeterReadings.Rows[rowIndex].Cells["ElecPrev"].ReadOnly = true;
            _dgvMeterReadings.Rows[rowIndex].Cells["WaterPrev"].ReadOnly = true;
        }

        UpdateSummary();
    }

    private void DgvMeterReadings_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0) return;

        var row = _dgvMeterReadings.Rows[e.RowIndex];
        string colName = _dgvMeterReadings.Columns[e.ColumnIndex].Name;

        // Auto-calculate electric
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

        // Auto-calculate water (METER mode)
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

        // Auto-calculate water (FLAT mode - per person)
        if (colName == "WaterPersons" && _settings.WaterBillingMode == "FLAT")
        {
            if (int.TryParse(row.Cells["WaterPersons"].Value?.ToString(), out int persons))
            {
                decimal amount = _settings.WaterFlatRatePerPerson * persons;
                row.Cells["WaterAmount"].Value = amount;
            }
        }

        UpdateSummary();
    }

    private void UpdateSummary()
    {
        decimal totalElec = 0, totalWater = 0;
        int roomCount = _dgvMeterReadings.Rows.Count;

        foreach (DataGridViewRow row in _dgvMeterReadings.Rows)
        {
            if (decimal.TryParse(row.Cells["ElecAmount"].Value?.ToString(), out decimal e)) totalElec += e;
            if (decimal.TryParse(row.Cells["WaterAmount"].Value?.ToString(), out decimal w)) totalWater += w;
        }

        _lblSummary.Text = $"รวม: {roomCount} ห้อง | ⚡ ค่าไฟรวม: ฿{totalElec:N2} | 💧 ค่าน้ำรวม: ฿{totalWater:N2} | 🏷️ รวมทั้งหมด: ฿{totalElec + totalWater:N2}";
    }

    private async Task SaveAllReadingsAsync()
    {
        if (_cmbBillingMonth.SelectedItem == null) return;
        string billingMonth = ((MonthItem)_cmbBillingMonth.SelectedItem).Value;

        int savedCount = 0;
        var errors = new List<string>();

        foreach (DataGridViewRow row in _dgvMeterReadings.Rows)
        {
            int roomId = Convert.ToInt32(row.Cells["RoomId"].Value);
            string roomNumber = row.Cells["RoomNumber"].Value?.ToString() ?? "";

            try
            {
                // Save Electric Meter
                if (decimal.TryParse(row.Cells["ElecPrev"].Value?.ToString(), out decimal elecPrev) &&
                    decimal.TryParse(row.Cells["ElecCurr"].Value?.ToString(), out decimal elecCurr) && elecCurr > 0)
                {
                    await _utilityBillService.RecordMeterReadingAsync(
                        roomId, UtilityType.Electric, elecPrev, elecCurr, billingMonth,
                        row.Cells["Notes"].Value?.ToString());
                    savedCount++;
                }

                // Save Water Meter (only in METER mode)
                if (_settings.WaterBillingMode == "METER")
                {
                    if (decimal.TryParse(row.Cells["WaterPrev"].Value?.ToString(), out decimal waterPrev) &&
                        decimal.TryParse(row.Cells["WaterCurr"].Value?.ToString(), out decimal waterCurr) && waterCurr > 0)
                    {
                        await _utilityBillService.RecordMeterReadingAsync(
                            roomId, UtilityType.Water, waterPrev, waterCurr, billingMonth,
                            row.Cells["Notes"].Value?.ToString());
                        savedCount++;
                    }
                }
            }
            catch (Exception ex)
            {
                errors.Add($"ห้อง {roomNumber}: {ex.Message}");
            }
        }

        if (errors.Count > 0)
        {
            MessageBox.Show($"บันทึกสำเร็จ {savedCount} รายการ\n\nข้อผิดพลาด:\n{string.Join("\n", errors)}",
                "⚠️ บันทึกไม่สมบูรณ์", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        else
        {
            MessageBox.Show($"✅ บันทึกเลขมิเตอร์สำเร็จ {savedCount} รายการ",
                "บันทึกสำเร็จ", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private async Task GenerateAllBillsAsync()
    {
        if (_cmbBillingMonth.SelectedItem == null) return;
        string billingMonth = ((MonthItem)_cmbBillingMonth.SelectedItem).Value;

        var confirm = MessageBox.Show(
            $"ต้องการสร้างใบแจ้งหนี้รายเดือนทุกห้อง สำหรับเดือน {((MonthItem)_cmbBillingMonth.SelectedItem).Display}?\n\n" +
            "ระบบจะรวม: ค่าเช่าห้อง + ค่าไฟ + ค่าน้ำ + ค่าบริการ + ค่าขยะ",
            "📄 ยืนยันสร้างใบแจ้งหนี้", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

        if (confirm != DialogResult.Yes) return;

        int generatedCount = 0;
        var errors = new List<string>();

        foreach (DataGridViewRow row in _dgvMeterReadings.Rows)
        {
            int roomId = Convert.ToInt32(row.Cells["RoomId"].Value);
            string roomNumber = row.Cells["RoomNumber"].Value?.ToString() ?? "";

            try
            {
                int waterPersons = 1;
                if (int.TryParse(row.Cells["WaterPersons"].Value?.ToString(), out int p)) waterPersons = p;

                await _utilityBillService.GenerateMonthlyBillAsync(roomId, billingMonth, waterPersons);
                generatedCount++;
            }
            catch (Exception ex)
            {
                errors.Add($"ห้อง {roomNumber}: {ex.Message}");
            }
        }

        if (errors.Count > 0)
        {
            MessageBox.Show($"สร้างใบแจ้งหนี้ {generatedCount} ห้อง\n\nข้อผิดพลาด:\n{string.Join("\n", errors)}",
                "⚠️ ไม่สมบูรณ์", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        else
        {
            MessageBox.Show($"✅ สร้างใบแจ้งหนี้สำเร็จ {generatedCount} ห้อง\n\nสามารถดูรายละเอียดและพิมพ์ใบแจ้งหนี้ได้ที่ปุ่ม 📋 ดูย้อนหลัง",
                "สร้างใบแจ้งหนี้สำเร็จ", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void ViewBillHistory()
    {
        if (_cmbBillingMonth.SelectedItem == null) return;
        string billingMonth = ((MonthItem)_cmbBillingMonth.SelectedItem).Value;

        using var historyForm = new UtilityBillHistoryForm(_utilityBillService, billingMonth);
        historyForm.ShowDialog();
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
