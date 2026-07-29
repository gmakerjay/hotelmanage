using HotelPOS.Common;
using HotelPOS.Common.Models;
using HotelPOS.Core.Services;

namespace HotelPOS.UI;

/// <summary>
/// หน้าจัดการข้อมูลลูกค้า พร้อมระบบค้นหาทันทีที่พิมพ์ (Instant Typing Search) ด้วยเบอร์โทร ชื่อ หรือเลขบัตร
/// </summary>
public class CustomerManagementControl : UserControl
{
    private readonly ICustomerService _customerService;
    private readonly IBookingService? _bookingService;
    private readonly IRoomService? _roomService;
    private readonly ISettingsService? _settingsService;
    private readonly IUtilityBillService? _utilityBillService;
    private readonly IPOSService? _posService;

    private DataGridView _dgvCustomers = null!;
    private TextBox _txtSearch = null!;

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

    private DataGridView _dgvStayHistory = null!;
    private DataGridView _dgvPOSHistory = null!;
    private DataGridView _dgvBillHistory = null!;
    private List<Customer> _customersList = new();
    private List<UtilityBill> _loadedBills = new();
    private GridPaginationPanel _pgPanel = null!;

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
        Font = new Font("Segoe UI", 11F, FontStyle.Regular);
        BackColor = Color.FromArgb(245, 247, 250);

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
        var lblTitle = new Label { Text = "ระบบจัดการข้อมูลผู้เข้าพัก", Font = new Font("Segoe UI", 14F, FontStyle.Bold), AutoSize = true, Margin = new Padding(0, 5, 20, 5) };

        var lblSearch = new Label { Text = "ค้นหาผู้เข้าพัก:", Font = new Font("Segoe UI", 10.5F, FontStyle.Bold), AutoSize = true, Margin = new Padding(5, 10, 5, 5) };
        _txtSearch = new TextBox
        {
            Width = 320,
            Font = new Font("Segoe UI", 11F),
            PlaceholderText = "พิมพ์เบอร์โทร / ชื่อ / เลขบัตร เพื่อค้นหาทันที...",
            Margin = new Padding(5, 6, 5, 5)
        };
        // Instant search on typing
        _txtSearch.TextChanged += async (s, e) => await LoadCustomersAsync(_txtSearch.Text);

        var btnRefresh = new Button
        {
            Text = "รีเฟรช",
            Size = new Size(100, 32),
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

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            FixedPanel = FixedPanel.Panel2
        };
        split.Resize += (s, e) =>
        {
            if (split.Width > 770)
            {
                try
                {
                    split.Panel1MinSize = 300;
                    split.Panel2MinSize = 350;
                    split.SplitterDistance = Math.Max(300, split.Width - 440);
                }
                catch { }
            }
        };

