using HotelManagement.Application.Common.Interfaces;
using HotelManagement.Application.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelManagement.API.Controllers;

public class NotificationDto
{
    public Guid Id { get; set; }
    public string Type { get; set; } = "";
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public string? Link { get; set; }
    public string? ActorName { get; set; }
    public bool IsRead { get; set; }
    public string CreatedAt { get; set; } = "";
}

public class NotificationFeedDto
{
    public int UnreadCount { get; set; }
    public List<NotificationDto> Items { get; set; } = new();
}

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantService _tenant;

    public NotificationsController(IApplicationDbContext db, ITenantService tenant)
    { _db = db; _tenant = tenant; }

    // GET /api/notifications — the 30 most recent, plus the unread count.
    [HttpGet]
    public async Task<ActionResult<ApiResponse<NotificationFeedDto>>> Get()
    {
        var tid = _tenant.TenantId;

        var recent = await _db.Notifications.AsNoTracking()
            .Where(n => n.TenantId == tid && !n.IsDeleted)
            .OrderByDescending(n => n.CreatedAt)
            .Take(30)
            .Select(n => new NotificationDto
            {
                Id = n.Id,
                Type = n.Type,
                Title = n.Title,
                Message = n.Message,
                Link = n.Link,
                ActorName = n.ActorName,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt.ToString("o")
            })
            .ToListAsync();

        var unread = await _db.Notifications
            .CountAsync(n => n.TenantId == tid && !n.IsDeleted && !n.IsRead);

        return Ok(ApiResponse<NotificationFeedDto>.Ok(new NotificationFeedDto
        {
            UnreadCount = unread,
            Items = recent
        }));
    }

    // POST /api/notifications/read — mark everything read.
    [HttpPost("read")]
    public async Task<ActionResult<ApiResponse<bool>>> MarkAllRead()
    {
        var tid = _tenant.TenantId;
        var unread = await _db.Notifications
            .Where(n => n.TenantId == tid && !n.IsRead && !n.IsDeleted)
            .ToListAsync();

        foreach (var n in unread) n.IsRead = true;
        if (unread.Count > 0) await _db.SaveChangesAsync(CancellationToken.None);

        return Ok(ApiResponse<bool>.Ok(true));
    }
}
