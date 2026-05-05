namespace HotelManagement.Domain.Enums;

public enum RoomStatus { Available = 1, Occupied = 2, Maintenance = 3 }
public enum RoomType { Standard = 1, Deluxe = 2, Suite = 3 }
public enum BookingStatus { Confirmed = 1, CheckedIn = 2, CheckedOut = 3, Cancelled = 4 }
public enum UserRole { SuperAdmin = 1, HotelAdmin = 2, Staff = 3 }
public enum TenantPlan { Basic = 1, Pro = 2, Enterprise = 3 }
