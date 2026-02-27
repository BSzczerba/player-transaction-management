using Application.DTOs;
using Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _svc;

    public NotificationsController(INotificationService svc) => _svc = svc;

    /// <summary>Get all notifications for the current user</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<NotificationDto>), 200)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var notifications = await _svc.GetByUserAsync(userId, ct);
        return Ok(notifications);
    }

    /// <summary>Get unread notifications for the current user</summary>
    [HttpGet("unread")]
    [ProducesResponseType(typeof(IEnumerable<NotificationDto>), 200)]
    public async Task<IActionResult> GetUnread(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var notifications = await _svc.GetUnreadByUserAsync(userId, ct);
        return Ok(notifications);
    }

    /// <summary>Get unread notification count</summary>
    [HttpGet("unread/count")]
    [ProducesResponseType(typeof(int), 200)]
    public async Task<IActionResult> GetUnreadCount(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var count = await _svc.GetUnreadCountAsync(userId, ct);
        return Ok(new { count });
    }

    /// <summary>Mark a notification as read</summary>
    [HttpPost("{id:guid}/read")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> MarkAsRead(Guid id, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        await _svc.MarkAsReadAsync(id, userId, ct);
        return NoContent();
    }

    /// <summary>Mark all notifications as read</summary>
    [HttpPost("read-all")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        await _svc.MarkAllAsReadAsync(userId, ct);
        return NoContent();
    }

    private Guid GetCurrentUserId()
        => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
