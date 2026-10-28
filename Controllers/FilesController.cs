namespace Staybnb.Controllers;

[Authorize]
public class FilesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IFileStorage _files;

    public FilesController(ApplicationDbContext db, IFileStorage files)
    {
        _db = db;
        _files = files;
    }

    public async Task<IActionResult> Document(int id)
    {
        var doc = await _db.GuestDocuments
            .Include(d => d.GuestCheckIn).ThenInclude(c => c.Booking).ThenInclude(b => b.Property)
            .FirstOrDefaultAsync(d => d.Id == id);
        if (doc == null) return NotFound();

        var booking = doc.GuestCheckIn.Booking;
        var uid = User.GetUserId();
        // only the guest, the host of that property or staff can open a document
        if (booking.GuestId != uid && booking.Property.HostId != uid && !User.IsStaff()) return Forbid();

        var path = _files.GetDocumentPath(doc.FileName);
        if (!System.IO.File.Exists(path)) return NotFound();

        var contentType = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            _ => "image/jpeg"
        };
        return PhysicalFile(path, contentType);
    }
}
