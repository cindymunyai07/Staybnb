using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace Staybnb.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<HostProperty> HostProperties { get; set; }
    public DbSet<PropertyImage> PropertyImages { get; set; }
    public DbSet<Amenity> Amenities { get; set; }
    public DbSet<Booking> Bookings { get; set; }
    public DbSet<Payment> Payments { get; set; }
    public DbSet<Review> Reviews { get; set; }
    public DbSet<WishlistItem> WishlistItems { get; set; }
    public DbSet<Message> Messages { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<ActivityLog> ActivityLogs { get; set; }
    public DbSet<HostApplication> HostApplications { get; set; }
    public DbSet<CheckInProcess> CheckInProcesses { get; set; }
    public DbSet<GuestCheckIn> GuestCheckIns { get; set; }
    public DbSet<GuestDocument> GuestDocuments { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<HostProperty>()
            .HasMany(p => p.Amenities)
            .WithMany(a => a.Properties)
            .UsingEntity(j => j.ToTable("PropertyAmenities"));

        builder.Entity<Message>().HasOne(m => m.Sender).WithMany().HasForeignKey(m => m.SenderId);
        builder.Entity<Message>().HasOne(m => m.Receiver).WithMany().HasForeignKey(m => m.ReceiverId);

        builder.Entity<Booking>().HasOne(b => b.Payment).WithOne(p => p.Booking).HasForeignKey<Payment>(p => p.BookingId);
        builder.Entity<Booking>().HasOne(b => b.GuestCheckIn).WithOne(g => g.Booking).HasForeignKey<GuestCheckIn>(g => g.BookingId);
        builder.Entity<HostProperty>().HasOne(p => p.CheckInProcess).WithOne(c => c.Property).HasForeignKey<CheckInProcess>(c => c.PropertyId);

        builder.Entity<WishlistItem>().HasIndex(w => new { w.UserId, w.PropertyId }).IsUnique();

        builder.Entity<IdentityRole>().HasData(
            new IdentityRole { Id = "3f1c2a10-0001-4a5b-9c11-000000000001", Name = AppRoles.Guest, NormalizedName = "GUEST", ConcurrencyStamp = "3f1c2a10-0001-4a5b-9c11-00000000a001" },
            new IdentityRole { Id = "3f1c2a10-0002-4a5b-9c11-000000000002", Name = AppRoles.Host, NormalizedName = "HOST", ConcurrencyStamp = "3f1c2a10-0002-4a5b-9c11-00000000a002" },
            new IdentityRole { Id = "3f1c2a10-0003-4a5b-9c11-000000000003", Name = AppRoles.Admin, NormalizedName = "ADMIN", ConcurrencyStamp = "3f1c2a10-0003-4a5b-9c11-00000000a003" },
            new IdentityRole { Id = "3f1c2a10-0004-4a5b-9c11-000000000004", Name = AppRoles.SuperAdmin, NormalizedName = "SUPERADMIN", ConcurrencyStamp = "3f1c2a10-0004-4a5b-9c11-00000000a004" });

        builder.Entity<Amenity>().HasData(
            new Amenity { Id = 1, Name = "WiFi", IconClass = "bi-wifi" },
            new Amenity { Id = 2, Name = "Kitchen", IconClass = "bi-egg-fried" },
            new Amenity { Id = 3, Name = "Washer", IconClass = "bi-droplet" },
            new Amenity { Id = 4, Name = "Dryer", IconClass = "bi-wind" },
            new Amenity { Id = 5, Name = "Air Conditioning", IconClass = "bi-snow" },
            new Amenity { Id = 6, Name = "Heating", IconClass = "bi-thermometer-sun" },
            new Amenity { Id = 7, Name = "Free Parking", IconClass = "bi-p-circle" },
            new Amenity { Id = 8, Name = "Pool", IconClass = "bi-water" },
            new Amenity { Id = 9, Name = "TV", IconClass = "bi-tv" },
            new Amenity { Id = 10, Name = "Braai Area", IconClass = "bi-fire" },
            new Amenity { Id = 11, Name = "Workspace", IconClass = "bi-laptop" },
            new Amenity { Id = 12, Name = "Pet Friendly", IconClass = "bi-heart" });

        // Restrict everywhere, SQL Server complains about multiple cascade paths otherwise
        foreach (var fk in builder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
        {
            fk.DeleteBehavior = DeleteBehavior.Restrict;
        }
    }
}
