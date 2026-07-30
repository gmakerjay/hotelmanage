using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using HotelPOS.Common;
using HotelPOS.Common.Models;
using HotelPOS.Core.Services;
using HotelPOS.Logging;
using HotelPOS.Printing;

namespace HotelPOS.UI;

/// <summary>
/// ฟอร์มแสดงประวัติการขายหน้าร้าน POS, รายงานยอดขาย, พิมพ์ใบเสร็จย้อนหลัง และจัดการประวัติย้อนหลัง
/// </summary>
public class POSSalesHistoryForm : Form
{
    private readonly IPOSService _posService;
    private readonly ISettingsService _settingsService;
    private readonly IAppLogger _logger;
    private readonly IAuditService? _auditService;

    private DateTimePicker _dtpStart = null!;
    private DateTimePicker _dtpEnd = null!;
    private TextBox _txtSearch = null!;
    private DataGridView _dgvSales = null!;
    private DataGridView _dgvSaleItems = null!;
    private Label _lblSummary = null!;
    private Label _lblSaleDetailHeader = null!;
    private Button _btnReprint = null!;
    private Button _btnVoidSale = null!;

    private List<Sale> _currentSales = new();
    private Sale? _selectedSale;
    private GridPaginationPanel _pgPanel = null!;

    public POSSalesHistoryForm(IPOSService posService, ISettingsService settingsService, IAppLogger logger, IAuditService? auditService = null)
    {
        _posService = posService;
        _settingsService = settingsService;
        _logger = logger;
        _auditService = auditService;

        InitializeUI();
        Load += async (s, e) => await LoadSalesDataAsync();
    }

