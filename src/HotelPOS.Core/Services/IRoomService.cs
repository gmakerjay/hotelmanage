using HotelPOS.Common;
using HotelPOS.Common.Models;

namespace HotelPOS.Core.Services;

public interface IRoomService
{
    Task<IEnumerable<RoomType>> GetRoomTypesAsync(bool activeOnly = true);
    Task<RoomType?> GetRoomTypeByIdAsync(int id);
    Task<int> SaveRoomTypeAsync(RoomType roomType);
    Task DeleteRoomTypeAsync(int id);

    Task<IEnumerable<Room>> GetRoomsAsync(string? floor = null, int? roomTypeId = null, RoomStatus? status = null);
    Task<Room?> GetRoomByIdAsync(int id);
    Task<int> SaveRoomAsync(Room room);
    Task UpdateRoomStatusAsync(int roomId, RoomStatus status, string? notes = null);
    Task DeleteRoomAsync(int id);
    Task<IEnumerable<string>> GetFloorsAsync();
}
