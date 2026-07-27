using System;
using HotelPOS.Common;
using HotelPOS.Licensing;

namespace HotelPOS.Core.Services;

public class LicenseStatusChangedEventArgs : EventArgs
{
    public LicenseStatus Status { get; }
    public LicenseFile? License { get; }
    public int DaysRemaining { get; }

    public LicenseStatusChangedEventArgs(LicenseStatus status, LicenseFile? license, int daysRemaining)
    {
        Status = status;
        License = license;
        DaysRemaining = daysRemaining;
    }
}

public interface ILicenseMonitorService : IDisposable
{
    event EventHandler<LicenseStatusChangedEventArgs>? LicenseStatusChanged;
    (LicenseStatus Status, LicenseFile? License, int DaysRemaining) CurrentState { get; }
    void StartMonitoring(int intervalMinutes = 30);
    void StopMonitoring();
    (LicenseStatus Status, LicenseFile? License, int DaysRemaining) CheckNow();
}
