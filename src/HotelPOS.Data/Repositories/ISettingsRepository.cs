using HotelPOS.Common.Models;

namespace HotelPOS.Data.Repositories;

public interface ISettingsRepository
{
    Task<IEnumerable<AppSetting>> GetAllAsync();
    Task<AppSetting?> GetByKeyAsync(string key);
    Task UpsertAsync(string key, string? value);
}
