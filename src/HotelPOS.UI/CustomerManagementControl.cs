using HotelPOS.Common.Models;
using HotelPOS.Core.Services;

namespace HotelPOS.UI;

/// <summary>
/// หน้าจัดการข้อมูลลูกค้า พร้อมระบบค้นหาทันทีที่พิมพ์ (Instant Typing Search) ด้วยเบอร์โทร ชื่อ หรือเลขบัตร
/// </summary>
public class CustomerManagementControl : UserControl
{
    private readonly ICustomerService _customerService;

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
        BackColor = Color.FromArgb(245, 247, 250);

        var topPanel = new Panel { Dock = DockStyle.Top, Height = 60, Padding = new Padding(15, 10, 15, 10), BackColor = Color.White };
        var lblTitle = new Label { Text = "ระบบจัดการข้อมูลลูกค้า", Font = new Font("Segoe UI", 14F, FontStyle.Bold), Location = new Point(15, 14), AutoSize = true };

        var lblSearch = new Label { Text = "ค้นหาลูกค้า:", Font = new Font("Segoe UI", 10.5F, FontStyle.Bold), Location = new Point(270, 18), AutoSize = true };
        _txtSearch = new TextBox
        {
            Location = new Point(360, 14),
            Width = 320,
            Font = new Font("Segoe UI", 11F),
            PlaceholderText = "พิมพ์เบอร์โทร / ชื่อ / เลขบัตรประชาชน เพื่อค้นหาทันที..."
        };
        // Instant search on typing
        _txtSearch.TextChanged += async (s, e) => await LoadCustomersAsync(_txtSearch.Text);

        topPanel.Controls.AddRange(new Control[] { lblTitle, lblSearch, _txtSearch });

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
            RowTemplate = { Height = 35 },
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            GridColor = Color.FromArgb(226, 232, 240)
        };
        _dgvCustomers.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            BackColor = Color.FromArgb(30, 41, 59),
            ForeColor = Color.White
        };
        _dgvCustomers.EnableHeadersVisualStyles = false;
        _dgvCustomers.DefaultCellStyle.Font = new Font("Segoe UI", 10.5F);
        _dgvCustomers.SelectionChanged += DgvCustomers_SelectionChanged;

        var panelInput = new Panel { Dock = DockStyle.Fill, Padding = new Padding(15), AutoScroll = true, BackColor = Color.White };
        var lblFormTitle = new Label { Text = "ข้อมูลรายละเอียดลูกค้า", Font = new Font("Segoe UI", 13F, FontStyle.Bold), Location = new Point(15, 10), AutoSize = true };

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

        _btnSave = new Button { Text = "บันทึกข้อมูล", Location = new Point(160, 325), Size = new Size(110, 38), Font = new Font("Segoe UI", 11F, FontStyle.Bold), BackColor = Color.ForestGreen, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
        _btnSave.FlatAppearance.BorderSize = 0;
        _btnSave.Click += BtnSave_Click;

        _btnClear = new Button { Text = "ล้างฟอร์ม", Location = new Point(280, 325), Size = new Size(120, 38), Font = new Font("Segoe UI", 11F), BackColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
        _btnClear.Click += (s, e) => ClearForm();

        _btnDelete = new Button { Text = "ลบข้อมูลลูกค้า", Location = new Point(160, 375), Size = new Size(240, 36), Font = new Font("Segoe UI", 10.5F), ForeColor = Color.Red, BackColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
        _btnDelete.Click += BtnDelete_Click;

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
            await LoadCustomersAsync(_txtSearch.Text);
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
            await LoadCustomersAsync(_txtSearch.Text);
        }
    }
}
