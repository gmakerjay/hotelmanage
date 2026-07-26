using HotelPOS.Common.Models;
using HotelPOS.Core.Services;

namespace HotelPOS.UI;

public class CustomerManagementControl : UserControl
{
    private readonly ICustomerService _customerService;

    private DataGridView _dgvCustomers = null!;
    private TextBox _txtSearch = null!;
    private Button _btnSearch = null!;

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

    private List<Customer> _customersList = new();

    public CustomerManagementControl(ICustomerService customerService)
    {
        _customerService = customerService;
        InitializeUI();
        Load += async (s, e) => await LoadCustomersAsync();
    }

    private void InitializeUI()
    {
        Dock = DockStyle.Fill;
        Font = new Font("Segoe UI", 11F, FontStyle.Regular);

        var topPanel = new Panel { Dock = DockStyle.Top, Height = 60, Padding = new Padding(15, 10, 15, 10) };
        var lblTitle = new Label { Text = "👤 ระบบจัดการข้อมูลลูกค้า", Font = new Font("Segoe UI", 14F, FontStyle.Bold), Location = new Point(15, 12), AutoSize = true };

        _txtSearch = new TextBox { Location = new Point(280, 12), Width = 260, Font = new Font("Segoe UI", 11F), PlaceholderText = "ค้นหาชื่อ / เบอร์ / เลขบัตร..." };
        _txtSearch.KeyDown += async (s, e) => { if (e.KeyCode == Keys.Enter) await LoadCustomersAsync(_txtSearch.Text); };

        _btnSearch = new Button { Text = "🔍 ค้นหา", Location = new Point(550, 10), Size = new Size(100, 36), Font = new Font("Segoe UI", 10.5F, FontStyle.Bold) };
        _btnSearch.Click += async (s, e) => await LoadCustomersAsync(_txtSearch.Text);

        topPanel.Controls.AddRange(new Control[] { lblTitle, _txtSearch, _btnSearch });

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
            ColumnHeadersHeight = 38,
            RowTemplate = { Height = 35 }
        };
        _dgvCustomers.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        _dgvCustomers.DefaultCellStyle.Font = new Font("Segoe UI", 10.5F);
        _dgvCustomers.SelectionChanged += DgvCustomers_SelectionChanged;

        var panelInput = new Panel { Dock = DockStyle.Fill, Padding = new Padding(15), AutoScroll = true };
        var lblFormTitle = new Label { Text = "ข้อมูลลูกค้า", Font = new Font("Segoe UI", 13F, FontStyle.Bold), Location = new Point(15, 10), AutoSize = true };

        var lblName = new Label { Text = "ชื่อ-นามสกุล *:", Location = new Point(15, 45), Font = new Font("Segoe UI", 11F, FontStyle.Bold), AutoSize = true };
        _txtFullName = new TextBox { Location = new Point(160, 42), Width = 240, Font = new Font("Segoe UI", 11F) };

        var lblPhone = new Label { Text = "เบอร์โทรศัพท์:", Location = new Point(15, 85), Font = new Font("Segoe UI", 11F), AutoSize = true };
        _txtPhone = new TextBox { Location = new Point(160, 82), Width = 240, Font = new Font("Segoe UI", 11F) };

        var lblEmail = new Label { Text = "อีเมล:", Location = new Point(15, 125), Font = new Font("Segoe UI", 11F), AutoSize = true };
        _txtEmail = new TextBox { Location = new Point(160, 122), Width = 240, Font = new Font("Segoe UI", 11F) };

        var lblIdCard = new Label { Text = "เลขบัตร/พาสปอร์ต:", Location = new Point(15, 165), Font = new Font("Segoe UI", 11F), AutoSize = true };
        _txtIdCard = new TextBox { Location = new Point(160, 162), Width = 240, Font = new Font("Segoe UI", 11F) };

        var lblAddress = new Label { Text = "ที่อยู่:", Location = new Point(15, 205), Font = new Font("Segoe UI", 11F), AutoSize = true };
        _txtAddress = new TextBox { Location = new Point(160, 202), Width = 240, Font = new Font("Segoe UI", 11F), Multiline = true, Height = 55 };

        var lblNotes = new Label { Text = "หมายเหตุ:", Location = new Point(15, 270), Font = new Font("Segoe UI", 11F), AutoSize = true };
        _txtNotes = new TextBox { Location = new Point(160, 267), Width = 240, Font = new Font("Segoe UI", 11F), Multiline = true, Height = 45 };

