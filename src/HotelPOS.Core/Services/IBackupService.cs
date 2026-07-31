using System.Threading.Tasks;

namespace HotelPOS.Core.Services;

public interface IBackupService
{
    string GetDatabasePath();
    Task<string> CreateBackupAsync(string? targetFilePath = null);
    Task RestoreBackupAsync(string sourceFilePath);
    Task<(bool IsOk, string Message)> CheckAndOptimizeDatabaseAsync();
    Task<string?> AutoPerformRollingBackupAsync(int maxKeepBackups = 30);
}
