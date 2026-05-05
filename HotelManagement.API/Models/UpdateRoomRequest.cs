namespace HotelManagement.API.Models
{
    public class UpdateRoomRequest
    {
        public string RoomNumber { get; set; } = string.Empty;
        public string RoomType { get; set; } = string.Empty;
        public decimal PricePerNight { get; set; }
        public string? Description { get; set; }
        public string Status { get; set; } = string.Empty;
        public List<string> Amenities { get; set; } = new();
        public List<IFormFile> NewImages { get; set; } = new();      // new files to upload
        public List<string> DeleteImageUrls { get; set; } = new();   // existing URLs to delete
    }
}