    private void InitializeUI()
    {
        Text = "ประวัติการขายหน้าร้าน (POS Sales History & Thermal Receipt)";
        Size = new Size(1120, 680);
        MinimumSize = new Size(1000, 600);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.FromArgb(248, 250, 252);
        Font = new Font("Segoe UI", 10F, FontStyle.Regular);
        MaximizeBox = true;
        MinimizeBox = false;

        // Top Filter Panel
        var pnlFilter = new Panel
        {
            Dock = DockStyle.Top,
            Height = 65,
            Padding = new Padding(12, 10, 12, 10),
            BackColor = Color.White
        };
        Controls.Add(pnlFilter);

        var lblStart = new Label { Text = "ตั้งแต่วันที่:", Location = new Point(12, 20), Size = new Size(75, 25), Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
        _dtpStart = new DateTimePicker
        {
            Location = new Point(90, 16),
            Size = new Size(130, 27),
            Format = DateTimePickerFormat.Short,
            Value = DateTime.Today
        };

        var lblEnd = new Label { Text = "ถึงวันที่:", Location = new Point(230, 20), Size = new Size(60, 25), Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
        _dtpEnd = new DateTimePicker
        {
            Location = new Point(295, 16),
            Size = new Size(130, 27),
            Format = DateTimePickerFormat.Short,
            Value = DateTime.Today
        };
        _dtpStart.ValueChanged += async (s, e) => await LoadSalesDataAsync();
        _dtpEnd.ValueChanged += async (s, e) => await LoadSalesDataAsync();

        var btnToday = new Button
        {
            Text = "วันนี้",
            Location = new Point(435, 15),
            Size = new Size(65, 29),
            BackColor = Color.FromArgb(241, 245, 249),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        btnToday.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        btnToday.Click += async (s, e) =>
        {
            _dtpStart.Value = DateTime.Today;
            _dtpEnd.Value = DateTime.Today;
            await LoadSalesDataAsync();
        };

        var btn7Days = new Button
        {
            Text = "7 วันล่าสุด",
            Location = new Point(506, 15),
            Size = new Size(85, 29),
            BackColor = Color.FromArgb(241, 245, 249),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        btn7Days.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        btn7Days.Click += async (s, e) =>
        {
            _dtpStart.Value = DateTime.Today.AddDays(-6);
            _dtpEnd.Value = DateTime.Today;
            await LoadSalesDataAsync();
        };

        var btnSearch = new Button
        {
            Text = "ค้นหา",
            Location = new Point(598, 14),
            Size = new Size(85, 31),
            BackColor = Color.FromArgb(37, 99, 235),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnSearch.FlatAppearance.BorderSize = 0;
        btnSearch.Click += async (s, e) => await LoadSalesDataAsync();

        _txtSearch = new TextBox
        {
            Location = new Point(695, 16),
            Size = new Size(260, 27),
            PlaceholderText = "ค้นหาตามเลขที่บิล..."
        };
        _txtSearch.TextChanged += (s, e) => FilterSalesGrid();

        pnlFilter.Controls.AddRange(new Control[] { lblStart, _dtpStart, lblEnd, _dtpEnd, btnToday, btn7Days, btnSearch, _txtSearch });

        // Main SplitContainer
        var mainSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            SplitterWidth = 8,
            IsSplitterFixed = false
        };
        Controls.Add(mainSplit);

        Shown += (s, e) =>
        {
            try
            {
                if (mainSplit.Width < 800)
                {
                    mainSplit.Orientation = Orientation.Horizontal;
                    mainSplit.SplitterDistance = Math.Max(150, mainSplit.Height - 250);
                }
                else
                {
                    mainSplit.Orientation = Orientation.Vertical;
                    mainSplit.Panel1MinSize = 400;
                    mainSplit.Panel2MinSize = 380;
                    mainSplit.SplitterDistance = (int)(mainSplit.Width * 0.58);
                }
            }
            catch { }
        };

        mainSplit.Resize += (s, e) =>
        {
            try
            {
                if (mainSplit.Width < 800)
                {
                    mainSplit.Orientation = Orientation.Horizontal;
                    mainSplit.SplitterDistance = Math.Max(150, mainSplit.Height - 250);
                }
                else
                {
                    mainSplit.Orientation = Orientation.Vertical;
                    mainSplit.Panel1MinSize = 400;
                    mainSplit.Panel2MinSize = 380;
                    int targetDist = (int)(mainSplit.Width * 0.58);
                    int maxDist = mainSplit.Width - mainSplit.Panel2MinSize;
                    int minDist = mainSplit.Panel1MinSize;
                    mainSplit.SplitterDistance = Math.Clamp(targetDist, minDist, Math.Max(minDist, maxDist));
                }
            }
            catch { }
        };

        // Left Panel: Sales List Grid
        var pnlLeft = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
        mainSplit.Panel1.Controls.Add(pnlLeft);

        _dgvSales = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            RowHeadersVisible = false,
            GridColor = Color.FromArgb(226, 232, 240),
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
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
                BackColor = Color.FromArgb(30, 41, 59),
                ForeColor = Color.White,
                SelectionBackColor = Color.FromArgb(30, 41, 59),
                SelectionForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Padding = new Padding(6, 8, 6, 8),
                Alignment = DataGridViewContentAlignment.MiddleCenter
            },
            EnableHeadersVisualStyles = false,
            RowTemplate = { Height = 36 },
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize
        };
        _dgvSales.EnableDoubleBuffering();
        _dgvSales.Columns.Add("Id", "ID");
        _dgvSales.Columns["Id"]!.Visible = false;

        _dgvSales.Columns.Add("SaleCode", "เลขที่บิล");
        _dgvSales.Columns["SaleCode"]!.MinimumWidth = 110;
        _dgvSales.Columns["SaleCode"]!.FillWeight = 22;

        _dgvSales.Columns.Add("CreatedAt", "วันเวลาที่ขาย");
        _dgvSales.Columns["CreatedAt"]!.MinimumWidth = 150;
        _dgvSales.Columns["CreatedAt"]!.FillWeight = 24;

        _dgvSales.Columns.Add("RoomNumber", "ห้องพัก");
        _dgvSales.Columns["RoomNumber"]!.MinimumWidth = 80;
        _dgvSales.Columns["RoomNumber"]!.FillWeight = 16;

        _dgvSales.Columns.Add("CustomerName", "ชื่อลูกค้า");
        _dgvSales.Columns["CustomerName"]!.MinimumWidth = 120;
        _dgvSales.Columns["CustomerName"]!.FillWeight = 24;

        _dgvSales.Columns.Add("SubTotal", "รวมหลัก");
        _dgvSales.Columns["SubTotal"]!.MinimumWidth = 85;
        _dgvSales.Columns["SubTotal"]!.FillWeight = 18;
        _dgvSales.Columns["SubTotal"]!.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

        _dgvSales.Columns.Add("Discount", "ส่วนลด");
        _dgvSales.Columns["Discount"]!.MinimumWidth = 75;
        _dgvSales.Columns["Discount"]!.FillWeight = 16;
        _dgvSales.Columns["Discount"]!.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

        _dgvSales.Columns.Add("TotalAmount", "ยอดสุทธิ");
        _dgvSales.Columns["TotalAmount"]!.MinimumWidth = 90;
        _dgvSales.Columns["TotalAmount"]!.FillWeight = 20;
        _dgvSales.Columns["TotalAmount"]!.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        _dgvSales.Columns["TotalAmount"]!.DefaultCellStyle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);

        _dgvSales.SelectionChanged += async (s, e) => await DisplaySelectedSaleItemsAsync();
        _pgPanel = new GridPaginationPanel(() => FilterSalesGrid());
        pnlLeft.Controls.Add(_pgPanel);
        pnlLeft.Controls.Add(_dgvSales);
        _dgvSales.BringToFront();

        // Right Panel: Sale Details & Items
        var pnlRight = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12), BackColor = Color.White };
        mainSplit.Panel2.Controls.Add(pnlRight);

