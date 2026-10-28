namespace Staybnb.Data;

public static class DataSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        await SeedCoreAsync(services);
        await CatalogueSeeder.SeedAsync(services);
        await Pics.RefreshAsync(services);
        await ReviewSeeder.SeedAsync(services);
    }

    private static async Task SeedCoreAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        if (await db.HostProperties.AnyAsync()) return;

        var admin = await RoleSeeder.EnsureUserAsync(userManager, "admin@staybnb.com", "Lunga", "Aphane", AppRoles.Admin);
        var host = await RoleSeeder.EnsureUserAsync(userManager, "host@staybnb.com", "Elsie", "Masuku", AppRoles.Host);
        var guest = await RoleSeeder.EnsureUserAsync(userManager, "guest@staybnb.com", "Banele", "Mthembu", AppRoles.Guest);
        var guest2 = await RoleSeeder.EnsureUserAsync(userManager, "guest2@staybnb.com", "Sboniso", "Mango", AppRoles.Guest);

        var amenities = await db.Amenities.ToListAsync();
        List<Amenity> Pick(params string[] names) => amenities.Where(a => names.Contains(a.Name)).ToList();

        HostProperty Make(string title, string city, string type, decimal price, decimal cleaning, int guests, int beds, int baths, string address,
                          string description, int imgA, int imgB, bool approved, string hostId, params string[] amenityNames)
        {
            var p = new HostProperty
            {
                Title = title, City = city, PropertyType = type, PricePerNight = price, CleaningFee = cleaning,
                MaxGuests = guests, Bedrooms = Math.Max(1, guests / 2), Beds = beds, Bathrooms = baths, Address = address,
                Description = description, HostId = hostId, IsApproved = approved, IsActive = approved,
                Amenities = Pick(amenityNames),
                Images = Pics.Gallery(type, imgA + imgB).Select(u => new PropertyImage { ImageUrl = u }).ToList()
            };
            return p;
        }

        // a handful of hand-written listings, the rest come from the catalogue below
        var p1 = Make("Luxury Apartment", "Cape Town", "Apartment", 1320, 250, 4, 2, 2, "12 Beach Road, Sea Point",
            "A bright, modern apartment a short walk from the promenade. Wake up to ocean views, cook in a fully equipped kitchen and unwind on the balcony.", 1, 2, true, host.Id,
            "WiFi", "Kitchen", "Air Conditioning", "TV", "Workspace", "Free Parking");
        var p2 = Make("Cozy Forest Cabin", "Durban", "Cabin", 3100, 450, 8, 5, 4, "8 Fern Valley Drive, Kloof",
            "Escape to our cosy forest cabin, tucked away among towering trees and birdsong. A peaceful retreat for guests seeking quiet, comfort and the essentials for a relaxing stay.", 3, 4, true, host.Id,
            "WiFi", "Kitchen", "Washer", "Dryer", "Air Conditioning", "Heating", "Braai Area");
        var p3 = Make("Exceptional Villa with Pool", "Drakensberg", "Villa", 2450, 600, 10, 6, 4, "Champagne Valley Road, Winterton",
            "A spacious mountain villa with a private pool and sweeping views of the Drakensberg. Perfect for family gatherings and group getaways.", 5, 6, true, host.Id,
            "WiFi", "Kitchen", "Pool", "Braai Area", "Heating", "Free Parking", "Pet Friendly");
        var p4 = Make("Beachfront Retreat", "Umhlanga", "Beachfront", 2100, 350, 6, 3, 3, "45 Lagoon Drive, Umhlanga Rocks",
            "Step straight from your door onto the sand. This beachfront retreat offers open-plan living, a large deck and sunrise views over the Indian Ocean.", 2, 4, true, host.Id,
            "WiFi", "Kitchen", "Air Conditioning", "TV", "Washer");
        var p5 = Make("Seaside Studio", "Cape Town", "Studio", 890, 150, 2, 1, 1, "3 Kloof Street, Gardens",
            "A compact, stylish studio in the heart of the city bowl, close to cafes, restaurants and the cable car.", 6, 1, false, guest2.Id,
            "WiFi", "Kitchen", "Workspace");

        db.HostProperties.AddRange(p1, p2, p3, p4, p5);
        await db.SaveChangesAsync();

        foreach (var p in new[] { p1, p2, p3, p4 })
        {
            db.CheckInProcesses.Add(new CheckInProcess { PropertyId = p.Id, Title = "Standard Check-in", StepsJson = CheckInConfig.Default().ToJson() });
        }

        foreach (var p in new[] { p1, p2, p3, p4 })
        {
            db.HostApplications.Add(new HostApplication
            {
                ApplicationUserId = host.Id, PropertyId = p.Id, Status = ApplicationStatus.Approved,
                AppliedAt = DateTime.Now.AddDays(-30), ReviewedById = admin.Id, ReviewedAt = DateTime.Now.AddDays(-29)
            });
        }
        db.HostApplications.Add(new HostApplication { ApplicationUserId = guest2.Id, PropertyId = p5.Id, Status = ApplicationStatus.Pending, AppliedAt = DateTime.Now.AddDays(-1) });

        var today = DateTime.Today;
        Booking NewBooking(HostProperty p, DateTime from, int nights, int guests, BookingStatus status)
        {
            var price = PriceCalculator.Calculate(p.PricePerNight, p.CleaningFee, nights);
            return new Booking
            {
                PropertyId = p.Id, GuestId = guest.Id, CheckInDate = from, CheckOutDate = from.AddDays(nights), NumberOfGuests = guests,
                NightlyRate = p.PricePerNight, CleaningFee = p.CleaningFee, ServiceFee = price.ServiceFee, TotalPrice = price.Total,
                Status = status, CreatedAt = DateTime.Now.AddDays(-1),
                Payment = new Payment { Amount = price.Total, Status = PaymentStatus.Paid, CardLast4 = "4242" }
            };
        }
        var pending = NewBooking(p2, today.AddDays(14), 2, 4, BookingStatus.Pending);
        var approvedBooking = NewBooking(p1, today.AddDays(5), 3, 2, BookingStatus.Approved);
        db.Bookings.AddRange(pending, approvedBooking);

        db.Notifications.Add(new Notification { UserId = host.Id, Title = "New Booking Request", Message = "Banele Mthembu requested Cozy Forest Cabin.", Type = NotificationType.Booking, Link = "/Host/Bookings" });
        db.Notifications.Add(new Notification { UserId = admin.Id, Title = "New Host Application", Message = "Sboniso Mango applied to host 'Seaside Studio'.", Type = NotificationType.Application, Link = "/Admin/Applications" });
        db.ActivityLogs.Add(new ActivityLog { UserId = guest2.Id, Action = "HostApplicationSubmitted", ActivityType = ActivityType.Application, Details = "Applied to host 'Seaside Studio'.", CreatedAt = DateTime.Now.AddDays(-1) });
        db.ActivityLogs.Add(new ActivityLog { UserId = guest.Id, Action = "BookingRequested", ActivityType = ActivityType.Booking, Details = "Requested Cozy Forest Cabin.", CreatedAt = DateTime.Now.AddDays(-1) });

        await db.SaveChangesAsync();
    }
}

