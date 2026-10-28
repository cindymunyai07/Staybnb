using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Staybnb.ViewModels;

public class RegisterViewModel
{
    [Required, StringLength(50), Display(Name = "First name")]
    public string FirstName { get; set; }

    [Required, StringLength(50), Display(Name = "Last name")]
    public string LastName { get; set; }

    [Required, EmailAddress]
    public string Email { get; set; }

    [Required, DataType(DataType.Password), StringLength(100, MinimumLength = 6)]
    public string Password { get; set; }

    [DataType(DataType.Password), Display(Name = "Confirm password")]
    [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
    public string ConfirmPassword { get; set; }
}

public class LoginViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; }

    [Required, DataType(DataType.Password)]
    public string Password { get; set; }

    [Display(Name = "Remember me")]
    public bool RememberMe { get; set; }

    public string ReturnUrl { get; set; }
}

public class PropertyListViewModel
{
    public List<HostProperty> Properties { get; set; } = new();
    public List<string> Cities { get; set; } = new();
    public string City { get; set; }
    public string Type { get; set; }
    public HashSet<int> WishlistIds { get; set; } = new();
}

public class PropertyDetailsViewModel
{
    public HostProperty Property { get; set; }
    public bool IsGuest { get; set; }
    public bool IsWishlisted { get; set; }
    public bool CanReview { get; set; }
    public int HostReviewCount { get; set; }
    public double HostRating { get; set; }
    public bool IsSuperhost => HostReviewCount >= 5 && HostRating >= 4.5;
}

public class CheckoutViewModel
{
    public int PropertyId { get; set; }
    public DateTime CheckIn { get; set; }
    public DateTime CheckOut { get; set; }
    public int Guests { get; set; }

    [Range(0.01, 100000000, ErrorMessage = "Enter the amount to pay.")]
    public decimal Amount { get; set; }

    [Required, StringLength(100), Display(Name = "Name on card")]
    public string CardholderName { get; set; }

    [Required, Display(Name = "Card number")]
    public string CardNumber { get; set; }

    [Required, Display(Name = "Expiry (MM/YY)")]
    public string Expiry { get; set; }

    [Required, Display(Name = "CVV")]
    public string Cvv { get; set; }

    [BindNever, ValidateNever] public HostProperty Property { get; set; }
    [BindNever, ValidateNever] public PriceBreakdown Price { get; set; }
}

public class PropertyFormViewModel
{
    public int? Id { get; set; }

    [Required, StringLength(100)]
    public string Title { get; set; }

    [Required, StringLength(4000)]
    public string Description { get; set; }

    [Range(1, 1000000, ErrorMessage = "Enter a price per night.")]
    [Display(Name = "Price per night (R)")]
    public decimal PricePerNight { get; set; }

    [Range(0, 1000000)]
    [Display(Name = "Cleaning fee (R)")]
    public decimal CleaningFee { get; set; }

    [Required, StringLength(300)]
    public string Address { get; set; }

    [Required, StringLength(100)]
    public string City { get; set; }

    [Required, Display(Name = "Property type")]
    public string PropertyType { get; set; }

    [Range(1, 50), Display(Name = "Max guests")]
    public int MaxGuests { get; set; } = 2;

    [Range(0, 50)]
    public int Bedrooms { get; set; } = 1;

    [Range(1, 50)]
    public int Beds { get; set; } = 1;

    [Range(1, 50)]
    public int Bathrooms { get; set; } = 1;

    public List<int> SelectedAmenityIds { get; set; } = new();

    [Display(Name = "Property images")]
    public List<IFormFile> Images { get; set; } = new();

    [BindNever, ValidateNever] public List<Amenity> AllAmenities { get; set; } = new();
    [BindNever, ValidateNever] public List<PropertyImage> ExistingImages { get; set; } = new();
}

public class HostDashboardViewModel
{
    public string HostName { get; set; }
    public int TotalProperties { get; set; }
    public int ActiveProperties { get; set; }
    public int TotalBookings { get; set; }
    public int BookingsThisMonth { get; set; }
    public decimal TotalEarnings { get; set; }
    public double AverageRating { get; set; }
    public int ReviewCount { get; set; }
    public int PendingRequests { get; set; }
    public int PendingDocuments { get; set; }
    public List<Booking> RecentBookings { get; set; } = new();
}

public class CheckInProcessViewModel
{
    public int PropertyId { get; set; }
    public string PropertyTitle { get; set; }

    [Required, StringLength(200)]
    public string Title { get; set; }

    [StringLength(1000)]
    public string Instructions { get; set; }

    [Display(Name = "House rules (one per line)")]
    public string RulesText { get; set; }

    public List<string> RequiredDocuments { get; set; } = new();
}

public class AdminDashboardViewModel
{
    public int TotalUsers { get; set; }
    public int PendingApplications { get; set; }
    public int ActiveProperties { get; set; }
    public decimal PlatformRevenue { get; set; }
    public int TotalBookings { get; set; }
    public int PendingBookings { get; set; }
    public int ApprovedHosts { get; set; }
    public List<ActivityLog> RecentActivity { get; set; } = new();
    public List<HostApplication> RecentApplications { get; set; } = new();
    public List<Booking> RecentBookings { get; set; } = new();
}

public class UserRowViewModel
{
    public ApplicationUser User { get; set; }
    public List<string> Roles { get; set; } = new();
}

public class LogsViewModel
{
    public List<ActivityLog> Logs { get; set; } = new();
    public string Search { get; set; }
    public ActivityType? Type { get; set; }
    public int Page { get; set; }
    public int TotalPages { get; set; }
}

public class ConversationItem
{
    public ApplicationUser Other { get; set; }
    public Message Last { get; set; }
    public int Unread { get; set; }
}

public class ThreadViewModel
{
    public ApplicationUser Other { get; set; }
    public List<Message> Messages { get; set; } = new();
}

public class ComposeViewModel
{
    public string ReceiverId { get; set; }

    [Required, StringLength(2000)]
    public string Content { get; set; }

    [BindNever, ValidateNever] public ApplicationUser Other { get; set; }
    [BindNever, ValidateNever] public List<ApplicationUser> Recipients { get; set; } = new();
}

public class NotificationsViewModel
{
    public List<Notification> Items { get; set; } = new();
    public Notification Selected { get; set; }
    public string Filter { get; set; }
    public int AllCount { get; set; }
    public int UnreadCount { get; set; }
    public int ReadCount { get; set; }
    public int TodayCount { get; set; }
}

public class NotificationBellViewModel
{
    public int UnreadNotifications { get; set; }
    public int UnreadMessages { get; set; }
}
