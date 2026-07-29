using HotelPOS.Common.Models;
using HotelPOS.Core.Services;

namespace HotelPOS.UI;

/// <summary>
/// ฟอร์มตั้งค่าอัตราค่าน้ำค่าไฟต่อหน่วย (กี่บาท : 1 หน่วย)
/// </summary>
public class UtilityRateSettingsForm : Form
{
    private readonly ISettingsService _settingsService;

    private ComboBox _cboElectricMode = null!;
    private NumericUpDown _numElectricRate = null!;
    private NumericUpDown _numElectricFlatRate = null!;
    private ComboBox _cboWaterMode = null!;
    private NumericUpDown _numWaterRate = null!;
    private NumericUpDown _numWaterFlatRate = null!;
    private NumericUpDown _numCommonAreaFee = null!;
    private NumericUpDown _numGarbageFee = null!;

    private Button _btnSave = null!;
    private Button _btnCancel = null!;

    private SystemSettingsDto _settings = null!;

    public UtilityRateSettingsForm(ISettingsService settingsService)
    {
        _settingsService = settingsService;

        InitializeComponents();
        Load += async (s, e) => await LoadSettingsAsync();
    }

    private void InitializeComponents()
    {
        Text = "ตั้งค่าอัตราค่าน้ำค่าไฟต่อหน่วย (Admin Authorized)";
        Width = 540;
        Height = 510;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Font = new Font("Segoe UI", 10.5F);
        BackColor = Color.FromArgb(245, 247, 250);

        var lblHeader = new Label
        {
            Text = "ตั้งค่าอัตราค่าน้ำค่าไฟและบริการ",
            Font = new Font("Segoe UI", 13F, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Location = new Point(20, 15),
            AutoSize = true
        };

        var lblSubheader = new Label
        {
            Text = "กำหนดอัตราค่าหน่วยและโหมดคิดค่าน้ำ-ค่าไฟสำหรับคำนวณบิลรายเดือน",
            Font = new Font("Segoe UI", 9.5F),
            ForeColor = Color.FromArgb(100, 116, 139),
            Location = new Point(20, 44),
            AutoSize = true
        };

        int currentY = 80;

        // 1. โหมดค่าไฟ
        var lblElecMode = new Label { Text = "รูปแบบการคิดค่าไฟฟ้า:", Location = new Point(20, currentY), AutoSize = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
        _cboElectricMode = new ComboBox
        {
            Location = new Point(290, currentY - 4),
            Width = 200,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 10.5F)
        };
        _cboElectricMode.Items.AddRange(new object[] { "ตามมิเตอร์ (METER)", "เหมาจ่ายรายเดือน (FLAT)" });
        _cboElectricMode.SelectedIndex = 0;
        currentY += 42;

        // 2. ค่าไฟต่อหน่วย
        var lblElec = new Label { Text = "อัตราค่าไฟฟ้า (บาท : 1 หน่วย):", Location = new Point(20, currentY), AutoSize = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
        _numElectricRate = new NumericUpDown
        {
            Location = new Point(290, currentY - 4),
            Width = 200,
            DecimalPlaces = 2,
            Maximum = 1000,
            Minimum = 0,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor = Color.FromArgb(234, 88, 12)
        };
        currentY += 42;

        // 3. ค่าไฟเหมาจ่าย (บาท/เดือน)
        var lblElecFlat = new Label { Text = "ค่าไฟเหมาจ่าย (บาท / เดือน):", Location = new Point(20, currentY), AutoSize = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
        _numElectricFlatRate = new NumericUpDown
        {
            Location = new Point(290, currentY - 4),
            Width = 200,
            DecimalPlaces = 2,
            Maximum = 100000,
            Minimum = 0,
            Font = new Font("Segoe UI", 10.5F)
        };
        currentY += 42;

        // 4. โหมดค่าน้ำ
        var lblWaterMode = new Label { Text = "รูปแบบการคิดค่าน้ำประปา:", Location = new Point(20, currentY), AutoSize = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
        _cboWaterMode = new ComboBox
        {
            Location = new Point(290, currentY - 4),
            Width = 200,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 10.5F)
        };
        _cboWaterMode.Items.AddRange(new object[] { "ตามมิเตอร์ (METER)", "เหมาจ่ายรายคน (FLAT)" });
        _cboWaterMode.SelectedIndex = 0;
        currentY += 42;

        // 5. ค่าน้ำต่อหน่วย (ตามมิเตอร์)
        var lblWater = new Label { Text = "อัตราค่าน้ำประปา (บาท : 1 หน่วย):", Location = new Point(20, currentY), AutoSize = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
        _numWaterRate = new NumericUpDown
        {
            Location = new Point(290, currentY - 4),
            Width = 200,
            DecimalPlaces = 2,
            Maximum = 1000,
            Minimum = 0,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor = Color.FromArgb(14, 116, 144)
        };
        currentY += 42;

        // 6. ค่าน้ำเหมาจ่าย (บาท/คน)
        var lblWaterFlat = new Label { Text = "ค่าน้ำเหมาจ่าย (บาท / คน):", Location = new Point(20, currentY), AutoSize = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
        _numWaterFlatRate = new NumericUpDown
        {
            Location = new Point(290, currentY - 4),
            Width = 200,
            DecimalPlaces = 2,
            Maximum = 10000,
            Minimum = 0,
            Font = new Font("Segoe UI", 10.5F)
        };
        currentY += 42;

        // 7. ค่าส่วนกลาง
        var lblCommon = new Label { Text = "ค่าบริการส่วนกลาง (บาท / เดือน):", Location = new Point(20, currentY), AutoSize = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
        _numCommonAreaFee = new NumericUpDown
        {
            Location = new Point(290, currentY - 4),
            Width = 200,
            DecimalPlaces = 2,
            Maximum = 100000,
            Minimum = 0,
            Font = new Font("Segoe UI", 10.5F)
        };
        currentY += 42;

        // 8. ค่าขยะ
        var lblGarbage = new Label { Text = "ค่าจัดเก็บขยะ (บาท / เดือน):", Location = new Point(20, currentY), AutoSize = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
        _numGarbageFee = new NumericUpDown
        {
            Location = new Point(290, currentY - 4),
            Width = 200,
            DecimalPlaces = 2,
            Maximum = 100000,
            Minimum = 0,
            Font = new Font("Segoe UI", 10.5F)
        };
        currentY += 50;

        // Buttons
        _btnSave = new Button
        {
            Text = "บันทึกอัตราค่าบริการ",
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Color.FromArgb(22, 163, 74),
            FlatStyle = FlatStyle.Flat,
            Size = new Size(190, 42),
            Location = new Point(170, currentY),
            Cursor = Cursors.Hand
        };
        _btnSave.FlatAppearance.BorderSize = 0;
        _btnSave.Click += async (s, e) => await SaveSettingsAsync();

        _btnCancel = new Button
        {
            Text = "ยกเลิก",
            Font = new Font("Segoe UI", 10F),
            ForeColor = Color.FromArgb(30, 41, 59),
            BackColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(110, 42),
            Location = new Point(370, currentY),
            Cursor = Cursors.Hand
        };
        _btnCancel.Click += (s, e) => Close();

        Controls.AddRange(new Control[]
        {
            lblHeader, lblSubheader,
            lblElecMode, _cboElectricMode,
            lblElec, _numElectricRate,
            lblElecFlat, _numElectricFlatRate,
            lblWaterMode, _cboWaterMode,
            lblWater, _numWaterRate,
            lblWaterFlat, _numWaterFlatRate,
            lblCommon, _numCommonAreaFee,
            lblGarbage, _numGarbageFee,
            _btnSave, _btnCancel
        });
    }

    private async Task LoadSettingsAsync()
    {
        _settings = await _settingsService.GetAllSettingsAsync();

        _cboElectricMode.SelectedIndex = _settings.ElectricBillingMode == "FLAT" ? 1 : 0;
        _numElectricRate.Value = Math.Min(_numElectricRate.Maximum, Math.Max(0, _settings.ElectricRatePerUnit));
        _numElectricFlatRate.Value = Math.Min(_numElectricFlatRate.Maximum, Math.Max(0, _settings.ElectricFlatRate));
        _cboWaterMode.SelectedIndex = _settings.WaterBillingMode == "FLAT" ? 1 : 0;
        _numWaterRate.Value = Math.Min(_numWaterRate.Maximum, Math.Max(0, _settings.WaterRatePerUnit));
        _numWaterFlatRate.Value = Math.Min(_numWaterFlatRate.Maximum, Math.Max(0, _settings.WaterFlatRatePerPerson));
        _numCommonAreaFee.Value = Math.Min(_numCommonAreaFee.Maximum, Math.Max(0, _settings.CommonAreaFee));
        _numGarbageFee.Value = Math.Min(_numGarbageFee.Maximum, Math.Max(0, _settings.GarbageFee));
    }

    private async Task SaveSettingsAsync()
    {
        try
        {
            _settings.ElectricBillingMode = _cboElectricMode.SelectedIndex == 1 ? "FLAT" : "METER";
            _settings.ElectricRatePerUnit = _numElectricRate.Value;
            _settings.ElectricFlatRate = _numElectricFlatRate.Value;
            _settings.WaterBillingMode = _cboWaterMode.SelectedIndex == 1 ? "FLAT" : "METER";
            _settings.WaterRatePerUnit = _numWaterRate.Value;
            _settings.WaterFlatRatePerPerson = _numWaterFlatRate.Value;
            _settings.CommonAreaFee = _numCommonAreaFee.Value;
            _settings.GarbageFee = _numGarbageFee.Value;

            await _settingsService.SaveAllSettingsAsync(_settings);
            MessageBox.Show("บันทึกตั้งค่าอัตราค่าน้ำค่าไฟเรียบร้อยแล้ว", "สำเร็จ", MessageBoxButtons.OK, MessageBoxIcon.Information);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"เกิดข้อผิดพลาดในการบันทึก: {ex.Message}", "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