internal static class CatalogueSeeder
{
    private record Row(string Title, string City, string Type, decimal Price, decimal Cleaning, int Guests, int Beds, int Baths, string Address);

    private static readonly Row[] Rows =
    {
        new("Camps Bay Sunset Villa", "Cape Town", "Villa", 5200, 900, 8, 5, 4, "21 Victoria Road, Camps Bay"),
        new("Bo-Kaap Colourful Cottage", "Cape Town", "Cottage", 1150, 200, 4, 2, 1, "14 Wale Street, Bo-Kaap"),
        new("Waterfront Harbour Studio", "Cape Town", "Studio", 980, 150, 2, 1, 1, "5 Dock Road, V&A Waterfront"),
        new("Clifton Beachfront Penthouse", "Cape Town", "Beachfront", 6400, 1000, 6, 3, 3, "2 Clifton Drive, Clifton"),
        new("Rosebank Loft Apartment", "Johannesburg", "Apartment", 1250, 200, 3, 2, 1, "88 Oxford Road, Rosebank"),
        new("Melville Garden Cottage", "Johannesburg", "Cottage", 850, 150, 2, 1, 1, "17 7th Street, Melville"),
        new("Parkhurst Family House", "Johannesburg", "House", 2200, 400, 7, 4, 3, "32 4th Avenue, Parkhurst"),
        new("Maboneng Industrial Studio", "Johannesburg", "Studio", 720, 120, 2, 1, 1, "9 Fox Street, Maboneng"),
        new("Sandton Executive Apartment", "Johannesburg", "Apartment", 1900, 300, 4, 2, 2, "101 Rivonia Road, Sandton"),
        new("Florida Road Studio", "Durban", "Studio", 790, 130, 2, 1, 1, "40 Florida Road, Morningside"),
        new("Morningside Family House", "Durban", "House", 1800, 300, 6, 3, 2, "12 Windermere Road, Morningside"),
        new("Berea Heritage Apartment", "Durban", "Apartment", 950, 150, 3, 2, 1, "7 Musgrave Road, Berea"),
        new("Glenwood Garden Cottage", "Durban", "Cottage", 1100, 180, 3, 2, 1, "23 Moore Road, Glenwood"),
        new("Umhlanga Skyline Apartment", "Umhlanga", "Apartment", 1750, 280, 4, 2, 2, "60 Lighthouse Road, Umhlanga"),
        new("Umhlanga Lagoon Cottage", "Umhlanga", "Cottage", 1300, 220, 4, 2, 2, "8 Lagoon Drive, Umhlanga"),
        new("Hatfield Compact Studio", "Pretoria", "Studio", 600, 100, 2, 1, 1, "15 Burnett Street, Hatfield"),
        new("Waterkloof Heritage House", "Pretoria", "House", 2100, 350, 8, 4, 3, "3 Rupert Street, Waterkloof"),
        new("Brooklyn Courtyard Apartment", "Pretoria", "Apartment", 1100, 180, 3, 2, 1, "27 Bronkhorst Street, Brooklyn"),
        new("Faerie Glen Garden Cabin", "Pretoria", "Cabin", 1400, 250, 4, 2, 1, "11 Hazel Road, Faerie Glen"),
        new("Winelands Vineyard Villa", "Stellenbosch", "Villa", 4300, 700, 10, 6, 5, "Helshoogte Road, Stellenbosch"),
        new("Dorp Street Cottage", "Stellenbosch", "Cottage", 1600, 250, 4, 2, 2, "44 Dorp Street, Stellenbosch"),
        new("Jonkershoek Mountain Cabin", "Stellenbosch", "Cabin", 1900, 300, 4, 2, 1, "Jonkershoek Road, Stellenbosch"),
        new("Summerstrand Beachfront Flat", "Gqeberha", "Beachfront", 1500, 250, 4, 2, 1, "18 Marine Drive, Summerstrand"),
        new("Walmer Family House", "Gqeberha", "House", 1300, 250, 6, 3, 2, "6 Main Road, Walmer"),
        new("Humewood Sea-View Studio", "Gqeberha", "Studio", 650, 100, 2, 1, 1, "2 Beach Road, Humewood"),
        new("Knysna Lagoon Cabin", "Knysna", "Cabin", 2300, 350, 4, 2, 1, "9 Thesen Island Road, Knysna"),
        new("Leisure Isle Villa", "Knysna", "Villa", 3900, 650, 8, 4, 3, "4 Bayswater Drive, Leisure Isle"),
        new("Knysna Waterfront Apartment", "Knysna", "Apartment", 1450, 220, 4, 2, 2, "Quay Street, Knysna Quays"),
        new("Whale Watcher Cottage", "Hermanus", "Cottage", 1800, 280, 4, 2, 2, "31 Marine Drive, Hermanus"),
        new("Voelklip Beach House", "Hermanus", "Beachfront", 3100, 450, 8, 4, 3, "12 Voelklip Drive, Hermanus"),
        new("Cathedral Peak Cabin", "Drakensberg", "Cabin", 1700, 300, 6, 3, 1, "Cathedral Peak Road, Winterton"),
        new("Berg Family Lodge", "Drakensberg", "House", 2700, 450, 10, 5, 3, "Sani Pass Road, Underberg"),
    };

