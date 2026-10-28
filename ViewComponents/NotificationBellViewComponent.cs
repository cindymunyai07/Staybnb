namespace Staybnb.ViewComponents;

public class NotificationBellViewComponent : ViewComponent
{
    private readonly ApplicationDbContext _db;
    public NotificationBellViewComponent(ApplicationDbContext db) => _db = db;

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var uid = UserClaimsPrincipal.GetUserId();
        if (uid == null) return Content(string.Empty);

        return View(new NotificationBellViewModel
        {
            UnreadNotifications = await _db.Notifications.CountAsync(n => n.UserId == uid && !n.IsRead),
            UnreadMessages = await _db.Messages.CountAsync(m => m.ReceiverId == uid && !m.IsRead)
        });
    }
}