        _dgvCustomers = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AllowUserToAddRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
            RowTemplate = { Height = 35 },
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            GridColor = Color.FromArgb(226, 232, 240)
        };
        _dgvCustomers.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            BackColor = Color.FromArgb(30, 41, 59),
            ForeColor = Color.White,
            WrapMode = DataGridViewTriState.True
        };
        _dgvCustomers.EnableHeadersVisualStyles = false;
        _dgvCustomers.DefaultCellStyle.Font = new Font("Segoe UI", 10.5F);
        _dgvCustomers.SelectionChanged += DgvCustomers_SelectionChanged;
        _dgvCustomers.DataBindingComplete += (s, e) =>
        {
            foreach (DataGridViewColumn col in _dgvCustomers.Columns)
            {
                col.MinimumWidth = 90;
            }
        };

        var panelInput = new Panel { Dock = DockStyle.Fill, Padding = new Padding(15), AutoScroll = true, BackColor = Color.White };

        // Mode Banner for Occupant Form
        _panelModeBanner = new Panel
        {
            Location = new Point(15, 10),
            Size = new Size(385, 42),
            BackColor = Color.FromArgb(240, 253, 244),
            BorderStyle = BorderStyle.FixedSingle
        };

        _lblModeText = new Label
        {
            Text = "โหมด: เพิ่มผู้เข้าพักใหม่",
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            ForeColor = Color.ForestGreen,
            Location = new Point(8, 10),
            AutoSize = true
        };

        _btnCancelEdit = new Button
        {
            Text = "ยกเลิกแก้ไข",
            Location = new Point(265, 6),
            Size = new Size(110, 28),
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            BackColor = Color.White,
            ForeColor = Color.DarkRed,
            FlatStyle = FlatStyle.Flat,
            Visible = false
        };
        _btnCancelEdit.Click += (s, e) => ClearForm();

        _panelModeBanner.Controls.Add(_lblModeText);
        _panelModeBanner.Controls.Add(_btnCancelEdit);

        var lblName = new Label { Text = "ชื่อ-นามสกุล *:", Location = new Point(15, 65), Font = new Font("Segoe UI", 11F, FontStyle.Bold), AutoSize = true };
        _txtFullName = new TextBox { Location = new Point(160, 62), Width = 240, Font = new Font("Segoe UI", 11F) };

        var lblPhone = new Label { Text = "เบอร์โทรศัพท์:", Location = new Point(15, 105), Font = new Font("Segoe UI", 11F), AutoSize = true };
        _txtPhone = new TextBox { Location = new Point(160, 102), Width = 240, Font = new Font("Segoe UI", 11F) };

        var lblEmail = new Label { Text = "อีเมล:", Location = new Point(15, 145), Font = new Font("Segoe UI", 11F), AutoSize = true };
        _txtEmail = new TextBox { Location = new Point(160, 142), Width = 240, Font = new Font("Segoe UI", 11F) };

        var lblIdCard = new Label { Text = "เลขบัตร/พาสปอร์ต:", Location = new Point(15, 185), Font = new Font("Segoe UI", 11F), AutoSize = true };
        _txtIdCard = new TextBox { Location = new Point(160, 182), Width = 240, Font = new Font("Segoe UI", 11F) };

        var lblAddress = new Label { Text = "ที่อยู่:", Location = new Point(15, 225), Font = new Font("Segoe UI", 11F), AutoSize = true };
        _txtAddress = new TextBox { Location = new Point(160, 222), Width = 240, Font = new Font("Segoe UI", 11F), Multiline = true, Height = 55 };

        var lblNotes = new Label { Text = "หมายเหตุ:", Location = new Point(15, 290), Font = new Font("Segoe UI", 11F), AutoSize = true };
        _txtNotes = new TextBox { Location = new Point(160, 287), Width = 240, Font = new Font("Segoe UI", 11F), Multiline = true, Height = 45 };

        _btnSave = new Button { Text = "บันทึกข้อมูล", Location = new Point(160, 345), Size = new Size(110, 38), Font = new Font("Segoe UI", 11F, FontStyle.Bold), BackColor = Color.ForestGreen, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
        _btnSave.FlatAppearance.BorderSize = 0;
        _btnSave.Click += BtnSave_Click;

        _btnClear = new Button { Text = "ล้างฟอร์ม", Location = new Point(280, 345), Size = new Size(120, 38), Font = new Font("Segoe UI", 11F), BackColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
        _btnClear.Click += (s, e) => ClearForm();

        _btnDelete = new Button { Text = "ลบข้อมูลผู้เข้าพัก", Location = new Point(160, 395), Size = new Size(240, 36), Font = new Font("Segoe UI", 10.5F), ForeColor = Color.Red, BackColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
        _btnDelete.Click += BtnDelete_Click;

        panelInput.Controls.AddRange(new Control[]
        {
            _panelModeBanner, lblName, _txtFullName, lblPhone, _txtPhone,
            lblEmail, _txtEmail, lblIdCard, _txtIdCard, lblAddress, _txtAddress,
            lblNotes, _txtNotes, _btnSave, _btnClear, _btnDelete
        });

        var tabControlRight = new TabControl
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10.5F)
        };

        var tabInfo = new TabPage { Text = "📝 ข้อมูลผู้เข้าพัก" };
        panelInput.Dock = DockStyle.Fill;
        tabInfo.Controls.Add(panelInput);

        var tabStayHistory = new TabPage { Text = "🏨 ประวัติเข้าพัก" };
        _dgvStayHistory = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AllowUserToAddRows = false,
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
                Padding = new Padding(6, 8, 6, 8),
                Alignment = DataGridViewContentAlignment.MiddleCenter
            },
            EnableHeadersVisualStyles = false,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize
        };
        tabStayHistory.Controls.Add(_dgvStayHistory);

        var tabPOSHistory = new TabPage { Text = "🛒 ประวัติซื้อสินค้า (POS)" };
        _dgvPOSHistory = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AllowUserToAddRows = false,
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
                Padding = new Padding(6, 8, 6, 8),
                Alignment = DataGridViewContentAlignment.MiddleCenter
            },
            EnableHeadersVisualStyles = false,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize
        };
        tabPOSHistory.Controls.Add(_dgvPOSHistory);

        var tabBillHistory = new TabPage { Text = "⚡ ค่าน้ำ/ค่าไฟ" };
        _dgvBillHistory = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AllowUserToAddRows = false,
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
                Padding = new Padding(6, 8, 6, 8),
                Alignment = DataGridViewContentAlignment.MiddleCenter
            },
            EnableHeadersVisualStyles = false,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize
        };
        tabBillHistory.Controls.Add(_dgvBillHistory);

        tabControlRight.TabPages.Add(tabInfo);
        tabControlRight.TabPages.Add(tabStayHistory);
        tabControlRight.TabPages.Add(tabPOSHistory);
        tabControlRight.TabPages.Add(tabBillHistory);

        // Double-click grid event handlers
        _dgvStayHistory.CellDoubleClick += async (s, ev) =>
        {
            if (ev.RowIndex >= 0 && _dgvStayHistory.Columns.Contains("BookingId"))
            {
                var val = _dgvStayHistory.Rows[ev.RowIndex].Cells["BookingId"].Value;
                if (val != null)
                {
                    int bookingId = Convert.ToInt32(val);
                    await ShowBookingReceiptPreviewAsync(bookingId);
                }
            }
        };

        _dgvPOSHistory.CellDoubleClick += async (s, ev) =>
        {
            if (ev.RowIndex >= 0 && _dgvPOSHistory.Columns.Contains("SaleId"))
            {
                var val = _dgvPOSHistory.Rows[ev.RowIndex].Cells["SaleId"].Value;
                if (val != null)
                {
                    int saleId = Convert.ToInt32(val);
                    await ShowPOSReceiptPreviewAsync(saleId);
                }
            }
        };

        _dgvBillHistory.CellDoubleClick += async (s, ev) =>
        {
            if (ev.RowIndex >= 0 && _dgvBillHistory.Columns.Contains("Id"))
            {
                var val = _dgvBillHistory.Rows[ev.RowIndex].Cells["Id"].Value;
                if (val != null)
                {
                    int billId = Convert.ToInt32(val);
                    await ShowUtilityBillPreviewAsync(billId);
                }
            }
        };

        _pgPanel = new GridPaginationPanel(() => UpdatePagination());
        split.Panel1.Controls.Add(_pgPanel);
        split.Panel1.Controls.Add(_dgvCustomers);
        _dgvCustomers.BringToFront();
        split.Panel2.Controls.Add(tabControlRight);

        Controls.Add(topPanel);
        Controls.Add(split);
        split.BringToFront();
    }

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
        if (_dgvCustomers.SelectedRows.Count == 0) return;
        var row = _dgvCustomers.SelectedRows[0];
        _selectedCustomerId = Convert.ToInt32(row.Cells["Id"].Value);
        var cust = _customersList.FirstOrDefault(c => c.Id == _selectedCustomerId);
        if (cust != null)
        {
            _txtFullName.Text = cust.FullName;
            _txtPhone.Text = cust.Phone;
            _txtEmail.Text = cust.Email;
            _txtIdCard.Text = cust.IdCardOrPassport;
            _txtAddress.Text = cust.Address;
            _txtNotes.Text = cust.Notes;

            // Update Mode Banner to Edit Mode
            _panelModeBanner.BackColor = Color.FromArgb(254, 243, 199); // Soft Amber
            _lblModeText.Text = $"โหมด: แก้ไขผู้เข้าพัก '{cust.FullName}'";
            _lblModeText.ForeColor = Color.DarkGoldenrod;
            _btnCancelEdit.Visible = true;

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

            _loadedBills.Clear();
            if (_utilityBillService != null && _roomService != null)
            {
                var rooms = (await _roomService.GetRoomsAsync()).ToList();
                foreach (var s in stays)
                {
                    var room = rooms.FirstOrDefault(r => r.RoomNumber == s.RoomNumber);
                    if (room != null)
                    {
                        var bills = await _utilityBillService.GetBillHistoryAsync(room.Id, 12);
                        foreach (var bill in bills)
                        {
                            var billDate = DateTime.TryParse(bill.BillingMonth + "-01", out var d) ? d : DateTime.MinValue;
                            if (billDate != DateTime.MinValue)
                            {
                                var stayStart = s.CheckIn.Date;
                                var stayEnd = s.CheckOut?.Date ?? DateTime.Today;
                                var billMonthStart = new DateTime(billDate.Year, billDate.Month, 1);
                                var billMonthEnd = billMonthStart.AddMonths(1).AddDays(-1);
                                if (stayStart <= billMonthEnd && stayEnd >= billMonthStart)
                                {
                                    if (!_loadedBills.Any(b => b.Id == bill.Id))
                                    {
                                        _loadedBills.Add(bill);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            _dgvBillHistory.DataSource = _loadedBills.Select(b => new
            {
                b.Id,
                เลขที่บิล = b.BillCode,
                ห้องพัก = b.RoomNumber ?? "-",
                รอบบิล = b.BillingMonth,
                ยอดรวม = b.TotalAmount.ToString("N2") + " บาท",
                สถานะชำระ = b.IsPaid ? "ชำระแล้ว" : "ยังไม่ชำระ"
            }).ToList();
            if (_dgvBillHistory.Columns.Contains("Id"))
            {
                _dgvBillHistory.Columns["Id"].Visible = false;
            }
        }
        catch
        {
            _dgvStayHistory.DataSource = null;
            _dgvPOSHistory.DataSource = null;
            _dgvBillHistory.DataSource = null;
        }
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

        // Reset Mode Banner
        _panelModeBanner.BackColor = Color.FromArgb(240, 253, 244); // Soft Green
        _lblModeText.Text = "โหมด: เพิ่มผู้เข้าพักใหม่";
        _lblModeText.ForeColor = Color.ForestGreen;
        _btnCancelEdit.Visible = false;

        _dgvStayHistory.DataSource = null;
        _dgvPOSHistory.DataSource = null;
        _dgvBillHistory.DataSource = null;
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
}
