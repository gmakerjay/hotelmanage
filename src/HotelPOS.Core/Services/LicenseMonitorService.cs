using System;
using System.Threading;
using HotelPOS.Common;
using HotelPOS.Licensing;
using HotelPOS.Logging;

namespace HotelPOS.Core.Services;

public class LicenseMonitorService : ILicenseMonitorService
{
    private readonly string? _dbPath;
    private readonly string? _licenseDirectory;
    private readonly IAppLogger? _logger;
    private Timer? _timer;
    private bool _isDisposed;

    public event EventHandler<LicenseStatusChangedEventArgs>? LicenseStatusChanged;
    public (LicenseStatus Status, LicenseFile? License, int DaysRemaining) CurrentState { get; private set; }

    public LicenseMonitorService(string? dbPath = null, string? licenseDirectory = null, IAppLogger? logger = null)
    {
        _dbPath = dbPath;
        _licenseDirectory = licenseDirectory;
        _logger = logger;
    }

    public void StartMonitoring(int intervalMinutes = 30)
    {
        StopMonitoring();

        // ตรวจสอบทันทีหนึ่งรอบ
        CheckNow();

        // ตั้งเวลาสุ่มตรวจทุก intervalMinutes นาที
        var period = TimeSpan.FromMinutes(Math.Max(1, intervalMinutes));
        _timer = new Timer(OnTimerCallback, null, period, period);

        _logger?.Info(LogCategory.License, $"LicenseMonitorService เริ่มต้นการตรวจสอบเบื้องหลังทุก {intervalMinutes} นาที");
    }

    public void StopMonitoring()
    {
        _timer?.Dispose();
        _timer = null;
    }

    public (LicenseStatus Status, LicenseFile? License, int DaysRemaining) CheckNow()
    {
        var previousStatus = CurrentState.Status;
        var newState = LicenseManager.CheckLicense(_dbPath, _licenseDirectory);
        CurrentState = newState;

        if (previousStatus != newState.Status)
        {
            _logger?.Warning(LogCategory.License, $"พบการเปลี่ยนแปลงสถานะ License จาก {previousStatus} เป็น {newState.Status}");
            LicenseStatusChanged?.Invoke(this, new LicenseStatusChangedEventArgs(newState.Status, newState.License, newState.DaysRemaining));
        }

        return newState;
    }

    private void OnTimerCallback(object? state)
    {
        try
        {
            CheckNow();
        }
        catch (Exception ex)
        {
            _logger?.Error(LogCategory.License, "เกิดข้อผิดพลาดในการตรวจสอบ License เบื้องหลัง", ex);
        }
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            StopMonitoring();
            _isDisposed = true;
        }
    }
}
