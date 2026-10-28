namespace Staybnb.Models;

public class ApplicationUser : IdentityUser
{
    [Required, StringLength(50)]
    public string FirstName { get; set; }

    [Required, StringLength(50)]
    public string LastName { get; set; }

    public string ProfilePictureUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public ICollection<HostProperty> HostProperties { get; set; } = new List<HostProperty>();
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();

    [NotMapped]
    public string FullName => $"{FirstName} {LastName}";
}
