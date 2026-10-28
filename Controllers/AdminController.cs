namespace Staybnb.Controllers;

[Authorize(Roles = AppRoles.Staff)]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IActivityLogger _log;
    private readonly INotificationService _notify;

    private const int PageSize = 25;

    public AdminController(ApplicationDbContext db, UserManager<ApplicationUser> users, IActivityLogger log, INotificationService notify)
    {
        _db = db;
        _users = users;
        _log = log;
        _notify = notify;
    }

    public async Task<IActionResult> Dashboard()
    {
        var revenueStatuses = new[] { BookingStatus.Approved, BookingStatus.CheckedIn, BookingStatus.Completed };
        var vm = new AdminDashboardViewModel
        {
            TotalUsers = await _users.Users.CountAsync(),
            PendingApplications = await _db.HostApplications.CountAsync(a => a.Status == ApplicationStatus.Pending),
            ActiveProperties = await _db.HostProperties.CountAsync(p => p.IsActive && p.IsApproved),
            PlatformRevenue = await _db.Bookings.Where(b => revenueStatuses.Contains(b.Status)).SumAsync(b => b.ServiceFee),
            TotalBookings = await _db.Bookings.CountAsync(),
            PendingBookings = await _db.Bookings.CountAsync(b => b.Status == BookingStatus.Pending),
            ApprovedHosts = (await _users.GetUsersInRoleAsync(AppRoles.Host)).Count,
            RecentActivity = await _db.ActivityLogs.Include(l => l.User).OrderByDescending(l => l.CreatedAt).Take(8).ToListAsync(),
            RecentApplications = await _db.HostApplications.Include(a => a.ApplicationUser).Include(a => a.Property).OrderByDescending(a => a.AppliedAt).Take(5).ToListAsync(),
            RecentBookings = await _db.Bookings.Include(b => b.Guest).Include(b => b.Property).OrderByDescending(b => b.CreatedAt).Take(5).ToListAsync()
        };
        return View(vm);
    }

    public async Task<IActionResult> Applications(ApplicationStatus? status)
    {
        IQueryable<HostApplication> query = _db.HostApplications.Include(a => a.ApplicationUser).Include(a => a.Property).ThenInclude(p => p.Images);
        if (status != null) query = query.Where(a => a.Status == status);
        ViewBag.Status = status;
        return View(await query.OrderBy(a => a.Status).ThenByDescending(a => a.AppliedAt).ToListAsync());
    }

    public async Task<IActionResult> ReviewApplication(int id)
    {
        var application = await _db.HostApplications
            .Include(a => a.ApplicationUser)
            .Include(a => a.Property).ThenInclude(p => p.Images)
            .Include(a => a.Property).ThenInclude(p => p.Amenities)
            .FirstOrDefaultAsync(a => a.Id == id);
        if (application == null) return NotFound();
        return View(application);
    }

    [HttpPost]
    public async Task<IActionResult> ApproveApplication(int id)
    {
        var adminId = User.GetUserId();
        var application = await _db.HostApplications.Include(a => a.Property).Include(a => a.ApplicationUser)
            .FirstOrDefaultAsync(a => a.Id == id);
        if (application == null) return NotFound();
        if (application.Status != ApplicationStatus.Pending)
        {
            TempData["Error"] = "This application has already been reviewed.";
            return RedirectToAction(nameof(Applications));
        }

        var applicant = application.ApplicationUser;
        if (await _users.IsInRoleAsync(applicant, AppRoles.Admin) || await _users.IsInRoleAsync(applicant, AppRoles.SuperAdmin))
        {
            TempData["Error"] = "This applicant is now an admin and cannot also become a host.";
            return RedirectToAction(nameof(Applications));
        }

        // approval and the role swap succeed or fail together
        await using var tx = await _db.Database.BeginTransactionAsync();

        application.Status = ApplicationStatus.Approved;
        application.ReviewedById = adminId;
        application.ReviewedAt = DateTime.Now;
        application.Property.IsApproved = true;
        application.Property.IsActive = true;

        if (!await _db.CheckInProcesses.AnyAsync(c => c.PropertyId == application.PropertyId))
        {
            _db.CheckInProcesses.Add(new CheckInProcess { PropertyId = application.PropertyId, Title = "Standard Check-in", StepsJson = CheckInConfig.Default().ToJson() });
        }
        await _db.SaveChangesAsync();

        // Host in, Guest out
        var roleChanged = false;
        if (!await _users.IsInRoleAsync(applicant, AppRoles.Host))
        {
            var add = await _users.AddToRoleAsync(applicant, AppRoles.Host);
            if (!add.Succeeded) return await FailApprovalAsync(tx, add);
            roleChanged = true;
        }
        if (await _users.IsInRoleAsync(applicant, AppRoles.Guest))
        {
            var remove = await _users.RemoveFromRoleAsync(applicant, AppRoles.Guest);
            if (!remove.Succeeded) return await FailApprovalAsync(tx, remove);
            roleChanged = true;
        }
        await tx.CommitAsync();

        await _log.LogAsync(adminId, "HostApplicationApproved", ActivityType.Application, $"Approved '{application.Property.Title}' for {applicant.Email}.");
        if (roleChanged)
            await _log.LogAsync(adminId, "RoleChanged", ActivityType.RoleChange, $"{applicant.Email}: Guest -> Host.");
        await _notify.NotifyAsync(applicant.Id, "Host Application Approved",
            $"Congratulations! Your application for '{application.Property.Title}' has been approved. You are now a Host.", NotificationType.Success, "/Host/Dashboard");

        TempData["Success"] = $"Approved. {applicant.FullName} is now a Host.";
        return RedirectToAction(nameof(Applications));
    }

    private async Task<IActionResult> FailApprovalAsync(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction tx, IdentityResult result)
    {
        await tx.RollbackAsync();
        TempData["Error"] = "Role change failed: " + string.Join("; ", result.Errors.Select(e => e.Description));
        return RedirectToAction(nameof(Applications));
    }

    [HttpPost]
    public async Task<IActionResult> RejectApplication(int id, string reason)
    {
        var adminId = User.GetUserId();
        var application = await _db.HostApplications.Include(a => a.Property).FirstOrDefaultAsync(a => a.Id == id);
        if (application == null) return NotFound();
        if (application.Status != ApplicationStatus.Pending)
        {
            TempData["Error"] = "This application has already been reviewed.";
            return RedirectToAction(nameof(Applications));
        }

        application.Status = ApplicationStatus.Rejected;
        application.ReviewedById = adminId;
        application.ReviewedAt = DateTime.Now;
        application.RejectionReason = string.IsNullOrWhiteSpace(reason) ? "Did not meet our listing standards." : reason.Trim();
        await _db.SaveChangesAsync();

        await _log.LogAsync(adminId, "HostApplicationRejected", ActivityType.Application, $"Rejected '{application.Property.Title}'.");
        await _notify.NotifyAsync(application.ApplicationUserId, "Host Application Rejected",
            $"Your application for '{application.Property.Title}' was rejected: {application.RejectionReason}", NotificationType.Warning, "/HostApplication/Status");

        TempData["Success"] = "Application rejected.";
        return RedirectToAction(nameof(Applications));
    }

    [Authorize(Roles = AppRoles.SuperAdmin)]
    public async Task<IActionResult> Users(string q)
    {
        IQueryable<ApplicationUser> query = _users.Users;
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(u => u.Email.Contains(term) || u.FirstName.Contains(term) || u.LastName.Contains(term));
        }
        var users = await query.OrderBy(u => u.Email).Take(100).ToListAsync();

        var ids = users.Select(u => u.Id).ToList();
        var roleRows = await (from ur in _db.UserRoles
                              join r in _db.Roles on ur.RoleId equals r.Id
                              where ids.Contains(ur.UserId)
                              select new { ur.UserId, r.Name }).ToListAsync();
        var rows = users.Select(u => new UserRowViewModel
        {
            User = u,
            Roles = roleRows.Where(x => x.UserId == u.Id).Select(x => x.Name).ToList()
        }).ToList();

        ViewBag.Search = q;
        return View(rows);
    }

    [HttpPost, Authorize(Roles = AppRoles.SuperAdmin)]
    public async Task<IActionResult> PromoteToAdmin(string id)
    {
        var target = await _users.FindByIdAsync(id);
        if (target == null) return NotFound();
        if (!await _users.IsInRoleAsync(target, AppRoles.Guest))
        {
            TempData["Error"] = "Only Guests can be promoted to Admin.";
            return RedirectToAction(nameof(Users));
        }

        await _users.RemoveFromRoleAsync(target, AppRoles.Guest);
        await _users.AddToRoleAsync(target, AppRoles.Admin);
        await _log.LogAsync(User.GetUserId(), "RoleChanged", ActivityType.RoleChange, $"{target.Email}: Guest -> Admin.");
        await _notify.NotifyAsync(target.Id, "You are now an Admin", "A SuperAdmin promoted you to Admin.", NotificationType.Success, "/Admin/Dashboard");
        TempData["Success"] = $"{target.FullName} is now an Admin.";
        return RedirectToAction(nameof(Users));
    }

    [HttpPost, Authorize(Roles = AppRoles.SuperAdmin)]
    public async Task<IActionResult> DemoteAdmin(string id)
    {
        var target = await _users.FindByIdAsync(id);
        if (target == null) return NotFound();
        if (target.Id == User.GetUserId() || await _users.IsInRoleAsync(target, AppRoles.SuperAdmin) || !await _users.IsInRoleAsync(target, AppRoles.Admin))
        {
            TempData["Error"] = "That user cannot be demoted.";
            return RedirectToAction(nameof(Users));
        }

        await _users.RemoveFromRoleAsync(target, AppRoles.Admin);
        await _users.AddToRoleAsync(target, AppRoles.Guest);
        await _log.LogAsync(User.GetUserId(), "RoleChanged", ActivityType.RoleChange, $"{target.Email}: Admin -> Guest.");
        await _notify.NotifyAsync(target.Id, "Role changed", "You are no longer an Admin.", NotificationType.Info, "/Properties");
        TempData["Success"] = $"{target.FullName} is now a Guest.";
        return RedirectToAction(nameof(Users));
    }

    public async Task<IActionResult> Bookings()
    {
        var list = await _db.Bookings.Include(b => b.Guest).Include(b => b.Property).Include(b => b.Payment)
            .OrderByDescending(b => b.CreatedAt).Take(200).ToListAsync();
        return View(list);
    }

    public async Task<IActionResult> Properties()
    {
        var list = await _db.HostProperties.Include(p => p.Host).Include(p => p.Images)
            .OrderByDescending(p => p.CreatedAt).ToListAsync();
        return View(list);
    }

    [HttpPost]
    public async Task<IActionResult> ToggleProperty(int id)
    {
        var property = await _db.HostProperties.FirstOrDefaultAsync(p => p.Id == id);
        if (property == null) return NotFound();
        if (!property.IsApproved)
        {
            TempData["Error"] = "Approve the application first.";
            return RedirectToAction(nameof(Properties));
        }

        property.IsActive = !property.IsActive;
        await _db.SaveChangesAsync();
        await _log.LogAsync(User.GetUserId(), property.IsActive ? "PropertyActivatedByAdmin" : "PropertyDeactivatedByAdmin", ActivityType.Property, $"'{property.Title}' is now {(property.IsActive ? "active" : "inactive")}.");
        if (!property.IsActive)
            await _notify.NotifyAsync(property.HostId, "Listing Deactivated", $"An admin deactivated '{property.Title}'.", NotificationType.Warning, "/Host/Properties");
        TempData["Success"] = "Property updated.";
        return RedirectToAction(nameof(Properties));
    }

    public async Task<IActionResult> Logs(string search, ActivityType? type, int page = 1)
    {
        IQueryable<ActivityLog> query = _db.ActivityLogs.Include(l => l.User);
        if (type != null) query = query.Where(l => l.ActivityType == type);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(l => l.Action.Contains(term) || l.Details.Contains(term) || l.User.Email.Contains(term));
        }

        var total = await query.CountAsync();
        var pages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        page = Math.Min(Math.Max(1, page), pages);

        return View(new LogsViewModel
        {
            Logs = await query.OrderByDescending(l => l.CreatedAt).Skip((page - 1) * PageSize).Take(PageSize).ToListAsync(),
            Search = search, Type = type, Page = page, TotalPages = pages
        });
    }
}
