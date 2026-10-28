namespace Staybnb.Controllers;

[Authorize]
public class NotificationsController : Controller
{
    private readonly ApplicationDbContext _db;
    public NotificationsController(ApplicationDbContext db) => _db = db;

    public async Task<IActionResult> Index(int? selectedId, string filter)
    {
        var uid = User.GetUserId();
        var all = await _db.Notifications.Where(n => n.UserId == uid).OrderByDescending(n => n.CreatedAt).ToListAsync();

        Notification selected = null;
        if (selectedId != null)
        {
            selected = all.FirstOrDefault(n => n.Id == selectedId);
            if (selected != null && !selected.IsRead)
            {
                selected.IsRead = true;
                await _db.SaveChangesAsync();
            }
        }

        var today = DateTime.Today;
        IEnumerable<Notification> items = filter switch
        {
            "unread" => all.Where(n => !n.IsRead),
            "read" => all.Where(n => n.IsRead),
            "today" => all.Where(n => n.CreatedAt >= today),
            _ => all
        };

        return View(new NotificationsViewModel
        {
            Items = items.ToList(),
            Selected = selected,
            Filter = filter,
            AllCount = all.Count,
            UnreadCount = all.Count(n => !n.IsRead),
            ReadCount = all.Count(n => n.IsRead),
            TodayCount = all.Count(n => n.CreatedAt >= today)
        });
    }

    [HttpPost]
    public async Task<IActionResult> MarkAllRead()
    {
        var uid = User.GetUserId();
        var unread = await _db.Notifications.Where(n => n.UserId == uid && !n.IsRead).ToListAsync();
        foreach (var n in unread) n.IsRead = true;
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var uid = User.GetUserId();
        var n = await _db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == uid);
        if (n != null)
        {
            _db.Notifications.Remove(n);
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }
}
