using HotelPOS.Common;
using HotelPOS.Common.Models;
using HotelPOS.Core.Services;

namespace HotelPOS.UI;

/// <summary>
/// หน้าจัดการข้อมูลผู้เข้าพัก พร้อมระบบค้นหาทันที (Instant Search), 
/// แสดงข้อมูลสัญญาเช่ารายเดือน มิเตอร์ไฟฟ้า/น้ำประปาสะสม และระบบติดตามสถานะบิล [ชำระแล้ว / ใกล้ครบกำหนด / เลยกำหนดชำระ]
/// </summary>
public class CustomerManagementControl : UserControl
{
    private readonly ICustomerService _customerService;
    private readonly IBookingService? _bookingService;
    private readonly IRoomService? _roomService;
    private readonly ISettingsService? _settingsService;
    private readonly IUtilityBillService? _utilityBillService;
    private readonly IPOSService? _posService;

    // Left Panel Controls
    private DataGridView _dgvCustomers = null!;
    private TextBox _txtSearch = null!;
    private GridPaginationPanel _pgPanel = null!;
    private List<Customer> _customersList = new();

    // Right Panel - Customer Form (Tab 1)
    private TextBox _txtFullName = null!;
    private TextBox _txtPhone = null!;
    private TextBox _txtEmail = null!;
    private TextBox _txtIdCard = null!;
    private TextBox _txtAddress = null!;
    private TextBox _txtNotes = null!;

    private Button _btnSave = null!;
    private Button _btnClear = null!;
    private Button _btnDelete = null!;
    private int _selectedCustomerId = 0;

    private Panel _panelModeBanner = null!;
    private Label _lblModeText = null!;
    private Button _btnCancelEdit = null!;

    // Customer Summary Stat Badges
    private Label _lblStatStayCount = null!;
    private Label _lblStatPosTotal = null!;
    private Label _lblStatBillCount = null!;

    // Rental & Utility Accordion Panel Controls on Tab 1
    private Panel _grpRentalSummary = null!;
    private Panel _pnlRentalHeader = null!;
    private Label _lblRentalHeaderTitle = null!;
    private Label _lblRentalHeaderSummaryBadge = null!;
    private Button _btnToggleRental = null!;
    private TableLayoutPanel _pnlRentalContentContainer = null!;
    private bool _isRentalExpanded = true;

    private Label _lblRentalInfo = null!;
    private Label _lblMeterInfo = null!;
    private Label _lblBillStatusBadge = null!;
    private Label _lblUnpaidTotalAlert = null!;

    // History Grids & Detail Cards
    private DataGridView _dgvStayHistory = null!;
    private DataGridView _dgvPOSHistory = null!;
    private DataGridView _dgvBillHistory = null!;
    private List<UtilityBill> _loadedBills = new();

    // Detail Panel Controls - Tab 2 (Stay History)
    private Panel _pnlStayDetail = null!;
    private Label _lblStayDetailTitle = null!;
    private Label _lblStayDetailInfo = null!;
    private Button _btnViewStayReceipt = null!;
    private int _selectedBookingId = 0;

    // Detail Panel Controls - Tab 3 (POS History)
    private Panel _pnlPosDetail = null!;
    private Label _lblPosDetailTitle = null!;
    private Label _lblPosDetailInfo = null!;
    private Button _btnViewPosReceipt = null!;
    private int _selectedSaleId = 0;

    // Detail Panel Controls - Tab 4 (Bill History)
    private Panel _pnlBillDetail = null!;
    private Label _lblBillDetailTitle = null!;
    private Label _lblBillDetailInfo = null!;
    private Button _btnViewBillReceipt = null!;
    private int _selectedBillId = 0;

    public CustomerManagementControl(
        ICustomerService customerService,
        IBookingService? bookingService = null,
        IRoomService? roomService = null,
        ISettingsService? settingsService = null,
        IUtilityBillService? utilityBillService = null,
        IPOSService? posService = null)
    {
        _customerService = customerService;
        _bookingService = bookingService;
        _roomService = roomService;
        _settingsService = settingsService;
        _utilityBillService = utilityBillService;
        _posService = posService;

        InitializeUI();
        Load += async (s, e) => await LoadCustomersAsync();
    }

    private void InitializeUI()
    {
        Dock = DockStyle.Fill;
        Font = new Font("Segoe UI", 10.5F, FontStyle.Regular);
        BackColor = Color.FromArgb(245, 247, 250);

        // --- Top Bar ---
        var topPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Padding = new Padding(15, 10, 15, 10),
            BackColor = Color.White,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        var lblTitle = new Label { Text = "ระบบจัดการข้อมูลผู้เข้าพัก และสัญญาเช่าห้องพัก", Font = new Font("Segoe UI", 14F, FontStyle.Bold), AutoSize = true, Margin = new Padding(0, 5, 20, 5) };

        var lblSearch = new Label { Text = "ค้นหา:", Font = new Font("Segoe UI", 10.5F, FontStyle.Bold), AutoSize = true, Margin = new Padding(5, 10, 5, 5) };
        _txtSearch = new TextBox
        {
            Width = 320,
            Font = new Font("Segoe UI", 11F),
            PlaceholderText = "พิมพ์เบอร์โทร / ชื่อ / เลขบัตร เพื่อค้นหาทันที...",
            Margin = new Padding(5, 6, 5, 5)
        };
        _txtSearch.TextChanged += async (s, e) => await LoadCustomersAsync(_txtSearch.Text);

        var btnRefresh = new Button
        {
            Text = "รีเฟรช",
            Size = new Size(110, 34),
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            BackColor = Color.FromArgb(241, 245, 249),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Margin = new Padding(15, 4, 5, 5)
        };
        btnRefresh.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        btnRefresh.Click += async (s, e) => {
            _txtSearch.Clear();
            await LoadCustomersAsync();
        };

        topPanel.Controls.AddRange(new Control[] { lblTitle, lblSearch, _txtSearch, btnRefresh });

        // --- Split Container ---
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterWidth = 6
        };

        split.Resize += (s, e) =>
        {
            try
            {
                if (split.Width < 850)
                {
                    split.Orientation = Orientation.Horizontal;
                    split.SplitterDistance = Math.Max(150, split.Height - 380);
                }
                else
                {
                    split.Orientation = Orientation.Vertical;
                    split.Panel1MinSize = 200;
                    split.Panel2MinSize = 200;
                    int targetDist = (int)(split.Width * 0.48);
                    int min = split.Panel1MinSize;
                    int max = split.Width - split.Panel2MinSize;
                    if (max > min && targetDist >= min && targetDist <= max)
                    {
                        split.SplitterDistance = targetDist;
                    }
                }
            }
            catch { }
        };

