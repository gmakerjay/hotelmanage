using HotelPOS.Common;
using HotelPOS.Core.Services;
using HotelPOS.Data;
using HotelPOS.Data.Repositories;
using HotelPOS.Logging;
using HotelPOS.Licensing;

namespace HotelPOS.UI;

internal static class Program
{
    // เก็บ logger ไว้ระดับ static เพื่อให้ Global Exception Handler เรียกใช้ได้แม้ DI container ยังไม่พร้อม
    private static IAppLogger? _logger;

    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--pdf-sample")
        {
            PdfGenerator.GenerateSamplePdfs();
            return;
        }

        ApplicationConfiguration.Initialize();

        // ---------- 1) ตั้งค่า Logger ก่อนสิ่งอื่นใด (ต้อง log ได้ตั้งแต่บรรทัดแรก) ----------
        var logFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PSoftRestRentManager", "logs");
        _logger = new AppLogger(logFolder, retentionDays: 90);
        LogContext.MachineId = Environment.MachineName;

        // ---------- 2) ดัก Exception ทุกจุดของโปรแกรมทุกซอกทุกมุม (UI, AppDomain, Async Tasks) ----------
        Application.ThreadException += (sender, args) =>
        {
            _logger.Fatal(LogCategory.System, "เกิดข้อผิดพลาดที่ไม่ได้ดักไว้ (UI Thread)", args.Exception);
            ShowDetailedErrorPopup(args.Exception, "เกิดข้อผิดพลาดในการทำงานของระบบ (UI Thread Exception)", logFolder);
        };
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

        AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
        {
            var ex = args.ExceptionObject as Exception ?? new Exception("AppDomain Unhandled Exception");
            _logger.Fatal(LogCategory.System, "เกิดข้อผิดพลาดร้ายแรงที่ไม่ได้ดักไว้ (AppDomain Unhandled)", ex);
            ShowDetailedErrorPopup(ex, "เกิดข้อผิดพลาดร้ายแรงในระดับแอปพลิเคชัน (AppDomain Unhandled Exception)", logFolder);
        };

        TaskScheduler.UnobservedTaskException += (sender, args) =>
        {
            _logger.Fatal(LogCategory.System, "เกิดข้อผิดพลาดใน Task ที่ไม่ได้สังเกต (Unobserved Task Exception)", args.Exception);
            args.SetObserved();
            ShowDetailedErrorPopup(args.Exception, "เกิดข้อผิดพลาดในขบวนการทำงานเบื้องหลัง (Background Task Exception)", logFolder);
        };

        try
        {
            // ---------- 3) เตรียมฐานข้อมูล (สร้างครั้งแรกถ้ายังไม่มี) ----------
            var connectionFactory = new DbConnectionFactory();
            var migrationRunner = new MigrationRunner(connectionFactory, _logger);
            migrationRunner.EnsureDatabaseIsReady();

            // ---------- 4) Composition Root แบบง่าย ----------
            ISettingsRepository settingsRepository = new SettingsRepository(connectionFactory, _logger);
            ISettingsService settingsService = new SettingsService(settingsRepository, _logger);

            // ---------- 5) ตรวจสอบลิขสิทธิ์การใช้งาน (License Verification) ----------
            var licenseResult = LicenseManager.CheckLicense();

            if (licenseResult.Status != LicenseStatus.Active)
            {
                var currentStatusText = licenseResult.Status == LicenseStatus.Expired 
                    ? "ใบอนุญาตหมดอายุแล้ว" 
                    : (licenseResult.Status == LicenseStatus.Invalid ? "ลิขสิทธิ์ไม่ถูกต้อง" : "ยังไม่ได้ลงทะเบียน");
                
                var dialogResult = MessageBox.Show(
                    $"ระบบยังไม่ได้ลงทะเบียนใช้งาน หรือลิขสิทธิ์หมดอายุ ({currentStatusText})\n\nคุณต้องการเปิดใช้งานรหัสลิขสิทธิ์ในตอนนี้หรือไม่?\n(หากยกเลิก โปรแกรมจะทำงานในโหมดจำกัดสิทธิ์ / อ่านอย่างเดียว)",
                    "PSoft Rest & Rent Manager - ตรวจสอบลิขสิทธิ์",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (dialogResult == DialogResult.Yes)
                {
                    using var activationForm = new LicenseActivationForm(currentStatusText);
                    if (activationForm.ShowDialog() == DialogResult.OK)
                    {
                        licenseResult = LicenseManager.CheckLicense();
                    }
                }
            }
            else if (licenseResult.License != null && licenseResult.License.LicenseType == LicenseType.Trial)
            {
                _logger.Info(LogCategory.System, $"กำลังใช้งานโหมดทดลองใช้ (เหลืออีก {licenseResult.DaysRemaining} วัน)");
            }

            // ---------- 5.5) ตั้งค่ารหัสผ่านผู้ดูแลระบบครั้งแรกเมื่อเปิดสิทธิ์หลัก ----------
            if (licenseResult.Status == LicenseStatus.Active && licenseResult.License != null && licenseResult.License.LicenseType != LicenseType.Trial)
            {
                var isPasswordSet = settingsService.GetAsync("is_custom_admin_password_set").GetAwaiter().GetResult();
                if (isPasswordSet != "1")
                {
                    using var pwdSetupForm = new AdminPasswordSetupForm(settingsService);
                    pwdSetupForm.ShowDialog();
                }
            }

            // ---------- 6) ยืนยันตัวตนผู้ใช้งาน (Login Authentication) ----------
            using (var loginForm = new LoginForm(settingsService))
            {
                if (loginForm.ShowDialog() != DialogResult.OK)
                {
                    _logger.Info(LogCategory.System, "ผู้ใช้ยกเลิกการเข้าสู่ระบบ ปิดโปรแกรม");
                    return;
                }
                _logger.Info(LogCategory.System, $"เข้าสู่ระบบสำเร็จ โดยผู้ใช้: {loginForm.LoggedInUser}");
            }

            _logger.Info(LogCategory.System, "เริ่มโปรแกรม PSoft Rest & Rent Manager สำเร็จ กำลังเปิดหน้าหลัก");

            Application.Run(new MainForm(settingsService, _logger, licenseResult.Status, licenseResult.License, licenseResult.DaysRemaining));
        }
        catch (Exception ex)
        {
            _logger.Fatal(LogCategory.System, "โปรแกรมเปิดไม่สำเร็จตั้งแต่เริ่มต้น (startup failure)", ex);
            ShowDetailedErrorPopup(ex, "โปรแกรมไม่สามารถเปิดได้ตั้งแต่เริ่มต้น (Application Startup Failure)", logFolder);
        }
    }

    public static void ShowDetailedErrorPopup(Exception ex, string userMessage, string? customLogFolder = null)
    {
        var logFolder = customLogFolder ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PSoftRestRentManager", "logs");

        using var errorDlg = new DetailedErrorDialog(ex, userMessage, logFolder);
        errorDlg.ShowDialog();
    }
}
