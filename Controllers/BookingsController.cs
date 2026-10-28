using System.Text.RegularExpressions;

namespace Staybnb.Controllers;

[Authorize(Roles = AppRoles.Guest)]
public class BookingsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IBookingService _bookings;
    private readonly IFileStorage _files;
    private readonly IActivityLogger _log;
    private readonly INotificationService _notify;

    public BookingsController(ApplicationDbContext db, IBookingService bookings, IFileStorage files,
        IActivityLogger log, INotificationService notify)
    {
        _db = db;
        _bookings = bookings;
        _files = files;
        _log = log;
        _notify = notify;
    }

    [HttpGet]
    public async Task<IActionResult> Checkout(int propertyId, DateTime checkIn, DateTime checkOut, int guests)
    {
        var error = await _bookings.ValidateRequestAsync(propertyId, checkIn, checkOut, guests);
        if (error != null)
        {
            TempData["Error"] = error;
            return RedirectToAction("Details", "Properties", new { id = propertyId });
        }

        var vm = new CheckoutViewModel { PropertyId = propertyId, CheckIn = checkIn.Date, CheckOut = checkOut.Date, Guests = guests };
        await FillCheckoutAsync(vm);
        // prefilled, but the server checks it again on submit
        vm.Amount = vm.Price.Total;
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> Checkout(CheckoutViewModel vm)
    {
        var digits = new string((vm.CardNumber ?? "").Where(char.IsDigit).ToArray());
        if (digits.Length < 13 || digits.Length > 19)
            ModelState.AddModelError(nameof(vm.CardNumber), "Enter a valid card number.");

        var expiry = (vm.Expiry ?? "").Trim();
        if (!Regex.IsMatch(expiry, @"^(0[1-9]|1[0-2])/\d{2}$"))
        {
            ModelState.AddModelError(nameof(vm.Expiry), "Use the format MM/YY.");
        }
        else
        {
            var expires = new DateTime(2000 + int.Parse(expiry.Substring(3, 2)), int.Parse(expiry.Substring(0, 2)), 1).AddMonths(1);
            if (expires <= DateTime.Today) ModelState.AddModelError(nameof(vm.Expiry), "This card has expired.");
        }

        if (!Regex.IsMatch((vm.Cvv ?? "").Trim(), @"^\d{3,4}$"))
            ModelState.AddModelError(nameof(vm.Cvv), "Enter the 3 or 4 digit CVV.");

        if (!await FillCheckoutAsync(vm)) return NotFound();
        if (!ModelState.IsValid) return View(vm);

        var result = await _bookings.CreateAsync(User.GetUserId(), vm.PropertyId, vm.CheckIn, vm.CheckOut, vm.Guests, vm.Amount, vm.CardNumber);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error);
            return View(vm);
        }

        TempData["Success"] = "Booking requested and payment received. The host will review your request.";
        return RedirectToAction(nameof(MyTrips));
    }

    private async Task<bool> FillCheckoutAsync(CheckoutViewModel vm)
    {
        var property = await _db.HostProperties.Include(p => p.Images).FirstOrDefaultAsync(p => p.Id == vm.PropertyId);
        if (property == null) return false;
        vm.Property = property;
        var nights = Math.Max(1, (vm.CheckOut.Date - vm.CheckIn.Date).Days);
        vm.Price = PriceCalculator.Calculate(property.PricePerNight, property.CleaningFee, nights);
        return true;
    }

    public async Task<IActionResult> MyTrips()
    {
        var uid = User.GetUserId();
        var trips = await _db.Bookings
            .Include(b => b.Property).ThenInclude(p => p.Images)
            .Include(b => b.Property).ThenInclude(p => p.Host)
            .Include(b => b.Payment).Include(b => b.GuestCheckIn)
            .Where(b => b.GuestId == uid)
            .OrderByDescending(b => b.CreatedAt).ToListAsync();
        return View(trips);
    }

    [HttpPost]
    public async Task<IActionResult> Cancel(int id)
    {
        var uid = User.GetUserId();
        var booking = await _db.Bookings.Include(b => b.Property).Include(b => b.Payment)
            .FirstOrDefaultAsync(b => b.Id == id && b.GuestId == uid);
        if (booking == null) return NotFound();

        var error = await _bookings.ChangeStatusAsync(booking, BookingStatus.Cancelled, uid);
        if (error != null) TempData["Error"] = error;
        else TempData["Success"] = "Booking cancelled and your payment was refunded.";
        return RedirectToAction(nameof(MyTrips));
    }

    private async Task<Booking> LoadBookingForCheckInAsync(int id)
    {
        var uid = User.GetUserId();
        return await _db.Bookings
            .Include(b => b.Property).ThenInclude(p => p.CheckInProcess)
            .Include(b => b.GuestCheckIn).ThenInclude(g => g.Documents)
            .FirstOrDefaultAsync(b => b.Id == id && b.GuestId == uid);
    }

    private async Task<GuestCheckIn> EnsureCheckInAsync(Booking booking)
    {
        var process = booking.Property.CheckInProcess;
        if (process == null)
        {
            process = new CheckInProcess { PropertyId = booking.PropertyId, Title = "Standard Check-in", StepsJson = CheckInConfig.Default().ToJson() };
            _db.CheckInProcesses.Add(process);
            await _db.SaveChangesAsync();
        }

        var checkIn = booking.GuestCheckIn;
        if (checkIn == null)
        {
            checkIn = new GuestCheckIn { BookingId = booking.Id, CheckInProcessId = process.Id, Status = CheckInStatus.NotStarted };
            _db.GuestCheckIns.Add(checkIn);
            await _db.SaveChangesAsync();
        }
        checkIn.Booking = booking;
        checkIn.CheckInProcess = process;
        return checkIn;
    }

    [HttpGet]
    public async Task<IActionResult> CheckIn(int id)
    {
        var booking = await LoadBookingForCheckInAsync(id);
        if (booking == null) return NotFound();
        if (booking.Status != BookingStatus.Approved && booking.Status != BookingStatus.CheckedIn)
        {
            TempData["Error"] = "Check-in opens once the host approves your booking.";
            return RedirectToAction(nameof(MyTrips));
        }
        return View(await EnsureCheckInAsync(booking));
    }

    [HttpPost]
    public async Task<IActionResult> AcceptRules(int id, bool accepted)
    {
        var booking = await LoadBookingForCheckInAsync(id);
        if (booking == null) return NotFound();
        if (booking.Status != BookingStatus.Approved) return RedirectToAction(nameof(MyTrips));

        if (!accepted)
        {
            TempData["Error"] = "Please tick the box to accept the house rules.";
            return RedirectToAction(nameof(CheckIn), new { id });
        }

        var checkIn = await EnsureCheckInAsync(booking);
        if (checkIn.Status == CheckInStatus.NotStarted)
        {
            checkIn.Status = CheckInStatus.RulesAccepted;
            checkIn.RulesAcceptedAt = DateTime.Now;
            await _db.SaveChangesAsync();
            await _log.LogAsync(User.GetUserId(), "CheckInRulesAccepted", ActivityType.CheckIn, $"Booking #{booking.Id}: house rules accepted.");
        }
        return RedirectToAction(nameof(CheckIn), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> UploadDocument(int id, string documentType, IFormFile document)
    {
        var booking = await LoadBookingForCheckInAsync(id);
        if (booking == null) return NotFound();
        if (booking.Status != BookingStatus.Approved) return RedirectToAction(nameof(MyTrips));

        var checkIn = await EnsureCheckInAsync(booking);
        if (checkIn.Status != CheckInStatus.RulesAccepted)
        {
            TempData["Error"] = checkIn.Status == CheckInStatus.NotStarted
                ? "Accept the house rules before uploading your document."
                : "Your document has already been submitted.";
            return RedirectToAction(nameof(CheckIn), new { id });
        }

        var config = CheckInConfig.Parse(checkIn.CheckInProcess.StepsJson);
        if (string.IsNullOrEmpty(documentType) || !config.RequiredDocuments.Contains(documentType))
        {
            TempData["Error"] = "Choose one of the accepted document types.";
            return RedirectToAction(nameof(CheckIn), new { id });
        }

        var error = _files.ValidateDocument(document);
        if (error != null)
        {
            TempData["Error"] = error;
            return RedirectToAction(nameof(CheckIn), new { id });
        }

        var stored = await _files.SaveDocumentAsync(document);
        _db.GuestDocuments.Add(new GuestDocument
        {
            GuestCheckInId = checkIn.Id, DocumentType = documentType, FileName = stored,
            Status = DocumentStatus.Pending, UploadedAt = DateTime.Now
        });
        checkIn.Status = CheckInStatus.DocumentSubmitted;
        await _db.SaveChangesAsync();

        await _log.LogAsync(User.GetUserId(), "GuestDocumentUploaded", ActivityType.CheckIn, $"Booking #{booking.Id}: {documentType} uploaded.");
        await _notify.NotifyAsync(booking.Property.HostId, "Document Awaiting Verification",
            $"A guest uploaded their {documentType} for '{booking.Property.Title}'.", NotificationType.CheckIn, "/Host/Documents");

        TempData["Success"] = "Document uploaded. The host will verify it shortly.";
        return RedirectToAction(nameof(CheckIn), new { id });
    }
}
