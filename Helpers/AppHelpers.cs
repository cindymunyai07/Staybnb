using System.Globalization;

namespace Staybnb.Helpers;

public static class AppRoles
{
    public const string Guest = "Guest";
    public const string Host = "Host";
    public const string Admin = "Admin";
    public const string SuperAdmin = "SuperAdmin";
    public const string Staff = "Admin,SuperAdmin";
    public const string GuestOrHost = "Guest,Host";

    public static readonly string[] All = { Guest, Host, Admin, SuperAdmin };
}

public static class UserExtensions
{
    public static string GetUserId(this ClaimsPrincipal principal) => principal.FindFirstValue(ClaimTypes.NameIdentifier);

    public static bool IsStaff(this ClaimsPrincipal principal) =>
        principal.IsInRole(AppRoles.Admin) || principal.IsInRole(AppRoles.SuperAdmin);
}

public static class UiHelpers
{
    public const string Placeholder = "/images/seed/ext_apartment_0.svg";

    public static string Rand(decimal value) => "R " + value.ToString("N0", CultureInfo.InvariantCulture).Replace(",", " ");

    public static string Money(decimal value) => "R " + value.ToString("N2", CultureInfo.InvariantCulture);

    public static string BadgeClass(string status) => status switch
    {
        "Pending" or "DocumentSubmitted" or "RulesAccepted" => "bg-warning-subtle text-warning-emphasis",
        "Approved" or "Verified" or "Paid" or "Completed" => "bg-success-subtle text-success-emphasis",
        "CheckedIn" => "bg-primary-subtle text-primary-emphasis",
        "Rejected" or "Cancelled" or "Refunded" => "bg-danger-subtle text-danger-emphasis",
        _ => "bg-secondary-subtle text-secondary-emphasis"
    };

    public static string TimeAgo(DateTime when)
    {
        var span = DateTime.Now - when;
        if (span.TotalMinutes < 1) return "just now";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
        if (span.TotalDays < 7) return $"{(int)span.TotalDays}d ago";
        return when.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);
    }
}

public record PriceBreakdown(int Nights, decimal PricePerNight, decimal Subtotal, decimal CleaningFee, decimal ServiceFee, decimal Total);

public static class PriceCalculator
{
    public const decimal ServiceFeeRate = 0.12m;

    public static PriceBreakdown Calculate(decimal pricePerNight, decimal cleaningFee, int nights)
    {
        // service fee is 12% of the nightly subtotal, rounded to cents
        var subtotal = pricePerNight * nights;
        var service = Math.Round(subtotal * ServiceFeeRate, 2, MidpointRounding.AwayFromZero);
        return new PriceBreakdown(nights, pricePerNight, subtotal, cleaningFee, service, subtotal + cleaningFee + service);
    }
}
