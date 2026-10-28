using System.Text.Json;

namespace Staybnb.Models;

public class CheckInProcess
{
    public int Id { get; set; }

    public int PropertyId { get; set; }
    [ForeignKey(nameof(PropertyId))]
    public HostProperty Property { get; set; }

    [Required, StringLength(200)]
    public string Title { get; set; }

    public string StepsJson { get; set; } = "{}";

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public class CheckInConfig
{
    public string Instructions { get; set; } = "";
    public List<string> Rules { get; set; } = new();
    public List<string> RequiredDocuments { get; set; } = new();

    public static readonly string[] DocumentOptions = { "ID Document", "Passport" };

    public static CheckInConfig Parse(string json)
    {
        try
        {
            var cfg = JsonSerializer.Deserialize<CheckInConfig>(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            cfg ??= new CheckInConfig();
            cfg.Rules ??= new List<string>();
            cfg.RequiredDocuments ??= new List<string>();
            if (cfg.RequiredDocuments.Count == 0) cfg.RequiredDocuments.AddRange(DocumentOptions);
            return cfg;
        }
        catch (JsonException)
        {
            return new CheckInConfig { RequiredDocuments = new List<string>(DocumentOptions) };
        }
    }

    public string ToJson() => JsonSerializer.Serialize(this);

    public static CheckInConfig Default() => new()
    {
        Instructions = "Welcome! Please complete the steps below before your arrival.",
        Rules = new List<string> { "No smoking inside the property", "No parties or events", "Quiet hours from 22:00 to 07:00", "Respect the neighbours and the property" },
        RequiredDocuments = new List<string>(DocumentOptions)
    };
}

public class GuestCheckIn
{
    public int Id { get; set; }

    public int BookingId { get; set; }
    [ForeignKey(nameof(BookingId))]
    public Booking Booking { get; set; }

    public int CheckInProcessId { get; set; }
    [ForeignKey(nameof(CheckInProcessId))]
    public CheckInProcess CheckInProcess { get; set; }

    public CheckInStatus Status { get; set; } = CheckInStatus.NotStarted;
    public DateTime? RulesAcceptedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public ICollection<GuestDocument> Documents { get; set; } = new List<GuestDocument>();
}

public class GuestDocument
{
    public int Id { get; set; }

    public int GuestCheckInId { get; set; }
    [ForeignKey(nameof(GuestCheckInId))]
    public GuestCheckIn GuestCheckIn { get; set; }

    [Required, StringLength(100)]
    public string DocumentType { get; set; }

    [Required, StringLength(200)]
    public string FileName { get; set; }

    public DocumentStatus Status { get; set; } = DocumentStatus.Pending;

    [StringLength(500)]
    public string ReviewNote { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.Now;
}
