namespace Staybnb.Models;

public class Review
{
    public int Id { get; set; }

    public int PropertyId { get; set; }
    [ForeignKey(nameof(PropertyId))]
    public HostProperty Property { get; set; }

    [Required]
    public string ReviewerId { get; set; }
    [ForeignKey(nameof(ReviewerId))]
    public ApplicationUser Reviewer { get; set; }

    [Range(1, 5)]
    public int Rating { get; set; }

    [StringLength(1000)]
    public string Comment { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public class WishlistItem
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; }
    [ForeignKey(nameof(UserId))]
    public ApplicationUser User { get; set; }

    public int PropertyId { get; set; }
    [ForeignKey(nameof(PropertyId))]
    public HostProperty Property { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public class Message
{
    public int Id { get; set; }

    [Required]
    public string SenderId { get; set; }
    [ForeignKey(nameof(SenderId))]
    public ApplicationUser Sender { get; set; }

    [Required]
    public string ReceiverId { get; set; }
    [ForeignKey(nameof(ReceiverId))]
    public ApplicationUser Receiver { get; set; }

    [Required, StringLength(2000)]
    public string Content { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.Now;
    public bool IsRead { get; set; }
}

public class Notification
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; }
    [ForeignKey(nameof(UserId))]
    public ApplicationUser User { get; set; }

    [Required, StringLength(100)]
    public string Title { get; set; }

    [StringLength(500)]
    public string Message { get; set; }

    public NotificationType Type { get; set; }

    public bool IsRead { get; set; }

    [StringLength(300)]
    public string Link { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public class ActivityLog
{
    public int Id { get; set; }

    public string UserId { get; set; }
    [ForeignKey(nameof(UserId))]
    public ApplicationUser User { get; set; }

    [Required, StringLength(100)]
    public string Action { get; set; }

    public ActivityType ActivityType { get; set; }

    [StringLength(500)]
    public string Details { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public class HostApplication
{
    public int Id { get; set; }

    [Required]
    public string ApplicationUserId { get; set; }
    [ForeignKey(nameof(ApplicationUserId))]
    public ApplicationUser ApplicationUser { get; set; }

    public int PropertyId { get; set; }
    [ForeignKey(nameof(PropertyId))]
    public HostProperty Property { get; set; }

    public ApplicationStatus Status { get; set; } = ApplicationStatus.Pending;
    public DateTime AppliedAt { get; set; } = DateTime.Now;

    public string ReviewedById { get; set; }
    public DateTime? ReviewedAt { get; set; }

    [StringLength(500)]
    public string RejectionReason { get; set; }
}
