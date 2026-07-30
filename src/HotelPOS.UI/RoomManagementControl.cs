using HotelPOS.Common;
using HotelPOS.Common.Models;
using HotelPOS.Core.Services;

namespace HotelPOS.UI;

public class RoomTypeComboItem
{
    public int Id { get; set; }
    public string DisplayName { get; set; } = "";
    public override string ToString() => DisplayName;
}

public class RoomManagementControl : UserControl
{
    private readonly IRoomService _roomService;

    private TabControl _tabControl = null!;
    private TabPage _tabRoomTypes = null!;
    private TabPage _tabRooms = null!;

    // Room Types UI
    private DataGridView _dgvRoomTypes = null!;
    private TextBox _txtSearchTypes = null!;
    private Panel _panelTypeModeBanner = null!;
    private Label _lblTypeModeText = null!;
    private Button _btnCancelTypeEdit = null!;
    private TextBox _txtTypeName = null!;
    private NumericUpDown _numDailyRate = null!;
    private NumericUpDown _numHourlyRate = null!;
    private NumericUpDown _numMonthlyRate = null!;
    private TextBox _txtTypeDesc = null!;
    private RadioButton _rbElecMeter = null!;
    private RadioButton _rbElecFlat = null!;
    private NumericUpDown _numElecFlatRate = null!;
    private RadioButton _rbWaterMeter = null!;
    private RadioButton _rbWaterFlat = null!;
    private NumericUpDown _numWaterFlatRate = null!;
    private Panel _panelSelectedColor = null!;
    private Button _btnSaveType = null!;
    private Button _btnDeleteType = null!;
    private Button _btnClearType = null!;
    private int _selectedTypeId = 0;

    // Rooms UI
    private DataGridView _dgvRooms = null!;
    private TextBox _txtSearchRooms = null!;
    private Panel _panelRoomModeBanner = null!;
    private Label _lblRoomModeText = null!;
    private Button _btnCancelRoomEdit = null!;
    private TextBox _txtRoomNum = null!;
    private TextBox _txtFloor = null!;
    private ComboBox _cboRoomTypes = null!;
    private Button _btnSaveRoom = null!;
    private Button _btnDeleteRoom = null!;
    private Button _btnClearRoom = null!;
    private int _selectedRoomId = 0;

    private List<RoomType> _roomTypesList = new();
    private List<Room> _allRoomsList = new();

    public RoomManagementControl(IRoomService roomService)
    {
        _roomService = roomService;
        InitializeUI();
        Load += async (s, e) => await LoadAllDataAsync();
    }

    private void InitializeUI()
    {
        Dock = DockStyle.Fill;
        Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);

