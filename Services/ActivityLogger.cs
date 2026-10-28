namespace Staybnb.Services;

public interface IActivityLogger
{
    Task LogAsync(string userId, string action, ActivityType type, string details = null);
}

public class ActivityLogger : IActivityLogger
{
    private readonly ApplicationDbContext _db;
    public ActivityLogger(ApplicationDbContext db) => _db = db;

    public async Task LogAsync(string userId, string action, ActivityType type, string details = null)
    {
        _db.ActivityLogs.Add(new ActivityLog
        {
            UserId = userId,
            Action = action,
            ActivityType = type,
            Details = details != null && details.Length > 500 ? details[..500] : details,
            CreatedAt = DateTime.Now
        });
        await _db.SaveChangesAsync();
    }
}
