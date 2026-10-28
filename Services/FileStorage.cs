namespace Staybnb.Services;

public interface IFileStorage
{
    string ValidateImage(IFormFile file);
    string ValidateDocument(IFormFile file);
    Task<string> SaveImageAsync(IFormFile file);
    Task<string> SaveDocumentAsync(IFormFile file);
    string GetDocumentPath(string fileName);
}

public class FileStorage : IFileStorage
{
    private const long MaxBytes = 5 * 1024 * 1024;
    private static readonly string[] ImageExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
    private static readonly string[] DocumentExtensions = { ".jpg", ".jpeg", ".png", ".pdf" };

    private readonly IWebHostEnvironment _env;
    public FileStorage(IWebHostEnvironment env) => _env = env;

    public string ValidateImage(IFormFile file) => Validate(file, ImageExtensions, "JPG, PNG or WEBP");
    public string ValidateDocument(IFormFile file) => Validate(file, DocumentExtensions, "JPG, PNG or PDF");

    private static string Validate(IFormFile file, string[] allowed, string label)
    {
        if (file == null || file.Length == 0) return "Please choose a file.";
        if (file.Length > MaxBytes) return $"'{file.FileName}' is larger than 5 MB.";
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowed.Contains(ext)) return $"'{file.FileName}' is not allowed. Use {label}.";
        if (!MatchesSignature(file, ext)) return $"'{file.FileName}' is not a valid {ext} file.";
        return null;
    }

    // checks the first bytes so a renamed .exe or .html can't slip through as an image
    private static bool MatchesSignature(IFormFile file, string ext)
    {
        Span<byte> h = stackalloc byte[12];
        using var stream = file.OpenReadStream();
        var read = stream.Read(h);
        if (read < 4) return false;
        return ext switch
        {
            ".jpg" or ".jpeg" => h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF,
            ".png" => h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47,
            ".webp" => read >= 12 && h[0] == 'R' && h[1] == 'I' && h[2] == 'F' && h[3] == 'F' && h[8] == 'W' && h[9] == 'E' && h[10] == 'B' && h[11] == 'P',
            ".pdf" => h[0] == '%' && h[1] == 'P' && h[2] == 'D' && h[3] == 'F',
            _ => false
        };
    }

    public async Task<string> SaveImageAsync(IFormFile file)
    {
        var dir = Path.Combine(_env.WebRootPath, "uploads", "properties");
        Directory.CreateDirectory(dir);
        var name = Guid.NewGuid().ToString("N") + Path.GetExtension(file.FileName).ToLowerInvariant();
        await using var stream = File.Create(Path.Combine(dir, name));
        await file.CopyToAsync(stream);
        return "/uploads/properties/" + name;
    }

    public async Task<string> SaveDocumentAsync(IFormFile file)
    {
        // ID documents stay outside wwwroot so they can't be opened by URL
        var dir = Path.Combine(_env.ContentRootPath, "PrivateUploads");
        Directory.CreateDirectory(dir);
        var name = Guid.NewGuid().ToString("N") + Path.GetExtension(file.FileName).ToLowerInvariant();
        await using var stream = File.Create(Path.Combine(dir, name));
        await file.CopyToAsync(stream);
        return name;
    }

    public string GetDocumentPath(string fileName) =>
        Path.Combine(_env.ContentRootPath, "PrivateUploads", Path.GetFileName(fileName));
}