    private static readonly Dictionary<string, string[]> TypeAmenities = new()
    {
        ["Apartment"] = new[] { "WiFi", "Kitchen", "Air Conditioning", "TV", "Workspace" },
        ["House"] = new[] { "WiFi", "Kitchen", "Washer", "Dryer", "Braai Area", "Free Parking" },
        ["Villa"] = new[] { "WiFi", "Kitchen", "Pool", "Braai Area", "Air Conditioning", "Free Parking" },
        ["Cabin"] = new[] { "WiFi", "Kitchen", "Heating", "Braai Area", "Pet Friendly" },
        ["Cottage"] = new[] { "WiFi", "Kitchen", "Heating", "Washer", "Pet Friendly" },
        ["Beachfront"] = new[] { "WiFi", "Kitchen", "Air Conditioning", "TV", "Free Parking" },
        ["Studio"] = new[] { "WiFi", "Kitchen", "Workspace", "TV" },
    };

    private static readonly Dictionary<string, string> Blurb = new()
    {
        ["Apartment"] = "A stylish apartment with everything you need for a comfortable city stay, close to restaurants, shops and transport.",
        ["House"] = "A spacious family house with plenty of room to spread out, a well-equipped kitchen and a relaxed outdoor area.",
        ["Villa"] = "A luxurious villa with generous living spaces, premium finishes and the privacy to unwind in style.",
        ["Cabin"] = "A cosy cabin surrounded by nature, ideal for a quiet escape with fresh air, birdsong and starry nights.",
        ["Cottage"] = "A charming cottage full of character, with a warm, homely feel and a peaceful garden setting.",
        ["Beachfront"] = "Wake up to the sound of the waves in this beachfront stay, just steps from the sand with open sea views.",
        ["Studio"] = "A smart, compact studio that is perfect for solo travellers and couples, with a comfortable bed and a handy kitchenette.",
    };

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        var users = services.GetRequiredService<UserManager<ApplicationUser>>();

