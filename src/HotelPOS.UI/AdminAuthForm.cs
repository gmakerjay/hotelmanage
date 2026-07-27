namespace HotelPOS.UI;

/// <summary>
/// ฟอร์มยืนยันรหัสผ่านผู้ดูแลระบบ (Admin Authentication)
/// </summary>
public class AdminAuthForm : Form
{
    private TextBox _txtAdminPassword = null!;
    private Button _btnConfirm = null!;
    private Button _btnCancel = null!;
    private Label _lblError = null!;

    public AdminAuthForm()
    {
        InitializeComponents();
    }

    private void InitializeComponents()
    {
        Text = "ยืนยันรหัสผ่านผู้ดูแลระบบ (Admin Verification)";
        Width = 440;
        Height = 240;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Font = new Font("Segoe UI", 10.5F);
        BackColor = Color.FromArgb(245, 247, 250);

        var lblPrompt = new Label
        {
            Text = "กรอกรหัสผ่านผู้ดูแลระบบ (Admin) เพื่อยืนยันการตั้งค่าอัตราต่อหน่วย:",
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Location = new Point(20, 20),
            Size = new Size(380, 45)
        };

        var lblPassword = new Label
        {
            Text = "รหัสผ่าน Admin:",
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            Location = new Point(20, 75),
            AutoSize = true
        };

        _txtAdminPassword = new TextBox
        {
            Location = new Point(140, 71),
            Width = 260,
            UseSystemPasswordChar = true,
            Font = new Font("Segoe UI", 11F)
        };

        _lblError = new Label
        {
            Text = "",
            ForeColor = Color.FromArgb(220, 38, 38),
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            Location = new Point(140, 105),
            AutoSize = true
        };

        _btnConfirm = new Button
        {
            Text = "ยืนยัน",
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Color.FromArgb(22, 163, 74),
            FlatStyle = FlatStyle.Flat,
            Size = new Size(125, 38),
            Location = new Point(140, 135),
            Cursor = Cursors.Hand
        };
        _btnConfirm.FlatAppearance.BorderSize = 0;
        _btnConfirm.Click += (s, e) => VerifyPassword();

        _btnCancel = new Button
        {
            Text = "ยกเลิก",
            Font = new Font("Segoe UI", 10F),
            ForeColor = Color.FromArgb(30, 41, 59),
            BackColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(110, 38),
            Location = new Point(275, 135),
            Cursor = Cursors.Hand
        };
        _btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

        AcceptButton = _btnConfirm;
        CancelButton = _btnCancel;

        Controls.AddRange(new Control[] { lblPrompt, lblPassword, _txtAdminPassword, _lblError, _btnConfirm, _btnCancel });
    }

    private void VerifyPassword()
    {
        string inputPassword = _txtAdminPassword.Text.Trim();

        if (inputPassword == "admin" || inputPassword == "1234")
        {
            DialogResult = DialogResult.OK;
            Close();
        }
        else
        {
            _lblError.Text = "รหัสผ่าน Admin ไม่ถูกต้อง!";
            _txtAdminPassword.SelectAll();
            _txtAdminPassword.Focus();
        }
    }
}
