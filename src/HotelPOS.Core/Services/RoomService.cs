using HotelPOS.Common;
using HotelPOS.Common.Models;
using HotelPOS.Data.Repositories;
using HotelPOS.Logging;

namespace HotelPOS.Core.Services;

public class RoomService : IRoomService
{
    private readonly IRoomRepository _roomRepository;
    private readonly IAppLogger _logger;

    public RoomService(IRoomRepository roomRepository, IAppLogger logger)
    {
        _roomRepository = roomRepository;
        _logger = logger;
    }

    public async Task<IEnumerable<RoomType>> GetRoomTypesAsync(bool activeOnly = true)
    {
        return await _roomRepository.GetRoomTypesAsync(activeOnly);
    }

    public async Task<RoomType?> GetRoomTypeByIdAsync(int id)
    {
        return await _roomRepository.GetRoomTypeByIdAsync(id);
    }

    public async Task<int> SaveRoomTypeAsync(RoomType roomType)
    {
        if (string.IsNullOrWhiteSpace(roomType.Name))
        {
            throw new ArgumentException("ชื่อประเภทห้องพักห้ามเป็นค่าว่าง");
        }
        return await _roomRepository.SaveRoomTypeAsync(roomType);
    }

    public async Task DeleteRoomTypeAsync(int id)
    {
        var assignedRooms = await _roomRepository.GetRoomsAsync(roomTypeId: id, activeOnly: false);
        if (assignedRooms.Any())
        {
            throw new InvalidOperationException($"ไม่สามารถลบประเภทห้องพักนี้ได้ เนื่องจากมีห้องพักจำนวน {assignedRooms.Count()} ห้องที่ใช้งานประเภทนี้อยู่ในระบบ");
        }
        await _roomRepository.DeleteRoomTypeAsync(id);
    }

    public async Task<IEnumerable<Room>> GetRoomsAsync(string? floor = null, int? roomTypeId = null, RoomStatus? status = null)
    {
        return await _roomRepository.GetRoomsAsync(floor, roomTypeId, status);
    }

    public async Task<Room?> GetRoomByIdAsync(int id)
    {
        return await _roomRepository.GetRoomByIdAsync(id);
    }

    public async Task<int> SaveRoomAsync(Room room)
    {
        if (string.IsNullOrWhiteSpace(room.RoomNumber))
        {
            throw new ArgumentException("เลขที่ห้องพักห้ามเป็นค่าว่าง");
        }
        if (room.RoomTypeId <= 0)
        {
            throw new ArgumentException("ต้องเลือกประเภทห้องพัก");
        }

        // ตรวจสอบว่าเลขห้องซ้ำกับห้องอื่นหรือไม่
        var existing = await _roomRepository.GetRoomByNumberAsync(room.RoomNumber.Trim());
        if (existing != null && existing.Id != room.Id)
        {
            throw new InvalidOperationException($"เลขห้อง '{room.RoomNumber}' มีในระบบแล้ว");
        }

        room.RoomNumber = room.RoomNumber.Trim();
        return await _roomRepository.SaveRoomAsync(room);
    }

    public async Task UpdateRoomStatusAsync(int roomId, RoomStatus status, string? notes = null)
    {
        var room = await _roomRepository.GetRoomByIdAsync(roomId);
        if (room == null)
        {
            throw new KeyNotFoundException($"ไม่พบห้องพัก ID={roomId}");
        }

        await _roomRepository.UpdateRoomStatusAsync(roomId, status, notes);
    }

    public async Task DeleteRoomAsync(int id)
    {
        var room = await _roomRepository.GetRoomByIdAsync(id);
        if (room == null) return;

        if (room.Status == RoomStatus.Occupied || room.Status == RoomStatus.Reserved)
        {
            var statusDesc = room.Status == RoomStatus.Occupied ? "มีผู้เข้าพักอยู่" : "มีการจองล่วงหน้า";
            throw new InvalidOperationException($"ไม่สามารถลบห้อง '{room.RoomNumber}' ได้ เนื่องจากห้องพักกำลัง{statusDesc}");
        }

        await _roomRepository.DeleteRoomAsync(id);
    }

    public async Task<IEnumerable<string>> GetFloorsAsync()
    {
        return await _roomRepository.GetFloorsAsync();
    }
}
