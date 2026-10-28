namespace Staybnb.Controllers;

public class PropertiesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly INotificationService _notify;
    private readonly IActivityLogger _log;

    public PropertiesController(ApplicationDbContext db, INotificationService notify, IActivityLogger log)
    {
        _db = db;
        _notify = notify;
        _log = log;
    }

    public async Task<IActionResult> Index(string city, string type)
    {
        // hosts and admins have their own dashboards
        if (User.IsInRole(AppRoles.Host) || User.IsStaff()) return RedirectToAction("Index", "Home");

        var active = _db.HostProperties.Where(p => p.IsActive && p.IsApproved);
        var cities = await active.Select(p => p.City).Distinct().OrderBy(c => c).ToListAsync();

        IQueryable<HostProperty> query = active.Include(p => p.Images).Include(p => p.Reviews).Include(p => p.Host);
        if (!string.IsNullOrWhiteSpace(city)) query = query.Where(p => p.City == city);
        if (!string.IsNullOrWhiteSpace(type)) query = query.Where(p => p.PropertyType == type);

        var vm = new PropertyListViewModel
        {
            Properties = await query.OrderByDescending(p => p.CreatedAt).ToListAsync(),
            Cities = cities,
            City = city,
            Type = type
        };

        if (User.IsInRole(AppRoles.Guest))
        {
            var uid = User.GetUserId();
            vm.WishlistIds = (await _db.WishlistItems.Where(w => w.UserId == uid).Select(w => w.PropertyId).ToListAsync()).ToHashSet();
        }
        return View(vm);
    }

    public async Task<IActionResult> Details(int id)
    {
        var property = await _db.HostProperties
            .Include(p => p.Images).Include(p => p.Amenities).Include(p => p.Host)
            .Include(p => p.CheckInProcess)
            .Include(p => p.Reviews).ThenInclude(r => r.Reviewer)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (property == null) return NotFound();

        var uid = User.GetUserId();
        var canPreview = User.IsStaff() || property.HostId == uid;
        if (!(property.IsActive && property.IsApproved) && !canPreview) return NotFound();

        var hostRatings = await _db.Reviews.Where(r => r.Property.HostId == property.HostId).Select(r => r.Rating).ToListAsync();
        var vm = new PropertyDetailsViewModel
        {
            Property = property,
            IsGuest = User.IsInRole(AppRoles.Guest),
            HostReviewCount = hostRatings.Count,
            HostRating = hostRatings.Count > 0 ? hostRatings.Average() : 0
        };
        if (vm.IsGuest)
        {
            vm.IsWishlisted = await _db.WishlistItems.AnyAsync(w => w.UserId == uid && w.PropertyId == id);
            var stayed = await _db.Bookings.AnyAsync(b => b.PropertyId == id && b.GuestId == uid
                && (b.Status == BookingStatus.CheckedIn || b.Status == BookingStatus.Completed));
            var reviewed = await _db.Reviews.AnyAsync(r => r.PropertyId == id && r.ReviewerId == uid);
            vm.CanReview = stayed && !reviewed;
        }
        return View(vm);
    }

    [HttpPost, Authorize(Roles = AppRoles.Guest)]
    public async Task<IActionResult> AddReview(int propertyId, int rating, string comment)
    {
        var uid = User.GetUserId();
        var stayed = await _db.Bookings.AnyAsync(b => b.PropertyId == propertyId && b.GuestId == uid
            && (b.Status == BookingStatus.CheckedIn || b.Status == BookingStatus.Completed));
        var reviewed = await _db.Reviews.AnyAsync(r => r.PropertyId == propertyId && r.ReviewerId == uid);
        if (!stayed || reviewed || rating < 1 || rating > 5)
        {
            TempData["Error"] = "You can only review a property once, after your check-in.";
            return RedirectToAction(nameof(Details), new { id = propertyId });
        }

        var property = await _db.HostProperties.FirstOrDefaultAsync(p => p.Id == propertyId);
        if (property == null) return NotFound();

        _db.Reviews.Add(new Review
        {
            PropertyId = propertyId, ReviewerId = uid, Rating = rating,
            Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim(), CreatedAt = DateTime.Now
        });
        await _db.SaveChangesAsync();
        await _notify.NotifyAsync(property.HostId, "New Review", $"A guest left a {rating}-star review on '{property.Title}'.", NotificationType.Info, "/Host/Reviews");
        TempData["Success"] = "Thanks for your review!";
        return RedirectToAction(nameof(Details), new { id = propertyId });
    }

    [HttpPost, Authorize(Roles = AppRoles.Guest)]
    public async Task<IActionResult> ToggleWishlist(int id, string returnUrl)
    {
        var uid = User.GetUserId();
        var item = await _db.WishlistItems.FirstOrDefaultAsync(w => w.UserId == uid && w.PropertyId == id);
        if (item != null)
        {
            _db.WishlistItems.Remove(item);
        }
        else if (await _db.HostProperties.AnyAsync(p => p.Id == id && p.IsActive && p.IsApproved))
        {
            _db.WishlistItems.Add(new WishlistItem { UserId = uid, PropertyId = id, CreatedAt = DateTime.Now });
        }
        await _db.SaveChangesAsync();

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) return LocalRedirect(returnUrl);
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Roles = AppRoles.Guest)]
    public async Task<IActionResult> Wishlist()
    {
        var uid = User.GetUserId();
        var items = await _db.WishlistItems.Where(w => w.UserId == uid)
            .Include(w => w.Property).ThenInclude(p => p.Images)
            .OrderByDescending(w => w.CreatedAt).ToListAsync();
        return View(items);
    }
}