        _tabControl = new TabControl
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 12.5F, FontStyle.Bold),
            ItemSize = new Size(260, 50),
            SizeMode = TabSizeMode.Fixed,
            Padding = new Point(20, 12)
        };

        _tabRoomTypes = new TabPage
        {
            Text = "ประเภทห้องพัก & ราคา",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
            BackColor = Color.White
        };

        _tabRooms = new TabPage
        {
            Text = "รายชื่อและเลขห้องพัก",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
            BackColor = Color.White
        };

        BuildRoomTypesTab();
        BuildRoomsTab();

        _tabControl.TabPages.Add(_tabRoomTypes);
        _tabControl.TabPages.Add(_tabRooms);

        Controls.Add(_tabControl);
    }

    private void BuildRoomTypesTab()
    {
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            FixedPanel = FixedPanel.Panel2
        };
        split.Resize += (s, e) =>
        {
            try
            {
                if (split.Width < 800)
                {
                    split.Orientation = Orientation.Horizontal;
                    split.SplitterDistance = Math.Max(150, split.Height - 320);
                }
                else
                {
                    split.Orientation = Orientation.Vertical;
                    split.Panel1MinSize = 300;
                    split.Panel2MinSize = 380;
                    split.SplitterDistance = Math.Max(300, split.Width - 460);
                }
            }
            catch { }
        };

        _dgvRoomTypes = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AllowUserToAddRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
            RowTemplate = { Height = 38 }
        };
        _dgvRoomTypes.EnableHeadersVisualStyles = false;
        _dgvRoomTypes.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
            BackColor = Color.FromArgb(30, 41, 59),
            ForeColor = Color.White,
            SelectionBackColor = Color.FromArgb(30, 41, 59),
            SelectionForeColor = Color.White,
            WrapMode = DataGridViewTriState.True
        };
        _dgvRoomTypes.ApplyZebraStyle();
        _dgvRoomTypes.CellClick += DgvRoomTypes_CellContentClick;
        _dgvRoomTypes.CellDoubleClick += (s, e) => UpdateRoomTypeSelectionFromGrid();
        _dgvRoomTypes.DataBindingComplete += (s, e) =>
        {
            foreach (DataGridViewColumn col in _dgvRoomTypes.Columns)
            {
                col.MinimumWidth = 90;
            }
        };

        var panelInput = new Panel { Dock = DockStyle.Fill, Padding = new Padding(15), AutoScroll = true };
        
        // Mode Banner for Room Type Form
        _panelTypeModeBanner = new Panel
        {
            Location = new Point(15, 10),
            Size = new Size(410, 42),
            BackColor = Color.FromArgb(240, 253, 244),
            BorderStyle = BorderStyle.FixedSingle
        };

        _lblTypeModeText = new Label
        {
            Text = "โหมด: เพิ่มประเภทห้องใหม่",
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
            ForeColor = Color.ForestGreen,
            Location = new Point(10, 10),
            AutoSize = true
        };

        _btnCancelTypeEdit = new Button
        {
            Text = "ยกเลิกแก้ไข",
            Location = new Point(285, 6),
            Size = new Size(115, 28),
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            BackColor = Color.White,
            ForeColor = Color.DarkRed,
            FlatStyle = FlatStyle.Flat,
            Visible = false
        };
        _btnCancelTypeEdit.Click += (s, e) => ClearTypeForm();

        _panelTypeModeBanner.Controls.Add(_lblTypeModeText);
        _panelTypeModeBanner.Controls.Add(_btnCancelTypeEdit);

        var lblName = new Label { Text = "ชื่อประเภทห้อง *:", Location = new Point(15, 65), Font = new Font("Segoe UI", 10.5F, FontStyle.Bold), AutoSize = true };
        _txtTypeName = new TextBox { Location = new Point(160, 62), Width = 265, Font = new Font("Segoe UI", 10.5F) };

        var lblDaily = new Label { Text = "ราคา/วัน (บาท):", Location = new Point(15, 105), Font = new Font("Segoe UI", 10.5F), AutoSize = true };
        _numDailyRate = new NumericUpDown { Location = new Point(160, 102), Width = 180, Font = new Font("Segoe UI", 10.5F), Maximum = 1000000, DecimalPlaces = 2 };

        var lblHourly = new Label { Text = "ราคา/ชั่วโมง (บาท):", Location = new Point(15, 145), Font = new Font("Segoe UI", 10.5F), AutoSize = true };
        _numHourlyRate = new NumericUpDown { Location = new Point(160, 142), Width = 180, Font = new Font("Segoe UI", 10.5F), Maximum = 1000000, DecimalPlaces = 2 };

        var lblMonthly = new Label { Text = "ราคา/เดือน (บาท):", Location = new Point(15, 185), Font = new Font("Segoe UI", 10.5F), AutoSize = true };
        _numMonthlyRate = new NumericUpDown { Location = new Point(160, 182), Width = 180, Font = new Font("Segoe UI", 10.5F), Maximum = 1000000, DecimalPlaces = 2 };

        var lblDesc = new Label { Text = "รายละเอียด:", Location = new Point(15, 225), Font = new Font("Segoe UI", 10.5F), AutoSize = true };
        _txtTypeDesc = new TextBox { Location = new Point(160, 222), Width = 265, Font = new Font("Segoe UI", 10.5F), Multiline = true, Height = 50 };

        // Color Picker UI
        var lblColor = new Label { Text = "สีประจำประเภทห้อง:", Location = new Point(15, 282), Font = new Font("Segoe UI", 10F, FontStyle.Bold), AutoSize = true };
        _panelSelectedColor = new Panel { Location = new Point(160, 279), Size = new Size(32, 28), BackColor = Color.FromArgb(2, 132, 199), BorderStyle = BorderStyle.FixedSingle };
        var btnPickColor = new Button { Text = "เลือกสี...", Location = new Point(198, 279), Size = new Size(68, 28), Font = new Font("Segoe UI", 8.5F), Cursor = Cursors.Hand };
        btnPickColor.Click += (s, e) =>
        {
            using var dlg = new ColorDialog { Color = _panelSelectedColor.BackColor };
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                _panelSelectedColor.BackColor = dlg.Color;
            }
        };

        var presetColors = new (string hex, Color col)[]
        {
            ("#0284C7", ColorTranslator.FromHtml("#0284C7")),
            ("#059669", ColorTranslator.FromHtml("#059669")),
            ("#7C3AED", ColorTranslator.FromHtml("#7C3AED")),
            ("#D97706", ColorTranslator.FromHtml("#D97706")),
            ("#DB2777", ColorTranslator.FromHtml("#DB2777")),
            ("#DC2626", ColorTranslator.FromHtml("#DC2626"))
        };
        int presetX = 272;
        var presetBtns = new List<Control>();
        foreach (var (hex, col) in presetColors)
        {
            var pBtn = new Button
            {
                Location = new Point(presetX, 280),
                Size = new Size(22, 25),
                BackColor = col,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Tag = hex
            };
            pBtn.FlatAppearance.BorderSize = 0;
            pBtn.Click += (s, e) => { if (s is Button b && b.Tag is string h) _panelSelectedColor.BackColor = ColorTranslator.FromHtml(h); };
            presetBtns.Add(pBtn);
            presetX += 25;
        }

        // --- Electric Billing Mode ---
        var groupElec = new GroupBox { Text = "รูปแบบค่าไฟฟ้า", Location = new Point(15, 318), Size = new Size(410, 75), Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = Color.FromArgb(6, 95, 70) };
        _rbElecMeter = new RadioButton { Text = "คิดตามมิเตอร์", Location = new Point(15, 26), AutoSize = true, Font = new Font("Segoe UI", 9.5F), Checked = true };
        _rbElecFlat = new RadioButton { Text = "เหมาจ่าย/เดือน:", Location = new Point(140, 26), AutoSize = true, Font = new Font("Segoe UI", 9.5F) };
        _numElecFlatRate = new NumericUpDown { Location = new Point(275, 24), Width = 110, Font = new Font("Segoe UI", 9.5F), Maximum = 100000, DecimalPlaces = 2, Enabled = false };
        _rbElecFlat.CheckedChanged += (s, e) => _numElecFlatRate.Enabled = _rbElecFlat.Checked;
        groupElec.Controls.AddRange(new Control[] { _rbElecMeter, _rbElecFlat, _numElecFlatRate });

        // --- Water Billing Mode ---
        var groupWater = new GroupBox { Text = "รูปแบบค่าน้ำประปา", Location = new Point(15, 400), Size = new Size(410, 75), Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = Color.FromArgb(14, 116, 144) };
        _rbWaterMeter = new RadioButton { Text = "คิดตามมิเตอร์", Location = new Point(15, 26), AutoSize = true, Font = new Font("Segoe UI", 9.5F), Checked = true };
        _rbWaterFlat = new RadioButton { Text = "เหมาจ่าย/เดือน:", Location = new Point(140, 26), AutoSize = true, Font = new Font("Segoe UI", 9.5F) };
        _numWaterFlatRate = new NumericUpDown { Location = new Point(275, 24), Width = 110, Font = new Font("Segoe UI", 9.5F), Maximum = 100000, DecimalPlaces = 2, Enabled = false };
        _rbWaterFlat.CheckedChanged += (s, e) => _numWaterFlatRate.Enabled = _rbWaterFlat.Checked;
        groupWater.Controls.AddRange(new Control[] { _rbWaterMeter, _rbWaterFlat, _numWaterFlatRate });

        _btnSaveType = new Button { Text = "บันทึกประเภทห้อง", Location = new Point(160, 488), Size = new Size(150, 38), Font = new Font("Segoe UI", 10.5F, FontStyle.Bold), BackColor = Color.ForestGreen, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
        _btnSaveType.Click += BtnSaveType_Click;

        _btnClearType = new Button { Text = "ล้างฟอร์ม", Location = new Point(318, 488), Size = new Size(107, 38), Font = new Font("Segoe UI", 10.5F), Cursor = Cursors.Hand };
        _btnClearType.Click += (s, e) => ClearTypeForm();

        _btnDeleteType = new Button { Text = "ลบประเภทห้องพักนี้", Location = new Point(160, 532), Size = new Size(265, 34), Font = new Font("Segoe UI", 9.5F), ForeColor = Color.Red, Cursor = Cursors.Hand };
        _btnDeleteType.Click += BtnDeleteType_Click;

        var typeInputControls = new List<Control>
        {
            _panelTypeModeBanner, lblName, _txtTypeName, lblDaily, _numDailyRate,
            lblHourly, _numHourlyRate, lblMonthly, _numMonthlyRate,
            lblDesc, _txtTypeDesc, lblColor, _panelSelectedColor, btnPickColor,
            groupElec, groupWater, _btnSaveType, _btnClearType, _btnDeleteType
        };
        typeInputControls.AddRange(presetBtns);
        panelInput.Controls.AddRange(typeInputControls.ToArray());

        var searchPanel = new Panel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(8), BackColor = Color.White };
        var lblSearch = new Label { Text = "ค้นหาประเภทห้อง:", Font = new Font("Segoe UI", 10F, FontStyle.Bold), Location = new Point(10, 12), AutoSize = true };
        _txtSearchTypes = new TextBox { Location = new Point(140, 9), Width = 260, Font = new Font("Segoe UI", 10F), PlaceholderText = "พิมพ์ชื่อประเภทห้อง / รายละเอียด..." };
        _txtSearchTypes.TextChanged += (s, e) => FilterRoomTypes();
        searchPanel.Controls.AddRange(new Control[] { lblSearch, _txtSearchTypes });

        var gridContainer = new Panel { Dock = DockStyle.Fill };
        gridContainer.Controls.Add(_dgvRoomTypes);
        gridContainer.Controls.Add(searchPanel);

        split.Panel1.Controls.Add(gridContainer);
        split.Panel2.Controls.Add(panelInput);

        _tabRoomTypes.Controls.Add(split);
    }

    private void BuildRoomsTab()
    {
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            FixedPanel = FixedPanel.Panel2
        };
        split.Resize += (s, e) =>
        {
            try
            {
                if (split.Width < 800)
                {
                    split.Orientation = Orientation.Horizontal;
                    split.SplitterDistance = Math.Max(150, split.Height - 320);
                }
                else
                {
                    split.Orientation = Orientation.Vertical;
                    split.Panel1MinSize = 300;
                    split.Panel2MinSize = 380;
                    split.SplitterDistance = Math.Max(300, split.Width - 460);
                }
            }
            catch { }
        };

        _dgvRooms = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AllowUserToAddRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
            RowTemplate = { Height = 38 }
        };
        _dgvRooms.ApplyZebraStyle();
        _dgvRooms.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
            BackColor = Color.FromArgb(30, 41, 59),
            ForeColor = Color.White,
            SelectionBackColor = Color.FromArgb(30, 41, 59),
            SelectionForeColor = Color.White,
            WrapMode = DataGridViewTriState.True
        };
        _dgvRooms.DefaultCellStyle.Font = new Font("Segoe UI", 10F);
        _dgvRooms.CellClick += DgvRooms_CellContentClick;
        _dgvRooms.CellDoubleClick += (s, e) => UpdateRoomSelectionFromGrid();
        _dgvRooms.DataBindingComplete += (s, e) =>
        {
            foreach (DataGridViewColumn col in _dgvRooms.Columns)
            {
                col.MinimumWidth = 90;
            }
        };

        var panelInput = new Panel { Dock = DockStyle.Fill, Padding = new Padding(15), AutoScroll = true };

        // Mode Banner for Room Form
        _panelRoomModeBanner = new Panel
        {
            Location = new Point(15, 10),
            Size = new Size(410, 42),
            BackColor = Color.FromArgb(240, 253, 244),
            BorderStyle = BorderStyle.FixedSingle
        };

        _lblRoomModeText = new Label
        {
            Text = "โหมด: เพิ่มห้องพักใหม่",
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
            ForeColor = Color.ForestGreen,
            Location = new Point(10, 10),
            AutoSize = true
        };

        _btnCancelRoomEdit = new Button
        {
            Text = "ยกเลิกแก้ไข",
            Location = new Point(285, 6),
            Size = new Size(115, 28),
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            BackColor = Color.White,
            ForeColor = Color.DarkRed,
            FlatStyle = FlatStyle.Flat,
            Visible = false
        };
        _btnCancelRoomEdit.Click += (s, e) => ClearRoomForm();

        _panelRoomModeBanner.Controls.Add(_lblRoomModeText);
        _panelRoomModeBanner.Controls.Add(_btnCancelRoomEdit);

        var lblRoomNum = new Label { Text = "เลขที่ห้อง *:", Location = new Point(15, 65), Font = new Font("Segoe UI", 10.5F, FontStyle.Bold), AutoSize = true };
        _txtRoomNum = new TextBox { Location = new Point(150, 62), Width = 275, Font = new Font("Segoe UI", 10.5F) };

        var lblFloor = new Label { Text = "ชั้น (Floor):", Location = new Point(15, 105), Font = new Font("Segoe UI", 10.5F), AutoSize = true };
        _txtFloor = new TextBox { Location = new Point(150, 102), Width = 275, Font = new Font("Segoe UI", 10.5F) };

        var lblType = new Label { Text = "ประเภทห้อง *:", Location = new Point(15, 145), Font = new Font("Segoe UI", 10.5F, FontStyle.Bold), AutoSize = true };
        _cboRoomTypes = new ComboBox { Location = new Point(150, 142), Width = 275, Font = new Font("Segoe UI", 10.5F), DropDownStyle = ComboBoxStyle.DropDownList };

        _btnSaveRoom = new Button { Text = "บันทึกห้องพัก", Location = new Point(150, 195), Size = new Size(150, 40), Font = new Font("Segoe UI", 10.5F, FontStyle.Bold), BackColor = Color.ForestGreen, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
        _btnSaveRoom.Click += BtnSaveRoom_Click;

        _btnClearRoom = new Button { Text = "ล้างฟอร์ม", Location = new Point(308, 195), Size = new Size(117, 40), Font = new Font("Segoe UI", 10.5F), Cursor = Cursors.Hand };
        _btnClearRoom.Click += (s, e) => ClearRoomForm();

        _btnDeleteRoom = new Button { Text = "ลบห้องพักนี้", Location = new Point(150, 248), Size = new Size(275, 36), Font = new Font("Segoe UI", 10F), ForeColor = Color.Red, Cursor = Cursors.Hand };
        _btnDeleteRoom.Click += BtnDeleteRoom_Click;

        panelInput.Controls.AddRange(new Control[]
        {
            _panelRoomModeBanner, lblRoomNum, _txtRoomNum, lblFloor, _txtFloor,
            lblType, _cboRoomTypes, _btnSaveRoom, _btnClearRoom, _btnDeleteRoom
        });

        var searchPanel = new Panel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(8), BackColor = Color.White };
        var lblSearch = new Label { Text = "ค้นหาห้องพัก:", Font = new Font("Segoe UI", 10F, FontStyle.Bold), Location = new Point(10, 12), AutoSize = true };
        _txtSearchRooms = new TextBox { Location = new Point(120, 9), Width = 260, Font = new Font("Segoe UI", 10F), PlaceholderText = "พิมพ์เลขห้อง / ชั้น / ประเภทห้อง..." };
        _txtSearchRooms.TextChanged += (s, e) => FilterRooms();
        searchPanel.Controls.AddRange(new Control[] { lblSearch, _txtSearchRooms });

        var gridContainer = new Panel { Dock = DockStyle.Fill };
        gridContainer.Controls.Add(_dgvRooms);
        gridContainer.Controls.Add(searchPanel);

        split.Panel1.Controls.Add(gridContainer);
        split.Panel2.Controls.Add(panelInput);

        _tabRooms.Controls.Add(split);
    }

    private async Task LoadAllDataAsync()
    {
        try
        {
            _roomTypesList = (await _roomService.GetRoomTypesAsync(false)).ToList();
            
            // Populate Room Types Grid with Action Buttons
            FilterRoomTypes();

            // Populate Room Types ComboBox
            _cboRoomTypes.Items.Clear();
            foreach (var t in _roomTypesList.Where(x => x.IsActive))
            {
                _cboRoomTypes.Items.Add(new RoomTypeComboItem
                {
                    Id = t.Id,
                    DisplayName = $"{t.Name} (฿{t.DailyRate:N0}/วัน)"
                });
            }
            if (_cboRoomTypes.Items.Count > 0) _cboRoomTypes.SelectedIndex = 0;

            // Populate Rooms Grid with Action Buttons
            _allRoomsList = (await _roomService.GetRoomsAsync()).ToList();
            FilterRooms();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"โหลดข้อมูลไม่สำเร็จ: {ex.Message}", "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void FilterRoomTypes()
    {
        string query = _txtSearchTypes.Text.Trim();
        var filtered = _roomTypesList.Where(t =>
        {
            if (string.IsNullOrWhiteSpace(query)) return true;
            return t.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                   (!string.IsNullOrEmpty(t.Description) && t.Description.Contains(query, StringComparison.OrdinalIgnoreCase));
        }).ToList();

        _dgvRoomTypes.Columns.Clear();
        _dgvRoomTypes.DataSource = filtered.Select(t => new
        {
            t.Id,
            ชื่อประเภท = t.Name,
            ประเภทราคาหลัก = GetRateTypeTag(t),
            ราคาต่อวัน = t.DailyRate > 0 ? $"฿{t.DailyRate:N0}" : "-",
            ราคาต่อชั่วโมง = t.HourlyRate > 0 ? $"฿{t.HourlyRate:N0}" : "-",
            ราคาต่อเดือน = t.MonthlyRate > 0 ? $"฿{t.MonthlyRate:N0}" : "-",
            รายละเอียด = t.Description,
            สถานะ = t.IsActive ? "ใช้งาน" : "ปิดใช้งาน"
        }).ToList();

        AddGridActionColumns(_dgvRoomTypes);
        ApplyGridRowColors(_dgvRoomTypes);
    }

    private void FilterRooms()
    {
        string query = _txtSearchRooms.Text.Trim();
        var filtered = _allRoomsList.Where(r =>
        {
            if (string.IsNullOrWhiteSpace(query)) return true;
            var type = _roomTypesList.FirstOrDefault(t => t.Id == r.RoomTypeId);
            var typeName = type?.Name ?? "";
            return r.RoomNumber.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                   (!string.IsNullOrEmpty(r.Floor) && r.Floor.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                   typeName.Contains(query, StringComparison.OrdinalIgnoreCase);
        }).ToList();

        _dgvRooms.Columns.Clear();
        _dgvRooms.DataSource = filtered.Select(r =>
        {
            var type = _roomTypesList.FirstOrDefault(t => t.Id == r.RoomTypeId);
            return new
            {
                r.Id,
                เลขห้อง = r.RoomNumber,
                ชั้น = r.Floor ?? "-",
                ประเภทห้อง = type?.Name ?? "-",
                รูปแบบราคา = GetRateTypeTag(type),
                ราคามาตรฐาน = GetPrimaryRateText(type),
                สถานะ = GetStatusName(r.Status)
            };
        }).ToList();

        AddGridActionColumns(_dgvRooms);
        ApplyGridRowColors(_dgvRooms);
    }

    private static string GetRateTypeTag(RoomType? type)
    {
        if (type == null) return "-";
        if (type.MonthlyRate > 0 && type.DailyRate == 0) return "รายเดือน";
        if (type.HourlyRate > 0 && type.DailyRate == 0) return "รายชั่วโมง";
        if (type.MonthlyRate > 0 && type.DailyRate > 0) return "รายเดือน / รายวัน";
        return "รายวัน";
    }

    private static string GetPrimaryRateText(RoomType? type)
    {
        if (type == null) return "-";
        if (type.MonthlyRate > 0) return $"฿{type.MonthlyRate:N0}/เดือน";
        if (type.DailyRate > 0) return $"฿{type.DailyRate:N0}/วัน";
        if (type.HourlyRate > 0) return $"฿{type.HourlyRate:N0}/ชม.";
        return "-";
    }

    private static void ApplyGridRowColors(DataGridView dgv)
    {
        bool hasRatePlan = dgv.Columns.Contains("รูปแบบราคา");
        bool hasPrimaryRate = dgv.Columns.Contains("ประเภทราคาหลัก");

        foreach (DataGridViewRow row in dgv.Rows)
        {
            if (row.IsNewRow) continue;

            string rateTag = "";
            if (hasRatePlan)
            {
                rateTag = row.Cells["รูปแบบราคา"]?.Value?.ToString() ?? "";
            }
            else if (hasPrimaryRate)
            {
                rateTag = row.Cells["ประเภทราคาหลัก"]?.Value?.ToString() ?? "";
            }

            if (rateTag.Contains("รายเดือน"))
            {
                row.DefaultCellStyle.BackColor = Color.FromArgb(243, 232, 255); // Soft Purple
                row.DefaultCellStyle.ForeColor = Color.FromArgb(107, 33, 168);  // Deep Purple
            }
            else if (rateTag.Contains("รายชั่วโมง"))
            {
                row.DefaultCellStyle.BackColor = Color.FromArgb(254, 243, 199); // Soft Amber
                row.DefaultCellStyle.ForeColor = Color.FromArgb(146, 64, 14);   // Deep Amber
            }
            else if (rateTag.Contains("รายวัน"))
            {
                row.DefaultCellStyle.BackColor = Color.FromArgb(236, 253, 245); // Soft Emerald
                row.DefaultCellStyle.ForeColor = Color.FromArgb(6, 95, 70);     // Deep Emerald
            }
        }
    }

    private static void AddGridActionColumns(DataGridView dgv)
    {
        if (dgv.Columns.Contains("EditAction")) return;

        var colEdit = new DataGridViewButtonColumn
        {
            Name = "EditAction",
            HeaderText = "แก้ไข",
            Text = "แก้ไข",
            UseColumnTextForButtonValue = true,
            Width = 85,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None
        };

        var colDelete = new DataGridViewButtonColumn
        {
            Name = "DeleteAction",
            HeaderText = "ลบ",
            Text = "ลบ",
            UseColumnTextForButtonValue = true,
            Width = 75,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None
        };

        dgv.Columns.Add(colEdit);
        dgv.Columns.Add(colDelete);
    }

    private static string GetStatusName(RoomStatus status)
    {
        return status switch
        {
            RoomStatus.Available => "ว่าง",
            RoomStatus.Occupied => "มีคนพัก",
            RoomStatus.Cleaning => "รอทำความสะอาด",
            RoomStatus.Reserved => "จองไว้",
            RoomStatus.Maintenance => "ปิดซ่อม",
            _ => "-"
        };
    }

    private void DgvRoomTypes_CellContentClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
        string colName = _dgvRoomTypes.Columns[e.ColumnIndex].Name;

        if (colName == "EditAction")
        {
            UpdateRoomTypeSelectionFromGrid();
        }
        else if (colName == "DeleteAction")
        {
            UpdateRoomTypeSelectionFromGrid();
            BtnDeleteType_Click(sender, EventArgs.Empty);
        }
    }

    private void DgvRooms_CellContentClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
        string colName = _dgvRooms.Columns[e.ColumnIndex].Name;

        if (colName == "EditAction")
        {
            UpdateRoomSelectionFromGrid();
        }
        else if (colName == "DeleteAction")
        {
            UpdateRoomSelectionFromGrid();
            BtnDeleteRoom_Click(sender, EventArgs.Empty);
        }
    }

    private void UpdateRoomTypeSelectionFromGrid()
    {
        if (_dgvRoomTypes.CurrentRow == null || _dgvRoomTypes.CurrentRow.Index < 0) return;
        var cellVal = _dgvRoomTypes.CurrentRow.Cells["Id"]?.Value;
        if (cellVal != null && int.TryParse(cellVal.ToString(), out int id))
        {
            _selectedTypeId = id;
            var type = _roomTypesList.FirstOrDefault(t => t.Id == _selectedTypeId);
            if (type != null)
            {
                _txtTypeName.Text = type.Name;
                _numDailyRate.Value = type.DailyRate;
                _numHourlyRate.Value = type.HourlyRate;
                _numMonthlyRate.Value = type.MonthlyRate;
                _txtTypeDesc.Text = type.Description;

                try
                {
                    _panelSelectedColor.BackColor = ColorTranslator.FromHtml(type.ColorHex ?? "#0284C7");
                }
                catch
                {
                    _panelSelectedColor.BackColor = Color.FromArgb(2, 132, 199);
                }

                _rbElecMeter.Checked = type.ElectricBillingMode == UtilityBillingMode.Meter;
                _rbElecFlat.Checked = type.ElectricBillingMode == UtilityBillingMode.FlatRate;
                _numElecFlatRate.Value = type.ElectricFlatRate;

                _rbWaterMeter.Checked = type.WaterBillingMode == UtilityBillingMode.Meter;
                _rbWaterFlat.Checked = type.WaterBillingMode == UtilityBillingMode.FlatRate;
                _numWaterFlatRate.Value = type.WaterFlatRate;

                // Update Mode Banner
                _panelTypeModeBanner.BackColor = Color.FromArgb(254, 243, 199); // Soft Amber
                _lblTypeModeText.Text = $"โหมด: แก้ไขประเภท '{type.Name}'";
                _lblTypeModeText.ForeColor = Color.DarkGoldenrod;
                _btnCancelTypeEdit.Visible = true;
            }
        }
    }

    private void UpdateRoomSelectionFromGrid()
    {
        if (_dgvRooms.CurrentRow == null || _dgvRooms.CurrentRow.Index < 0) return;
        var cellVal = _dgvRooms.CurrentRow.Cells["Id"]?.Value;
        if (cellVal != null && int.TryParse(cellVal.ToString(), out int roomId))
        {
            _selectedRoomId = roomId;
            var room = _allRoomsList.FirstOrDefault(r => r.Id == _selectedRoomId);
            if (room != null)
            {
                _txtRoomNum.Text = room.RoomNumber;
                _txtFloor.Text = room.Floor;
                for (int i = 0; i < _cboRoomTypes.Items.Count; i++)
                {
                    if (_cboRoomTypes.Items[i] is RoomTypeComboItem item && item.Id == room.RoomTypeId)
                    {
                        _cboRoomTypes.SelectedIndex = i;
                        break;
                    }
                }

                // Update Mode Banner
                _panelRoomModeBanner.BackColor = Color.FromArgb(254, 243, 199); // Soft Amber
                _lblRoomModeText.Text = $"โหมด: แก้ไขห้อง '{room.RoomNumber}' (ชั้น {room.Floor ?? "-"})";
                _lblRoomModeText.ForeColor = Color.DarkGoldenrod;
                _btnCancelRoomEdit.Visible = true;
            }
        }
    }

    private void ClearTypeForm()
    {
        _selectedTypeId = 0;
        _txtTypeName.Clear();
        _numDailyRate.Value = 0;
        _numHourlyRate.Value = 0;
        _numMonthlyRate.Value = 0;
        _txtTypeDesc.Clear();
        _panelSelectedColor.BackColor = Color.FromArgb(2, 132, 199);

        _rbElecMeter.Checked = true;
        _numElecFlatRate.Value = 0;
        _rbWaterMeter.Checked = true;
        _numWaterFlatRate.Value = 0;

        // Reset Mode Banner
        _panelTypeModeBanner.BackColor = Color.FromArgb(240, 253, 244);
        _lblTypeModeText.Text = "โหมด: เพิ่มประเภทห้องใหม่";
        _lblTypeModeText.ForeColor = Color.ForestGreen;
        _btnCancelTypeEdit.Visible = false;
    }

    private void ClearRoomForm()
    {
        _selectedRoomId = 0;
        _txtRoomNum.Clear();
        _txtFloor.Clear();
        if (_cboRoomTypes.Items.Count > 0) _cboRoomTypes.SelectedIndex = 0;

        // Reset Mode Banner
        _panelRoomModeBanner.BackColor = Color.FromArgb(240, 253, 244);
        _lblRoomModeText.Text = "โหมด: เพิ่มห้องพักใหม่";
        _lblRoomModeText.ForeColor = Color.ForestGreen;
        _btnCancelRoomEdit.Visible = false;
    }

    private async void BtnSaveType_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_txtTypeName.Text))
        {
            MessageBox.Show("กรุณากรอกชื่อประเภทห้องพัก", "เตือน", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        try
        {
            var roomType = new RoomType
            {
                Id = _selectedTypeId,
                Name = _txtTypeName.Text.Trim(),
                DailyRate = _numDailyRate.Value,
                HourlyRate = _numHourlyRate.Value,
                MonthlyRate = _numMonthlyRate.Value,
                Description = _txtTypeDesc.Text.Trim(),
                ColorHex = ColorTranslator.ToHtml(_panelSelectedColor.BackColor),
                IsActive = true,
                ElectricBillingMode = _rbElecFlat.Checked ? UtilityBillingMode.FlatRate : UtilityBillingMode.Meter,
                ElectricFlatRate = _numElecFlatRate.Value,
                WaterBillingMode = _rbWaterFlat.Checked ? UtilityBillingMode.FlatRate : UtilityBillingMode.Meter,
                WaterFlatRate = _numWaterFlatRate.Value
            };

            if (_selectedTypeId > 0)
            {
                if (MessageBox.Show("คุณแน่ใจหรือไม่ที่จะบันทึกการแก้ไขนี้?", "ยืนยันการบันทึก", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                {
                    return;
                }
            }
            await _roomService.SaveRoomTypeAsync(roomType);
            ClearTypeForm();
            await LoadAllDataAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"บันทึกไม่สำเร็จ: {ex.Message}", "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void BtnDeleteType_Click(object? sender, EventArgs e)
    {
        if (_selectedTypeId == 0)
        {
            MessageBox.Show("กรุณาเลือกประเภทห้องพักที่ต้องการลบจากตาราง", "แจ้งเตือน", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (MessageBox.Show("คุณแน่ใจหรือไม่ที่จะลบประเภทห้องพักนี้ออกจากระบบ?", "ยืนยันการลบ", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
        {
            try
            {
                await _roomService.DeleteRoomTypeAsync(_selectedTypeId);
                MessageBox.Show("ลบประเภทห้องพักเรียบร้อยแล้ว", "สำเร็จ", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ClearTypeForm();
                await LoadAllDataAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"ไม่สามารถลบประเภทห้องพักได้: {ex.Message}", "ป้องกันความปลอดภัย", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }

    private async void BtnSaveRoom_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_txtRoomNum.Text))
        {
            MessageBox.Show("กรุณากรอกเลขที่ห้อง", "เตือน", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (_cboRoomTypes.SelectedItem is not RoomTypeComboItem selectedType)
        {
            MessageBox.Show("กรุณาเลือกประเภทห้อง", "เตือน", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            var room = new Room
            {
                Id = _selectedRoomId,
                RoomNumber = _txtRoomNum.Text.Trim(),
                Floor = _txtFloor.Text.Trim(),
                RoomTypeId = selectedType.Id,
                IsActive = true
            };
            await _roomService.SaveRoomAsync(room);
            ClearRoomForm();
            await LoadAllDataAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"บันทึกไม่สำเร็จ: {ex.Message}", "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void BtnDeleteRoom_Click(object? sender, EventArgs e)
    {
        if (_selectedRoomId == 0)
        {
            MessageBox.Show("กรุณาเลือกห้องพักที่ต้องการลบจากตารางก่อนครับ", "แจ้งเตือน", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var room = _allRoomsList.FirstOrDefault(r => r.Id == _selectedRoomId) ?? await _roomService.GetRoomByIdAsync(_selectedRoomId);
        if (room == null)
        {
            MessageBox.Show("ไม่พบข้อมูลห้องพักที่เลือก", "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        if (MessageBox.Show($"คุณแน่ใจหรือไม่ที่จะลบห้องพัก '{room.RoomNumber}' (ชั้น {room.Floor ?? "-"}) ออกจากระบบ?\n\n(หากห้องพักมีคนเข้าพักหรือมีรายการจองอยู่ ระบบจะไม่ยินยอมให้ลบ)", "ยืนยันการลบห้องพัก", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
        {
            try
            {
                await _roomService.DeleteRoomAsync(_selectedRoomId);
                MessageBox.Show($"ลบห้องพัก '{room.RoomNumber}' ออกจากระบบเรียบร้อยแล้ว", "สำเร็จ", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ClearRoomForm();
                await LoadAllDataAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"ไม่สามารถลบห้องพักได้: {ex.Message}", "ป้องกันความปลอดภัย", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
