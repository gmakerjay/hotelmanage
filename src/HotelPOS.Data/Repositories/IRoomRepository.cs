using HotelPOS.Common;
using HotelPOS.Common.Models;

namespace HotelPOS.Data.Repositories;

public interface IRoomRepository
{
    Task<IEnumerable<RoomType>> GetRoomTypesAsync(bool activeOnly = true);
    Task<RoomType?> GetRoomTypeByIdAsync(int id);
    Task<int> SaveRoomTypeAsync(RoomType roomType);
    Task DeleteRoomTypeAsync(int id);

    Task<IEnumerable<Room>> GetRoomsAsync(string? floor = null, int? roomTypeId = null, RoomStatus? status = null, bool activeOnly = true);
    Task<Room?> GetRoomByIdAsync(int id);
    Task<Room?> GetRoomByNumberAsync(string roomNumber);
    Task<int> SaveRoomAsync(Room room);
    Task UpdateRoomStatusAsync(int roomId, RoomStatus status, string? notes = null);
    Task DeleteRoomAsync(int id);
    Task<IEnumerable<string>> GetFloorsAsync();
}
