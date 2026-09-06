namespace HotelManagement.API.Models;

public class BulkCreateRoomsRequest
{
    public List<BulkRoomItem> Rooms { get; set; } = new();
}

public class BulkRoomItem
{
    /// <summary>1-based spreadsheet row number, for error reporting.</summary>
    public int Row { get; set; }
    public string? RoomNumber { get; set; }
    public string? RoomType { get; set; }
    public decimal? PricePerNight { get; set; }
    public string? Description { get; set; }
    public string? Status { get; set; }
    public List<string>? Amenities { get; set; }
}
