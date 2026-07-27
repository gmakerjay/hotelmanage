using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Dapper;
using HotelPOS.Common;
using HotelPOS.Common.Models;
using HotelPOS.Core.Services;
using HotelPOS.Data;
using HotelPOS.Logging;
using HotelPOS.Printing;

namespace HotelPOS.UI;

public class POSControl : UserControl
{
    private readonly IPOSService _posService;
    private readonly ISettingsService _settingsService;
    private readonly IAppLogger _logger;

    // Data lists
    private List<ProductCategory> _categories = new();
    private List<Product> _products = new();
    private List<dynamic> _activeFolios = new();
    
    // Cart state
    private readonly Dictionary<int, (Product Product, int Quantity)> _cart = new();

    // Left Panel UI
    private FlowLayoutPanel _flpCategories = null!;
    private TextBox _txtSearch = null!;
    private FlowLayoutPanel _flpProducts = null!;
    private int? _selectedCategoryId = null;

    // Right Panel (Cart) UI
    private DataGridView _dgvCart = null!;
    private TextBox _txtDiscount = null!;
    private Label _lblSubTotal = null!;
    private Label _lblTotal = null!;
    private ComboBox _cboFolio = null!;
    private CheckBox _chkChargeRoom = null!;
    private Button _btnCheckout = null!;
    private Button _btnClearCart = null!;
    private Button _btnManageInventory = null!;

    public POSControl(IPOSService posService, ISettingsService settingsService, IAppLogger logger)
    {
        _posService = posService;
        _settingsService = settingsService;
        _logger = logger;

        InitializeUI();
        Load += async (s, e) => await LoadInitialDataAsync();
    }

    private void InitializeUI()
    {
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(241, 245, 249);
        Font = new Font("Segoe UI", 10.5F, FontStyle.Regular);

        // Split Container: Left (Products List) vs Right (Cart & Options)
        var mainSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            SplitterDistance = 750,
            SplitterWidth = 8,
            IsSplitterFixed = false
        };
        Controls.Add(mainSplit);