        _btnSave = new Button { Text = "💾 บันทึก", Location = new Point(160, 325), Size = new Size(110, 38), Font = new Font("Segoe UI", 11F, FontStyle.Bold), BackColor = Color.ForestGreen, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        _btnSave.Click += BtnSave_Click;

        _btnClear = new Button { Text = "✨ ล้างฟอร์ม", Location = new Point(280, 325), Size = new Size(120, 38), Font = new Font("Segoe UI", 11F) };
        _btnClear.Click += (s, e) => ClearForm();

        _btnDelete = new Button { Text = "🗑️ ลบข้อมูลลูกค้า", Location = new Point(160, 375), Size = new Size(240, 36), Font = new Font("Segoe UI", 10.5F), ForeColor = Color.Red };
        _btnDelete.Click += BtnDelete_Click;

        // ToolTips Guide
        var tt = new ToolTip();
        tt.SetToolTip(_txtSearch, "พิมพ์ค้นหาชื่อลูกค้า, เบอร์โทรศัพท์ หรือเลขบัตรประชาชน");
        tt.SetToolTip(_btnSearch, "ค้นหาข้อมูลลูกค้าตามเงื่อนไขที่พิมพ์");
        tt.SetToolTip(_txtFullName, "กรอกชื่อ-นามสกุลลูกค้า");
        tt.SetToolTip(_txtPhone, "กรอกเบอร์โทรศัพท์ติดต่อลูกค้า");
        tt.SetToolTip(_txtEmail, "กรอกที่อยู่อีเมลของลูกค้า");
        tt.SetToolTip(_txtIdCard, "กรอกเลขบัตรประชาชน หรือ เลขพาสปอร์ต");
        tt.SetToolTip(_txtAddress, "กรอกที่อยู่ของลูกค้าสำหรับการติดต่อหรือออกใบเสร็จ");
        tt.SetToolTip(_txtNotes, "บันทึกประวัติความชอบ/หมายเหตุลูกค้าเพิ่มเติม");
        tt.SetToolTip(_btnSave, "บันทึกข้อมูลลูกค้าลงฐานข้อมูล");
        tt.SetToolTip(_btnDelete, "ลบประวัติและข้อมูลลูกค้าออกจากระบบ");

        panelInput.Controls.AddRange(new Control[]
        {
            lblFormTitle, lblName, _txtFullName, lblPhone, _txtPhone,
            lblEmail, _txtEmail, lblIdCard, _txtIdCard, lblAddress, _txtAddress,
            lblNotes, _txtNotes, _btnSave, _btnClear, _btnDelete
        });

        split.Panel1.Controls.Add(_dgvCustomers);
        split.Panel2.Controls.Add(panelInput);

        Controls.Add(split);
        Controls.Add(topPanel);
    }

    private async Task LoadCustomersAsync(string? query = null)
    {
        try
        {
            _customersList = (await _customerService.GetCustomersAsync(query)).ToList();
            _dgvCustomers.DataSource = _customersList.Select(c => new
            {
                c.Id,
                ชื่อนามสกุล = c.FullName,
                เบอร์โทร = c.Phone ?? "-",
                เลขบัตร = c.IdCardOrPassport ?? "-",
                อีเมล = c.Email ?? "-",
                วันที่ลงทะเบียน = c.CreatedAt.ToString("dd/MM/yyyy")
            }).ToList();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"โหลดข้อมูลลูกค้าไม่สำเร็จ: {ex.Message}", "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void DgvCustomers_SelectionChanged(object? sender, EventArgs e)
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
    }

    private async void BtnSave_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_txtFullName.Text))
        {
            MessageBox.Show("กรุณากรอกชื่อ-นามสกุลลูกค้า", "เตือน", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
            await LoadCustomersAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"บันทึกข้อมูลลูกค้าไม่สำเร็จ: {ex.Message}", "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void BtnDelete_Click(object? sender, EventArgs e)
    {
        if (_selectedCustomerId == 0) return;
        if (MessageBox.Show("ยืนยันการลบข้อมูลลูกค้ารายนี้?", "ยืนยัน", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            await _customerService.DeleteCustomerAsync(_selectedCustomerId);
            ClearForm();
            await LoadCustomersAsync();
        }
    }
}
