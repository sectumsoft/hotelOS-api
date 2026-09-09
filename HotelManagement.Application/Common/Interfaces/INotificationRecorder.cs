namespace HotelManagement.Application.Common.Interfaces;

/// <summary>Records a header-bell activity item for the current tenant.</summary>
public interface INotificationRecorder
{
    Task RecordAsync(
        string type,
        string title,
        string message,
        string? link = null,
        string? entityId = null,
        CancellationToken ct = default);
}
