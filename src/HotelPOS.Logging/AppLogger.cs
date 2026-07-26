using HotelPOS.Common;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace HotelPOS.Logging;

/// <summary>
/// ค่า context ที่ต้องแนบไปกับ log ทุกบรรทัด
/// </summary>
public static class LogContext
{
    public static int? CurrentUserId { get; set; }
    public static string MachineId { get; set; } = Environment.MachineName;
}

public class AppLogger : IAppLogger, IDisposable
{
    private readonly ILogger _serilog;
    private readonly string _logFolderPath;

    /// <param name="logFolderPath">โฟลเดอร์เก็บไฟล์ log เช่น %AppData%\HotelPOS\logs</param>
    /// <param name="retentionDays">จำนวนวันเก็บ log ก่อนลบอัตโนมัติ (ดีฟอลต์ 90 วัน)</param>
    public AppLogger(string logFolderPath, int retentionDays = 90)
    {
        _logFolderPath = logFolderPath;

        // Create categorized sub-directories for clean organization
        var dirs = new[]
        {
            Path.Combine(logFolderPath, "errors"),
            Path.Combine(logFolderPath, "system"),
            Path.Combine(logFolderPath, "database"),
            Path.Combine(logFolderPath, "audit"),
            Path.Combine(logFolderPath, "json")
        };

        foreach (var dir in dirs)
        {
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }

        const string textOutputTemplate = "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] [{Category}] [CorrId:{CorrelationId}] [User:{UserId}] [Machine:{MachineId}] {Message:lj}{NewLine}{Exception}";

        _serilog = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Enrich.WithProperty("Application", "PSOFT_HotelRoomManager")

            // 1. Errors & Fatal Logs (แยกเก็บลง errors/error-.txt ขนาดไม่เกิน 5MB ต่อไฟล์)
            .WriteTo.Logger(lc => lc
                .Filter.ByIncludingOnly(e => e.Level >= LogEventLevel.Error)
                .WriteTo.File(
                    path: Path.Combine(logFolderPath, "errors", "error-.txt"),
                    rollingInterval: RollingInterval.Day,
                    fileSizeLimitBytes: 5 * 1024 * 1024, // 5MB max per log file
                    rollOnFileSizeLimit: true,
                    retainedFileCountLimit: retentionDays,
                    shared: true,
                    outputTemplate: textOutputTemplate))

            // 2. Database Logs (แยกเก็บลง database/db-.txt)
            .WriteTo.Logger(lc => lc
                .Filter.ByIncludingOnly(e => MatchingCategory(e, LogCategory.Database))
                .WriteTo.File(
                    path: Path.Combine(logFolderPath, "database", "db-.txt"),
                    rollingInterval: RollingInterval.Day,
                    fileSizeLimitBytes: 5 * 1024 * 1024,
                    rollOnFileSizeLimit: true,
                    retainedFileCountLimit: retentionDays,
                    shared: true,
                    outputTemplate: textOutputTemplate))

            // 3. Audit Trail Logs (แยกเก็บลง audit/audit-.txt สำหรับตรวจสอบการทำงานผู้ใช้)
            .WriteTo.Logger(lc => lc
                .Filter.ByIncludingOnly(e => MatchingCategory(e, LogCategory.Audit))
                .WriteTo.File(
                    path: Path.Combine(logFolderPath, "audit", "audit-.txt"),
                    rollingInterval: RollingInterval.Day,
                    fileSizeLimitBytes: 5 * 1024 * 1024,
                    rollOnFileSizeLimit: true,
                    retainedFileCountLimit: retentionDays,
                    shared: true,
                    outputTemplate: textOutputTemplate))

            // 4. System & General Logs (แยกเก็บลง system/system-.txt)
            .WriteTo.Logger(lc => lc
                .WriteTo.File(
                    path: Path.Combine(logFolderPath, "system", "system-.txt"),
                    rollingInterval: RollingInterval.Day,
                    fileSizeLimitBytes: 5 * 1024 * 1024,
                    rollOnFileSizeLimit: true,
                    retainedFileCountLimit: retentionDays,
                    shared: true,
                    outputTemplate: textOutputTemplate))

            // 5. Full Compact Structured JSON Log (สำหรับวิเคราะห์เชิงลึกด้วย Log Viewer)
            .WriteTo.File(
                new CompactJsonFormatter(),
                path: Path.Combine(logFolderPath, "json", "app-.json"),
                rollingInterval: RollingInterval.Day,
                fileSizeLimitBytes: 10 * 1024 * 1024,
                rollOnFileSizeLimit: true,
                retainedFileCountLimit: retentionDays,
                shared: true)
            .CreateLogger();
    }

    private static bool MatchingCategory(LogEvent logEvent, LogCategory expectedCategory)
    {
        if (logEvent.Properties.TryGetValue("Category", out var categoryProp) &&
            categoryProp is ScalarValue scalar &&
            scalar.Value is string catStr)
        {
            return catStr.Equals(expectedCategory.ToString(), StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }

    public void Dispose()
    {
        if (_serilog is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    public string NewCorrelationId() => Guid.NewGuid().ToString("N");

    public void Trace(LogCategory category, string message, string? correlationId = null)
        => Write(LogEventLevel.Verbose, category, message, null, correlationId);

    public void Debug(LogCategory category, string message, string? correlationId = null)
        => Write(LogEventLevel.Debug, category, message, null, correlationId);

    public void Info(LogCategory category, string message, string? correlationId = null)
        => Write(LogEventLevel.Information, category, message, null, correlationId);

    public void Warning(LogCategory category, string message, string? correlationId = null)
        => Write(LogEventLevel.Warning, category, message, null, correlationId);

    public void Error(LogCategory category, string message, Exception? exception = null, string? correlationId = null)
        => Write(LogEventLevel.Error, category, message, exception, correlationId);

    public void Fatal(LogCategory category, string message, Exception? exception = null, string? correlationId = null)
        => Write(LogEventLevel.Fatal, category, message, exception, correlationId);

    private void Write(LogEventLevel level, LogCategory category, string message, Exception? exception, string? correlationId)
    {
        _serilog
            .ForContext("Category", category.ToString())
            .ForContext("UserId", LogContext.CurrentUserId)
            .ForContext("MachineId", LogContext.MachineId)
            .ForContext("CorrelationId", correlationId ?? "-")
            .Write(level, exception, message);
    }
}
