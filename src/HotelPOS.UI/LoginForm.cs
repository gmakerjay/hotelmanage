using HotelPOS.Logging;

namespace HotelPOS.UI;

public class LoginForm : Form
{
    private TextBox _txtUsername = null!;
    private TextBox _txtPassword = null!;
    private Button _btnLogin = null!;
    private Button _btnCancel = null!;
    private Label _lblError = null!;

    public string LoggedInUser { get; private set; } = "admin";

    public LoginForm()
    {
        InitializeComponent();
        try
        {
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        }
        catch { }
    }

    private void InitializeComponent()
    {
        Text = "เข้าสู่ระบบ — โปรแกรมจัดการห้องพัก PSOFT";
        Width = 460;
        Height = 360;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(248, 249, 250);
        Font = new Font("Segoe UI", 10.5F, FontStyle.Regular);

        // Top Banner Panel
        var topPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 85,
            BackColor = Color.FromArgb(30, 41, 59) // Dark Slate
        };

        var lblIcon = new Label
        {
            Text = "🔒",
            Font = new Font("Segoe UI", 24F),
            ForeColor = Color.White,
            Location = new Point(20, 18),
            AutoSize = true
        };

        var lblHeader = new Label
        {
            Text = "โปรแกรมจัดการห้องพัก PSOFT",
            Font = new Font("Segoe UI", 13.5F, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(70, 18),
            AutoSize = true
        };

        var lblSubHeader = new Label
        {
            Text = "กรุณากรอกชื่อผู้ใช้และรหัสผ่านเพื่อเข้าใช้งาน",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
            ForeColor = Color.FromArgb(203, 213, 225),
            Location = new Point(72, 48),
            AutoSize = true
        };

        topPanel.Controls.AddRange(new Control[] { lblIcon, lblHeader, lblSubHeader });

        // Content Input Panel
        var lblUsername = new Label
        {
            Text = "ชื่อผู้ใช้ (Username):",
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            Location = new Point(40, 110),
            AutoSize = true
        };

        _txtUsername = new TextBox
        {
            Location = new Point(40, 135),
            Width = 365,
            Font = new Font("Segoe UI", 11F),
            Text = "admin"
        };

        var lblPassword = new Label
        {
            Text = "รหัสผ่าน (Password):",
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            Location = new Point(40, 175),
            AutoSize = true
        };

        _txtPassword = new TextBox
        {
            Location = new Point(40, 200),
            Width = 365,
            Font = new Font("Segoe UI", 11F),
            UseSystemPasswordChar = true,
            Text = ""
        };

        _lblError = new Label
        {
            Text = "",
            ForeColor = Color.Crimson,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            Location = new Point(40, 235),
            AutoSize = true
        };

        _btnLogin = new Button
        {
            Text = "🔑 เข้าสู่ระบบ",
            Location = new Point(40, 260),
            Size = new Size(180, 42),
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            BackColor = Color.FromArgb(16, 185, 129), // Emerald Green
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        _btnLogin.FlatAppearance.BorderSize = 0;
        _btnLogin.Click += BtnLogin_Click;

        _btnCancel = new Button
        {
            Text = "❌ ปิดโปรแกรม",
            Location = new Point(225, 260),
            Size = new Size(180, 42),
            Font = new Font("Segoe UI", 10.5F),
            BackColor = Color.FromArgb(226, 232, 240),
            ForeColor = Color.FromArgb(51, 65, 85),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        _btnCancel.FlatAppearance.BorderSize = 0;
        _btnCancel.Click += (s, e) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        AcceptButton = _btnLogin;
        CancelButton = _btnCancel;

        Controls.AddRange(new Control[]
        {
            topPanel,
            lblUsername, _txtUsername,
            lblPassword, _txtPassword,
            _lblError,
            _btnLogin, _btnCancel
        });
    }

    private void BtnLogin_Click(object? sender, EventArgs e)
    {
        string username = _txtUsername.Text.Trim();
        string password = _txtPassword.Text.Trim();

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            _lblError.Text = "⚠️ กรุณากรอกชื่อผู้ใช้และรหัสผ่านให้ครบถ้วน";
            return;
        }

        // ตรวจสอบชื่อผู้ใช้และรหัสผ่านเริ่มต้น (admin / psoft123)
        if (username.Equals("admin", StringComparison.OrdinalIgnoreCase) && password == "psoft123")
        {
            LoggedInUser = username;
            DialogResult = DialogResult.OK;
            Close();
        }
        else
        {
            _lblError.Text = "❌ ชื่อผู้ใช้หรือรหัสผ่านไม่ถูกต้อง (ตัวอย่าง: admin / psoft123)";
            _txtPassword.SelectAll();
            _txtPassword.Focus();
        }
    }
}