        _lblSaleDetailHeader = new Label
        {
            Text = "รายละเอียดรายการสินค้าในบิล",
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Location = new Point(12, 10),
            AutoSize = true
        };
        pnlRight.Controls.Add(_lblSaleDetailHeader);

        _dgvSaleItems = new DataGridView
        {
            Location = new Point(12, 40),
            Size = new Size(pnlRight.Width - 24, Math.Max(150, pnlRight.Height - 115)),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            RowHeadersVisible = false,
            GridColor = Color.FromArgb(226, 232, 240),
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
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
                BackColor = Color.FromArgb(241, 245, 249),
                ForeColor = Color.FromArgb(51, 65, 85),
                SelectionBackColor = Color.FromArgb(241, 245, 249),
                SelectionForeColor = Color.FromArgb(51, 65, 85),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Padding = new Padding(6, 8, 6, 8),
                Alignment = DataGridViewContentAlignment.MiddleCenter
            },
            EnableHeadersVisualStyles = false,
            RowTemplate = { Height = 36 },
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize
        };
        _dgvSaleItems.EnableDoubleBuffering();
        _dgvSaleItems.Columns.Add("ProductName", "ชื่อสินค้า");
        _dgvSaleItems.Columns["ProductName"]!.MinimumWidth = 140;
        _dgvSaleItems.Columns["ProductName"]!.FillWeight = 45;

        _dgvSaleItems.Columns.Add("UnitPrice", "ราคา/หน่วย");
        _dgvSaleItems.Columns["UnitPrice"]!.MinimumWidth = 80;
        _dgvSaleItems.Columns["UnitPrice"]!.FillWeight = 20;
        _dgvSaleItems.Columns["UnitPrice"]!.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

        _dgvSaleItems.Columns.Add("Qty", "จำนวน");
        _dgvSaleItems.Columns["Qty"]!.MinimumWidth = 60;
        _dgvSaleItems.Columns["Qty"]!.FillWeight = 15;
        _dgvSaleItems.Columns["Qty"]!.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

        _dgvSaleItems.Columns.Add("LineTotal", "รวมเงิน");
        _dgvSaleItems.Columns["LineTotal"]!.MinimumWidth = 80;
        _dgvSaleItems.Columns["LineTotal"]!.FillWeight = 20;
        _dgvSaleItems.Columns["LineTotal"]!.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

        pnlRight.Controls.Add(_dgvSaleItems);

        // Action Buttons at Right Bottom
        var pnlActions = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 55,
            Padding = new Padding(0, 8, 12, 8)
        };
        pnlRight.Controls.Add(pnlActions);

        _btnReprint = new Button
        {
            Text = "พิมพ์ใบเสร็จย้อนหลัง",
            BackColor = Color.FromArgb(37, 99, 235),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            Size = new Size(185, 38),
            Location = new Point(12, 8),
            Cursor = Cursors.Hand,
            Enabled = false
        };
        _btnReprint.FlatAppearance.BorderSize = 0;
        _btnReprint.Click += async (s, e) => await ReprintReceiptAsync();

        _btnVoidSale = new Button
        {
            Text = "ยกเลิกบิลขาย",
            BackColor = Color.White,
            ForeColor = Color.Red,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            Size = new Size(130, 38),
            Location = new Point(205, 8),
            Cursor = Cursors.Hand,
            Enabled = false
        };
        _btnVoidSale.FlatAppearance.BorderColor = Color.Red;
        _btnVoidSale.Click += async (s, e) => await VoidSelectedSaleAsync();

        pnlActions.Controls.AddRange(new Control[] { _btnReprint, _btnVoidSale });

        // Bottom Summary Bar
        var pnlSummary = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 45,
            BackColor = Color.FromArgb(30, 41, 59),
            Padding = new Padding(15, 10, 15, 10)
        };
        Controls.Add(pnlSummary);

        _lblSummary = new Label
        {
            Text = "รวมทั้งหมด: 0 รายการ | ยอดขายรวม: 0.00 บาท",
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
            ForeColor = Color.White,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
        pnlSummary.Controls.Add(_lblSummary);
        mainSplit.BringToFront();
    }

    private async Task LoadSalesDataAsync()
    {
        try
        {
            var start = _dtpStart.Value.Date;
            var end = _dtpEnd.Value.Date.AddDays(1).AddTicks(-1);

            var sales = await _posService.GetSalesAsync(start, end);
            _currentSales = sales.Where(s => !s.IsDeleted).OrderByDescending(s => s.CreatedAt).ToList();

            _pgPanel.Reset();
            FilterSalesGrid();
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Pos, "โหลดประวัติการขาย POS ไม่สำเร็จ", ex);
            MessageBox.Show($"โหลดประวัติการขายไม่สำเร็จ: {ex.Message}", "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void FilterSalesGrid()
    {
        string query = _txtSearch.Text.Trim();
        _dgvSales.Rows.Clear();

        var filtered = string.IsNullOrWhiteSpace(query)
            ? _currentSales
            : _currentSales.Where(s => 
                s.SaleCode.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                (s.RoomNumber != null && s.RoomNumber.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                (s.CustomerName != null && s.CustomerName.Contains(query, StringComparison.OrdinalIgnoreCase))
              ).ToList();

        decimal grandTotal = filtered.Sum(s => s.TotalAmount);
        _lblSummary.Text = $"รวมทั้งหมด: {filtered.Count} รายการขาย | ยอดขายรวมสุทธิ: {grandTotal:N2} บาท";

        _pgPanel.UpdateState(filtered.Count);
        var pageData = _pgPanel.GetPageData(filtered).ToList();

        foreach (var s in pageData)
        {
            _dgvSales.Rows.Add(
                s.Id, 
                s.SaleCode, 
                s.CreatedAt.ToString("dd/MM/yyyy HH:mm"), 
                s.RoomNumber ?? "หน้าร้าน Walk-In",
                s.CustomerName ?? "-",
                s.SubTotal.ToString("N2"), 
                s.DiscountAmount.ToString("N2"), 
                s.TotalAmount.ToString("N2")
            );
        }

        if (_dgvSales.Rows.Count > 0)
        {
            _dgvSales.Rows[0].Selected = true;
        }
        else
        {
            _dgvSaleItems.Rows.Clear();
            _lblSaleDetailHeader.Text = "รายละเอียดรายการสินค้าในบิล";
            _btnReprint.Enabled = false;
            _btnVoidSale.Enabled = false;
        }
    }

    private async Task DisplaySelectedSaleItemsAsync()
    {
        if (_dgvSales.SelectedRows.Count == 0)
        {
            _selectedSale = null;
            _dgvSaleItems.Rows.Clear();
            _btnReprint.Enabled = false;
            _btnVoidSale.Enabled = false;
            return;
        }

        var saleId = (int)_dgvSales.SelectedRows[0].Cells["Id"].Value;
        _selectedSale = _currentSales.FirstOrDefault(s => s.Id == saleId);

        if (_selectedSale == null) return;

        _lblSaleDetailHeader.Text = $"รายการสินค้าในบิลเลขที่: {_selectedSale.SaleCode} (ยอดรวม {_selectedSale.TotalAmount:N2} บาท)";
        _btnReprint.Enabled = true;
        _btnVoidSale.Enabled = true;

        try
        {
            var items = await _posService.GetSaleItemsBySaleIdAsync(saleId);
            _dgvSaleItems.Rows.Clear();
            foreach (var item in items)
            {
                _dgvSaleItems.Rows.Add(item.ProductNameSnapshot, item.UnitPrice.ToString("N2"), item.Quantity, item.LineTotal.ToString("N2"));
            }
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Pos, $"โหลดรายการสินค้าของบิล ID={saleId} ไม่สำเร็จ", ex);
        }
    }

    private async Task ReprintReceiptAsync()
    {
        if (_selectedSale == null) return;

        try
        {
            var settings = await _settingsService.GetAllSettingsAsync();
            var printerName = settings.PrinterName;
            if (string.IsNullOrEmpty(printerName))
            {
                MessageBox.Show("ไม่ได้ตั้งค่าเครื่องพิมพ์เริ่มต้นไว้ กรุณาไปตั้งค่าที่เมนูตั้งค่าระบบ", "ไม่พบเครื่องพิมพ์", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var sale = await _posService.GetSaleByIdAsync(_selectedSale.Id);
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

            var printer = new ReceiptInvoicePrinter(dummyBooking, room, customer, dummyFolio, settings);
            printer.ShowPrintPreview();

            _logger.Info(LogCategory.Pos, $"พิมพ์ใบเสร็จย้อนหลัง บิลเลขที่ '{sale.SaleCode}' สำเร็จ");
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Pos, "พิมพ์ใบเสร็จ POS ย้อนหลังไม่สำเร็จ", ex);
            MessageBox.Show($"พิมพ์ใบเสร็จย้อนหลังไม่สำเร็จ: {ex.Message}", "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task VoidSelectedSaleAsync()
    {
        if (_selectedSale == null) return;

        using var authForm = new AdminAuthForm(_settingsService);
        if (authForm.ShowDialog() != DialogResult.OK) return;

        if (MessageBox.Show($"คุณต้องการยกเลิกบิลขายเลขที่ '{_selectedSale.SaleCode}' ใช่หรือไม่?\n(ระบบจะคืนจำนวนสต็อกสินค้าและหักยอดออกจาก Folio ห้องพัก หากมีการชาร์จไว้)",
            "ยืนยันการยกเลิกบิล", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
        {
            try
            {
                await _posService.VoidSaleAsync(_selectedSale.Id);

                _logger.Info(LogCategory.Pos, $"ยกเลิกบิลขายเลขที่ '{_selectedSale.SaleCode}' เรียบร้อยแล้ว");
                if (_auditService != null)
                {
                    await _auditService.LogAsync("VOID_POS_SALE", "sales", _selectedSale.Id.ToString(), $"ยกเลิกบิลขาย POS เลขที่ {_selectedSale.SaleCode} ยอดสุทธิ {_selectedSale.TotalAmount:N2} บาท");
                }
                MessageBox.Show("ยกเลิกรายการขายเรียบร้อยแล้ว", "สำเร็จ", MessageBoxButtons.OK, MessageBoxIcon.Information);
                await LoadSalesDataAsync();
            }
            catch (Exception ex)
            {
                _logger.Error(LogCategory.Pos, "ยกเลิกรายการขายไม่สำเร็จ", ex);
                Program.ShowDetailedErrorPopup(ex, "ยกเลิกรายการขายล้มเหลวเนื่องจากข้อผิดพลาดในการเชื่อมต่อฐานข้อมูล");
            }
        }
    }
}
