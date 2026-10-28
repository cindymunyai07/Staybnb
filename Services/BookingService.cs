using System.Data;

namespace Staybnb.Services;

public interface IBookingService
{
    Task<string> ValidateRequestAsync(int propertyId, DateTime checkIn, DateTime checkOut, int guests);
    Task<(bool Success, string Error, Booking Booking)> CreateAsync(string guestId, int propertyId, DateTime checkIn, DateTime checkOut, int guests, decimal amountPaid, string cardNumber);
    Task<string> ChangeStatusAsync(Booking booking, BookingStatus newStatus, string actorId);
}

public class BookingService : IBookingService
{
    private readonly ApplicationDbContext _db;
    private readonly IActivityLogger _log;
    private readonly INotificationService _notify;

    public BookingService(ApplicationDbContext db, IActivityLogger log, INotificationService notify)
    {
        _db = db;
        _log = log;
        _notify = notify;
    }

    public async Task<string> ValidateRequestAsync(int propertyId, DateTime checkIn, DateTime checkOut, int guests)
    {
        var property = await _db.HostProperties.AsNoTracking().FirstOrDefaultAsync(p => p.Id == propertyId);
        if (property == null || !property.IsActive || !property.IsApproved)
            return "This property is not available for booking.";
        if (checkIn.Date < DateTime.Today)
            return "Check-in date cannot be in the past.";
        if (checkOut.Date <= checkIn.Date)
            return "Check-out must be after check-in.";
        if (guests < 1 || guests > property.MaxGuests)
            return $"This property accommodates 1 to {property.MaxGuests} guests.";

        // block the dates if any live booking overlaps them
        var inDate = checkIn.Date;
        var outDate = checkOut.Date;
        var clash = await _db.Bookings.AnyAsync(b => b.PropertyId == propertyId
            && b.Status != BookingStatus.Rejected && b.Status != BookingStatus.Cancelled
            && b.CheckInDate < outDate && b.CheckOutDate > inDate);
        if (clash)
            return "Those dates are already booked. Please choose different dates.";

        return null;
    }

    public async Task<(bool Success, string Error, Booking Booking)> CreateAsync(string guestId, int propertyId,
        DateTime checkIn, DateTime checkOut, int guests, decimal amountPaid, string cardNumber)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        var error = await ValidateRequestAsync(propertyId, checkIn, checkOut, guests);
        if (error != null) return (false, error, null);

        var property = await _db.HostProperties.FirstAsync(p => p.Id == propertyId);
        if (property.HostId == guestId) return (false, "You cannot book your own property.", null);

        var nights = (checkOut.Date - checkIn.Date).Days;
        var price = PriceCalculator.Calculate(property.PricePerNight, property.CleaningFee, nights);

        // the simulated payment has to match the calculated total
        if (amountPaid != price.Total)
            return (false, $"Payment amount must equal the total price of {UiHelpers.Money(price.Total)}.", null);

        var digits = new string((cardNumber ?? "").Where(char.IsDigit).ToArray());
        var booking = new Booking
        {
            PropertyId = propertyId,
            GuestId = guestId,
            CheckInDate = checkIn.Date,
            CheckOutDate = checkOut.Date,
            NumberOfGuests = guests,
            NightlyRate = price.PricePerNight,
            CleaningFee = price.CleaningFee,
            ServiceFee = price.ServiceFee,
            TotalPrice = price.Total,
            Status = BookingStatus.Pending,
            CreatedAt = DateTime.Now,
            Payment = new Payment
            {
                Amount = amountPaid,
                Status = PaymentStatus.Paid,
                CardLast4 = digits.Length >= 4 ? digits[^4..] : digits,
                CreatedAt = DateTime.Now
            }
        };
        _db.Bookings.Add(booking);
        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        await _log.LogAsync(guestId, "BookingRequested", ActivityType.Booking,
            $"Booking #{booking.Id} for '{property.Title}' ({checkIn:yyyy-MM-dd} to {checkOut:yyyy-MM-dd}).");
        await _log.LogAsync(guestId, "PaymentProcessed", ActivityType.Payment,
            $"Simulated payment of {UiHelpers.Money(amountPaid)} for booking #{booking.Id}.");
        await _notify.NotifyAsync(property.HostId, "New Booking Request",
            $"A guest requested '{property.Title}' for {checkIn:dd MMM} - {checkOut:dd MMM}.", NotificationType.Booking, "/Host/Bookings");
        await _notify.NotifyAsync(guestId, "Booking Requested",
            $"Your request for '{property.Title}' was sent and payment of {UiHelpers.Money(amountPaid)} received. Waiting for the host.", NotificationType.Booking, "/Bookings/MyTrips");

        return (true, null, booking);
    }

    // the only moves a booking is allowed to make
    private static bool IsAllowed(BookingStatus current, BookingStatus next) => (current, next) switch
    {
        (BookingStatus.Pending, BookingStatus.Approved) => true,
        (BookingStatus.Pending, BookingStatus.Rejected) => true,
        (BookingStatus.Pending, BookingStatus.Cancelled) => true,
        (BookingStatus.Approved, BookingStatus.Cancelled) => true,
        (BookingStatus.Approved, BookingStatus.CheckedIn) => true,
        (BookingStatus.CheckedIn, BookingStatus.Completed) => true,
        _ => false
    };

    public async Task<string> ChangeStatusAsync(Booking booking, BookingStatus newStatus, string actorId)
    {
        if (!IsAllowed(booking.Status, newStatus))
            return $"A {booking.Status} booking cannot be changed to {newStatus}.";

        var old = booking.Status;
        booking.Status = newStatus;
        if ((newStatus == BookingStatus.Rejected || newStatus == BookingStatus.Cancelled) && booking.Payment != null)
            booking.Payment.Status = PaymentStatus.Refunded;
        await _db.SaveChangesAsync();

        var title = booking.Property?.Title ?? "your stay";
        await _log.LogAsync(actorId, "BookingStatusChanged", ActivityType.Booking, $"Booking #{booking.Id}: {old} -> {newStatus}.");

        switch (newStatus)
        {
            case BookingStatus.Approved:
                await _notify.NotifyAsync(booking.GuestId, "Booking Approved",
                    $"Your booking for '{title}' was approved. Complete your online check-in.", NotificationType.Booking, $"/Bookings/CheckIn/{booking.Id}");
                break;
            case BookingStatus.Rejected:
                await _notify.NotifyAsync(booking.GuestId, "Booking Declined",
                    $"The host declined your booking for '{title}'. Your payment was refunded.", NotificationType.Warning, "/Bookings/MyTrips");
                break;
            case BookingStatus.Cancelled:
                if (booking.Property != null && actorId == booking.GuestId)
                    await _notify.NotifyAsync(booking.Property.HostId, "Booking Cancelled",
                        $"A guest cancelled their booking for '{title}'.", NotificationType.Warning, "/Host/Bookings");
                break;
            case BookingStatus.CheckedIn:
                await _notify.NotifyAsync(booking.GuestId, "Check-in Complete",
                    $"Your documents for '{title}' were verified. Enjoy your stay!", NotificationType.CheckIn, "/Bookings/MyTrips");
                break;
            case BookingStatus.Completed:
                await _notify.NotifyAsync(booking.GuestId, "Stay Completed",
                    $"We hope you enjoyed '{title}'. Leave a review!", NotificationType.Info, $"/Properties/Details/{booking.PropertyId}");
                break;
        }
        return null;
    }
}
