namespace Staybnb.Controllers;

[Authorize]
public class MessagesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IActivityLogger _log;
    private readonly INotificationService _notify;

    public MessagesController(ApplicationDbContext db, UserManager<ApplicationUser> users, IActivityLogger log, INotificationService notify)
    {
        _db = db;
        _users = users;
        _log = log;
        _notify = notify;
    }

    private async Task<bool> CanMessageAsync(ApplicationUser me, ApplicationUser other)
    {
        if (me.Id == other.Id) return false;
        var mine = await _users.GetRolesAsync(me);
        var theirs = await _users.GetRolesAsync(other);
        var meStaff = mine.Contains(AppRoles.Admin) || mine.Contains(AppRoles.SuperAdmin);
        var otherStaff = theirs.Contains(AppRoles.Admin) || theirs.Contains(AppRoles.SuperAdmin);
        // admins can talk to anyone, otherwise it has to be guest <-> host
        if (meStaff || otherStaff) return true;
        return (mine.Contains(AppRoles.Guest) && theirs.Contains(AppRoles.Host))
            || (mine.Contains(AppRoles.Host) && theirs.Contains(AppRoles.Guest));
    }

    public async Task<IActionResult> Inbox()
    {
        var uid = User.GetUserId();
        var messages = await _db.Messages.Include(m => m.Sender).Include(m => m.Receiver)
            .Where(m => m.SenderId == uid || m.ReceiverId == uid)
            .OrderByDescending(m => m.Timestamp).ToListAsync();

        var conversations = messages
            .GroupBy(m => m.SenderId == uid ? m.ReceiverId : m.SenderId)
            .Select(g =>
            {
                var last = g.First();
                return new ConversationItem
                {
                    Other = last.SenderId == uid ? last.Receiver : last.Sender,
                    Last = last,
                    Unread = g.Count(m => m.ReceiverId == uid && !m.IsRead)
                };
            })
            .OrderByDescending(c => c.Last.Timestamp).ToList();
        return View(conversations);
    }

    public async Task<IActionResult> Thread(string id)
    {
        var me = await _users.GetUserAsync(User);
        var other = await _users.FindByIdAsync(id);
        if (other == null) return NotFound();

        var messages = await _db.Messages
            .Where(m => (m.SenderId == me.Id && m.ReceiverId == id) || (m.SenderId == id && m.ReceiverId == me.Id))
            .OrderBy(m => m.Timestamp).ToListAsync();

        var unread = messages.Where(m => m.ReceiverId == me.Id && !m.IsRead).ToList();
        if (unread.Count > 0)
        {
            foreach (var m in unread) m.IsRead = true;
            await _db.SaveChangesAsync();
        }
        return View(new ThreadViewModel { Other = other, Messages = messages });
    }

    [HttpGet]
    public async Task<IActionResult> Compose(string to)
    {
        var me = await _users.GetUserAsync(User);
        var vm = new ComposeViewModel { ReceiverId = to };

        if (User.IsStaff())
        {
            vm.Recipients = await _users.Users.Where(u => u.Id != me.Id).OrderBy(u => u.Email).Take(300).ToListAsync();
            return View(vm);
        }

        var other = string.IsNullOrEmpty(to) ? null : await _users.FindByIdAsync(to);
        if (other == null || !await CanMessageAsync(me, other))
        {
            TempData["Error"] = "Use a 'Message' button on a property or booking to start a conversation.";
            return RedirectToAction(nameof(Inbox));
        }
        vm.Other = other;
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> Send(ComposeViewModel vm)
    {
        var me = await _users.GetUserAsync(User);
        var other = string.IsNullOrEmpty(vm.ReceiverId) ? null : await _users.FindByIdAsync(vm.ReceiverId);

        if (other == null || !await CanMessageAsync(me, other))
        {
            TempData["Error"] = "You cannot message that user.";
            return RedirectToAction(nameof(Inbox));
        }
        if (string.IsNullOrWhiteSpace(vm.Content) || vm.Content.Length > 2000)
        {
            TempData["Error"] = "Write a message of up to 2000 characters.";
            return RedirectToAction(nameof(Thread), new { id = other.Id });
        }

        _db.Messages.Add(new Message { SenderId = me.Id, ReceiverId = other.Id, Content = vm.Content.Trim(), Timestamp = DateTime.Now, IsRead = false });
        await _db.SaveChangesAsync();

        await _log.LogAsync(me.Id, "MessageSent", ActivityType.Message, $"Message sent to {other.Email}.");
        await _notify.NotifyAsync(other.Id, "New Message", $"{me.FullName} sent you a message.", NotificationType.Message, $"/Messages/Thread/{me.Id}");
        return RedirectToAction(nameof(Thread), new { id = other.Id });
    }
}