        // --- Left Panel: DataGridView Customers ---
        _dgvCustomers = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AllowUserToAddRows = false,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
            RowTemplate = { Height = 36 },
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            GridColor = Color.FromArgb(226, 232, 240)
        };
        _dgvCustomers.EnableDoubleBuffering();
        _dgvCustomers.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
            BackColor = Color.FromArgb(30, 41, 59),
            ForeColor = Color.White,
            SelectionBackColor = Color.FromArgb(30, 41, 59),
            SelectionForeColor = Color.White,
            WrapMode = DataGridViewTriState.True,
            Alignment = DataGridViewContentAlignment.MiddleLeft
        };
        _dgvCustomers.EnableHeadersVisualStyles = false;
        _dgvCustomers.DefaultCellStyle = new DataGridViewCellStyle
        {
            Font = new Font("Segoe UI", 10F),
            SelectionBackColor = Color.FromArgb(219, 234, 254),
            SelectionForeColor = Color.FromArgb(15, 23, 42)
        };
        _dgvCustomers.SelectionChanged += DgvCustomers_SelectionChanged;

        _pgPanel = new GridPaginationPanel(() => UpdatePagination());
        split.Panel1.Controls.Add(_pgPanel);
        split.Panel1.Controls.Add(_dgvCustomers);
        _dgvCustomers.BringToFront();

        // --- Right Panel: Tab Control ---
        var tabControlRight = new TabControl
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10.5F),
            DrawMode = TabDrawMode.OwnerDrawFixed,
            SizeMode = TabSizeMode.Fixed,
            ItemSize = new Size(185, 42),
            Padding = new Point(12, 6)
        };

        tabControlRight.DrawItem += (s, e) =>
        {
            var g = e.Graphics;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            var tabRect = tabControlRight.GetTabRect(e.Index);
            bool isSelected = (tabControlRight.SelectedIndex == e.Index);

            Color backColor = isSelected ? Color.FromArgb(30, 41, 59) : Color.FromArgb(241, 245, 249);
            Color textColor = isSelected ? Color.White : Color.FromArgb(71, 85, 105);

            using var brushBack = new SolidBrush(backColor);
            g.FillRectangle(brushBack, tabRect);

            if (isSelected)
            {
                using var accentBrush = new SolidBrush(Color.FromArgb(59, 130, 246));
                g.FillRectangle(accentBrush, tabRect.X, tabRect.Y, tabRect.Width, 4);
            }
            else
            {
                using var borderPen = new Pen(Color.FromArgb(226, 232, 240));
                g.DrawRectangle(borderPen, tabRect.X, tabRect.Y, tabRect.Width - 1, tabRect.Height - 1);
            }

            string title = tabControlRight.TabPages[e.Index].Text;
            using var font = new Font("Segoe UI", isSelected ? 10.5F : 10F, isSelected ? FontStyle.Bold : FontStyle.Regular);
            using var brushText = new SolidBrush(textColor);
            var sf = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            g.DrawString(title, font, brushText, tabRect, sf);
        };

        var tabInfo = new TabPage { Text = "ข้อมูลผู้เข้าพัก และค่าน้ำไฟ" };
        tabInfo.Controls.Add(BuildCustomerFormPanel());

        var tabStayHistory = new TabPage { Text = "ประวัติเข้าพัก" };
        tabStayHistory.Controls.Add(BuildStayHistoryPanel());

        var tabPOSHistory = new TabPage { Text = "ประวัติซื้อสินค้า (POS)" };
        tabPOSHistory.Controls.Add(BuildPOSHistoryPanel());

        var tabBillHistory = new TabPage { Text = "ค่าน้ำ/ค่าไฟ" };
        tabBillHistory.Controls.Add(BuildBillHistoryPanel());

        tabControlRight.TabPages.Add(tabInfo);
        tabControlRight.TabPages.Add(tabStayHistory);
        tabControlRight.TabPages.Add(tabPOSHistory);
        tabControlRight.TabPages.Add(tabBillHistory);

        split.Panel2.Controls.Add(tabControlRight);

        Controls.Add(topPanel);
        Controls.Add(split);
        split.BringToFront();
    }

    #region Tab 1: Customer Form Panel
    private Panel BuildCustomerFormPanel()
    {
        var panelInput = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(15),
            AutoScroll = true,
            BackColor = Color.White
        };

        // --- Stats Banner ---
        var pnlStats = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 65,
            ColumnCount = 3,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 10),
            BackColor = Color.FromArgb(248, 250, 252)
        };
        pnlStats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        pnlStats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        pnlStats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34F));

        var cardStay = CreateStatCard("เข้าพักสะสม", out _lblStatStayCount, "0 ครั้ง", Color.FromArgb(37, 99, 235));
        var cardPos = CreateStatCard("ยอด POS รวม", out _lblStatPosTotal, "0.00 บาท", Color.FromArgb(22, 163, 74));
        var cardBill = CreateStatCard("บิลค่าน้ำไฟ", out _lblStatBillCount, "0 รายการ", Color.FromArgb(217, 119, 6));

        pnlStats.Controls.Add(cardStay, 0, 0);
        pnlStats.Controls.Add(cardPos, 1, 0);
        pnlStats.Controls.Add(cardBill, 2, 0);

        // --- Rental & Utilities Accordion Panel ---
        _grpRentalSummary = new Panel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 5, 0, 10),
            BackColor = Color.White
        };

        _pnlRentalHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 40,
            BackColor = Color.FromArgb(30, 41, 59),
            Cursor = Cursors.Hand,
            Padding = new Padding(10, 0, 10, 0)
        };

        _lblRentalHeaderTitle = new Label
        {
            Text = "🏢 ข้อมูลสัญญาเช่าห้องพัก & มิเตอร์ค่าน้ำ/ค่าไฟล่าสุด",
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(10, 9),
            Cursor = Cursors.Hand
        };

        _lblRentalHeaderSummaryBadge = new Label
        {
            Text = "[ ไม่พบข้อมูลสัญญาเช่า ]",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(203, 213, 225),
            AutoSize = true,
            Location = new Point(360, 10),
            Cursor = Cursors.Hand
        };

        _btnToggleRental = new Button
        {
            Text = "▲ หุบเข้า",
            Size = new Size(90, 28),
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            BackColor = Color.FromArgb(51, 65, 85),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        _btnToggleRental.FlatAppearance.BorderSize = 0;

        _pnlRentalHeader.Controls.Add(_lblRentalHeaderTitle);
        _pnlRentalHeader.Controls.Add(_lblRentalHeaderSummaryBadge);
        _pnlRentalHeader.Controls.Add(_btnToggleRental);
        _btnToggleRental.Location = new Point(_pnlRentalHeader.Width - 100, 6);

        _pnlRentalContentContainer = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            Padding = new Padding(12),
            Margin = new Padding(0),
            BackColor = Color.FromArgb(248, 250, 252)
        };
        _pnlRentalContentContainer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _pnlRentalContentContainer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _pnlRentalContentContainer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _pnlRentalContentContainer.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _lblRentalInfo = new Label
        {
            Text = "ห้องพัก: -",
            Font = new Font("Segoe UI", 10.5F),
            ForeColor = Color.FromArgb(15, 23, 42),
            AutoSize = true,
            Margin = new Padding(0, 4, 0, 8)
        };

        _lblMeterInfo = new Label
        {
            Text = "มิเตอร์ไฟ: -  |  มิเตอร์น้ำ: -",
            Font = new Font("Segoe UI", 10.5F),
            ForeColor = Color.FromArgb(15, 23, 42),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 8)
        };

        _lblBillStatusBadge = new Label
        {
            Text = "บิลล่าสุด: -",
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
            ForeColor = Color.DarkGoldenrod,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 8)
        };

        _lblUnpaidTotalAlert = new Label
        {
            Text = "ยอดค้างชำระรวมทั้งหมด: 0.00 บาท",
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor = Color.ForestGreen,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 4)
        };

        _pnlRentalContentContainer.Controls.Add(_lblRentalInfo, 0, 0);
        _pnlRentalContentContainer.Controls.Add(_lblMeterInfo, 0, 1);
        _pnlRentalContentContainer.Controls.Add(_lblBillStatusBadge, 0, 2);
        _pnlRentalContentContainer.Controls.Add(_lblUnpaidTotalAlert, 0, 3);

        _grpRentalSummary.Controls.Add(_pnlRentalContentContainer);
        _grpRentalSummary.Controls.Add(_pnlRentalHeader);

        void ToggleRentalPanel()
        {
            _isRentalExpanded = !_isRentalExpanded;
            _pnlRentalContentContainer.Visible = _isRentalExpanded;
            _btnToggleRental.Text = _isRentalExpanded ? "▲ หุบเข้า" : "▼ กางออก";
        }

        _pnlRentalHeader.Click += (s, e) => ToggleRentalPanel();
        _lblRentalHeaderTitle.Click += (s, e) => ToggleRentalPanel();
        _lblRentalHeaderSummaryBadge.Click += (s, e) => ToggleRentalPanel();
        _btnToggleRental.Click += (s, e) => ToggleRentalPanel();

        // --- Mode Banner ---
        _panelModeBanner = new Panel
        {
            Dock = DockStyle.Top,
            Height = 44,
            BackColor = Color.FromArgb(240, 253, 244),
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(0, 5, 0, 10)
        };

        _lblModeText = new Label
        {
            Text = "โหมด: เพิ่มผู้เข้าพักใหม่",
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
            ForeColor = Color.ForestGreen,
            Location = new Point(12, 10),
            AutoSize = true
        };

        _btnCancelEdit = new Button
        {
            Text = "ยกเลิกแก้ไข",
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(_panelModeBanner.Width - 130, 7),
            Size = new Size(115, 30),
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            BackColor = Color.White,
            ForeColor = Color.DarkRed,
            FlatStyle = FlatStyle.Flat,
            Visible = false,
            Cursor = Cursors.Hand
        };
        _btnCancelEdit.FlatAppearance.BorderColor = Color.LightGray;
        _btnCancelEdit.Click += (s, e) => ClearForm();

        _panelModeBanner.Controls.Add(_lblModeText);
        _panelModeBanner.Controls.Add(_btnCancelEdit);

        // --- Input Fields Table Layout ---
        var tableForm = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            Padding = new Padding(0, 10, 0, 10)
        };
        tableForm.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
        tableForm.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        int row = 0;
        void AddRow(string labelText, Control inputControl)
        {
            tableForm.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var lbl = new Label
            {
                Text = labelText,
                Font = new Font("Segoe UI", 10.5F, labelText.Contains('*') ? FontStyle.Bold : FontStyle.Regular),
                Anchor = AnchorStyles.Left,
                AutoSize = true,
                Margin = new Padding(0, 8, 10, 8)
            };
            inputControl.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            inputControl.Margin = new Padding(0, 4, 0, 8);
            tableForm.Controls.Add(lbl, 0, row);
            tableForm.Controls.Add(inputControl, 1, row);
            row++;
        }

        _txtFullName = new TextBox { Font = new Font("Segoe UI", 11F) };
        _txtPhone = new TextBox { Font = new Font("Segoe UI", 11F) };
        _txtEmail = new TextBox { Font = new Font("Segoe UI", 11F) };
        _txtIdCard = new TextBox { Font = new Font("Segoe UI", 11F) };
        _txtAddress = new TextBox { Font = new Font("Segoe UI", 11F), Multiline = true, Height = 50 };
        _txtNotes = new TextBox { Font = new Font("Segoe UI", 11F), Multiline = true, Height = 40 };

        AddRow("ชื่อ-นามสกุล *:", _txtFullName);
        AddRow("เบอร์โทรศัพท์:", _txtPhone);
        AddRow("อีเมล:", _txtEmail);
        AddRow("เลขบัตร/พาสปอร์ต:", _txtIdCard);
        AddRow("ที่อยู่:", _txtAddress);
        AddRow("หมายเหตุ:", _txtNotes);

        // --- Action Buttons ---
        var pnlButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0, 15, 0, 15)
        };

        _btnSave = new Button
        {
            Text = "💾  บันทึกข้อมูล",
            Size = new Size(150, 42),
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            BackColor = Color.FromArgb(22, 163, 74),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 12, 0)
        };
        _btnSave.FlatAppearance.BorderSize = 0;
        _btnSave.Click += BtnSave_Click;

        _btnClear = new Button
        {
            Text = "➕  เพิ่มคนใหม่ / ล้างฟอร์ม",
            Size = new Size(210, 42),
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
            BackColor = Color.FromArgb(37, 99, 235),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 12, 0)
        };
        _btnClear.FlatAppearance.BorderSize = 0;
        _btnClear.Click += (s, e) => ClearForm();

        _btnDelete = new Button
        {
            Text = "🗑️  ลบข้อมูลผู้เข้าพัก",
            Size = new Size(170, 42),
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Color.FromArgb(220, 38, 38),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Visible = false,
            Margin = new Padding(0, 0, 0, 0)
        };
        _btnDelete.FlatAppearance.BorderSize = 0;
        _btnDelete.Click += BtnDelete_Click;

        pnlButtons.Controls.AddRange(new Control[] { _btnSave, _btnClear, _btnDelete });

        panelInput.Controls.Add(pnlButtons);
        panelInput.Controls.Add(tableForm);
        panelInput.Controls.Add(_grpRentalSummary);
        panelInput.Controls.Add(_panelModeBanner);
        panelInput.Controls.Add(pnlStats);

        return panelInput;
    }

    private Panel CreateStatCard(string title, out Label valLabel, string defaultVal, Color accentColor)
    {
        var card = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(3),
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };
        var lblTitle = new Label
        {
            Text = title,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(100, 116, 139),
            Location = new Point(10, 8),
            AutoSize = true
        };
        valLabel = new Label
        {
            Text = defaultVal,
            Font = new Font("Segoe UI", 12.5F, FontStyle.Bold),
            ForeColor = accentColor,
            Location = new Point(10, 30),
            AutoSize = true
        };
        card.Controls.Add(lblTitle);
        card.Controls.Add(valLabel);
        return card;
    }
    #endregion

    #region Tab 2: Stay History Panel & Action Card
    private Panel BuildStayHistoryPanel()
    {
        var main = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };

        _dgvStayHistory = CreateHistoryGrid();
        _dgvStayHistory.Dock = DockStyle.Top;
        _dgvStayHistory.Height = 260;

        _pnlStayDetail = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(15),
            BackColor = Color.FromArgb(248, 250, 252),
            BorderStyle = BorderStyle.FixedSingle
        };

        _lblStayDetailTitle = new Label
        {
            Text = "รายละเอียดการเข้าพักที่เลือก",
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59),
            Dock = DockStyle.Top,
            Height = 28
        };

        _lblStayDetailInfo = new Label
        {
            Text = "กรุณาคลิกเลือกรายการจากตารางด้านบน เพื่อดูรายละเอียดและพิมพ์ใบเสร็จ",
            Font = new Font("Segoe UI", 10.5F),
            ForeColor = Color.FromArgb(71, 85, 105),
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 10, 0, 10)
        };

        _btnViewStayReceipt = new Button
        {
            Text = "เปิดดูใบเสร็จ / พิมพ์เอกสาร (Print Preview)",
            Dock = DockStyle.Bottom,
            Height = 44,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            BackColor = Color.FromArgb(203, 213, 225),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Enabled = false,
            Cursor = Cursors.Hand
        };
        _btnViewStayReceipt.FlatAppearance.BorderSize = 0;
        _btnViewStayReceipt.Click += async (s, e) =>
        {
            if (_selectedBookingId > 0)
            {
                await ShowBookingReceiptPreviewAsync(_selectedBookingId);
            }
        };

        _pnlStayDetail.Controls.Add(_lblStayDetailInfo);
        _pnlStayDetail.Controls.Add(_lblStayDetailTitle);
        _pnlStayDetail.Controls.Add(_btnViewStayReceipt);

        _dgvStayHistory.SelectionChanged += DgvStayHistory_SelectionChanged;
        _dgvStayHistory.CellDoubleClick += async (s, ev) =>
        {
            if (ev.RowIndex >= 0 && _dgvStayHistory.Columns.Contains("BookingId"))
            {
                var val = _dgvStayHistory.Rows[ev.RowIndex].Cells["BookingId"].Value;
                if (val != null)
                {
                    await ShowBookingReceiptPreviewAsync(Convert.ToInt32(val));
                }
            }
        };

        main.Controls.Add(_pnlStayDetail);
        main.Controls.Add(_dgvStayHistory);
        return main;
    }

    private void DgvStayHistory_SelectionChanged(object? sender, EventArgs e)
    {
        if (_dgvStayHistory.SelectedRows.Count > 0 && _dgvStayHistory.Columns.Contains("BookingId"))
        {
            var val = _dgvStayHistory.SelectedRows[0].Cells["BookingId"].Value;
            if (val != null && int.TryParse(val.ToString(), out int bId) && bId > 0)
            {
                _selectedBookingId = bId;
                var row = _dgvStayHistory.SelectedRows[0];
                string code = row.Cells["เลขบิล"].Value?.ToString() ?? "-";
                string room = row.Cells["ห้องพัก"].Value?.ToString() ?? "-";
                string checkIn = row.Cells["วันที่เข้าพัก"].Value?.ToString() ?? "-";
                string checkOut = row.Cells["วันที่ออก"].Value?.ToString() ?? "-";
                string status = row.Cells["สถานะ"].Value?.ToString() ?? "-";
                string total = row.Cells["ยอดชำระ"].Value?.ToString() ?? "-";

                _lblStayDetailInfo.Text = $"เลขที่บิลการจอง:  {code}\n" +
                                          $"ห้องพัก:  {room}   |   สถานะ:  {status}\n" +
                                          $"เช็คอิน:  {checkIn}   ถึง   เช็คเอ้าท์:  {checkOut}\n" +
                                          $"ยอดชำระเงินรวม:  {total}";
                _lblStayDetailInfo.ForeColor = Color.FromArgb(15, 23, 42);
                _btnViewStayReceipt.Enabled = true;
                _btnViewStayReceipt.BackColor = Color.FromArgb(37, 99, 235);
                return;
            }
        }
        _selectedBookingId = 0;
        _lblStayDetailInfo.Text = "กรุณาคลิกเลือกรายการจากตารางด้านบน เพื่อดูรายละเอียดและพิมพ์ใบเสร็จ";
        _lblStayDetailInfo.ForeColor = Color.FromArgb(71, 85, 105);
        _btnViewStayReceipt.Enabled = false;
        _btnViewStayReceipt.BackColor = Color.FromArgb(203, 213, 225);
    }
    #endregion

    #region Tab 3: POS History Panel & Action Card
    private Panel BuildPOSHistoryPanel()
    {
        var main = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };

        _dgvPOSHistory = CreateHistoryGrid();
        _dgvPOSHistory.Dock = DockStyle.Top;
        _dgvPOSHistory.Height = 260;

        _pnlPosDetail = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(15),
            BackColor = Color.FromArgb(248, 250, 252),
            BorderStyle = BorderStyle.FixedSingle
        };

        _lblPosDetailTitle = new Label
        {
            Text = "รายละเอียดการสั่งซื้อ POS ที่เลือก",
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59),
            Dock = DockStyle.Top,
            Height = 28
        };

        _lblPosDetailInfo = new Label
        {
            Text = "กรุณาคลิกเลือกรายการจากตารางด้านบน เพื่อดูรายละเอียดและพิมพ์ใบเสร็จ POS",
            Font = new Font("Segoe UI", 10.5F),
            ForeColor = Color.FromArgb(71, 85, 105),
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 10, 0, 10)
        };

        _btnViewPosReceipt = new Button
        {
            Text = "เปิดดูใบเสร็จ POS (Print Preview)",
            Dock = DockStyle.Bottom,
            Height = 44,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            BackColor = Color.FromArgb(203, 213, 225),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Enabled = false,
            Cursor = Cursors.Hand
        };
        _btnViewPosReceipt.FlatAppearance.BorderSize = 0;
        _btnViewPosReceipt.Click += async (s, e) =>
        {
            if (_selectedSaleId > 0)
            {
                await ShowPOSReceiptPreviewAsync(_selectedSaleId);
            }
        };

        _pnlPosDetail.Controls.Add(_lblPosDetailInfo);
        _pnlPosDetail.Controls.Add(_lblPosDetailTitle);
        _pnlPosDetail.Controls.Add(_btnViewPosReceipt);

        _dgvPOSHistory.SelectionChanged += DgvPOSHistory_SelectionChanged;
        _dgvPOSHistory.CellDoubleClick += async (s, ev) =>
        {
            if (ev.RowIndex >= 0 && _dgvPOSHistory.Columns.Contains("SaleId"))
            {
                var val = _dgvPOSHistory.Rows[ev.RowIndex].Cells["SaleId"].Value;
                if (val != null)
                {
                    await ShowPOSReceiptPreviewAsync(Convert.ToInt32(val));
                }
            }
        };

        main.Controls.Add(_pnlPosDetail);
        main.Controls.Add(_dgvPOSHistory);
        return main;
    }

    private void DgvPOSHistory_SelectionChanged(object? sender, EventArgs e)
    {
        if (_dgvPOSHistory.SelectedRows.Count > 0 && _dgvPOSHistory.Columns.Contains("SaleId"))
        {
            var val = _dgvPOSHistory.SelectedRows[0].Cells["SaleId"].Value;
            if (val != null && int.TryParse(val.ToString(), out int sId) && sId > 0)
            {
                _selectedSaleId = sId;
                var row = _dgvPOSHistory.SelectedRows[0];
                string code = row.Cells["เลขที่บิล"].Value?.ToString() ?? "-";
                string date = row.Cells["วันที่ขาย"].Value?.ToString() ?? "-";
                string total = row.Cells["ยอดชำระ"].Value?.ToString() ?? "-";
                string items = row.Cells["รายการสินค้า"].Value?.ToString() ?? "-";

                _lblPosDetailInfo.Text = $"เลขที่บิลขาย POS:  {code}\n" +
                                         $"วันที่ทำรายการ:  {date}\n" +
                                         $"รายการสินค้า:  {items}\n" +
                                         $"ยอดชำระเงินรวม:  {total}";
                _lblPosDetailInfo.ForeColor = Color.FromArgb(15, 23, 42);
                _btnViewPosReceipt.Enabled = true;
                _btnViewPosReceipt.BackColor = Color.FromArgb(22, 163, 74);
                return;
            }
        }
        _selectedSaleId = 0;
        _lblPosDetailInfo.Text = "กรุณาคลิกเลือกรายการจากตารางด้านบน เพื่อดูรายละเอียดและพิมพ์ใบเสร็จ POS";
        _lblPosDetailInfo.ForeColor = Color.FromArgb(71, 85, 105);
        _btnViewPosReceipt.Enabled = false;
        _btnViewPosReceipt.BackColor = Color.FromArgb(203, 213, 225);
    }
    #endregion

    #region Tab 4: Bill History Panel & Action Card
    private Panel BuildBillHistoryPanel()
    {
        var main = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };

        _dgvBillHistory = CreateHistoryGrid();
        _dgvBillHistory.Dock = DockStyle.Top;
        _dgvBillHistory.Height = 260;

        _pnlBillDetail = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(15),
            BackColor = Color.FromArgb(248, 250, 252),
            BorderStyle = BorderStyle.FixedSingle
        };

        _lblBillDetailTitle = new Label
        {
            Text = "รายละเอียดใบแจ้งหนี้ค่าน้ำ/ค่าไฟที่เลือก",
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59),
            Dock = DockStyle.Top,
            Height = 28
        };

        _lblBillDetailInfo = new Label
        {
            Text = "กรุณาคลิกเลือกรายการจากตารางด้านบน เพื่อดูรายละเอียดและพิมพ์ใบแจ้งหนี้",
            Font = new Font("Segoe UI", 10.5F),
            ForeColor = Color.FromArgb(71, 85, 105),
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 10, 0, 10)
        };

        _btnViewBillReceipt = new Button
        {
            Text = "เปิดดูใบแจ้งหนี้ค่าน้ำ/ค่าไฟ (Print Preview)",
            Dock = DockStyle.Bottom,
            Height = 44,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            BackColor = Color.FromArgb(203, 213, 225),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Enabled = false,
            Cursor = Cursors.Hand
        };
        _btnViewBillReceipt.FlatAppearance.BorderSize = 0;
        _btnViewBillReceipt.Click += async (s, e) =>
        {
            if (_selectedBillId > 0)
            {
                await ShowUtilityBillPreviewAsync(_selectedBillId);
            }
        };

        _pnlBillDetail.Controls.Add(_lblBillDetailInfo);
        _pnlBillDetail.Controls.Add(_lblBillDetailTitle);
        _pnlBillDetail.Controls.Add(_btnViewBillReceipt);

        _dgvBillHistory.SelectionChanged += DgvBillHistory_SelectionChanged;
        _dgvBillHistory.CellDoubleClick += async (s, ev) =>
        {
            if (ev.RowIndex >= 0 && _dgvBillHistory.Columns.Contains("Id"))
            {
                var val = _dgvBillHistory.Rows[ev.RowIndex].Cells["Id"].Value;
                if (val != null)
                {
                    await ShowUtilityBillPreviewAsync(Convert.ToInt32(val));
                }
            }
        };

        main.Controls.Add(_pnlBillDetail);
        main.Controls.Add(_dgvBillHistory);
        return main;
    }

    private void DgvBillHistory_SelectionChanged(object? sender, EventArgs e)
    {
        if (_dgvBillHistory.SelectedRows.Count > 0 && _dgvBillHistory.Columns.Contains("Id"))
        {
            var val = _dgvBillHistory.SelectedRows[0].Cells["Id"].Value;
            if (val != null && int.TryParse(val.ToString(), out int bId) && bId > 0)
            {
                _selectedBillId = bId;
                var bill = _loadedBills.FirstOrDefault(b => b.Id == _selectedBillId);
                var row = _dgvBillHistory.SelectedRows[0];
                string code = row.Cells["เลขที่บิล"].Value?.ToString() ?? "-";
                string room = row.Cells["ห้องพัก"].Value?.ToString() ?? "-";
                string month = row.Cells["รอบบิล"].Value?.ToString() ?? "-";
                string total = row.Cells["ยอดรวม"].Value?.ToString() ?? "-";
                string status = row.Cells["สถานะชำระ"].Value?.ToString() ?? "-";

                string meterDetails = "";
                if (bill != null)
                {
                    meterDetails = $"มิเตอร์ไฟ:  {bill.ElectricPrev} ➔ {bill.ElectricCurr} ({bill.ElectricUnits} หน่วย @ {bill.ElectricRate:N2} บ. = {bill.ElectricAmount:N2} บาท)\n" +
                                   $"มิเตอร์น้ำ:  {bill.WaterPrev} ➔ {bill.WaterCurr} ({bill.WaterUnits} หน่วย @ {bill.WaterRate:N2} บ. = {bill.WaterAmount:N2} บาท)\n";
                }

                _lblBillDetailInfo.Text = $"เลขที่ใบแจ้งหนี้:  {code}  (รอบบิลประจำเดือน: {month})\n" +
                                          $"ห้องพัก:  {room}   |   สถานะการชำระ:  {status}\n" +
                                          meterDetails +
                                          $"ยอดชำระเงินรวม:  {total}";
                _lblBillDetailInfo.ForeColor = Color.FromArgb(15, 23, 42);
                _btnViewBillReceipt.Enabled = true;
                _btnViewBillReceipt.BackColor = Color.FromArgb(217, 119, 6);
                return;
            }
        }
        _selectedBillId = 0;
        _lblBillDetailInfo.Text = "กรุณาคลิกเลือกรายการจากตารางด้านบน เพื่อดูรายละเอียดและพิมพ์ใบแจ้งหนี้";
        _lblBillDetailInfo.ForeColor = Color.FromArgb(71, 85, 105);
        _btnViewBillReceipt.Enabled = false;
        _btnViewBillReceipt.BackColor = Color.FromArgb(203, 213, 225);
    }

    private DataGridView CreateHistoryGrid()
    {
        var dgv = new DataGridView
        {
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AllowUserToAddRows = false,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            RowTemplate = { Height = 36 },
            GridColor = Color.FromArgb(226, 232, 240),
            DefaultCellStyle = new DataGridViewCellStyle
            {
                Font = new Font("Segoe UI", 9.5F),
                Padding = new Padding(6, 2, 6, 2),
                BackColor = Color.White,
                ForeColor = Color.FromArgb(15, 23, 42),
                SelectionBackColor = Color.FromArgb(224, 231, 255),
                SelectionForeColor = Color.FromArgb(15, 23, 42)
            },
            ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                BackColor = Color.FromArgb(71, 85, 105),
                ForeColor = Color.White,
                SelectionBackColor = Color.FromArgb(71, 85, 105),
                SelectionForeColor = Color.White,
                Padding = new Padding(6, 8, 6, 8),
                Alignment = DataGridViewContentAlignment.MiddleCenter
            },
            EnableHeadersVisualStyles = false,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize
        };
        dgv.EnableDoubleBuffering();
        return dgv;
    }
    #endregion

    #region Helper Methods for Bill Due Status
    private (string Text, Color Color, string BadgeType) GetBillStatusInfo(UtilityBill bill)
    {
        if (bill.IsPaid)
        {
            string paidDateStr = bill.PaidAt.HasValue ? $" (ชำระเมื่อ {bill.PaidAt.Value:dd/MM/yyyy})" : "";
            return ($"[ชำระแล้ว]{paidDateStr}", Color.ForestGreen, "PAID");
        }

        var dueDate = bill.CreatedAt.Date.AddDays(5);
        int daysOverdue = (DateTime.Today - dueDate).Days;

        if (daysOverdue > 0)
        {
            return ($"[เลยกำหนดชำระ ({daysOverdue} วัน)]", Color.Red, "OVERDUE");
        }
        else
        {
            int daysRemaining = Math.Abs(daysOverdue);
            if (daysRemaining == 0)
            {
                return ("[ครบกำหนดวันนี้]", Color.DarkGoldenrod, "DUE_SOON");
            }
            return ($"[ใกล้ครบกำหนด (เหลือ {daysRemaining} วัน)]", Color.DarkGoldenrod, "DUE_SOON");
        }
    }
    #endregion

    #region Data Loading & Logic
    private void UpdatePagination()
    {
        _pgPanel.UpdateState(_customersList.Count);
        var pageData = _pgPanel.GetPageData(_customersList).ToList();

        _dgvCustomers.DataSource = pageData.Select(c => new
        {
            c.Id,
            ชื่อนามสกุล = c.FullName,
            เบอร์โทร = c.Phone ?? "-",
            เลขบัตร = c.IdCardOrPassport ?? "-",
            อีเมล = c.Email ?? "-",
            วันที่ลงทะเบียน = c.CreatedAt.ToString("dd/MM/yyyy")
        }).ToList();

        _dgvCustomers.RowHeadersVisible = false;

        if (_dgvCustomers.Columns.Contains("Id"))
        {
            _dgvCustomers.Columns["Id"].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            _dgvCustomers.Columns["Id"].Width = 55;
            _dgvCustomers.Columns["Id"].HeaderText = "ID";
            _dgvCustomers.Columns["Id"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        }
        if (_dgvCustomers.Columns.Contains("ชื่อนามสกุล"))
        {
            _dgvCustomers.Columns["ชื่อนามสกุล"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _dgvCustomers.Columns["ชื่อนามสกุล"].FillWeight = 180;
        }
    }

    private async Task LoadCustomersAsync(string? query = null)
    {
        try
        {
            _customersList = (await _customerService.GetCustomersAsync(query)).ToList();
            _pgPanel.Reset();
            UpdatePagination();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"โหลดข้อมูลผู้เข้าพักไม่สำเร็จ: {ex.Message}", "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void DgvCustomers_SelectionChanged(object? sender, EventArgs e)
    {
        if (_dgvCustomers.SelectedRows.Count > 0)
        {
            var row = _dgvCustomers.SelectedRows[0];
            if (row.Cells["Id"].Value != null && int.TryParse(row.Cells["Id"].Value.ToString(), out int id))
            {
                _selectedCustomerId = id;
                await LoadCustomerFormAsync(_selectedCustomerId);
                return;
            }
        }

        ClearForm();
    }

    private async Task LoadCustomerFormAsync(int customerId)
    {
        var cust = _customersList.FirstOrDefault(c => c.Id == customerId);
        if (cust != null)
        {
            _txtFullName.Text = cust.FullName;
            _txtPhone.Text = cust.Phone;
            _txtEmail.Text = cust.Email;
            _txtIdCard.Text = cust.IdCardOrPassport;
            _txtAddress.Text = cust.Address;
            _txtNotes.Text = cust.Notes;

            _panelModeBanner.BackColor = Color.FromArgb(254, 243, 199);
            _lblModeText.Text = $"โหมด: แก้ไขผู้เข้าพัก '{cust.FullName}'";
            _lblModeText.ForeColor = Color.DarkGoldenrod;
            _btnCancelEdit.Visible = true;
            _btnDelete.Visible = true;

            await LoadCustomerHistoriesAsync(_selectedCustomerId);
        }
    }

    private async Task LoadCustomerHistoriesAsync(int customerId)
    {
        try
        {
            var stays = (await _customerService.GetCustomerStayHistoryAsync(customerId)).ToList();
            _dgvStayHistory.DataSource = stays.Select(s => new
            {
                BookingId = s.BookingId,
                เลขบิล = s.BookingCode,
                ห้องพัก = s.RoomNumber,
                วันที่เข้าพัก = s.CheckIn.ToString("dd/MM/yyyy HH:mm"),
                วันที่ออก = s.CheckOut?.ToString("dd/MM/yyyy HH:mm") ?? "-",
                สถานะ = s.Status,
                ยอดชำระ = s.TotalAmount.ToString("N2") + " บาท"
            }).ToList();

            if (_dgvStayHistory.Columns.Contains("BookingId"))
            {
                _dgvStayHistory.Columns["BookingId"].Visible = false;
            }

            _lblStatStayCount.Text = $"{stays.Count} ครั้ง";

            var sales = (await _customerService.GetCustomerPOSHistoryAsync(customerId)).ToList();
            _dgvPOSHistory.DataSource = sales.Select(s => new
            {
                SaleId = s.SaleId,
                เลขที่บิล = s.SaleCode,
                วันที่ขาย = s.Date.ToString("dd/MM/yyyy HH:mm"),
                ยอดชำระ = s.TotalAmount.ToString("N2") + " บาท",
                รายการสินค้า = s.ItemsSummary ?? "-"
            }).ToList();

            if (_dgvPOSHistory.Columns.Contains("SaleId"))
            {
                _dgvPOSHistory.Columns["SaleId"].Visible = false;
            }

            _lblStatPosTotal.Text = sales.Sum(s => s.TotalAmount).ToString("N2") + " บาท";

            if (_utilityBillService != null)
            {
                var bills = await _utilityBillService.GetBillHistoryAsync(customerId, 24);
                _loadedBills = bills.ToList();
            }
            else
            {
                _loadedBills.Clear();
            }

            _dgvBillHistory.DataSource = _loadedBills.Select(b => {
                var statusInfo = GetBillStatusInfo(b);
                return new
                {
                    b.Id,
                    เลขที่บิล = b.BillCode,
                    ห้องพัก = b.RoomNumber ?? "-",
                    รอบบิล = b.BillingMonth,
                    ยอดรวม = b.TotalAmount.ToString("N2") + " บาท",
                    สถานะชำระ = statusInfo.Text
                };
            }).ToList();

            if (_dgvBillHistory.Columns.Contains("Id"))
            {
                _dgvBillHistory.Columns["Id"].Visible = false;
            }

            _lblStatBillCount.Text = $"{_loadedBills.Count} รายการ";

            UpdateRentalAndUtilitySummaryCard(stays);

            DgvStayHistory_SelectionChanged(null, EventArgs.Empty);
            DgvPOSHistory_SelectionChanged(null, EventArgs.Empty);
            DgvBillHistory_SelectionChanged(null, EventArgs.Empty);
        }
        catch
        {
            _dgvStayHistory.DataSource = null;
            _dgvPOSHistory.DataSource = null;
            _dgvBillHistory.DataSource = null;
            _lblStatStayCount.Text = "0 ครั้ง";
            _lblStatPosTotal.Text = "0.00 บาท";
            _lblStatBillCount.Text = "0 รายการ";
            ResetRentalAndUtilitySummaryCard();
        }
    }

    private void UpdateRentalAndUtilitySummaryCard(List<CustomerStayHistoryDto> stays)
    {
        var latestStay = stays.FirstOrDefault(s => s.CheckOut == null || s.CheckOut >= DateTime.Today) ?? stays.FirstOrDefault();

        decimal unpaidSum = _loadedBills.Where(b => !b.IsPaid).Sum(b => b.TotalAmount);
        int unpaidCount = _loadedBills.Count(b => !b.IsPaid);
        var latestBill = _loadedBills.OrderByDescending(b => b.BillingMonth).FirstOrDefault();

        if (latestStay != null && _loadedBills.Any())
        {
            int daysStayed = Math.Max(1, (int)(DateTime.Today - latestStay.CheckIn).TotalDays);
            int monthsStayed = Math.Max(1, (daysStayed / 30));

            _lblRentalHeaderSummaryBadge.Text = $"[ห้อง {latestStay.RoomNumber} | {(unpaidCount > 0 ? $"ค้าง {unpaidSum:N2} บ." : "ชำระครบแล้ว")}]";
            _lblRentalHeaderSummaryBadge.ForeColor = unpaidCount > 0 ? Color.FromArgb(252, 165, 165) : Color.FromArgb(134, 239, 172);

            _lblRentalInfo.Text = $"ห้องพักเช่าปัจจุบัน: ห้อง {latestStay.RoomNumber}   |   สัญญาเช่า: รายเดือน ({latestStay.TotalAmount:N2} บาท/เดือน)\n" +
                                  $"ระยะเวลาเช่าพักอาศัย: อยู่มาแล้ว {monthsStayed} เดือน ({daysStayed} วัน ตั้งแต่ {latestStay.CheckIn:dd/MM/yyyy})";

            if (latestBill != null)
            {
                var statusInfo = GetBillStatusInfo(latestBill);
                _lblMeterInfo.Text = $"มิเตอร์ไฟล่าสุด ({latestBill.BillingMonth}):  {latestBill.ElectricPrev} ➔ {latestBill.ElectricCurr}  [ ใช้ไป {latestBill.ElectricUnits} หน่วย @ {latestBill.ElectricRate:N2} บ. = {latestBill.ElectricAmount:N2} บาท ]\n" +
                                     $"มิเตอร์น้ำล่าสุด ({latestBill.BillingMonth}):  {latestBill.WaterPrev} ➔ {latestBill.WaterCurr}  [ ใช้ไป {latestBill.WaterUnits} หน่วย @ {latestBill.WaterRate:N2} บ. = {latestBill.WaterAmount:N2} บาท ]";

                _lblBillStatusBadge.Text = $"บิลล่าสุด: {latestBill.BillCode} (รอบบิล {latestBill.BillingMonth}) ยอด {latestBill.TotalAmount:N2} บาท ➔ {statusInfo.Text}";
                _lblBillStatusBadge.ForeColor = statusInfo.Color;
            }
            else
            {
                _lblMeterInfo.Text = "มิเตอร์ไฟ: ไม่พบบิลในระบบ  |  มิเตอร์น้ำ: ไม่พบบิลในระบบ";
                _lblBillStatusBadge.Text = "บิลล่าสุด: ไม่พบข้อมูลบิลค่าน้ำไฟ";
                _lblBillStatusBadge.ForeColor = Color.Gray;
            }

            if (unpaidCount > 0)
            {
                _lblUnpaidTotalAlert.Text = $"ยอดค้างชำระรวมทั้งหมด:  {unpaidSum:N2} บาท (ค้างชำระ {unpaidCount} บิล)";
                _lblUnpaidTotalAlert.ForeColor = Color.Red;
            }
            else
            {
                _lblUnpaidTotalAlert.Text = "ยอดค้างชำระรวมทั้งหมด:  0.00 บาท (ชำระครบถ้วนทั้งหมด)";
                _lblUnpaidTotalAlert.ForeColor = Color.ForestGreen;
            }
        }
        else
        {
            ResetRentalAndUtilitySummaryCard();
        }
    }

    private void ResetRentalAndUtilitySummaryCard()
    {
        _lblRentalHeaderSummaryBadge.Text = "[ ไม่พบข้อมูลสัญญาเช่า ]";
        _lblRentalHeaderSummaryBadge.ForeColor = Color.FromArgb(203, 213, 225);

        _lblRentalInfo.Text = "ผู้เข้าพักรายนี้ไม่มีสัญญาห้องเช่ารายเดือนที่เปิดอยู่ (ไม่พบข้อมูลมิเตอร์ค่าน้ำ/ค่าไฟ)";
        _lblMeterInfo.Text = "มิเตอร์ไฟ: -  |  มิเตอร์น้ำ: -";
        _lblBillStatusBadge.Text = "บิลล่าสุด: -";
        _lblBillStatusBadge.ForeColor = Color.DarkGoldenrod;
        _lblUnpaidTotalAlert.Text = "ยอดค้างชำระรวมทั้งหมด: 0.00 บาท";
        _lblUnpaidTotalAlert.ForeColor = Color.ForestGreen;
    }

    private async Task ShowBookingReceiptPreviewAsync(int bookingId)
    {
        if (_bookingService == null || _roomService == null) return;
        try
        {
            var booking = await _bookingService.GetBookingByIdAsync(bookingId);
            if (booking == null) return;

            var room = (await _roomService.GetRoomsAsync()).FirstOrDefault(r => r.Id == booking.RoomId) ?? new Room { RoomNumber = "?" };
            var customer = await _customerService.GetCustomerByIdAsync(booking.CustomerId);
            var folio = await _bookingService.GetFolioByBookingIdAsync(booking.Id);

            SystemSettingsDto? settings = null;
            if (_settingsService != null)
            {
                settings = await _settingsService.GetAllSettingsAsync();
            }

            UtilityBill? utilityBill = null;
            if (_utilityBillService != null && booking.RatePlan == RatePlanType.Monthly)
            {
                var checkoutDate = booking.CheckOutActual ?? DateTime.Now;
                string billingMonth = checkoutDate.ToString("yyyy-MM");
                var bills = await _utilityBillService.GetBillsByMonthAsync(billingMonth);
                utilityBill = bills.FirstOrDefault(b => b.RoomId == booking.RoomId);
            }

            var printer = new HotelPOS.Printing.ReceiptInvoicePrinter(
                settings?.ShopName ?? "ชื่อร้าน/ที่พักของคุณ",
                settings?.ShopAddress ?? "123/45 ถนนสุขุมวิท กรุงเทพฯ",
                settings?.ShopPhone ?? "02-123-4567",
                settings?.ShopTaxId ?? "0105560000000",
                booking,
                room,
                customer,
                folio,
                "admin",
                settings,
                utilityBill
            );
            printer.ShowPrintPreview();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"ไม่สามารถโหลดบิลเข้าพักย้อนหลังได้: {ex.Message}", "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task ShowPOSReceiptPreviewAsync(int saleId)
    {
        if (_posService == null || _settingsService == null) return;
        try
        {
            var sale = await _posService.GetSaleByIdAsync(saleId);
            if (sale == null) return;

            Room room = new Room { RoomNumber = "หน้าร้าน (Retail)" };
            Customer customer = new Customer { FullName = "ลูกค้าทั่วไป" };

            if (sale.FolioId.HasValue)
            {
                var folioDetails = await _posService.GetFolioDetailsAsync(sale.FolioId.Value);
                if (folioDetails.HasValue)
                {
                    room = folioDetails.Value.Room;
                    customer = folioDetails.Value.Customer;
                }
            }
            else if (sale.CustomerId.HasValue)
            {
                var cust = await _posService.GetCustomerByIdAsync(sale.CustomerId.Value);
                if (cust != null) customer = cust;
            }

            var settings = await _settingsService.GetAllSettingsAsync();
            var dummyBooking = new Booking
            {
                BookingCode = sale.SaleCode,
                AgreedRate = sale.SubTotal,
                CreatedAt = sale.CreatedAt
            };
            var dummyFolio = new Folio
            {
                TotalAmount = sale.TotalAmount,
                DiscountAmount = sale.DiscountAmount
            };

            var printer = new HotelPOS.Printing.ReceiptInvoicePrinter(dummyBooking, room, customer, dummyFolio, settings);
            printer.ShowPrintPreview();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"ไม่สามารถแสดงตัวอย่างใบเสร็จ POS ได้: {ex.Message}", "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task ShowUtilityBillPreviewAsync(int billId)
    {
        if (_utilityBillService == null || _settingsService == null) return;
        try
        {
            var bill = _loadedBills.FirstOrDefault(b => b.Id == billId);
            if (bill == null) return;

            var settings = await _settingsService.GetAllSettingsAsync();
            var printer = new HotelPOS.Printing.UtilityInvoicePrinter(bill, null, settings);
            printer.ShowPrintPreview();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"ไม่สามารถแสดงตัวอย่างใบแจ้งหนี้ค่าน้ำค่าไฟได้: {ex.Message}", "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ClearForm()
    {
        _selectedCustomerId = 0;
        _txtFullName.Clear();
        _txtPhone.Clear();
        _txtEmail.Clear();
        _txtIdCard.Clear();
        _txtAddress.Clear();
        _txtNotes.Clear();

        _panelModeBanner.BackColor = Color.FromArgb(240, 253, 244);
        _lblModeText.Text = "โหมด: เพิ่มผู้เข้าพักใหม่";
        _lblModeText.ForeColor = Color.ForestGreen;
        _btnCancelEdit.Visible = false;
        _btnDelete.Visible = false;
        _lblStatStayCount.Text = "0 ครั้ง";
        _lblStatPosTotal.Text = "0.00 บาท";
        _lblStatBillCount.Text = "0 รายการ";

        ResetRentalAndUtilitySummaryCard();

        _dgvStayHistory.DataSource = null;
        _dgvPOSHistory.DataSource = null;
        _dgvBillHistory.DataSource = null;

        DgvStayHistory_SelectionChanged(null, EventArgs.Empty);
        DgvPOSHistory_SelectionChanged(null, EventArgs.Empty);
        DgvBillHistory_SelectionChanged(null, EventArgs.Empty);

        _dgvCustomers.ClearSelection();
    }

    private async void BtnSave_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_txtFullName.Text))
        {
            MessageBox.Show("กรุณากรอกชื่อ-นามสกุลผู้เข้าพัก", "เตือน", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            var customer = new Customer
            {
                Id = _selectedCustomerId,
                FullName = _txtFullName.Text.Trim(),
                Phone = _txtPhone.Text.Trim(),
                Email = _txtEmail.Text.Trim(),
                IdCardOrPassport = _txtIdCard.Text.Trim(),
                Address = _txtAddress.Text.Trim(),
                Notes = _txtNotes.Text.Trim()
            };

            await _customerService.SaveCustomerAsync(customer);
            ClearForm();
            await LoadCustomersAsync(_txtSearch.Text);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"บันทึกข้อมูลผู้เข้าพักไม่สำเร็จ: {ex.Message}", "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void BtnDelete_Click(object? sender, EventArgs e)
    {
        if (_selectedCustomerId == 0) return;
        if (MessageBox.Show("ยืนยันการลบข้อมูลผู้เข้าพักรายนี้?", "ยืนยัน", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            await _customerService.DeleteCustomerAsync(_selectedCustomerId);
            ClearForm();
            await LoadCustomersAsync(_txtSearch.Text);
        }
    }
    #endregion
}
