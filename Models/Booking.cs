namespace Staybnb.Models;

public class Booking
{
    public int Id { get; set; }

    public int PropertyId { get; set; }
    [ForeignKey(nameof(PropertyId))]
    public HostProperty Property { get; set; }

    [Required]
    public string GuestId { get; set; }
    [ForeignKey(nameof(GuestId))]
    public ApplicationUser Guest { get; set; }

    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }
    public int NumberOfGuests { get; set; } = 1;

    [Column(TypeName = "decimal(18,2)")] public decimal NightlyRate { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal CleaningFee { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal ServiceFee { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal TotalPrice { get; set; }

    public BookingStatus Status { get; set; } = BookingStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public Payment Payment { get; set; }
    public GuestCheckIn GuestCheckIn { get; set; }

    [NotMapped]
    public int Nights => (CheckOutDate.Date - CheckInDate.Date).Days;
}

public class Payment
{
    public int Id { get; set; }

    public int BookingId { get; set; }
    [ForeignKey(nameof(BookingId))]
    public Booking Booking { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    public PaymentStatus Status { get; set; } = PaymentStatus.Paid;

    [StringLength(4)]
    public string CardLast4 { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
