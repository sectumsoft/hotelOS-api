using HotelManagement.Application.Common.Interfaces;
using HotelManagement.Domain.Entities;

namespace HotelManagement.Infrastructure.Services;

public class NotificationRecorder : INotificationRecorder
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantService _tenant;

    public NotificationRecorder(IApplicationDbContext db, ITenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task RecordAsync(string type, string title, string message,
        string? link = null, string? entityId = null, CancellationToken ct = default)
    {
        var tenantId = _tenant.TenantId;
        if (tenantId == Guid.Empty) return; // no tenant context (SuperAdmin, jobs) — nothing to attach to

        _db.Notifications.Add(new Notification
        {
            TenantId = tenantId,
            Type = type,
            Title = title,
            Message = message,
            Link = link,
            EntityId = entityId,
            ActorName = string.IsNullOrWhiteSpace(_tenant.TenantName) ? null : _tenant.TenantName,
            IsRead = false
        });

        try { await _db.SaveChangesAsync(ct); }
        catch { /* a notification must never break the actual operation */ }
    }
}