        var hostA = await RoleSeeder.EnsureUserAsync(users, "host@staybnb.com", "Elsie", "Masuku", AppRoles.Host);
        var hostB = await RoleSeeder.EnsureUserAsync(users, "host2@staybnb.com", "Thabo", "Nkosi", AppRoles.Host);
        var admin = await RoleSeeder.EnsureUserAsync(users, "admin@staybnb.com", "Lunga", "Aphane", AppRoles.Admin);

        var amenities = await db.Amenities.ToListAsync();
        var existing = (await db.HostProperties.Select(p => p.Title).ToListAsync()).ToHashSet();
        var added = false;

        for (var i = 0; i < Rows.Length; i++)
        {
            var r = Rows[i];
            if (existing.Contains(r.Title)) continue;

            var host = i % 2 == 0 ? hostA : hostB;
            var names = TypeAmenities[r.Type];
            var property = new HostProperty
            {
                Title = r.Title, City = r.City, PropertyType = r.Type, PricePerNight = r.Price, CleaningFee = r.Cleaning,
                MaxGuests = r.Guests, Bedrooms = Math.Max(1, r.Guests / 2), Beds = r.Beds, Bathrooms = r.Baths,
                Address = r.Address, Description = $"{Blurb[r.Type]} Located in {r.City}.",
                HostId = host.Id, IsApproved = true, IsActive = true, CreatedAt = DateTime.Now.AddDays(-(i + 1)),
                Amenities = amenities.Where(a => names.Contains(a.Name)).ToList(),
                Images = Pics.Gallery(r.Type, i).Select(u => new PropertyImage { ImageUrl = u }).ToList(),
                CheckInProcess = new CheckInProcess { Title = "Standard Check-in", StepsJson = CheckInConfig.Default().ToJson() }
            };
            db.HostProperties.Add(property);
            db.HostApplications.Add(new HostApplication
            {
                ApplicationUserId = host.Id, Property = property, Status = ApplicationStatus.Approved,
                AppliedAt = DateTime.Now.AddDays(-40), ReviewedById = admin.Id, ReviewedAt = DateTime.Now.AddDays(-39)
            });
            added = true;
        }

        if (added) await db.SaveChangesAsync();
    }
}

