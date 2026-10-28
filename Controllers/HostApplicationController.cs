namespace Staybnb.Controllers;

[Authorize(Roles = AppRoles.GuestOrHost)]
public class HostApplicationController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IFileStorage _files;
    private readonly IActivityLogger _log;
    private readonly INotificationService _notify;

    public HostApplicationController(ApplicationDbContext db, IFileStorage files, IActivityLogger log, INotificationService notify)
    {
        _db = db;
        _files = files;
        _log = log;
        _notify = notify;
    }

    private async Task<bool> HasPendingApplicationAsync()
    {
        var uid = User.GetUserId();
        return User.IsInRole(AppRoles.Guest)
            && await _db.HostApplications.AnyAsync(a => a.ApplicationUserId == uid && a.Status == ApplicationStatus.Pending);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        if (await HasPendingApplicationAsync())
        {
            TempData["Error"] = "You already have an application under review.";
            return RedirectToAction(nameof(Status));
        }
        var vm = new PropertyFormViewModel();
        vm.AllAmenities = await _db.Amenities.OrderBy(a => a.Name).ToListAsync();
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> Create(PropertyFormViewModel vm)
    {
        if (await HasPendingApplicationAsync())
        {
            TempData["Error"] = "You already have an application under review.";
            return RedirectToAction(nameof(Status));
        }

        if (!PropertyTypes.All.Contains(vm.PropertyType ?? ""))
            ModelState.AddModelError(nameof(vm.PropertyType), "Choose a property type.");

        // no image, no application
        var files = (vm.Images ?? new List<IFormFile>()).Where(f => f != null && f.Length > 0).ToList();
        if (files.Count == 0)
            ModelState.AddModelError(nameof(vm.Images), "Upload at least one property image. An application cannot be submitted without one.");
        foreach (var f in files)
        {
            var err = _files.ValidateImage(f);
            if (err != null) ModelState.AddModelError(nameof(vm.Images), err);
        }

        if (!ModelState.IsValid)
        {
            vm.AllAmenities = await _db.Amenities.OrderBy(a => a.Name).ToListAsync();
            return View(vm);
        }

        var uid = User.GetUserId();
        var property = new HostProperty
        {
            Title = vm.Title.Trim(), Description = vm.Description.Trim(), PricePerNight = vm.PricePerNight, CleaningFee = vm.CleaningFee,
            Address = vm.Address.Trim(), City = vm.City.Trim(), PropertyType = vm.PropertyType, MaxGuests = vm.MaxGuests,
            Bedrooms = vm.Bedrooms, Beds = vm.Beds, Bathrooms = vm.Bathrooms, HostId = uid,
            IsActive = false, IsApproved = false, CreatedAt = DateTime.Now
        };
        property.Amenities = await _db.Amenities.Where(a => vm.SelectedAmenityIds.Contains(a.Id)).ToListAsync();
        foreach (var f in files)
        {
            property.Images.Add(new PropertyImage { ImageUrl = await _files.SaveImageAsync(f) });
        }

        _db.HostApplications.Add(new HostApplication { ApplicationUserId = uid, Property = property, Status = ApplicationStatus.Pending, AppliedAt = DateTime.Now });
        await _db.SaveChangesAsync();

        var isHost = User.IsInRole(AppRoles.Host);
        await _log.LogAsync(uid, isHost ? "AdditionalPropertyApplicationSubmitted" : "HostApplicationSubmitted", ActivityType.Application,
            $"Applied to host '{property.Title}' in {property.City}.");
        await _notify.NotifyAsync(uid, "Application Submitted", $"Your application for '{property.Title}' was received and is under review.", NotificationType.Application, "/HostApplication/Status");
        await _notify.NotifyAdminsAsync("New Host Application", $"A new application was submitted for '{property.Title}'.", NotificationType.Application, "/Admin/Applications");

        TempData["Success"] = "Application submitted! An admin will review it soon.";
        return isHost ? RedirectToAction("Properties", "Host") : RedirectToAction(nameof(Status));
    }

    public async Task<IActionResult> Status()
    {
        var uid = User.GetUserId();
        var applications = await _db.HostApplications
            .Include(a => a.Property).ThenInclude(p => p.Images)
            .Where(a => a.ApplicationUserId == uid)
            .OrderByDescending(a => a.AppliedAt).ToListAsync();
        return View(applications);
    }
}
