namespace Staybnb.Models;

public static class PropertyTypes
{
    public static readonly string[] All = { "Apartment", "House", "Villa", "Cabin", "Cottage", "Beachfront", "Studio" };
}

public class HostProperty
{
    public int Id { get; set; }

    [Required, StringLength(100)]
    public string Title { get; set; }

    [Required, StringLength(4000)]
    public string Description { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal PricePerNight { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CleaningFee { get; set; }

    [Required]
    public string HostId { get; set; }
    [ForeignKey(nameof(HostId))]
    public ApplicationUser Host { get; set; }

    [Required, StringLength(300)]
    public string Address { get; set; }

    [Required, StringLength(100)]
    public string City { get; set; }

    [Required, StringLength(50)]
    public string PropertyType { get; set; }

    public int MaxGuests { get; set; } = 2;
    public int Bedrooms { get; set; } = 1;
    public int Beds { get; set; } = 1;
    public int Bathrooms { get; set; } = 1;

    public bool IsActive { get; set; }

    public bool IsApproved { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public ICollection<PropertyImage> Images { get; set; } = new List<PropertyImage>();
    public ICollection<Amenity> Amenities { get; set; } = new List<Amenity>();
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
    public CheckInProcess CheckInProcess { get; set; }

    [NotMapped]
    public double AverageRating => Reviews != null && Reviews.Count > 0 ? Reviews.Average(r => r.Rating) : 0;
}

public class PropertyImage
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    [ForeignKey(nameof(PropertyId))]
    public HostProperty Property { get; set; }

    [Required]
    public string ImageUrl { get; set; }
}

public class Amenity
{
    public int Id { get; set; }

    [Required, StringLength(50)]
    public string Name { get; set; }

    [StringLength(50)]
    public string IconClass { get; set; }

    public ICollection<HostProperty> Properties { get; set; } = new List<HostProperty>();
}
