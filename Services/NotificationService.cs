namespace Staybnb.Services;

public interface INotificationService
{
    Task NotifyAsync(string userId, string title, string message, NotificationType type, string link = null);
    Task NotifyAdminsAsync(string title, string message, NotificationType type, string link = null);
}

public class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public NotificationService(ApplicationDbContext db, UserManager<ApplicationUser> users)
    {
        _db = db;
        _users = users;
    }

    public async Task NotifyAsync(string userId, string title, string message, NotificationType type, string link = null)
    {
        _db.Notifications.Add(new Notification
        {
            UserId = userId, Title = title, Message = message, Type = type, Link = link, CreatedAt = DateTime.Now
        });
        await _db.SaveChangesAsync();
    }

    public async Task NotifyAdminsAsync(string title, string message, NotificationType type, string link = null)
    {
        var admins = await _users.GetUsersInRoleAsync(AppRoles.Admin);
        var supers = await _users.GetUsersInRoleAsync(AppRoles.SuperAdmin);
        var ids = admins.Concat(supers).Select(u => u.Id).Distinct().ToList();
        foreach (var id in ids)
        {
            _db.Notifications.Add(new Notification { UserId = id, Title = title, Message = message, Type = type, Link = link, CreatedAt = DateTime.Now });
        }
        await _db.SaveChangesAsync();
    }
}