internal static class Pics
{
    private static readonly Dictionary<string, string[]> Exterior = new()
    {
        ["Villa"] = new[] { "1613977257363-707ba9348227", "1706808849780-7a04fbac83ef", "1512917774080-9991f1c4c750", "1613977257365-aaae5a9817ff", "1582268611958-ebfd161ef9cf", "1593714604578-d9e41b00c6c6", "1711114378455-b1f479d94a19" },
        ["House"] = new[] { "1580587771525-78b9dba3b914", "1600596542815-ffad4c1539a9", "1613490493576-7fde63acd811", "1505843513577-22bb7d21e455", "1589129140837-67287c22521b", "1595483921906-7da233885832", "1656712193274-d391a185fde6" },
        ["Apartment"] = new[] { "1600596542815-ffad4c1539a9", "1580587771525-78b9dba3b914", "1505843513577-22bb7d21e455", "1656712193274-d391a185fde6", "1618168138573-fb0ca752e4b7", "1613490493576-7fde63acd811" },
        ["Cabin"] = new[] { "1570793005386-840846445fed", "1782392455081-a09db8267afb", "1697807650304-907257330a3e", "1575403071235-5dcd06cbf169", "1525113990976-399835c43838", "1622150736551-03defd21c5cd", "1595877244574-e90ce41ce089", "1604609165678-096d20fab1ad" },
        ["Cottage"] = new[] { "1591474200742-8e512e6f98f8", "1499696010180-025ef6e1a8f9", "1589129140837-67287c22521b", "1595877244574-e90ce41ce089", "1622150736551-03defd21c5cd", "1613490493576-7fde63acd811" },
        ["Beachfront"] = new[] { "1605352081508-2e09927ecfe3", "1551524164-687a55dd1126", "1544984243-ec57ea16fe25", "1499793983690-e29da59ef1c2", "1595483921906-7da233885832", "1618168138573-fb0ca752e4b7", "1604609165678-096d20fab1ad" },
        ["Studio"] = new[] { "1600596542815-ffad4c1539a9", "1505843513577-22bb7d21e455", "1656712193274-d391a185fde6", "1618168138573-fb0ca752e4b7", "1580587771525-78b9dba3b914" }
    };

    private static readonly string[] Living =
    {
        "1564078516393-cf04bd966897", "1613545325278-f24b0cae1224", "1613545325268-9265e1609167", "1682184805271-11671b7ecf4c",
        "1760072513367-55182245e76c", "1503174971373-b1f69850bded", "1772567732985-6fefa2dff56d", "1638972691611-69633a3d3127",
        "1590725175785-de025cc60835", "1700884597572-45ea007196a8"
    };

    private static readonly string[] CabinLiving =
    {
        "1781794902242-f8578908d640", "1631630259742-c0f0b17c6c10", "1773579089983-50348cc9abd0",
        "1774199679021-db3b421867c3", "1774311237295-a65a4c1ff38a"
    };

    private static readonly string[] Bedroom =
    {
        "1522771739844-6a9f6d5f14af", "1617325247661-675ab4b64ae2", "1699942681763-d1da9f692489", "1757344454333-cc666252e596",
        "1718717722247-26f4c6c09192", "1610307522657-8c0304960189", "1765862835193-3c37388a409e", "1760072513376-67a46aab0fd1",
        "1552558636-f6a8f071c2b3", "1765862835319-18fb6f8caff6", "1765862835260-47843a7bba45", "1765279333918-949ddcb655ba",
        "1775241186452-c3d99b09f223", "1771287490579-afd6e8d3c09a"
    };

    private static readonly string[] Kitchen =
    {
        "1507089947368-19c1da9775ae", "1778731660397-6a66493be391", "1713365829670-d8df1e593248",
        "1773578978637-c9771e7a9913", "1704902949428-65e05241291a"
    };

    private static string Url(string id) => $"https://images.unsplash.com/photo-{id}?auto=format&fit=crop&w=1200&q=75";

    // different strides per slot so neighbouring listings don't share the same combination
    public static List<string> Gallery(string type, int n, int rank = 0)
    {
        var ext = Exterior.TryGetValue(type, out var e) ? e : Exterior["House"];
        var living = type == "Cabin" ? CabinLiving : Living;
        return new List<string>
        {
            Url(ext[rank % ext.Length]),
            Url(living[(n * 3 + 1) % living.Length]),
            Url(Bedroom[(n * 5 + 2) % Bedroom.Length]),
            Url(Kitchen[(n * 2) % Kitchen.Length]),
            Url(ext[(rank + ext.Length / 2 + 1) % ext.Length]),
            Url(Bedroom[(n * 5 + 9) % Bedroom.Length]),
            Url(living[(n * 3 + 3) % living.Length])
        };
    }

