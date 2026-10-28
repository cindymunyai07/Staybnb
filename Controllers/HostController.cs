namespace Staybnb.Controllers;

[Authorize(Roles = AppRoles.Host)]
public class HostController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IBookingService _bookings;
    private readonly IFileStorage _files;
    private readonly IActivityLogger _log;
    private readonly INotificationService _notify;

    private static readonly BookingStatus[] EarningStatuses = { BookingStatus.Approved, BookingStatus.CheckedIn, BookingStatus.Completed };

    public HostController(ApplicationDbContext db, UserManager<ApplicationUser> users, IBookingService bookings,
        IFileStorage files, IActivityLogger log, INotificationService notify)
    {
        _db = db;
        _users = users;
        _bookings = bookings;
        _files = files;
        _log = log;
        _notify = notify;
    }

    public async Task<IActionResult> Dashboard()
    {
        var uid = User.GetUserId();
        var me = await _users.GetUserAsync(User);
        var monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

        var properties = _db.HostProperties.Where(p => p.HostId == uid && p.IsApproved);
        var bookings = _db.Bookings.Where(b => b.Property.HostId == uid);
        var reviews = _db.Reviews.Where(r => r.Property.HostId == uid);

        var vm = new HostDashboardViewModel
        {
            HostName = me.FullName,
            TotalProperties = await properties.CountAsync(),
            ActiveProperties = await properties.CountAsync(p => p.IsActive),
            TotalBookings = await bookings.CountAsync(),
            BookingsThisMonth = await bookings.CountAsync(b => b.CreatedAt >= monthStart),
            TotalEarnings = await bookings.Where(b => EarningStatuses.Contains(b.Status)).SumAsync(b => b.TotalPrice - b.ServiceFee),
            ReviewCount = await reviews.CountAsync(),
            PendingRequests = await bookings.CountAsync(b => b.Status == BookingStatus.Pending),
            PendingDocuments = await _db.GuestDocuments.CountAsync(d => d.Status == DocumentStatus.Pending && d.GuestCheckIn.Booking.Property.HostId == uid),
            RecentBookings = await bookings.Include(b => b.Property).Include(b => b.Guest).OrderByDescending(b => b.CreatedAt).Take(5).ToListAsync()
        };
        vm.AverageRating = vm.ReviewCount > 0 ? await reviews.AverageAsync(r => r.Rating) : 0;
        return View(vm);
    }

    public async Task<IActionResult> Properties()
    {
        var uid = User.GetUserId();
        var list = await _db.HostProperties
            .Include(p => p.Images).Include(p => p.Bookings).Include(p => p.Reviews)
            .Where(p => p.HostId == uid).OrderByDescending(p => p.CreatedAt).ToListAsync();
        return View(list);
    }

    [HttpPost]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var property = await _db.HostProperties.FirstOrDefaultAsync(p => p.Id == id && p.HostId == User.GetUserId());
        if (property == null) return NotFound();
        if (!property.IsApproved)
        {
            TempData["Error"] = "This property is still awaiting admin approval.";
            return RedirectToAction(nameof(Properties));
        }

        property.IsActive = !property.IsActive;
        await _db.SaveChangesAsync();
        await _log.LogAsync(User.GetUserId(), property.IsActive ? "PropertyActivated" : "PropertyDeactivated", ActivityType.Property, $"'{property.Title}' is now {(property.IsActive ? "active" : "inactive")}.");
        TempData["Success"] = $"'{property.Title}' is now {(property.IsActive ? "active" : "inactive")}.";
        return RedirectToAction(nameof(Properties));
    }

    [HttpGet]
    public async Task<IActionResult> EditProperty(int id)
    {
        var property = await _db.HostProperties.Include(p => p.Amenities).Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == id && p.HostId == User.GetUserId());
        if (property == null) return NotFound();

        var vm = new PropertyFormViewModel
        {
            Id = property.Id, Title = property.Title, Description = property.Description, PricePerNight = property.PricePerNight,
            CleaningFee = property.CleaningFee, Address = property.Address, City = property.City, PropertyType = property.PropertyType,
            MaxGuests = property.MaxGuests, Bedrooms = property.Bedrooms, Beds = property.Beds, Bathrooms = property.Bathrooms,
            SelectedAmenityIds = property.Amenities.Select(a => a.Id).ToList()
        };
        await FillFormListsAsync(vm, property);
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> EditProperty(int id, PropertyFormViewModel vm)
    {
        var property = await _db.HostProperties.Include(p => p.Amenities).Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == id && p.HostId == User.GetUserId());
        if (property == null) return NotFound();

        if (!PropertyTypes.All.Contains(vm.PropertyType ?? ""))
            ModelState.AddModelError(nameof(vm.PropertyType), "Choose a property type.");

        var newImages = (vm.Images ?? new List<IFormFile>()).Where(f => f != null && f.Length > 0).ToList();
        foreach (var f in newImages)
        {
            var err = _files.ValidateImage(f);
            if (err != null) ModelState.AddModelError(nameof(vm.Images), err);
        }

        if (!ModelState.IsValid)
        {
            vm.Id = id;
            await FillFormListsAsync(vm, property);
            return View(vm);
        }

        property.Title = vm.Title.Trim();
        property.Description = vm.Description.Trim();
        property.PricePerNight = vm.PricePerNight;
        property.CleaningFee = vm.CleaningFee;
        property.Address = vm.Address.Trim();
        property.City = vm.City.Trim();
        property.PropertyType = vm.PropertyType;
        property.MaxGuests = vm.MaxGuests;
        property.Bedrooms = vm.Bedrooms;
        property.Beds = vm.Beds;
        property.Bathrooms = vm.Bathrooms;

        property.Amenities.Clear();
        foreach (var a in await _db.Amenities.Where(a => vm.SelectedAmenityIds.Contains(a.Id)).ToListAsync())
            property.Amenities.Add(a);
        foreach (var f in newImages)
            property.Images.Add(new PropertyImage { ImageUrl = await _files.SaveImageAsync(f) });

        await _db.SaveChangesAsync();
        await _log.LogAsync(User.GetUserId(), "PropertyUpdated", ActivityType.Property, $"Updated '{property.Title}'.");
        TempData["Success"] = "Property updated.";
        return RedirectToAction(nameof(Properties));
    }

    private async Task FillFormListsAsync(PropertyFormViewModel vm, HostProperty property)
    {
        vm.AllAmenities = await _db.Amenities.OrderBy(a => a.Name).ToListAsync();
        vm.ExistingImages = property.Images.ToList();
    }

    public async Task<IActionResult> Bookings()
    {
        var uid = User.GetUserId();
        var list = await _db.Bookings
            .Include(b => b.Property).Include(b => b.Guest).Include(b => b.Payment).Include(b => b.GuestCheckIn)
            .Where(b => b.Property.HostId == uid)
            .OrderByDescending(b => b.CreatedAt).ToListAsync();
        return View(list);
    }

    private Task<Booking> OwnedBookingAsync(int id) =>
        _db.Bookings.Include(b => b.Property).Include(b => b.Payment)
            .FirstOrDefaultAsync(b => b.Id == id && b.Property.HostId == User.GetUserId());

    [HttpPost]
    public Task<IActionResult> ApproveBooking(int id) => ChangeBookingAsync(id, BookingStatus.Approved, "Booking approved.");

    [HttpPost]
    public Task<IActionResult> RejectBooking(int id) => ChangeBookingAsync(id, BookingStatus.Rejected, "Booking rejected and the guest was refunded.");

    [HttpPost]
    public Task<IActionResult> CompleteBooking(int id) => ChangeBookingAsync(id, BookingStatus.Completed, "Stay marked as completed.");

    private async Task<IActionResult> ChangeBookingAsync(int id, BookingStatus status, string successMessage)
    {
        var booking = await OwnedBookingAsync(id);
        if (booking == null) return NotFound();

        var error = await _bookings.ChangeStatusAsync(booking, status, User.GetUserId());
        if (error != null) TempData["Error"] = error;
        else TempData["Success"] = successMessage;
        return RedirectToAction(nameof(Bookings));
    }

    [HttpGet]
    public async Task<IActionResult> ConfigureCheckIn(int propertyId)
    {
        var property = await _db.HostProperties.Include(p => p.CheckInProcess)
            .FirstOrDefaultAsync(p => p.Id == propertyId && p.HostId == User.GetUserId());
        if (property == null) return NotFound();

        var process = property.CheckInProcess;
        var config = process != null ? CheckInConfig.Parse(process.StepsJson) : CheckInConfig.Default();
        return View(new CheckInProcessViewModel
        {
            PropertyId = property.Id, PropertyTitle = property.Title,
            Title = process?.Title ?? "Standard Check-in",
            Instructions = config.Instructions,
            RulesText = string.Join(Environment.NewLine, config.Rules),
            RequiredDocuments = config.RequiredDocuments
        });
    }

    [HttpPost]
    public async Task<IActionResult> ConfigureCheckIn(CheckInProcessViewModel vm)
    {
        var property = await _db.HostProperties.Include(p => p.CheckInProcess)
            .FirstOrDefaultAsync(p => p.Id == vm.PropertyId && p.HostId == User.GetUserId());
        if (property == null) return NotFound();
        vm.PropertyTitle = property.Title;

        var docs = (vm.RequiredDocuments ?? new List<string>()).Where(d => CheckInConfig.DocumentOptions.Contains(d)).Distinct().ToList();
        if (docs.Count == 0) ModelState.AddModelError(nameof(vm.RequiredDocuments), "Select at least one accepted document type.");
        if (!ModelState.IsValid) return View(vm);

        var config = new CheckInConfig
        {
            Instructions = vm.Instructions?.Trim() ?? "",
            Rules = (vm.RulesText ?? "").Split('\n').Select(r => r.Trim()).Where(r => r.Length > 0).ToList(),
            RequiredDocuments = docs
        };

        if (property.CheckInProcess == null)
        {
            _db.CheckInProcesses.Add(new CheckInProcess { PropertyId = property.Id, Title = vm.Title.Trim(), StepsJson = config.ToJson() });
        }
        else
        {
            property.CheckInProcess.Title = vm.Title.Trim();
            property.CheckInProcess.StepsJson = config.ToJson();
        }
        await _db.SaveChangesAsync();
        await _log.LogAsync(User.GetUserId(), "CheckInProcessConfigured", ActivityType.Property, $"Check-in rules updated for '{property.Title}'.");
        TempData["Success"] = "Check-in process saved.";
        return RedirectToAction(nameof(Properties));
    }

    private IQueryable<GuestDocument> HostDocuments(string uid) =>
        _db.GuestDocuments
            .Include(d => d.GuestCheckIn).ThenInclude(c => c.Booking).ThenInclude(b => b.Property)
            .Include(d => d.GuestCheckIn).ThenInclude(c => c.Booking).ThenInclude(b => b.Guest)
            .Where(d => d.GuestCheckIn.Booking.Property.HostId == uid);

    public async Task<IActionResult> Documents()
    {
        var docs = await HostDocuments(User.GetUserId()).OrderBy(d => d.Status).ThenByDescending(d => d.UploadedAt).ToListAsync();
        return View(docs);
    }

    [HttpPost]
    public async Task<IActionResult> VerifyDocument(int id, bool approve, string note)
    {
        var uid = User.GetUserId();
        var doc = await HostDocuments(uid).FirstOrDefaultAsync(d => d.Id == id);
        if (doc == null) return NotFound();
        if (doc.Status != DocumentStatus.Pending)
        {
            TempData["Error"] = "This document has already been reviewed.";
            return RedirectToAction(nameof(Documents));
        }

        var checkIn = doc.GuestCheckIn;
        var booking = checkIn.Booking;
        if (booking.Status != BookingStatus.Approved)
        {
            TempData["Error"] = "This booking is no longer active, so its document can't be verified.";
            return RedirectToAction(nameof(Documents));
        }

        if (approve)
        {
            doc.Status = DocumentStatus.Approved;
            doc.ReviewNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
            checkIn.Status = CheckInStatus.Verified;
            await _db.SaveChangesAsync();
            await _log.LogAsync(uid, "GuestDocumentApproved", ActivityType.CheckIn, $"Booking #{booking.Id}: {doc.DocumentType} approved.");

            var error = await _bookings.ChangeStatusAsync(booking, BookingStatus.CheckedIn, uid);
            TempData[error == null ? "Success" : "Error"] = error ?? "Document approved. The guest is now checked in.";
        }
        else
        {
            doc.Status = DocumentStatus.Rejected;
            doc.ReviewNote = string.IsNullOrWhiteSpace(note) ? "Please upload a clearer copy." : note.Trim();
            // back one step so the guest can upload a new document
            checkIn.Status = CheckInStatus.RulesAccepted;
            await _db.SaveChangesAsync();
            await _log.LogAsync(uid, "GuestDocumentRejected", ActivityType.CheckIn, $"Booking #{booking.Id}: {doc.DocumentType} rejected.");
            await _notify.NotifyAsync(booking.GuestId, "Document Rejected",
                $"Your {doc.DocumentType} for '{booking.Property.Title}' was rejected: {doc.ReviewNote}", NotificationType.Warning, $"/Bookings/CheckIn/{booking.Id}");
            TempData["Success"] = "Document rejected. The guest can upload a new one.";
        }
        return RedirectToAction(nameof(Documents));
    }

    public async Task<IActionResult> Earnings()
    {
        var uid = User.GetUserId();
        var list = await _db.Bookings.Include(b => b.Property).Include(b => b.Guest)
            .Where(b => b.Property.HostId == uid && EarningStatuses.Contains(b.Status))
            .OrderByDescending(b => b.CreatedAt).ToListAsync();
        return View(list);
    }

    public async Task<IActionResult> Reviews()
    {
        var uid = User.GetUserId();
        var list = await _db.Reviews.Include(r => r.Property).Include(r => r.Reviewer)
            .Where(r => r.Property.HostId == uid).OrderByDescending(r => r.CreatedAt).ToListAsync();
        return View(list);
    }
}
