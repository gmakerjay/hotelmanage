using HotelPOS.Common.Models;
using HotelPOS.Core.Services;

namespace HotelPOS.UI;

/// <summary>
/// ฟอร์มตั้งค่าอัตราค่าน้ำค่าไฟต่อหน่วย (กี่บาท : 1 หน่วย)
/// ต้องผ่านการยืนยันรหัสผ่านผู้ดูแลระบบ (Admin) ก่อนเข้าถึง
/// </summary>
public class UtilityRateSettingsForm : Form
{
    private readonly ISettingsService _settingsService;

    private NumericUpDown _numElectricRate = null!;
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
        Text = "⚙️ ตั้งค่าอัตราค่าน้ำค่าไฟต่อหน่วย (Admin Authorized)";
        Width = 520;
        Height = 440;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Font = new Font("Segoe UI", 10.5F);
        BackColor = Color.FromArgb(245, 247, 250);

        var lblHeader = new Label
        {
            Text = "⚙️ ตั้งค่าอัตราค่าน้ำค่าไฟ (กี่บาท : 1 หน่วย)",
            Font = new Font("Segoe UI", 13F, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Location = new Point(20, 15),
            AutoSize = true
        };

        var lblSubheader = new Label
        {
            Text = "กำหนดอัตราค่าหน่วยสำหรับคำนวณบิลรายเดือน (ยืนยันสิทธิ์ Admin เรียบร้อย)",
            Font = new Font("Segoe UI", 9.5F),
            ForeColor = Color.FromArgb(100, 116, 139),
            Location = new Point(20, 44),
            AutoSize = true
        };

        int currentY = 85;

        // 1. ค่าไฟต่อหน่วย
        var lblElec = new Label { Text = "⚡ อัตราค่าไฟฟ้า (บาท : 1 หน่วย):", Location = new Point(20, currentY), AutoSize = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
        _numElectricRate = new NumericUpDown
        {
            Location = new Point(280, currentY - 4),
            Width = 190,
            DecimalPlaces = 2,
            Maximum = 1000,
            Minimum = 0,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor = Color.FromArgb(234, 88, 12)
        };
        currentY += 45;

        // 2. โหมดค่าน้ำ
        var lblWaterMode = new Label { Text = "💧 รูปแบบการคิดค่าน้ำประปา:", Location = new Point(20, currentY), AutoSize = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
        _cboWaterMode = new ComboBox
        {
            Location = new Point(280, currentY - 4),
            Width = 190,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 10.5F)
        };
        _cboWaterMode.Items.AddRange(new object[] { "ตามมิเตอร์ (METER)", "เหมาจ่ายรายคน (FLAT)" });
        _cboWaterMode.SelectedIndex = 0;
        currentY += 45;

        // 3. ค่าน้ำต่อหน่วย (ตามมิเตอร์)
        var lblWater = new Label { Text = "💧 อัตราค่าน้ำประปา (บาท : 1 หน่วย):", Location = new Point(20, currentY), AutoSize = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
        _numWaterRate = new NumericUpDown
        {
            Location = new Point(280, currentY - 4),
            Width = 190,
            DecimalPlaces = 2,
            Maximum = 1000,
            Minimum = 0,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor = Color.FromArgb(14, 116, 144)
        };
        currentY += 45;

        // 4. ค่าน้ำเหมาจ่าย (บาท/คน)
        var lblWaterFlat = new Label { Text = "💧 ค่าน้ำเหมาจ่าย (บาท / คน):", Location = new Point(20, currentY), AutoSize = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
        _numWaterFlatRate = new NumericUpDown
        {
            Location = new Point(280, currentY - 4),
            Width = 190,
            DecimalPlaces = 2,
            Maximum = 10000,
            Minimum = 0,
            Font = new Font("Segoe UI", 10.5F)
        };
        currentY += 45;

        // 5. ค่าส่วนกลาง
        var lblCommon = new Label { Text = "🏢 ค่าบริการส่วนกลาง (บาท / เดือน):", Location = new Point(20, currentY), AutoSize = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
        _numCommonAreaFee = new NumericUpDown
        {
            Location = new Point(280, currentY - 4),
            Width = 190,
            DecimalPlaces = 2,
            Maximum = 100000,
            Minimum = 0,
            Font = new Font("Segoe UI", 10.5F)
        };
        currentY += 45;

        // 6. ค่าขยะ
        var lblGarbage = new Label { Text = "🗑️ ค่าจัดเก็บขยะ (บาท / เดือน):", Location = new Point(20, currentY), AutoSize = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
        _numGarbageFee = new NumericUpDown
        {
            Location = new Point(280, currentY - 4),
            Width = 190,
            DecimalPlaces = 2,
            Maximum = 100000,
            Minimum = 0,
            Font = new Font("Segoe UI", 10.5F)
        };
        currentY += 55;

        // Buttons
        _btnSave = new Button
        {
            Text = "💾 บันทึกอัตราค่าบริการ",
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Color.FromArgb(22, 163, 74),
            FlatStyle = FlatStyle.Flat,
            Size = new Size(190, 42),
            Location = new Point(160, currentY),
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
            Location = new Point(360, currentY),
            Cursor = Cursors.Hand
        };
        _btnCancel.Click += (s, e) => Close();

        Controls.AddRange(new Control[]
        {
            lblHeader, lblSubheader,
            lblElec, _numElectricRate,
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

        _numElectricRate.Value = Math.Min(_numElectricRate.Maximum, Math.Max(0, _settings.ElectricRatePerUnit));
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
            _settings.ElectricRatePerUnit = _numElectricRate.Value;
            _settings.WaterBillingMode = _cboWaterMode.SelectedIndex == 1 ? "FLAT" : "METER";
            _settings.WaterRatePerUnit = _numWaterRate.Value;
            _settings.WaterFlatRatePerPerson = _numWaterFlatRate.Value;
            _settings.CommonAreaFee = _numCommonAreaFee.Value;
            _settings.GarbageFee = _numGarbageFee.Value;

            await _settingsService.SaveAllSettingsAsync(_settings);
            MessageBox.Show("✅ บันทึกตั้งค่าอัตราค่าน้ำค่าไฟต่อหน่วยเรียบร้อยแล้ว", "สำเร็จ", MessageBoxButtons.OK, MessageBoxIcon.Information);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"เกิดข้อผิดพลาดในการบันทึก: {ex.Message}", "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