        // --- LEFT PANEL ---
        var pnlLeft = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };
        mainSplit.Panel1.Controls.Add(pnlLeft);

        var lblLeftTitle = new Label
        {
            Text = "รายการขายสินค้าและบริการเสริม (POS Shop Front)",
            Font = new Font("Segoe UI", 14F, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Location = new Point(12, 10),
            AutoSize = true
        };
        pnlLeft.Controls.Add(lblLeftTitle);

        // Search Bar and Manage inventory button
        var pnlSearch = new Panel
        {
            Location = new Point(12, 45),
            Size = new Size(720, 40)
        };
        pnlLeft.Controls.Add(pnlSearch);

        _txtSearch = new TextBox
        {
            Location = new Point(0, 5),
            Size = new Size(350, 28),
            PlaceholderText = "ค้นหาชื่อสินค้า หรือ SKU..."
        };
        _txtSearch.TextChanged += async (s, e) => await FilterProductsAsync();
        pnlSearch.Controls.Add(_txtSearch);

        _btnManageInventory = new Button
        {
            Text = "⚙️ จัดการสินค้า / สต็อกสินค้า",
            BackColor = Color.White,
            ForeColor = Color.FromArgb(30, 41, 59),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            Location = new Point(365, 3),
            Size = new Size(180, 32),
            Cursor = Cursors.Hand
        };
        _btnManageInventory.Click += BtnManageInventory_Click;
        pnlSearch.Controls.Add(_btnManageInventory);

        // Category Flow Panel
        _flpCategories = new FlowLayoutPanel
        {
            Location = new Point(12, 90),
            Size = new Size(720, 45),
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoScroll = true
        };
        pnlLeft.Controls.Add(_flpCategories);

        // Products Grid
        _flpProducts = new FlowLayoutPanel
        {
            Location = new Point(12, 140),
            Size = new Size(720, 600),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            AutoScroll = true,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0, 0, 15, 10)
        };
        pnlLeft.Controls.Add(_flpProducts);


        // --- RIGHT PANEL (CART) ---
        var pnlRight = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            BackColor = Color.White
        };
        mainSplit.Panel2.Controls.Add(pnlRight);

        var lblRightTitle = new Label
        {
            Text = "ตะกร้าสินค้า (Cart)",
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Location = new Point(12, 10),
            AutoSize = true
        };
        pnlRight.Controls.Add(lblRightTitle);

        // Cart DataGridView
        _dgvCart = new DataGridView
        {
            Location = new Point(12, 45),
            Size = new Size(460, 350),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            RowHeadersVisible = false,
            GridColor = Color.FromArgb(241, 245, 249),
            ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(241, 245, 249),
                ForeColor = Color.FromArgb(51, 65, 85),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
            },
            EnableHeadersVisualStyles = false
        };
        _dgvCart.Columns.Add("Id", "ID");
        _dgvCart.Columns["Id"]!.Visible = false;
        
        _dgvCart.Columns.Add("Name", "สินค้า");
        _dgvCart.Columns["Name"]!.Width = 180;
        
        _dgvCart.Columns.Add("Price", "ราคา");
        _dgvCart.Columns["Price"]!.Width = 80;
        _dgvCart.Columns["Price"]!.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        
        _dgvCart.Columns.Add("Qty", "จำนวน");
        _dgvCart.Columns["Qty"]!.Width = 70;
        _dgvCart.Columns["Qty"]!.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        
        _dgvCart.Columns.Add("Total", "รวม");
        _dgvCart.Columns["Total"]!.Width = 90;
        _dgvCart.Columns["Total"]!.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

        _dgvCart.CellDoubleClick += DgvCart_CellDoubleClick;
        pnlRight.Controls.Add(_dgvCart);

        int bottomY = 410;

        // Discount box
        var lblDiscount = new Label
        {
            Text = "ส่วนลด (บาท):",
            Location = new Point(12, bottomY + 3),
            Size = new Size(100, 25),
            ForeColor = Color.FromArgb(71, 85, 105)
        };
        _txtDiscount = new TextBox
        {
            Location = new Point(120, bottomY),
            Size = new Size(120, 28),
            Text = "0",
            TextAlign = HorizontalAlignment.Right
        };
        _txtDiscount.TextChanged += (s, e) => CalculateSummary();
        pnlRight.Controls.Add(lblDiscount);
        pnlRight.Controls.Add(_txtDiscount);
        
        // SubTotal Label
        _lblSubTotal = new Label
        {
            Text = "รวมเงินหลัก: 0.00 บาท",
            Location = new Point(255, bottomY + 3),
            Size = new Size(200, 25),
            TextAlign = ContentAlignment.MiddleRight,
            Font = new Font("Segoe UI", 10F, FontStyle.Regular),
            ForeColor = Color.FromArgb(100, 116, 139)
        };
        pnlRight.Controls.Add(_lblSubTotal);
        bottomY += 40;

        // Folio Routing Option
        _chkChargeRoom = new CheckBox
        {
            Text = "ชาร์จค่าใช้จ่ายเข้าบัญชีห้องพัก (Folio)",
            Location = new Point(12, bottomY),
            Size = new Size(280, 25),
            ForeColor = Color.FromArgb(30, 41, 59)
        };
        _chkChargeRoom.CheckedChanged += (s, e) => ToggleFolioSelection();
        pnlRight.Controls.Add(_chkChargeRoom);
        bottomY += 30;

        _cboFolio = new ComboBox
        {
            Location = new Point(12, bottomY),
            Size = new Size(320, 28),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Enabled = false
        };
        pnlRight.Controls.Add(_cboFolio);
        bottomY += 40;

        // Total amount highlight
        _lblTotal = new Label
        {
            Text = "ยอดสุทธิ: 0.00 บาท",
            Font = new Font("Segoe UI", 16F, FontStyle.Bold),
            ForeColor = Color.FromArgb(37, 99, 235),
            Location = new Point(12, bottomY),
            Size = new Size(460, 40),
            TextAlign = ContentAlignment.MiddleLeft
        };
        pnlRight.Controls.Add(_lblTotal);
        bottomY += 45;

        // Primary Buttons
        _btnCheckout = new Button
        {
            Text = "ชำระเงิน / ปิดบิลขาย",
            BackColor = Color.FromArgb(22, 163, 74),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            Location = new Point(12, bottomY),
            Size = new Size(220, 45),
            Cursor = Cursors.Hand
        };
        _btnCheckout.FlatAppearance.BorderSize = 0;
        _btnCheckout.Click += BtnCheckout_Click;
        pnlRight.Controls.Add(_btnCheckout);

        _btnClearCart = new Button
        {
            Text = "ล้างตะกร้า",
            BackColor = Color.White,
            ForeColor = Color.FromArgb(220, 38, 38),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
            Location = new Point(245, bottomY),
            Size = new Size(110, 45),
            Cursor = Cursors.Hand
        };
        _btnClearCart.Click += (s, e) => ClearCart();
        pnlRight.Controls.Add(_btnClearCart);
    }

    private async Task LoadInitialDataAsync()
    {
        try
        {
            _categories = (await _posService.GetCategoriesAsync()).ToList();
            _products = (await _posService.GetProductsAsync()).ToList();
            
            // Render Categories Toolbar
            RenderCategoryFilters();
            await FilterProductsAsync();
            
            // Active folios listing
            _activeFolios = (await _posService.GetActiveFoliosAsync()).ToList();
            _cboFolio.Items.Clear();
            foreach (var f in _activeFolios)
            {
                _cboFolio.Items.Add($"ห้อง {f.RoomNumber} - คุณ {f.GuestName}");
            }
            if (_cboFolio.Items.Count > 0) _cboFolio.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.UI, "โหลดข้อมูลเริ่มต้น POS ล้มเหลว", ex);
        }
    }

    private void RenderCategoryFilters()
    {
        _flpCategories.Controls.Clear();

        var btnAll = new Button
        {
            Text = "ทั้งหมด (All)",
            Height = 32,
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            BackColor = _selectedCategoryId == null ? Color.FromArgb(37, 99, 235) : Color.White,
            ForeColor = _selectedCategoryId == null ? Color.White : Color.FromArgb(71, 85, 105),
            Cursor = Cursors.Hand
        };
        btnAll.FlatAppearance.BorderSize = _selectedCategoryId == null ? 0 : 1;
        btnAll.Click += async (s, e) =>
        {
            _selectedCategoryId = null;
            RenderCategoryFilters();
            await FilterProductsAsync();
        };
        _flpCategories.Controls.Add(btnAll);

        foreach (var cat in _categories)
        {
            var isSelected = _selectedCategoryId == cat.Id;
            var btn = new Button
            {
                Text = cat.Name,
                Height = 32,
                AutoSize = true,
                FlatStyle = FlatStyle.Flat,
                BackColor = isSelected ? Color.FromArgb(37, 99, 235) : Color.White,
                ForeColor = isSelected ? Color.White : Color.FromArgb(71, 85, 105),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = isSelected ? 0 : 1;
            btn.Click += async (s, e) =>
            {
                _selectedCategoryId = cat.Id;
                RenderCategoryFilters();
                await FilterProductsAsync();
            };
            _flpCategories.Controls.Add(btn);
        }
    }

    private async Task FilterProductsAsync()
    {
        _flpProducts.Controls.Clear();
        var query = _txtSearch.Text.Trim();
        var filtered = _products.Where(p => 
            (_selectedCategoryId == null || p.CategoryId == _selectedCategoryId) &&
            (string.IsNullOrEmpty(query) || p.Name.Contains(query, StringComparison.OrdinalIgnoreCase) || (p.Sku != null && p.Sku.Contains(query, StringComparison.OrdinalIgnoreCase)))
        ).ToList();

        foreach (var p in filtered)
        {
            var pnlCard = new Panel
            {
                Size = new Size(160, 130),
                BackColor = Color.White,
                Padding = new Padding(8),
                Margin = new Padding(0, 0, 12, 12)
            };

            // Custom border rendering
            pnlCard.Paint += (s, e) =>
            {
                ControlPaint.DrawBorder(e.Graphics, pnlCard.ClientRectangle, Color.FromArgb(226, 232, 240), ButtonBorderStyle.Solid);
            };

            var lblName = new Label
            {
                Text = p.Name,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(15, 23, 42),
                Location = new Point(8, 8),
                Size = new Size(144, 40),
                TextAlign = ContentAlignment.TopLeft
            };
            pnlCard.Controls.Add(lblName);

            // Stock display
            var stockText = !p.TrackStock ? "ไม่จำกัด" : $"คงเหลือ: {p.StockQty}";
            var stockColor = !p.TrackStock ? Color.Gray : (p.StockQty > 0 ? Color.FromArgb(22, 163, 74) : Color.Red);
            var lblStock = new Label
            {
                Text = stockText,
                Font = new Font("Segoe UI", 8F, FontStyle.Regular),
                ForeColor = stockColor,
                Location = new Point(8, 50),
                Size = new Size(144, 18)
            };
            pnlCard.Controls.Add(lblStock);

            // Price tag
            var lblPrice = new Label
            {
                Text = $"{p.Price:N2} ฿",
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(220, 38, 38),
                Location = new Point(8, 70),
                Size = new Size(144, 20)
            };
            pnlCard.Controls.Add(lblPrice);

            // Add button
            var btnAdd = new Button
            {
                Text = p.StockQty <= 0 && p.TrackStock ? "หมด" : "+ เพิ่ม",
                BackColor = p.StockQty <= 0 && p.TrackStock ? Color.FromArgb(226, 232, 240) : Color.FromArgb(37, 99, 235),
                ForeColor = p.StockQty <= 0 && p.TrackStock ? Color.Gray : Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                Location = new Point(8, 92),
                Size = new Size(144, 28),
                Enabled = !(p.StockQty <= 0 && p.TrackStock),
                Cursor = Cursors.Hand
            };
            btnAdd.FlatAppearance.BorderSize = 0;
            btnAdd.Click += (s, e) => AddToCart(p);
            pnlCard.Controls.Add(btnAdd);

            _flpProducts.Controls.Add(pnlCard);
        }
    }

    private void AddToCart(Product product)
    {
        if (_cart.ContainsKey(product.Id))
        {
            var item = _cart[product.Id];
            if (product.TrackStock && product.StockQty <= item.Quantity)
            {
                MessageBox.Show($"สินค้า '{product.Name}' มีสต็อกไม่เพียงพอ", "คำเตือนสต็อก", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            _cart[product.Id] = (product, item.Quantity + 1);
        }
        else
        {
            _cart[product.Id] = (product, 1);
        }

        RefreshCartGrid();
    }

    private void RefreshCartGrid()
    {
        _dgvCart.Rows.Clear();
        foreach (var id in _cart.Keys)
        {
            var val = _cart[id];
            var lineTotal = val.Product.Price * val.Quantity;
            _dgvCart.Rows.Add(id, val.Product.Name, val.Product.Price.ToString("N2"), val.Quantity, lineTotal.ToString("N2"));
        }
        CalculateSummary();
    }

    private void CalculateSummary()
    {
        decimal subTotal = 0;
        foreach (var item in _cart.Values)
        {
            subTotal += item.Product.Price * item.Quantity;
        }

        decimal.TryParse(_txtDiscount.Text, out var discount);
        var total = subTotal - discount;

        _lblSubTotal.Text = $"รวมเงินหลัก: {subTotal:N2} บาท";
        _lblTotal.Text = $"ยอดสุทธิ: {total:N2} บาท";
    }

    private void DgvCart_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0) return;
        var idVal = _dgvCart.Rows[e.RowIndex].Cells["Id"].Value;
        if (idVal != null && int.TryParse(idVal.ToString(), out var prodId))
        {
            // Prompt to adjust quantity or remove
            using var dlg = new Form
            {
                Text = "จัดการรายการในตะกร้า",
                Size = new Size(300, 160),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false
            };

            var lbl = new Label { Text = "แก้ไขจำนวนสินค้า:", Location = new Point(20, 20), Size = new Size(120, 20) };
            var num = new NumericUpDown { Location = new Point(140, 18), Size = new Size(100, 25), Minimum = 1, Maximum = 999 };
            
            var currentQty = _cart[prodId].Quantity;
            num.Value = currentQty;

            var btnSave = new Button { Text = "บันทึก", Location = new Point(20, 70), Size = new Size(110, 32), DialogResult = DialogResult.OK };
            var btnDelete = new Button { Text = "ลบรายการ", Location = new Point(145, 70), Size = new Size(110, 32), BackColor = Color.Red, ForeColor = Color.White };
            btnDelete.Click += (s, ev) =>
            {
                _cart.Remove(prodId);
                dlg.DialogResult = DialogResult.Yes;
                dlg.Close();
            };

            dlg.Controls.AddRange(new Control[] { lbl, num, btnSave, btnDelete });
            var res = dlg.ShowDialog();
            if (res == DialogResult.OK)
            {
                var prod = _cart[prodId].Product;
                if (prod.TrackStock && prod.StockQty < (int)num.Value)
                {
                    MessageBox.Show($"สินค้ามีสต็อกไม่เพียงพอ (สต็อก: {prod.StockQty})", "คำเตือนสต็อก", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                else
                {
                    _cart[prodId] = (prod, (int)num.Value);
                }
            }

            RefreshCartGrid();
        }
    }

    private void ToggleFolioSelection()
    {
        _cboFolio.Enabled = _chkChargeRoom.Checked;
        if (_chkChargeRoom.Checked)
        {
            _btnCheckout.Text = "ชาร์จเข้าบัญชีห้องพัก (Folio)";
            _btnCheckout.BackColor = Color.FromArgb(15, 23, 42);
        }
        else
        {
            _btnCheckout.Text = "ชำระเงิน / ปิดบิลขาย";
            _btnCheckout.BackColor = Color.FromArgb(22, 163, 74);
        }
    }

    private void ClearCart()
    {
        _cart.Clear();
        _txtDiscount.Text = "0";
        RefreshCartGrid();
    }

    private async void BtnCheckout_Click(object? sender, EventArgs e)
    {
        if (_cart.Count == 0)
        {
            MessageBox.Show("ไม่มีสินค้าในตะกร้า", "ตะกร้าว่างเปล่า", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            decimal.TryParse(_txtDiscount.Text, out var discount);
            decimal subTotal = _cart.Values.Sum(v => v.Product.Price * v.Quantity);
            decimal total = subTotal - discount;

            Sale sale = new Sale
            {
                DiscountAmount = discount,
                TotalAmount = total,
                SubTotal = subTotal
            };

            List<SaleItem> items = _cart.Values.Select(v => new SaleItem
            {
                ProductId = v.Product.Id,
                Quantity = v.Quantity,
                UnitPrice = v.Product.Price,
                LineTotal = v.Product.Price * v.Quantity,
                ProductNameSnapshot = v.Product.Name
            }).ToList();

            Payment? payment = null;

            if (_chkChargeRoom.Checked)
            {
                if (_cboFolio.SelectedIndex < 0)
                {
                    MessageBox.Show("กรุณาเลือกห้องพักที่จะทำการชาร์จค่าใช้จ่าย", "ไม่พบห้องพัก", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var selectedFolio = _activeFolios[_cboFolio.SelectedIndex];
                sale.FolioId = (int)selectedFolio.FolioId;

                var confirmMsg = $"ต้องการชาร์จค่าสินค้ายอดสุทธิ {total:N2} บาท เข้าห้องพักหมายเลข {selectedFolio.RoomNumber} หรือไม่?";
                if (MessageBox.Show(confirmMsg, "ยืนยันการชาร์จห้องพัก", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                {
                    return;
                }

                // Complete Sale
                await _posService.SubmitSaleAsync(sale, items, null);
                MessageBox.Show("บันทึกการชาร์จยอดเข้าห้องพักเรียบร้อยแล้ว", "สำเร็จ", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ClearCart();
                await LoadInitialDataAsync();
            }
            else
            {
                using var payDlg = new POSPaymentForm(total);
                if (payDlg.ShowDialog() == DialogResult.OK)
                {
                    payment = new Payment
                    {
                        Method = payDlg.SelectedMethod,
                        Amount = total,
                        ReferenceNo = payDlg.ReferenceNo
                    };

                    int saleId = await _posService.SubmitSaleAsync(sale, items, payment);
                    
                    MessageBox.Show("บันทึกการชำระเงินสำเร็จ!", "สำเร็จ", MessageBoxButtons.OK, MessageBoxIcon.Information);

                    if (payDlg.PrintReceipt)
                    {
                        // Print dynamic thermal receipt
                        await PrintThermalReceiptAsync(saleId);
                    }

                    ClearCart();
                    await LoadInitialDataAsync();
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"ชำระเงินล้มเหลว: {ex.Message}", "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task PrintThermalReceiptAsync(int saleId)
    {
        try
        {
            var printerName = await _settingsService.GetAsync("default_printer_name");
            if (string.IsNullOrEmpty(printerName))
            {
                MessageBox.Show("ไม่ได้ตั้งค่าเครื่องพิมพ์เริ่มต้นไว้ กรุณาไปตั้งค่าที่เมนูตั้งค่าระบบ", "ไม่พบเครื่องพิมพ์", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var paperSizeStr = await _settingsService.GetAsync("default_paper_size");
            int.TryParse(paperSizeStr, out var paperSizeVal);
            var paperSize = (PaperSize)paperSizeVal;

            if (paperSize == PaperSize.A4)
            {
                MessageBox.Show("การพิมพ์ใบเสร็จ POS หน้าร้านรองรับกระดาษม้วนแบบ Slip 80mm หรือ 58mm เท่านั้น", "ขนาดกระดาษไม่ตรง", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Fetch the sale info
            var sale = await _posService.GetSaleByIdAsync(saleId);
            var items = await _posService.GetSaleItemsBySaleIdAsync(saleId);
            var payments = await _posService.GetPaymentsBySaleIdAsync(saleId);

            if (sale == null) return;

            var connectionFactory = new DbConnectionFactory();
            using var connection = connectionFactory.CreateConnection();
            
            Room room = new Room { RoomNumber = "หน้าร้าน (Retail)" };
            Customer customer = new Customer { FullName = "ลูกค้าทั่วไป" };
            
            if (sale.FolioId.HasValue)
            {
                var folioData = await connection.QuerySingleOrDefaultAsync<dynamic>(
                    @"SELECT r.id AS RoomId, r.room_number, r.room_type_id, r.floor, r.status AS RoomStatus, r.notes AS RoomNotes,
                             c.id AS CustomerId, c.full_name, c.phone, c.email
                      FROM folios f
                      JOIN bookings b ON f.booking_id = b.id
                      JOIN rooms r ON b.room_id = r.id
                      JOIN customers c ON b.customer_id = c.id
                      WHERE f.id = @FolioId", new { FolioId = sale.FolioId.Value });
                if (folioData != null)
                {
                    room = new Room 
                    { 
                        Id = (int)folioData.RoomId,
                        RoomNumber = folioData.room_number,
                        RoomTypeId = (int)folioData.room_type_id,
                        Floor = folioData.floor,
                        Status = (RoomStatus)folioData.RoomStatus,
                        Notes = folioData.RoomNotes
                    };
                    customer = new Customer 
                    { 
                        Id = (int)folioData.CustomerId,
                        FullName = folioData.full_name,
                        Phone = folioData.phone,
                        Email = folioData.email
                    };
                }
            }
            else if (sale.CustomerId.HasValue)
            {
                var custData = await connection.QuerySingleOrDefaultAsync<Customer>(
                    "SELECT * FROM customers WHERE id = @Id", new { Id = sale.CustomerId.Value });
                if (custData != null) customer = custData;
            }

            var settings = await _settingsService.GetAllSettingsAsync();

            var booking = new Booking
            {
                BookingCode = sale.SaleCode,
                CheckInPlanned = sale.CreatedAt,
                CheckOutActual = sale.CreatedAt,
                AgreedRate = 0,
                RoomId = room.Id,
                CustomerId = customer.Id
            };

            var folio = new Folio
            {
                RoomCharges = 0,
                ExtraCharges = sale.TotalAmount,
                DiscountAmount = sale.DiscountAmount,
                TotalAmount = sale.TotalAmount
            };

            // Generate receipt document & rasterize Thai Unicode to bitmap format
            var printerEngine = new ReceiptInvoicePrinter(
                settings?.ShopName ?? "โรงแรม HotelPOS TH",
                settings?.ShopAddress ?? "123/45 ถนนสุขุมวิท กรุงเทพฯ",
                settings?.ShopPhone ?? "02-123-4567",
                settings?.ShopTaxId ?? "0105560000000",
                booking,
                room,
                customer,
                folio,
                "admin",
                settings,
                null
            );
            
            // Custom printer task trigger
            printerEngine.Print(printerName);
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Printing, "พิมพ์ใบเสร็จขาย POS ล้มเหลว", ex);
            MessageBox.Show($"พิมพ์ใบเสร็จล้มเหลว: {ex.Message}", "ข้อผิดพลาดเครื่องพิมพ์", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // --- INLINE INVENTORY MANAGEMENT ---
    private void BtnManageInventory_Click(object? sender, EventArgs e)
    {
        using var dlg = new Form
        {
            Text = "จัดการสต็อกและสินค้า (POS Inventory & Products)",
            Size = new Size(880, 520),
            StartPosition = FormStartPosition.CenterParent,
            BackColor = Color.FromArgb(248, 250, 252),
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular)
        };

        var mainTab = new TabControl { Dock = DockStyle.Fill };
        dlg.Controls.Add(mainTab);

        // Tab 1: Product List & CRUD
        var tabProducts = new TabPage("รายการสินค้า (Products)");
        mainTab.TabPages.Add(tabProducts);

        var splitProd = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 480 };
        tabProducts.Controls.Add(splitProd);

        var dgvProds = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            BackgroundColor = Color.White,
            RowHeadersVisible = false
        };
        dgvProds.Columns.Add("Id", "ID");
        dgvProds.Columns.Add("Name", "ชื่อสินค้า");
        dgvProds.Columns.Add("Category", "ประเภท");
        dgvProds.Columns.Add("Price", "ราคาขาย");
        dgvProds.Columns.Add("Stock", "สต็อก");
        splitProd.Panel1.Controls.Add(dgvProds);

        // Inputs for product
        var pnlProdInputs = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };
        splitProd.Panel2.Controls.Add(pnlProdInputs);

        int iy = 15;
        var lblPName = new Label { Text = "ชื่อสินค้า:", Location = new Point(12, iy), Size = new Size(80, 20) };
        var txtPName = new TextBox { Location = new Point(100, iy - 3), Size = new Size(200, 25) };
        pnlProdInputs.Controls.AddRange(new Control[] { lblPName, txtPName });
        iy += 35;

        var lblPCat = new Label { Text = "ประเภท:", Location = new Point(12, iy), Size = new Size(80, 20) };
        var cboPCat = new ComboBox { Location = new Point(100, iy - 3), Size = new Size(200, 25), DropDownStyle = ComboBoxStyle.DropDownList };
        pnlProdInputs.Controls.AddRange(new Control[] { lblPCat, cboPCat });
        iy += 35;

        var lblPPrice = new Label { Text = "ราคาขาย:", Location = new Point(12, iy), Size = new Size(80, 20) };
        var numPPrice = new NumericUpDown { Location = new Point(100, iy - 3), Size = new Size(100, 25), Maximum = 100000 };
        pnlProdInputs.Controls.AddRange(new Control[] { lblPPrice, numPPrice });
        iy += 35;

        var chkTrackStock = new CheckBox { Text = "ควบคุมสต็อกสินค้า", Location = new Point(100, iy), Size = new Size(200, 20) };
        pnlProdInputs.Controls.Add(chkTrackStock);
        iy += 30;

        var lblPStock = new Label { Text = "สต็อกคงเหลือ:", Location = new Point(12, iy), Size = new Size(80, 20) };
        var numPStock = new NumericUpDown { Location = new Point(100, iy - 3), Size = new Size(100, 25), Maximum = 99999 };
        pnlProdInputs.Controls.AddRange(new Control[] { lblPStock, numPStock });
        iy += 45;

        var btnSaveProd = new Button { Text = "บันทึกสินค้า", Location = new Point(12, iy), Size = new Size(130, 35), BackColor = Color.FromArgb(37, 99, 235), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        var btnDelProd = new Button { Text = "ลบสินค้า", Location = new Point(155, iy), Size = new Size(130, 35), BackColor = Color.White, ForeColor = Color.Red, FlatStyle = FlatStyle.Flat };
        pnlProdInputs.Controls.AddRange(new Control[] { btnSaveProd, btnDelProd });

        // Tab 2: Categories CRUD
        var tabCats = new TabPage("ประเภทสินค้า (Categories)");
        mainTab.TabPages.Add(tabCats);

        var splitCat = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 480 };
        tabCats.Controls.Add(splitCat);

        var dgvCats = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            BackgroundColor = Color.White,
            RowHeadersVisible = false
        };
        dgvCats.Columns.Add("Id", "ID");
        dgvCats.Columns.Add("Name", "ชื่อประเภท");
        splitCat.Panel1.Controls.Add(dgvCats);

        var pnlCatInputs = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };
        splitCat.Panel2.Controls.Add(pnlCatInputs);

        int cy = 15;
        var lblCName = new Label { Text = "ชื่อประเภท:", Location = new Point(12, cy), Size = new Size(80, 20) };
        var txtCName = new TextBox { Location = new Point(100, cy - 3), Size = new Size(200, 25) };
        pnlCatInputs.Controls.AddRange(new Control[] { lblCName, txtCName });
        cy += 45;

        var btnSaveCat = new Button { Text = "บันทึกประเภท", Location = new Point(12, cy), Size = new Size(130, 35), BackColor = Color.FromArgb(37, 99, 235), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        var btnDelCat = new Button { Text = "ลบประเภท", Location = new Point(155, cy), Size = new Size(130, 35), BackColor = Color.White, ForeColor = Color.Red, FlatStyle = FlatStyle.Flat };
        pnlCatInputs.Controls.AddRange(new Control[] { btnSaveCat, btnDelCat });

        // Data binding helpers
        Action reloadGrids = () =>
        {
            dgvProds.Rows.Clear();
            foreach (var p in _products)
            {
                var catName = _categories.FirstOrDefault(c => c.Id == p.CategoryId)?.Name ?? "ไม่มี";
                dgvProds.Rows.Add(p.Id, p.Name, catName, p.Price.ToString("N2"), p.TrackStock ? p.StockQty.ToString() : "ไม่จำกัด");
            }

            dgvCats.Rows.Clear();
            foreach (var c in _categories)
            {
                dgvCats.Rows.Add(c.Id, c.Name);
            }

            cboPCat.Items.Clear();
            foreach (var c in _categories)
            {
                cboPCat.Items.Add(c.Name);
            }
            if (cboPCat.Items.Count > 0) cboPCat.SelectedIndex = 0;
        };

        // Load data initial for dialog
        reloadGrids();

        // Product Form select
        Product? selectedProduct = null;
        dgvProds.SelectionChanged += (s, ev) =>
        {
            if (dgvProds.SelectedRows.Count > 0)
            {
                var r = dgvProds.SelectedRows[0];
                var id = (int)r.Cells[0].Value;
                selectedProduct = _products.FirstOrDefault(p => p.Id == id);
                if (selectedProduct != null)
                {
                    txtPName.Text = selectedProduct.Name;
                    var catIdx = _categories.FindIndex(c => c.Id == selectedProduct.CategoryId);
                    if (catIdx >= 0 && catIdx < cboPCat.Items.Count) cboPCat.SelectedIndex = catIdx;
                    numPPrice.Value = selectedProduct.Price;
                    chkTrackStock.Checked = selectedProduct.TrackStock;
                    numPStock.Value = selectedProduct.StockQty;
                    numPStock.Enabled = selectedProduct.TrackStock;
                }
            }
            else
            {
                selectedProduct = null;
                txtPName.Text = "";
                numPPrice.Value = 0;
                chkTrackStock.Checked = false;
                numPStock.Value = 0;
            }
        };

        chkTrackStock.CheckedChanged += (s, ev) => numPStock.Enabled = chkTrackStock.Checked;

        btnSaveProd.Click += async (s, ev) =>
        {
            if (string.IsNullOrWhiteSpace(txtPName.Text))
            {
                MessageBox.Show("กรุณากรอกชื่อสินค้า", "คำเตือน", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (cboPCat.SelectedIndex < 0)
            {
                MessageBox.Show("กรุณาเลือกประเภทสินค้า", "คำเตือน", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var isNew = selectedProduct == null;
            var prod = selectedProduct ?? new Product();
            prod.Name = txtPName.Text.Trim();
            prod.CategoryId = _categories[cboPCat.SelectedIndex].Id;
            prod.Price = numPPrice.Value;
            prod.TrackStock = chkTrackStock.Checked;
            prod.StockQty = (int)numPStock.Value;

            await _posService.SaveProductAsync(prod);
            _products = (await _posService.GetProductsAsync()).ToList();
            reloadGrids();
            if (isNew) dgvProds.ClearSelection();
            MessageBox.Show("บันทึกข้อมูลสินค้าเรียบร้อยแล้ว", "สำเร็จ", MessageBoxButtons.OK, MessageBoxIcon.Information);
        };

        btnDelProd.Click += async (s, ev) =>
        {
            if (selectedProduct == null) return;
            if (MessageBox.Show($"ต้องการลบสินค้า '{selectedProduct.Name}' ใช่หรือไม่?", "ยืนยันการลบ", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                await _posService.DeleteProductAsync(selectedProduct.Id);
                _products = (await _posService.GetProductsAsync()).ToList();
                reloadGrids();
                dgvProds.ClearSelection();
            }
        };

        // Category Form select
        ProductCategory? selectedCategory = null;
        dgvCats.SelectionChanged += (s, ev) =>
        {
            if (dgvCats.SelectedRows.Count > 0)
            {
                var r = dgvCats.SelectedRows[0];
                var id = (int)r.Cells[0].Value;
                selectedCategory = _categories.FirstOrDefault(c => c.Id == id);
                if (selectedCategory != null)
                {
                    txtCName.Text = selectedCategory.Name;
                }
            }
            else
            {
                selectedCategory = null;
                txtCName.Text = "";
            }
        };

        btnSaveCat.Click += async (s, ev) =>
        {
            if (string.IsNullOrWhiteSpace(txtCName.Text))
            {
                MessageBox.Show("กรุณากรอกชื่อประเภทสินค้า", "คำเตือน", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var isNew = selectedCategory == null;
            var cat = selectedCategory ?? new ProductCategory();
            cat.Name = txtCName.Text.Trim();

            await _posService.SaveCategoryAsync(cat);
            _categories = (await _posService.GetCategoriesAsync()).ToList();
            reloadGrids();
            if (isNew) dgvCats.ClearSelection();
            MessageBox.Show("บันทึกประเภทสินค้าเรียบร้อยแล้ว", "สำเร็จ", MessageBoxButtons.OK, MessageBoxIcon.Information);
        };

        btnDelCat.Click += async (s, ev) =>
        {
            if (selectedCategory == null) return;
            if (MessageBox.Show($"ต้องการลบประเภทสินค้า '{selectedCategory.Name}' ใช่หรือไม่?", "ยืนยันการลบ", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                await _posService.DeleteCategoryAsync(selectedCategory.Id);
                _categories = (await _posService.GetCategoriesAsync()).ToList();
                reloadGrids();
                dgvCats.ClearSelection();
            }
        };

        dlg.ShowDialog();

        // Refresh main view lists after closing dialog
        LoadInitialDataAsync().GetAwaiter().GetResult();
    }
}
