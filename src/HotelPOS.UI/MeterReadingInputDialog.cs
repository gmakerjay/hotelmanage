using System;
using System.Drawing;
using System.Windows.Forms;
using HotelPOS.Common;
using HotelPOS.Common.Models;
using HotelPOS.Core.Services;

namespace HotelPOS.UI;

/// <summary>
/// Pop-up Dialog สำหรับบันทึกค่าน้ำ-ค่าไฟ และค่าบริการรายเดือน
/// ดึงเลขมิเตอร์เดือนก่อนหน้าให้อัตโนมัติ ผู้ใช้มีหน้าที่คีย์เฉพาะเลขมิเตอร์ล่าสุดของเดือนปัจจุบัน
/// ระบบจะทำการหักลบ คำนวณหน่วยที่ใช้ และสรุปยอดเงินรวมให้อัตโนมัติ
/// </summary>
public class MeterReadingInputDialog : Form
{
    public decimal ElecPrev => (decimal)_numElecPrev.Value;
    public decimal ElecCurr => (decimal)_numElecCurr.Value;
    public decimal ElecUnits => Math.Max(0, ElecCurr - ElecPrev);
    public decimal ElecAmount
    {
        get
        {
            if (_settings.ElectricBillingMode == "FLAT")
                return _settings.ElectricFlatRate;
            return ElecUnits * _settings.ElectricRatePerUnit;
        }
    }

    public decimal WaterPrev => (decimal)_numWaterPrev.Value;
    public decimal WaterCurr => (decimal)_numWaterCurr.Value;
    public decimal WaterUnits => Math.Max(0, WaterCurr - WaterPrev);
    public int WaterPersons => (int)_numWaterPersons.Value;
    public decimal WaterAmount
    {
        get
        {
            if (_settings.WaterBillingMode == "FLAT")
                return WaterPersons * _settings.WaterFlatRatePerPerson;
            return WaterUnits * _settings.WaterRatePerUnit;
        }
    }

    public decimal RoomRate => (decimal)_numRoomRate.Value;
    public decimal CommonAreaFee => (decimal)_numCommonArea.Value;
    public decimal GarbageFee => (decimal)_numGarbage.Value;
    public decimal ExtraCharges => (decimal)_numExtraCharges.Value;
    public decimal DiscountAmount => (decimal)_numDiscount.Value;
    public string Notes => _txtNotes.Text.Trim();

    public decimal TotalAmount
    {
        get => Math.Max(0, RoomRate + ElecAmount + WaterAmount + CommonAreaFee + GarbageFee + ExtraCharges - DiscountAmount);
    }

    public bool PrintBillRequested => _printRequested;
    public bool MarkAsPaidRequested => _markAsPaidRequested;
    public PaymentMethod SelectedPaymentMethod => _selectedPaymentMethod;

    private readonly Room _room;
    private readonly string _tenantName;
    private readonly string _billingMonth;
    private readonly SystemSettingsDto _settings;
    private readonly ISettingsService _settingsService;

    private NumericUpDown _numElecPrev = null!;
    private NumericUpDown _numElecCurr = null!;
    private Label _lblElecUnits = null!;
    private Label _lblElecAmount = null!;

    private NumericUpDown _numWaterPrev = null!;
    private NumericUpDown _numWaterCurr = null!;
    private NumericUpDown _numWaterPersons = null!;
    private Label _lblWaterUnits = null!;
    private Label _lblWaterAmount = null!;

    private NumericUpDown _numRoomRate = null!;
    private NumericUpDown _numCommonArea = null!;
    private NumericUpDown _numGarbage = null!;
    private NumericUpDown _numExtraCharges = null!;
    private NumericUpDown _numDiscount = null!;
    private TextBox _txtNotes = null!;

    private Label _lblTotalAmount = null!;
    private bool _printRequested = false;
    private bool _markAsPaidRequested = false;
    private PaymentMethod _selectedPaymentMethod = PaymentMethod.Cash;

    public MeterReadingInputDialog(
        Room room,
        string tenantName,
        string billingMonth,
        decimal roomRate,
        decimal elecPrev,
        decimal elecCurr,
        decimal waterPrev,
        decimal waterCurr,
        int waterPersons,
        decimal extraCharges,
        decimal discountAmount,
        string notes,
        SystemSettingsDto settings,
        ISettingsService settingsService)
    {
        _room = room;
        _tenantName = tenantName;
        _billingMonth = billingMonth;
        _settings = settings;
        _settingsService = settingsService;

        InitializeUI(roomRate, elecPrev, elecCurr, waterPrev, waterCurr, waterPersons, extraCharges, discountAmount, notes);
        CalculateTotal();
    }

