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
    private Button _btnSalesHistory = null!;
    private readonly IAuditService? _auditService;

    public POSControl(IPOSService posService, ISettingsService settingsService, IAppLogger logger, IAuditService? auditService = null)
    {
        _posService = posService;
        _settingsService = settingsService;
        _logger = logger;
        _auditService = auditService;

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
            SplitterWidth = 8,
            IsSplitterFixed = false
        };
        Controls.Add(mainSplit);

        mainSplit.Resize += (s, e) =>
        {
            try
            {
                if (mainSplit.Width > 700)
                {
                    mainSplit.Panel1MinSize = 300;
                    mainSplit.Panel2MinSize = 300;
                    int targetDist = (int)(mainSplit.Width * 0.58);
                    int maxDist = mainSplit.Width - mainSplit.Panel2MinSize;
                    int minDist = mainSplit.Panel1MinSize;
                    mainSplit.SplitterDistance = Math.Clamp(targetDist, minDist, Math.Max(minDist, maxDist));
                }
            }
            catch { }
        };

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
            Size = new Size(pnlLeft.Width - 24, 40),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
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

        _btnSalesHistory = new Button
        {
            Text = "📜 ประวัติการขาย & พิมพ์ใบเสร็จย้อนหลัง",
            BackColor = Color.White,
            ForeColor = Color.FromArgb(30, 41, 59),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            Location = new Point(555, 3),
            Size = new Size(230, 32),
            Cursor = Cursors.Hand
        };
        _btnSalesHistory.Click += (s, e) =>
        {
            using var form = new POSSalesHistoryForm(_posService, _settingsService, _logger, _auditService);
            form.ShowDialog();
        };
        pnlSearch.Controls.Add(_btnSalesHistory);

        // Category Flow Panel
        _flpCategories = new FlowLayoutPanel
        {
            Location = new Point(12, 90),
            Size = new Size(pnlLeft.Width - 24, 45),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoScroll = true
        };
        pnlLeft.Controls.Add(_flpCategories);

        // Products Grid
        _flpProducts = new FlowLayoutPanel
        {
            Location = new Point(12, 140),
            Size = new Size(pnlLeft.Width - 24, Math.Max(200, pnlLeft.Height - 150)),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            AutoScroll = true,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0, 0, 15, 10)
        };
        pnlLeft.Controls.Add(_flpProducts);

        pnlLeft.SizeChanged += (s, e) =>
        {
            int w = Math.Max(200, pnlLeft.ClientSize.Width - 24);
            pnlSearch.Width = w;
            _flpCategories.Width = w;
            _flpProducts.Width = w;
        };


        // --- RIGHT PANEL (CART) ---
        var pnlRight = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
            BackColor = Color.White
        };
        mainSplit.Panel2.Controls.Add(pnlRight);

        var lblRightTitle = new Label
        {
            Text = "ตะกร้าสินค้า (Cart)",
            Font = new Font("Segoe UI", 12.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Dock = DockStyle.Top,
            Height = 35
        };

        var pnlCartBottom = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 225,
            Padding = new Padding(0, 6, 0, 0),
            BackColor = Color.FromArgb(248, 250, 252)
        };

        // Cart DataGridView
        _dgvCart = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            RowHeadersVisible = false,
            GridColor = Color.FromArgb(241, 245, 249),
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(241, 245, 249),
                ForeColor = Color.FromArgb(51, 65, 85),
                SelectionBackColor = Color.FromArgb(241, 245, 249),
                SelectionForeColor = Color.FromArgb(51, 65, 85),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
            },
            EnableHeadersVisualStyles = false
        };
        _dgvCart.EnableDoubleBuffering();
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

        // Bottom Summary & Checkout Controls
        int bottomY = 6;

        var lblDiscount = new Label
        {
            Text = "ส่วนลด (บาท):",
            Location = new Point(8, bottomY + 3),
            Size = new Size(95, 25),
            ForeColor = Color.FromArgb(71, 85, 105)
        };
        _txtDiscount = new TextBox
        {
            Location = new Point(105, bottomY),
            Size = new Size(110, 28),
            Text = "0",
            TextAlign = HorizontalAlignment.Right
        };
        _txtDiscount.TextChanged += (s, e) => CalculateSummary();

        _lblSubTotal = new Label
        {
            Text = "รวมเงินหลัก: 0.00 บาท",
            Location = new Point(225, bottomY + 3),
            Size = new Size(200, 25),
            TextAlign = ContentAlignment.MiddleRight,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
            ForeColor = Color.FromArgb(100, 116, 139),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        pnlCartBottom.Controls.AddRange(new Control[] { lblDiscount, _txtDiscount, _lblSubTotal });
        bottomY += 34;

        _chkChargeRoom = new CheckBox
        {
            Text = "ชาร์จค่าใช้จ่ายเข้าบัญชีห้องพัก (Folio)",
            Location = new Point(8, bottomY),
            Size = new Size(280, 25),
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59)
        };
        _chkChargeRoom.CheckedChanged += (s, e) => ToggleFolioSelection();
        pnlCartBottom.Controls.Add(_chkChargeRoom);
        bottomY += 28;

        _cboFolio = new ComboBox
        {
            Location = new Point(8, bottomY),
            Size = new Size(340, 28),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Enabled = false,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        pnlCartBottom.Controls.Add(_cboFolio);
        bottomY += 36;

        _lblTotal = new Label
        {
            Text = "ยอดสุทธิ: 0.00 บาท",
            Font = new Font("Segoe UI", 15F, FontStyle.Bold),
            ForeColor = Color.FromArgb(37, 99, 235),
            Location = new Point(8, bottomY),
            Size = new Size(400, 36),
            TextAlign = ContentAlignment.MiddleLeft,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        pnlCartBottom.Controls.Add(_lblTotal);
        bottomY += 40;

        _btnCheckout = new Button
        {
            Text = "คิดเงิน / ชำระเงิน (F10)",
            BackColor = Color.FromArgb(22, 163, 74),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 11.5F, FontStyle.Bold),
            Location = new Point(8, bottomY),
            Size = new Size(230, 44),
            Cursor = Cursors.Hand
        };
        _btnCheckout.FlatAppearance.BorderSize = 0;
        _btnCheckout.Click += BtnCheckout_Click;
        pnlCartBottom.Controls.Add(_btnCheckout);

        _btnClearCart = new Button
        {
            Text = "ล้างตะกร้า",
            BackColor = Color.White,
            ForeColor = Color.FromArgb(220, 38, 38),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            Location = new Point(245, bottomY),
            Size = new Size(110, 44),
            Cursor = Cursors.Hand
        };
        _btnClearCart.FlatAppearance.BorderColor = Color.FromArgb(220, 38, 38);
        _btnClearCart.Click += (s, e) => ClearCart();
        pnlCartBottom.Controls.Add(_btnClearCart);

        // Add controls in Dock order (Controls added first will fill space remaining)
        pnlRight.Controls.Add(_dgvCart);
        pnlRight.Controls.Add(pnlCartBottom);
        pnlRight.Controls.Add(lblRightTitle);
    }

    public async Task LoadInitialDataAsync()
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
            _btnCheckout.Text = "คิดเงิน / ชำระเงิน (F10)";
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
            _logger.Error(LogCategory.Pos, "กระบวนการชำระเงิน/ปิดบิลขาย POS ล้มเหลว", ex);
            Program.ShowDetailedErrorPopup(ex, "ชำระเงิน/บันทึกการขายหน้าร้าน POS ล้มเหลวเนื่องจากข้อผิดพลาดของระบบ");
        }
    }

    private async Task PrintThermalReceiptAsync(int saleId)
    {
        try
        {
            var settings = await _settingsService.GetAllSettingsAsync();
            var printerName = settings.PrinterName;
            if (string.IsNullOrEmpty(printerName))
            {
                MessageBox.Show("ไม่ได้ตั้งค่าเครื่องพิมพ์เริ่มต้นไว้ กรุณาไปตั้งค่าที่เมนูตั้งค่าระบบ", "ไม่พบเครื่องพิมพ์", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (settings.PaperType == "A4")
            {
                MessageBox.Show("การพิมพ์ใบเสร็จ POS หน้าร้านรองรับกระดาษม้วนแบบ Slip 80mm หรือ 58mm เท่านั้น", "ขนาดกระดาษไม่ตรง", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Fetch the sale info
            var sale = await _posService.GetSaleByIdAsync(saleId);
            var items = await _posService.GetSaleItemsBySaleIdAsync(saleId);
            var payments = await _posService.GetPaymentsBySaleIdAsync(saleId);

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
                // ดึงข้อมูลลูกค้าผ่าน Service layer (ไม่ query DB ตรงจาก UI)
                var folioDetails = await _posService.GetCustomerByIdAsync(sale.CustomerId.Value);
                if (folioDetails != null) customer = folioDetails;
            }

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
                booking,
                room,
                customer,
                folio,
                settings,
                "admin"
            );
            
            // Custom printer task trigger
            printerEngine.Print(printerName);
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Printing, "พิมพ์ใบเสร็จขาย POS ล้มเหลว", ex);
            Program.ShowDetailedErrorPopup(ex, "พิมพ์ใบเสร็จรับเงินอย่างย่อ POS ล้มเหลวเนื่องจากข้อผิดพลาดของระบบหรือไดรเวอร์เครื่องพิมพ์");
        }
    }

    // --- INLINE INVENTORY MANAGEMENT ---
    private void BtnManageInventory_Click(object? sender, EventArgs e)
    {
        using var dlg = new Form
        {
            Text = "จัดการสต็อกและสินค้า (POS Inventory & Products)",
            Size = new Size(1020, 600),
            MinimumSize = new Size(950, 540),
            StartPosition = FormStartPosition.CenterParent,
            BackColor = Color.FromArgb(248, 250, 252),
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
            MaximizeBox = true,
            MinimizeBox = false
        };

        var mainTab = new TabControl { Dock = DockStyle.Fill };
        dlg.Controls.Add(mainTab);

        // Tab 1: Product List & CRUD
        var tabProducts = new TabPage("รายการสินค้า (Products)");
        mainTab.TabPages.Add(tabProducts);

        var splitProd = new SplitContainer { Dock = DockStyle.Fill };
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
        var pnlProdInputs = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12), AutoScroll = true };
        splitProd.Panel2.Controls.Add(pnlProdInputs);

        int iy = 15;
        var lblPName = new Label { Text = "ชื่อสินค้า:", Location = new Point(12, iy), Size = new Size(80, 20) };
        var txtPName = new TextBox { Location = new Point(100, iy - 3), Size = new Size(220, 25) };
        pnlProdInputs.Controls.AddRange(new Control[] { lblPName, txtPName });
        iy += 38;

        var lblPCat = new Label { Text = "ประเภท:", Location = new Point(12, iy), Size = new Size(80, 20) };
        var cboPCat = new ComboBox { Location = new Point(100, iy - 3), Size = new Size(220, 25), DropDownStyle = ComboBoxStyle.DropDownList };
        pnlProdInputs.Controls.AddRange(new Control[] { lblPCat, cboPCat });
        iy += 38;

        var lblPPrice = new Label { Text = "ราคาขาย:", Location = new Point(12, iy), Size = new Size(80, 20) };
        var numPPrice = new NumericUpDown { Location = new Point(100, iy - 3), Size = new Size(120, 25), Maximum = 100000, DecimalPlaces = 2 };
        pnlProdInputs.Controls.AddRange(new Control[] { lblPPrice, numPPrice });
        iy += 38;

        var chkTrackStock = new CheckBox { Text = "ควบคุมสต็อกสินค้า", Location = new Point(100, iy), Size = new Size(220, 24) };
        pnlProdInputs.Controls.Add(chkTrackStock);
        iy += 34;

        var lblPStock = new Label { Text = "สต็อกคงเหลือ:", Location = new Point(12, iy), Size = new Size(80, 20) };
        var numPStock = new NumericUpDown { Location = new Point(100, iy - 3), Size = new Size(120, 25), Maximum = 99999 };
        pnlProdInputs.Controls.AddRange(new Control[] { lblPStock, numPStock });
        iy += 48;

        var btnSaveProd = new Button { Text = "บันทึกสินค้า", Location = new Point(12, iy), Size = new Size(125, 36), BackColor = Color.FromArgb(37, 99, 235), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
        btnSaveProd.FlatAppearance.BorderSize = 0;
        var btnDelProd = new Button { Text = "ลบสินค้า", Location = new Point(145, iy), Size = new Size(125, 36), BackColor = Color.White, ForeColor = Color.Red, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
        btnDelProd.FlatAppearance.BorderColor = Color.Red;
        pnlProdInputs.Controls.AddRange(new Control[] { btnSaveProd, btnDelProd });

        iy += 46;
        var btnExportProds = new Button { Text = "ส่งออก (CSV)", Location = new Point(12, iy), Size = new Size(125, 34), BackColor = Color.White, ForeColor = Color.FromArgb(30, 41, 59), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
        var btnImportProds = new Button { Text = "นำเข้า (CSV)", Location = new Point(145, iy), Size = new Size(125, 34), BackColor = Color.White, ForeColor = Color.FromArgb(30, 41, 59), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
        pnlProdInputs.Controls.AddRange(new Control[] { btnExportProds, btnImportProds });

        // Tab 2: Categories CRUD
        var tabCats = new TabPage("ประเภทสินค้า (Categories)");
        mainTab.TabPages.Add(tabCats);

        var splitCat = new SplitContainer { Dock = DockStyle.Fill };
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

        var pnlCatInputs = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12), AutoScroll = true };
        splitCat.Panel2.Controls.Add(pnlCatInputs);

        int cy = 15;
        var lblCName = new Label { Text = "ชื่อประเภท:", Location = new Point(12, cy), Size = new Size(80, 20) };
        var txtCName = new TextBox { Location = new Point(100, cy - 3), Size = new Size(220, 25) };
        pnlCatInputs.Controls.AddRange(new Control[] { lblCName, txtCName });
        cy += 48;

        var btnSaveCat = new Button { Text = "บันทึกประเภท", Location = new Point(12, cy), Size = new Size(125, 36), BackColor = Color.FromArgb(37, 99, 235), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
        btnSaveCat.FlatAppearance.BorderSize = 0;
        var btnDelCat = new Button { Text = "ลบประเภท", Location = new Point(145, cy), Size = new Size(125, 36), BackColor = Color.White, ForeColor = Color.Red, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
        btnDelCat.FlatAppearance.BorderColor = Color.Red;
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
            if (_auditService != null)
            {
                var action = isNew ? "CREATE_PRODUCT" : "UPDATE_PRODUCT_STOCK";
                var detail = isNew
                    ? $"เพิ่มสินค้าใหม่ '{prod.Name}' ราคา {prod.Price:N2} บาท (สต็อก: {prod.StockQty})"
                    : $"แก้ไขสินค้า '{prod.Name}' (ID={prod.Id}): ราคา {prod.Price:N2} บาท, สต็อก {prod.StockQty}";
                await _auditService.LogAsync(action, "products", prod.Id.ToString(), detail);
            }
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

        btnExportProds.Click += async (s, ev) =>
        {
            using var sfd = new SaveFileDialog { Filter = "CSV File (*.csv)|*.csv", FileName = "Products.csv" };
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    var expImpService = new ExportImportService(null!, null!, _auditService ?? new AuditService(null!, _logger), _posService);
                    await expImpService.ExportProductsToCsvAsync(sfd.FileName);
                    MessageBox.Show("ส่งออกข้อมูลสินค้าและสต็อกเรียบร้อยแล้ว", "สำเร็จ", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"ส่งออกไม่สำเร็จ: {ex.Message}", "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        };

        btnImportProds.Click += async (s, ev) =>
        {
            using var ofd = new OpenFileDialog { Filter = "CSV File (*.csv)|*.csv" };
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    var expImpService = new ExportImportService(null!, null!, _auditService ?? new AuditService(null!, _logger), _posService);
                    int count = await expImpService.ImportProductsFromCsvAsync(ofd.FileName);
                    _products = (await _posService.GetProductsAsync()).ToList();
                    _categories = (await _posService.GetCategoriesAsync()).ToList();
                    reloadGrids();
                    MessageBox.Show($"นำเข้าข้อมูลสินค้าและสต็อกเรียบร้อยแล้ว จำนวน {count} รายการ", "สำเร็จ", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"นำเข้าไม่สำเร็จ: {ex.Message}", "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
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

        dlg.Shown += (s, ev) =>
        {
            try
            {
                if (splitProd.Width > 700)
                {
                    splitProd.Panel1MinSize = 350;
                    splitProd.Panel2MinSize = 300;
                    splitProd.SplitterDistance = Math.Min(580, Math.Max(350, splitProd.Width - 340));
                }
                if (splitCat.Width > 700)
                {
                    splitCat.Panel1MinSize = 350;
                    splitCat.Panel2MinSize = 300;
                    splitCat.SplitterDistance = Math.Min(580, Math.Max(350, splitCat.Width - 340));
                }
            }
            catch { }
        };

        dlg.ShowDialog();

        // Refresh main view lists after closing dialog
        LoadInitialDataAsync().GetAwaiter().GetResult();
    }

    public void SetRoomCharge(string roomNumber)
    {
        _chkChargeRoom.Checked = true;
        
        // Find the index of the folio matching roomNumber
        for (int i = 0; i < _activeFolios.Count; i++)
        {
            if (_activeFolios[i].RoomNumber == roomNumber)
            {
                _cboFolio.SelectedIndex = i;
                break;
            }
        }
    }
}
