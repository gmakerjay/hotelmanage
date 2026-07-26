using System.Threading.Tasks;

namespace HotelPOS.Core.Services;

public interface IBackupService
{
    string GetDatabasePath();
    Task<string> CreateBackupAsync(string? targetFilePath = null);
    Task RestoreBackupAsync(string sourceFilePath);
}
