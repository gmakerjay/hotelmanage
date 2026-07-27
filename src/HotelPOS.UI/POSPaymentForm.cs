using System;
using System.Drawing;
using System.Windows.Forms;
using HotelPOS.Common;
using HotelPOS.Common.Models;

namespace HotelPOS.UI;

public class POSPaymentForm : Form
{
    private readonly decimal _totalAmount;
    
    private ComboBox _cboMethod = null!;
    private TextBox _txtReceived = null!;
    private Label _lblChange = null!;
    private TextBox _txtRefNo = null!;
    
    private Button _btnOk = null!;
    private Button _btnCancel = null!;

    public PaymentMethod SelectedMethod => (PaymentMethod)(_cboMethod.SelectedIndex >= 0 ? _cboMethod.SelectedIndex : 0);
    public decimal ReceivedAmount => decimal.TryParse(_txtReceived.Text, out var val) ? val : _totalAmount;
    public string ReferenceNo => _txtRefNo.Text.Trim();
    public bool PrintReceipt { get; private set; }

    public POSPaymentForm(decimal totalAmount)
    {
        _totalAmount = totalAmount;

        InitializeUI();
    }

    private void InitializeUI()
    {
        Text = "ชำระเงิน (POS Checkout Payment)";
        Size = new Size(420, 380);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.FromArgb(248, 250, 252);
        Font = new Font("Segoe UI", 10F, FontStyle.Regular);

        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 60,
            BackColor = Color.FromArgb(15, 23, 42)
        };
        var lblTitle = new Label
        {
            Text = "ยืนยันการรับเงินชำระสินค้า",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            Location = new Point(20, 18),
            AutoSize = true
        };
        pnlHeader.Controls.Add(lblTitle);
        Controls.Add(pnlHeader);

        int startY = 80;

        // Total Label
        var lblTotalText = new Label
        {
            Text = $"ยอดรวมทั้งสิ้น:",
            Location = new Point(30, startY),
            Size = new Size(120, 25),
            Font = new Font("Segoe UI", 10.5F, FontStyle.Regular),
            ForeColor = Color.FromArgb(71, 85, 105)
        };
        var lblTotalVal = new Label
        {
            Text = $"{_totalAmount:N2} บาท",
            Location = new Point(160, startY - 2),
            Size = new Size(200, 30),
            Font = new Font("Segoe UI", 14F, FontStyle.Bold),
            ForeColor = Color.FromArgb(220, 38, 38)
        };
        Controls.Add(lblTotalText);
        Controls.Add(lblTotalVal);
        startY += 40;

        // Payment Method Dropdown
        var lblMethod = new Label
        {
            Text = "ช่องทางชำระเงิน:",
            Location = new Point(30, startY + 3),
            Size = new Size(120, 25),
            ForeColor = Color.FromArgb(71, 85, 105)
        };
        _cboMethod = new ComboBox
        {
            Location = new Point(160, startY),
            Size = new Size(210, 28),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _cboMethod.Items.Add("เงินสด");
        _cboMethod.Items.Add("โอนเงิน");
        _cboMethod.Items.Add("บัตรเครดิต/เดบิต");
        _cboMethod.Items.Add("พร้อมเพย์");
        _cboMethod.SelectedIndex = 0;
        _cboMethod.SelectedIndexChanged += (s, e) => ToggleMethodFields();
        
        Controls.Add(lblMethod);
        Controls.Add(_cboMethod);
        startY += 40;

        // Received Cash
        var lblReceived = new Label
        {
            Text = "รับเงินมา:",
            Location = new Point(30, startY + 3),
            Size = new Size(120, 25),
            ForeColor = Color.FromArgb(71, 85, 105)
        };
        _txtReceived = new TextBox
        {
            Location = new Point(160, startY),
            Size = new Size(210, 28),
            Text = _totalAmount.ToString("0.00"),
            TextAlign = HorizontalAlignment.Right
        };
        _txtReceived.TextChanged += (s, e) => CalculateChange();
        Controls.Add(lblReceived);
        Controls.Add(_txtReceived);
        startY += 40;

        // Change Due Label
        var lblChangeText = new Label
        {
            Text = "เงินทอน:",
            Location = new Point(30, startY + 3),
            Size = new Size(120, 25),
            ForeColor = Color.FromArgb(71, 85, 105)
        };
        _lblChange = new Label
        {
            Text = "0.00 บาท",
            Location = new Point(160, startY),
            Size = new Size(210, 25),
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor = Color.FromArgb(22, 163, 74)
        };
        Controls.Add(lblChangeText);
        Controls.Add(_lblChange);
        startY += 35;

        // Ref No
        var lblRefNo = new Label
        {
            Text = "เลขที่อ้างอิงสลิป:",
            Location = new Point(30, startY + 3),
            Size = new Size(120, 25),
            ForeColor = Color.FromArgb(71, 85, 105)
        };
        _txtRefNo = new TextBox
        {
            Location = new Point(160, startY),
            Size = new Size(210, 28),
            Enabled = false
        };
        Controls.Add(lblRefNo);
        Controls.Add(_txtRefNo);
        startY += 55;

        // Action Buttons
        _btnOk = new Button
        {
            Text = "ชำระเงินและปิดบิล",
            BackColor = Color.FromArgb(37, 99, 235),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            Location = new Point(30, startY),
            Size = new Size(160, 40),
            Cursor = Cursors.Hand
        };
        _btnOk.FlatAppearance.BorderSize = 0;
        _btnOk.Click += BtnOk_Click;

        _btnCancel = new Button
        {
            Text = "ยกเลิก",
            BackColor = Color.White,
            ForeColor = Color.FromArgb(71, 85, 105),
            FlatStyle = FlatStyle.Flat,
            Location = new Point(210, startY),
            Size = new Size(160, 40),
            Cursor = Cursors.Hand
        };
        _btnCancel.Click += (s, e) => DialogResult = DialogResult.Cancel;

        Controls.Add(_btnOk);
        Controls.Add(_btnCancel);
    }

    private void ToggleMethodFields()
    {
        var isCash = _cboMethod.SelectedIndex == 0;
        _txtReceived.Enabled = isCash;
        _txtRefNo.Enabled = !isCash;
        
        if (!isCash)
        {
            _txtReceived.Text = _totalAmount.ToString("0.00");
            _lblChange.Text = "0.00 บาท";
        }
        else
        {
            CalculateChange();
        }
    }

    private void CalculateChange()
    {
        if (decimal.TryParse(_txtReceived.Text, out var received))
        {
            var change = received - _totalAmount;
            if (change < 0)
            {
                _lblChange.Text = "ยอดเงินไม่พอ";
                _lblChange.ForeColor = Color.Red;
                _btnOk.Enabled = false;
            }
            else
            {
                _lblChange.Text = $"{change:N2} บาท";
                _lblChange.ForeColor = Color.FromArgb(22, 163, 74);
                _btnOk.Enabled = true;
            }
        }
        else
        {
            _lblChange.Text = "ระบุตัวเลข";
            _lblChange.ForeColor = Color.Red;
            _btnOk.Enabled = false;
        }
    }

    private void BtnOk_Click(object? sender, EventArgs e)
    {
        var dialog = MessageBox.Show("ต้องการพิมพ์ใบเสร็จรับเงินด้วยหรือไม่?", "พิมพ์ใบเสร็จ", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
        if (dialog == DialogResult.Cancel) return;

        PrintReceipt = dialog == DialogResult.Yes;
        DialogResult = DialogResult.OK;
    }
}