    private void InitializeUI(
        decimal roomRate,
        decimal elecPrev,
        decimal elecCurr,
        decimal waterPrev,
        decimal waterCurr,
        int waterPersons,
        decimal extraCharges,
        decimal discountAmount,
        string notes)
    {
        Text = $"บันทึกและคำนวณค่าน้ำ-ค่าไฟ | ห้อง {_room.RoomNumber}";
        Size = new Size(720, 730);
        MinimumSize = new Size(700, 680);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.FromArgb(248, 250, 252);
        Font = new Font("Segoe UI", 10F, FontStyle.Regular);

        // Header Banner
        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 70,
            BackColor = Color.FromArgb(30, 41, 59),
            Padding = new Padding(15, 10, 15, 10)
        };

        var lblHeaderRoom = new Label
        {
            Text = $"ห้อง {_room.RoomNumber}  (ผู้เช่า: {_tenantName})",
            Font = new Font("Segoe UI", 14F, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(15, 10),
            AutoSize = true
        };

        var lblHeaderMonth = new Label
        {
            Text = $"รอบบิลเดือน: {_billingMonth}  (คีย์เฉพาะเลขมิเตอร์ล่าสุดของเดือนนี้ ระบบคำนวณให้อัตโนมัติ)",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
            ForeColor = Color.FromArgb(203, 213, 225),
            Location = new Point(16, 38),
            AutoSize = true
        };

        var btnAdminOverride = new Button
        {
            Text = "ปลดล็อคมิเตอร์ก่อนหน้า (Admin)",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            BackColor = Color.FromArgb(239, 68, 68),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(180, 28),
            Location = new Point(485, 15),
            Cursor = Cursors.Hand
        };
        btnAdminOverride.FlatAppearance.BorderSize = 0;
        btnAdminOverride.Click += async (s, e) =>
        {
            if (await PromptAdminPasswordAsync())
            {
                if (_numElecPrev != null) { _numElecPrev.Enabled = true; _numElecPrev.ReadOnly = false; _numElecPrev.BackColor = Color.White; }
                if (_numWaterPrev != null) { _numWaterPrev.Enabled = true; _numWaterPrev.ReadOnly = false; _numWaterPrev.BackColor = Color.White; }
                MessageBox.Show("ปลดล็อคมิเตอร์ก่อนหน้าสำเร็จ คุณสามารถแก้ไขตัวเลขก่อนหน้าได้แล้ว", "สำเร็จ", MessageBoxButtons.OK, MessageBoxIcon.Information);
                btnAdminOverride.Enabled = false;
            }
            else
            {
                MessageBox.Show("รหัสผ่านไม่ถูกต้อง", "ล้มเหลว", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        };

        pnlHeader.Controls.Add(lblHeaderRoom);
        pnlHeader.Controls.Add(lblHeaderMonth);
        pnlHeader.Controls.Add(btnAdminOverride);

        // Main Container (Explicitly placed below pnlHeader Y=70)
        var pnlBody = new Panel
        {
            Location = new Point(0, 70),
            Size = new Size(705, 505),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            Padding = new Padding(15),
            AutoScroll = true,
            BackColor = Color.FromArgb(248, 250, 252)
        };

        int curY = 15;

        // Group 1: ค่าไฟฟ้า (Electricity)
        bool isElecMeter = _settings.ElectricBillingMode == "METER";
        var grpElec = new GroupBox
        {
            Text = isElecMeter ? "ค่าไฟฟ้า (ตามมิเตอร์)" : "ค่าไฟฟ้า (เหมาจ่ายรายเดือน)",
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(194, 65, 12),
            Location = new Point(15, curY),
            Size = new Size(670, 120),
            BackColor = Color.White
        };

        if (isElecMeter)
        {
            var lblElecPrev = new Label { Text = "มิเตอร์ก่อนหน้า (ล็อก):", Location = new Point(15, 30), Size = new Size(140, 24), Font = new Font("Segoe UI", 9.5F, FontStyle.Regular), ForeColor = Color.FromArgb(71, 85, 105) };
            _numElecPrev = new NumericUpDown
            {
                Location = new Point(160, 27),
                Size = new Size(120, 28),
                Maximum = 999999,
                DecimalPlaces = 0,
                Value = elecPrev,
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                ReadOnly = true,
                Enabled = false,
                BackColor = Color.FromArgb(241, 245, 249)
            };
            _numElecPrev.ValueChanged += (s, e) => CalculateTotal();

            var lblElecCurr = new Label { Text = "มิเตอร์ล่าสุด (คีย์ช่องนี้):", Location = new Point(300, 30), Size = new Size(145, 24), Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), ForeColor = Color.FromArgb(15, 23, 42) };
            _numElecCurr = new NumericUpDown
            {
                Location = new Point(450, 27),
                Size = new Size(140, 28),
                Maximum = 999999,
                DecimalPlaces = 0,
                Value = elecCurr,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                BackColor = Color.FromArgb(254, 243, 199)
            };
            _numElecCurr.ValueChanged += (s, e) => CalculateTotal();

            _lblElecUnits = new Label { Text = "หน่วยที่ใช้: 0 หน่วย", Location = new Point(15, 75), Size = new Size(240, 24), Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = Color.FromArgb(194, 65, 12) };
            _lblElecAmount = new Label { Text = $"คิดเป็นเงิน (@ {_settings.ElectricRatePerUnit:N2} บ./หน่วย): 0.00 บาท", Location = new Point(270, 75), Size = new Size(380, 24), Font = new Font("Segoe UI", 10.5F, FontStyle.Bold), ForeColor = Color.FromArgb(194, 65, 12), TextAlign = ContentAlignment.TopRight };

            grpElec.Controls.AddRange(new Control[] { lblElecPrev, _numElecPrev, lblElecCurr, _numElecCurr, _lblElecUnits, _lblElecAmount });
        }
        else
        {
            _numElecPrev = new NumericUpDown { Value = 0, Visible = false };
            _numElecCurr = new NumericUpDown { Value = 0, Visible = false };
            _lblElecUnits = new Label { Visible = false };
            _lblElecAmount = new Label { Text = $"ค่าไฟเหมาจ่ายรายเดือน: {_settings.ElectricFlatRate:N2} บาท", Location = new Point(15, 40), Size = new Size(635, 30), Font = new Font("Segoe UI", 12F, FontStyle.Bold), ForeColor = Color.FromArgb(194, 65, 12), TextAlign = ContentAlignment.MiddleLeft };

            grpElec.Controls.Add(_lblElecAmount);
        }

        pnlBody.Controls.Add(grpElec);
        curY += 130;

        // Group 2: ค่าน้ำประปา (Water Supply)
        bool isWaterMeter = _settings.WaterBillingMode == "METER";
        var grpWater = new GroupBox
        {
            Text = isWaterMeter ? "ค่าน้ำประปา (ตามมิเตอร์)" : "ค่าน้ำประปา (เหมาจ่ายรายคน)",
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(14, 116, 144),
            Location = new Point(15, curY),
            Size = new Size(670, 120),
            BackColor = Color.White
        };

        if (isWaterMeter)
        {
            var lblWaterPrev = new Label { Text = "มิเตอร์ก่อนหน้า (ล็อก):", Location = new Point(15, 30), Size = new Size(140, 24), Font = new Font("Segoe UI", 9.5F, FontStyle.Regular), ForeColor = Color.FromArgb(71, 85, 105) };
            _numWaterPrev = new NumericUpDown
            {
                Location = new Point(160, 27),
                Size = new Size(120, 28),
                Maximum = 999999,
                DecimalPlaces = 0,
                Value = waterPrev,
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                ReadOnly = true,
                Enabled = false,
                BackColor = Color.FromArgb(241, 245, 249)
            };
            _numWaterPrev.ValueChanged += (s, e) => CalculateTotal();

            var lblWaterCurr = new Label { Text = "มิเตอร์ล่าสุด (คีย์ช่องนี้):", Location = new Point(300, 30), Size = new Size(145, 24), Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), ForeColor = Color.FromArgb(15, 23, 42) };
            _numWaterCurr = new NumericUpDown
            {
                Location = new Point(450, 27),
                Size = new Size(140, 28),
                Maximum = 999999,
                DecimalPlaces = 0,
                Value = waterCurr,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                BackColor = Color.FromArgb(207, 250, 254)
            };
            _numWaterCurr.ValueChanged += (s, e) => CalculateTotal();

            _numWaterPersons = new NumericUpDown { Value = 1, Visible = false };

            _lblWaterUnits = new Label { Text = "หน่วยที่ใช้: 0 หน่วย", Location = new Point(15, 75), Size = new Size(240, 24), Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = Color.FromArgb(14, 116, 144) };
            _lblWaterAmount = new Label { Text = $"คิดเป็นเงิน (@ {_settings.WaterRatePerUnit:N2} บ./หน่วย): 0.00 บาท", Location = new Point(270, 75), Size = new Size(380, 24), Font = new Font("Segoe UI", 10.5F, FontStyle.Bold), ForeColor = Color.FromArgb(14, 116, 144), TextAlign = ContentAlignment.TopRight };

            grpWater.Controls.AddRange(new Control[] { lblWaterPrev, _numWaterPrev, lblWaterCurr, _numWaterCurr, _lblWaterUnits, _lblWaterAmount });
        }
        else
        {
            _numWaterPrev = new NumericUpDown { Value = 0, Visible = false };
            _numWaterCurr = new NumericUpDown { Value = 0, Visible = false };

            var lblPersons = new Label { Text = "จำนวนคนพักในห้อง:", Location = new Point(15, 35), Size = new Size(140, 24), Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
            _numWaterPersons = new NumericUpDown
            {
                Location = new Point(160, 32),
                Size = new Size(100, 28),
                Minimum = 1,
                Maximum = 50,
                Value = Math.Max(1, waterPersons),
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold)
            };
            _numWaterPersons.ValueChanged += (s, e) => CalculateTotal();

            _lblWaterUnits = new Label { Visible = false };
            _lblWaterAmount = new Label { Text = $"ค่าน้ำเหมาจ่าย (@ {_settings.WaterFlatRatePerPerson:N2} บ./คน): 0.00 บาท", Location = new Point(270, 35), Size = new Size(380, 24), Font = new Font("Segoe UI", 10.5F, FontStyle.Bold), ForeColor = Color.FromArgb(14, 116, 144), TextAlign = ContentAlignment.TopRight };

            grpWater.Controls.AddRange(new Control[] { lblPersons, _numWaterPersons, _lblWaterAmount });
        }

        pnlBody.Controls.Add(grpWater);
        curY += 130;

        // Group 3: ค่าห้องพัก & ค่าบริการอื่นๆ / ค่าจิปาถะ
        var grpFees = new GroupBox
        {
            Text = "ค่าห้องพักและค่าบริการอื่นๆ / ค่าจิปาถะ",
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59),
            Location = new Point(15, curY),
            Size = new Size(670, 160),
            BackColor = Color.White
        };

