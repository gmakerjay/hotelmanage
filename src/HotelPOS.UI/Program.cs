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
            "HotelPOS", "logs");
        _logger = new AppLogger(logFolder, retentionDays: 90);
        LogContext.MachineId = Environment.MachineName;

        // ---------- 2) ดัก Exception ทุกจุดของโปรแกรม (SKILL.md ข้อ 7.6) ----------
        Application.ThreadException += (sender, args) =>
        {
            _logger.Fatal(LogCategory.System, "เกิดข้อผิดพลาดที่ไม่ได้ดักไว้ (UI Thread)", args.Exception);
            ShowFriendlyErrorAndContinue(args.Exception);
        };
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

        AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            _logger.Fatal(LogCategory.System, "เกิดข้อผิดพลาดร้ายแรงที่ไม่ได้ดักไว้ (AppDomain)", ex);
            MessageBox.Show(
                "โปรแกรมพบข้อผิดพลาดร้ายแรงและต้องปิดตัวลง กรุณาส่งไฟล์ log ให้ทีมซัพพอร์ต",
                "HotelPOS - ข้อผิดพลาดร้ายแรง",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        };

        try
        {
            // ---------- 3) เตรียมฐานข้อมูล (สร้างครั้งแรกถ้ายังไม่มี) ----------
            var connectionFactory = new DbConnectionFactory();
            var migrationRunner = new MigrationRunner(connectionFactory, _logger);
            migrationRunner.EnsureDatabaseIsReady();

            // ---------- 4) Composition Root แบบง่าย (ยังไม่ใช้ DI container เต็มรูปแบบ) ----------
            // เมื่อโปรเจคใหญ่ขึ้นในเฟสถัดไป ให้พิจารณาเปลี่ยนมาใช้ Microsoft.Extensions.DependencyInjection
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
                    "HotelPOS - ตรวจสอบลิขสิทธิ์",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (dialogResult == DialogResult.Yes)
                {
                    using var activationForm = new LicenseActivationForm(currentStatusText);
                    if (activationForm.ShowDialog() == DialogResult.OK)
                    {
                        // ตรวจสอบใหม่อีกครั้งหลังจากเปิดใช้งานสำเร็จ
                        licenseResult = LicenseManager.CheckLicense();
                    }
                }
            }
            else if (licenseResult.License != null && licenseResult.License.LicenseType == LicenseType.Trial)
            {
                _logger.Info(LogCategory.System, $"กำลังใช้งานโหมดทดลองใช้ (เหลืออีก {licenseResult.DaysRemaining} วัน)");
            }

            // ---------- 6) ยืนยันตัวตนผู้ใช้งาน (Login Authentication: admin / psoft123) ----------
            using (var loginForm = new LoginForm())
            {
                if (loginForm.ShowDialog() != DialogResult.OK)
                {
                    _logger.Info(LogCategory.System, "ผู้ใช้ยกเลิกการเข้าสู่ระบบ ปิดโปรแกรม");
                    return;
                }
                _logger.Info(LogCategory.System, $"เข้าสู่ระบบสำเร็จ โดยผู้ใช้: {loginForm.LoggedInUser}");
            }

            _logger.Info(LogCategory.System, "เริ่มโปรแกรม HotelPOS สำเร็จ กำลังเปิดหน้าหลัก");

            Application.Run(new MainForm(settingsService, _logger, licenseResult.Status, licenseResult.License, licenseResult.DaysRemaining));
        }
        catch (Exception ex)
        {
            _logger.Fatal(LogCategory.System, "โปรแกรมเปิดไม่สำเร็จตั้งแต่เริ่มต้น (startup failure)", ex);
            MessageBox.Show(
                $"โปรแกรมไม่สามารถเปิดได้ กรุณาตรวจสอบไฟล์ log ที่ {logFolder}\n\nรายละเอียด: {ex.Message}",
                "HotelPOS - เปิดโปรแกรมไม่สำเร็จ",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void ShowFriendlyErrorAndContinue(Exception ex)
    {
        MessageBox.Show(
            $"เกิดข้อผิดพลาดขึ้น แต่โปรแกรมยังทำงานต่อได้\n\nรายละเอียด: {ex.Message}\n\n" +
            "หากเกิดซ้ำ กรุณากด \"ส่งออก Log\" ในเมนูช่วยเหลือ แล้วส่งให้ทีมซัพพอร์ต",
            "HotelPOS - แจ้งเตือน",
            MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }
}