    public static async Task RefreshAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        var seeded = await db.HostProperties.Include(p => p.Images).ToListAsync();
        var changed = false;

        // rank within each type, so covers only repeat once that type's pool is used up
        var rank = seeded.OrderBy(p => p.Id).GroupBy(p => p.PropertyType)
            .SelectMany(g => g.Select((p, k) => (p.Id, k))).ToDictionary(x => x.Id, x => x.k);

        foreach (var p in seeded)
        {
            // leave anything a host uploaded alone
            if (p.Images.Any(i => i.ImageUrl.StartsWith("/uploads/"))) continue;

            var urls = Gallery(p.PropertyType, p.Id, rank[p.Id]);
            var images = p.Images.OrderBy(i => i.Id).ToList();
            for (var k = 0; k < urls.Count; k++)
            {
                if (k < images.Count)
                {
                    if (images[k].ImageUrl != urls[k]) { images[k].ImageUrl = urls[k]; changed = true; }
                }
                else
                {
                    p.Images.Add(new PropertyImage { ImageUrl = urls[k] });
                    changed = true;
                }
            }
        }
        if (changed) await db.SaveChangesAsync();
    }
}

internal static class ReviewSeeder
{
    private static readonly (string First, string Last, string Email)[] Reviewers =
    {
        ("Nomsa", "Dlamini", "nomsa.dlamini@gmail.com"),
        ("Pieter", "van Wyk", "pieter.vanwyk@gmail.com"),
        ("Aisha", "Patel", "aisha.patel@gmail.com"),
        ("Sipho", "Khumalo", "sipho.khumalo@gmail.com"),
        ("Emma", "Roberts", "emma.roberts@gmail.com"),
        ("Thandi", "Mokoena", "thandi.mokoena@gmail.com"),
        ("Johan", "Botha", "johan.botha@gmail.com")
    };

    private static readonly string[] Comments =
    {
        "Lovely place, exactly as pictured. Check-in was easy and the host replied quickly.",
        "Spotless and very comfortable. We would happily stay again.",
        "Great location and close to everything. A little noisy on Friday night but nothing serious.",
        "The kitchen had everything we needed and the beds were really comfortable.",
        "Perfect for a weekend away. Communication with the host was excellent.",
        "Good value for money. The hot water took a minute to warm up, otherwise no complaints.",
        "Beautiful setting and so peaceful. We did not want to leave.",
        "Clean, quiet and well equipped. Parking was easy.",
        "The host was friendly and gave us some good restaurant tips.",
        "Nice stay overall. The photos do not quite do the space justice.",
        "Great for the whole family, plenty of space and a lovely outdoor area.",
        "Smooth booking and a stress-free check-in. Highly recommended."
    };

    private static readonly int[] Ratings = { 5, 5, 4, 5, 4, 5, 5, 3, 5, 4 };

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        var users = services.GetRequiredService<UserManager<ApplicationUser>>();

        var reviewers = new List<ApplicationUser>();
        foreach (var r in Reviewers)
            reviewers.Add(await RoleSeeder.EnsureUserAsync(users, r.Email, r.First, r.Last, AppRoles.Guest));

        var properties = await db.HostProperties
            .Where(p => p.IsApproved && !p.Reviews.Any())
            .OrderBy(p => p.Id).ToListAsync();
        if (properties.Count == 0) return;

        foreach (var p in properties)
        {
            var count = 2 + p.Id % 5;
            for (var k = 0; k < count; k++)
            {
                db.Reviews.Add(new Review
                {
                    PropertyId = p.Id,
                    ReviewerId = reviewers[(p.Id + k) % reviewers.Count].Id,
                    Rating = Ratings[(p.Id * 3 + k * 7) % Ratings.Length],
                    Comment = Comments[(p.Id * 5 + k * 3) % Comments.Length],
                    CreatedAt = DateTime.Now.AddDays(-(12 + k * 23 + p.Id % 9))
                });
            }
        }
        await db.SaveChangesAsync();
    }
}