        int fy = 26;
        var lblRoomRate = new Label { Text = "ค่าเช่าห้องพัก:", Location = new Point(15, fy), Size = new Size(110, 24), Font = new Font("Segoe UI", 9.5F) };
        _numRoomRate = new NumericUpDown { Location = new Point(125, fy - 3), Size = new Size(120, 27), Maximum = 500000, DecimalPlaces = 2, Value = roomRate, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
        _numRoomRate.ValueChanged += (s, e) => CalculateTotal();

        var lblCommonArea = new Label { Text = "ค่าส่วนกลาง:", Location = new Point(265, fy), Size = new Size(95, 24), Font = new Font("Segoe UI", 9.5F) };
        _numCommonArea = new NumericUpDown { Location = new Point(360, fy - 3), Size = new Size(100, 27), Maximum = 50000, DecimalPlaces = 2, Value = _settings.CommonAreaFee, Font = new Font("Segoe UI", 10F) };
        _numCommonArea.ValueChanged += (s, e) => CalculateTotal();

        var lblGarbage = new Label { Text = "ค่าขยะ:", Location = new Point(480, fy), Size = new Size(65, 24), Font = new Font("Segoe UI", 9.5F) };
        _numGarbage = new NumericUpDown { Location = new Point(545, fy - 3), Size = new Size(100, 27), Maximum = 50000, DecimalPlaces = 2, Value = _settings.GarbageFee, Font = new Font("Segoe UI", 10F) };
        _numGarbage.ValueChanged += (s, e) => CalculateTotal();

        fy += 40;
        var lblExtra = new Label { Text = "ค่าอื่นๆ/จิปาถะ:", Location = new Point(15, fy), Size = new Size(110, 24), Font = new Font("Segoe UI", 9.5F) };
        _numExtraCharges = new NumericUpDown { Location = new Point(125, fy - 3), Size = new Size(120, 27), Maximum = 100000, DecimalPlaces = 2, Value = extraCharges, Font = new Font("Segoe UI", 10F) };
        _numExtraCharges.ValueChanged += (s, e) => CalculateTotal();

        var lblDiscount = new Label { Text = "ส่วนลดพิเศษ:", Location = new Point(265, fy), Size = new Size(95, 24), Font = new Font("Segoe UI", 9.5F), ForeColor = Color.DarkRed };
        _numDiscount = new NumericUpDown { Location = new Point(360, fy - 3), Size = new Size(100, 27), Maximum = 100000, DecimalPlaces = 2, Value = discountAmount, Font = new Font("Segoe UI", 10F), ForeColor = Color.DarkRed };
        _numDiscount.ValueChanged += (s, e) => CalculateTotal();

        fy += 40;
        var lblNotes = new Label { Text = "หมายเหตุ:", Location = new Point(15, fy), Size = new Size(110, 24), Font = new Font("Segoe UI", 9.5F) };
        _txtNotes = new TextBox { Location = new Point(125, fy - 3), Size = new Size(520, 27), Text = notes, Font = new Font("Segoe UI", 9.5F) };

        grpFees.Controls.AddRange(new Control[] { lblRoomRate, _numRoomRate, lblCommonArea, _numCommonArea, lblGarbage, _numGarbage, lblExtra, _numExtraCharges, lblDiscount, _numDiscount, lblNotes, _txtNotes });
        pnlBody.Controls.Add(grpFees);
        curY += 170;

        // High-Contrast Summary Card & Validation Banner
        var pnlTotal = new Panel
        {
            Location = new Point(15, curY),
            Size = new Size(670, 75),
            BackColor = Color.FromArgb(15, 23, 42), // Slate 900
            Padding = new Padding(15, 10, 15, 10)
        };

        var lblTotalTitle = new Label
        {
            Text = "สรุปยอดสุทธิที่ต้องจัดเก็บ (NET TOTAL DUE):",
            Font = new Font("Segoe UI", 11.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(148, 163, 184),
            Location = new Point(15, 12),
            AutoSize = true
        };

        _lblTotalAmount = new Label
        {
            Text = "0.00 บาท",
            Font = new Font("Segoe UI", 20F, FontStyle.Bold),
            ForeColor = Color.FromArgb(74, 222, 128), // Bright Emerald Green
            Location = new Point(300, 30),
            Size = new Size(355, 38),
            TextAlign = ContentAlignment.MiddleRight
        };

        var lblValidationWarn = new Label
        {
            Name = "lblValidationWarn",
            Text = "",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(248, 113, 113),
            Location = new Point(15, 42),
            AutoSize = true,
            Visible = false
        };

        pnlTotal.Controls.Add(lblTotalTitle);
        pnlTotal.Controls.Add(_lblTotalAmount);
        pnlTotal.Controls.Add(lblValidationWarn);
        pnlBody.Controls.Add(pnlTotal);

        // Footer Actions
        var pnlFooter = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 65,
            BackColor = Color.White,
            Padding = new Padding(15, 10, 15, 10)
        };

        var btnSaveAndBill = new Button
        {
            Text = "ออกใบแจ้งหนี้ (ค้างชำระ)",
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
            BackColor = Color.FromArgb(37, 99, 235),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(185, 42),
            Location = new Point(175, 10),
            Cursor = Cursors.Hand,
            DialogResult = DialogResult.OK
        };
        btnSaveAndBill.FlatAppearance.BorderSize = 0;
        btnSaveAndBill.Click += (s, e) =>
        {
            if (_settings.ElectricBillingMode == "METER" && ElecCurr < ElecPrev)
            {
                MessageBox.Show($"เลขมิเตอร์ไฟล่าสุด ({ElecCurr:N0}) น้อยกว่าเลขก่อนหน้า ({ElecPrev:N0})\nกรุณาตรวจสอบและแก้ไขให้ถูกต้อง", "ข้อมูลไม่ถูกต้อง", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
                return;
            }

            if (_settings.WaterBillingMode == "METER" && WaterCurr < WaterPrev)
            {
                MessageBox.Show($"เลขมิเตอร์น้ำล่าสุด ({WaterCurr:N0}) น้อยกว่าเลขก่อนหน้า ({WaterPrev:N0})\nกรุณาตรวจสอบและแก้ไขให้ถูกต้อง", "ข้อมูลไม่ถูกต้อง", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
                return;
            }

            _printRequested = true;
            _markAsPaidRequested = false;
            DialogResult = DialogResult.OK;
        };

        var btnPayNow = new Button
        {
            Text = "รับชำระเงินทันที (ออกใบเสร็จ)",
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
            BackColor = Color.FromArgb(22, 163, 74),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(210, 42),
            Location = new Point(370, 10),
            Cursor = Cursors.Hand
        };
        btnPayNow.FlatAppearance.BorderSize = 0;
        btnPayNow.Click += (s, e) =>
        {
            if (_settings.ElectricBillingMode == "METER" && ElecCurr < ElecPrev)
            {
                MessageBox.Show($"เลขมิเตอร์ไฟล่าสุด ({ElecCurr:N0}) น้อยกว่าเลขก่อนหน้า ({ElecPrev:N0})\nกรุณาตรวจสอบและแก้ไขให้ถูกต้อง", "ข้อมูลไม่ถูกต้อง", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
                return;
            }

            if (_settings.WaterBillingMode == "METER" && WaterCurr < WaterPrev)
            {
                MessageBox.Show($"เลขมิเตอร์น้ำล่าสุด ({WaterCurr:N0}) น้อยกว่าเลขก่อนหน้า ({WaterPrev:N0})\nกรุณาตรวจสอบและแก้ไขให้ถูกต้อง", "ข้อมูลไม่ถูกต้อง", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
                return;
            }

            string elecDetail = isElecMeter ? $"({ElecUnits:N0} หน่วย)" : "(เหมาจ่าย)";
            string waterDetail = isWaterMeter ? $"({WaterUnits:N0} หน่วย)" : $"(เหมาจ่าย {WaterPersons} คน)";

            using var confirmDlg = new PaymentConfirmationDialog(
                _room.RoomNumber,
                _tenantName,
                RoomRate,
                ElecAmount,
                elecDetail,
                WaterAmount,
                waterDetail,
                CommonAreaFee,
                GarbageFee,
                ExtraCharges,
                DiscountAmount,
                TotalAmount
            );

            if (confirmDlg.ShowDialog() != DialogResult.Yes)
            {
                DialogResult = DialogResult.None;
                return;
            }

            _markAsPaidRequested = true;

            // ถามยืนยันการพิมพ์ใบเสร็จรับเงินทันที
            var printAsk = MessageBox.Show(
                "บันทึกรับชำระเงินสำเร็จเรียบร้อย!\n\nต้องการพิมพ์ [ใบเสร็จรับเงิน] ทันทีหรือไม่?",
                "พิมพ์ใบเสร็จรับเงิน", MessageBoxButtons.YesNo, MessageBoxIcon.Information);

            _printRequested = (printAsk == DialogResult.Yes);
            DialogResult = DialogResult.OK;
        };

        var btnCancel = new Button
        {
            Text = "ยกเลิก",
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            BackColor = Color.FromArgb(241, 245, 249),
            ForeColor = Color.FromArgb(71, 85, 105),
            FlatStyle = FlatStyle.Flat,
            Size = new Size(90, 42),
            Location = new Point(590, 10),
            Cursor = Cursors.Hand,
            DialogResult = DialogResult.Cancel
        };
        btnCancel.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);

        pnlFooter.Controls.AddRange(new Control[] { btnSaveAndBill, btnPayNow, btnCancel });

        // Add controls to Form in correct Docking Order in WinForms: Top first, Bottom second, Fill LAST!
        Controls.Add(pnlHeader);
        Controls.Add(pnlFooter);
        Controls.Add(pnlBody);

        // Auto focus to current electric meter reading and reset scroll to top
        Shown += (s, e) =>
        {
            pnlBody.AutoScrollPosition = new Point(0, 0);
            if (isElecMeter && _numElecCurr != null && _numElecCurr.Visible)
            {
                _numElecCurr.Focus();
                _numElecCurr.Select(0, _numElecCurr.Text.Length);
            }
            else if (isWaterMeter && _numWaterCurr != null && _numWaterCurr.Visible)
            {
                _numWaterCurr.Focus();
                _numWaterCurr.Select(0, _numWaterCurr.Text.Length);
            }
            pnlBody.AutoScrollPosition = new Point(0, 0);
        };
    }

    private void CalculateTotal()
    {
        if (_lblElecUnits == null) return;

        bool elecInvalid = _settings.ElectricBillingMode == "METER" && ElecCurr < ElecPrev;
        bool waterInvalid = _settings.WaterBillingMode == "METER" && WaterCurr < WaterPrev;

        var lblWarn = Controls.Find("lblValidationWarn", true).FirstOrDefault() as Label;
        if (lblWarn != null)
        {
            if (elecInvalid && waterInvalid)
            {
                lblWarn.Text = "⚠️ เลขมิเตอร์ไฟและน้ำล่าสุด น้อยกว่าเลขก่อนหน้า!";
                lblWarn.Visible = true;
            }
            else if (elecInvalid)
            {
                lblWarn.Text = "⚠️ เลขมิเตอร์ไฟล่าสุด น้อยกว่าเลขก่อนหน้า!";
                lblWarn.Visible = true;
            }
            else if (waterInvalid)
            {
                lblWarn.Text = "⚠️ เลขมิเตอร์น้ำล่าสุด น้อยกว่าเลขก่อนหน้า!";
                lblWarn.Visible = true;
            }
            else
            {
                lblWarn.Visible = false;
            }
        }

        // Elec Units & Amount
        if (_settings.ElectricBillingMode == "METER")
        {
            var eUnits = ElecUnits;
            var eAmt = ElecAmount;
            _lblElecUnits.Text = $"หน่วยที่ใช้: {eUnits:N0} หน่วย";
            _lblElecAmount.Text = $"คิดเป็นเงิน (@ {_settings.ElectricRatePerUnit:N2} บ./หน่วย): {eAmt:N2} บาท";
        }
        else
        {
            var eAmt = ElecAmount;
            _lblElecAmount.Text = $"ค่าไฟเหมาจ่ายรายเดือน: {eAmt:N2} บาท";
        }

        // Water Units & Amount
        if (_settings.WaterBillingMode == "METER")
        {
            var wUnits = WaterUnits;
            var wAmt = WaterAmount;
            _lblWaterUnits.Text = $"หน่วยที่ใช้: {wUnits:N0} หน่วย";
            _lblWaterAmount.Text = $"คิดเป็นเงิน (@ {_settings.WaterRatePerUnit:N2} บ./หน่วย): {wAmt:N2} บาท";
        }
        else
        {
            var wAmt = WaterAmount;
            _lblWaterAmount.Text = $"ค่าน้ำเหมาจ่าย ({WaterPersons} คน @ {_settings.WaterFlatRatePerPerson:N2} บ./คน): {wAmt:N2} บาท";
        }

        // Grand Total
        _lblTotalAmount.Text = $"{TotalAmount:N2} บาท";
    }

    private async Task<bool> PromptAdminPasswordAsync()
    {
        using var frm = new Form
        {
            Width = 300,
            Height = 150,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            Text = "ยืนยันรหัสผ่าน Admin",
            StartPosition = FormStartPosition.CenterParent,
            MaximizeBox = false,
            MinimizeBox = false
        };

        var lbl = new Label { Left = 20, Top = 20, Text = "รหัสผ่าน Admin:" };
        var txt = new TextBox { Left = 20, Top = 45, Width = 240, UseSystemPasswordChar = true };
        var btnOk = new Button { Text = "ตกลง", Left = 100, Top = 75, Width = 80, DialogResult = DialogResult.OK };
        var btnCancel = new Button { Text = "ยกเลิก", Left = 180, Top = 75, Width = 80, DialogResult = DialogResult.Cancel };

        frm.Controls.AddRange(new Control[] { lbl, txt, btnOk, btnCancel });
        frm.AcceptButton = btnOk;
        frm.CancelButton = btnCancel;

        if (frm.ShowDialog() == DialogResult.OK)
        {
            string input = txt.Text;
            string dbPassword = await _settingsService.GetAsync("admin_password") ?? "psoft123";
            if (string.IsNullOrWhiteSpace(dbPassword)) dbPassword = "psoft123";

            var (isMatch, _) = PasswordHelper.VerifyPassword(input, dbPassword);
            return isMatch;
        }

        return false;
    }
}
