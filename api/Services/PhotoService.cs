using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace AutoCo.Api.Services;

public interface IPhotoService
{
    string? GetStudentFotoUrl(int studentId);
    string? GetProfessorFotoUrl(int professorId);
    Task<bool> SaveStudentFotoAsync(int studentId, Stream data, string contentType);
    Task<bool> SaveProfessorFotoAsync(int professorId, Stream data, string contentType);
    Task<(int Imported, List<string> NotFound, List<string> Errors)> ImportZipFotosAsync(
        Stream zipStream, IReadOnlyDictionary<string, int> dniToStudentId);
    bool DeleteStudentFoto(int studentId);
    bool DeleteProfessorFoto(int professorId);
}

public class PhotoService : IPhotoService
{
    private readonly string _basePath;

    public PhotoService(IConfiguration config)
    {
        _basePath = config["Photos:BasePath"] ?? "/app/fotos";
    }

    public string? GetStudentFotoUrl(int studentId)
    {
        var path = System.IO.Path.Combine(_basePath, "alumnes", $"{studentId}.jpg");
        return System.IO.File.Exists(path) ? $"/fotos/alumnes/{studentId}.jpg" : null;
    }

    public string? GetProfessorFotoUrl(int professorId)
    {
        var path = System.IO.Path.Combine(_basePath, "professors", $"{professorId}.jpg");
        return System.IO.File.Exists(path) ? $"/fotos/professors/{professorId}.jpg" : null;
    }

    public async Task<bool> SaveStudentFotoAsync(int studentId, Stream data, string contentType)
    {
        var dir = System.IO.Path.Combine(_basePath, "alumnes");
        System.IO.Directory.CreateDirectory(dir);
        var path = System.IO.Path.Combine(dir, $"{studentId}.jpg");
        return await SaveImageAsync(data, contentType, path);
    }

    public async Task<bool> SaveProfessorFotoAsync(int professorId, Stream data, string contentType)
    {
        var dir = System.IO.Path.Combine(_basePath, "professors");
        System.IO.Directory.CreateDirectory(dir);
        var path = System.IO.Path.Combine(dir, $"{professorId}.jpg");
        return await SaveImageAsync(data, contentType, path);
    }

    public async Task<(int Imported, List<string> NotFound, List<string> Errors)> ImportZipFotosAsync(
        Stream zipStream, IReadOnlyDictionary<string, int> dniToStudentId)
    {
        int imported = 0;
        var notFound = new List<string>();
        var errors   = new List<string>();

        var dir = System.IO.Path.Combine(_basePath, "alumnes");
        System.IO.Directory.CreateDirectory(dir);

        using var archive = new System.IO.Compression.ZipArchive(zipStream, System.IO.Compression.ZipArchiveMode.Read);

        foreach (var entry in archive.Entries)
        {
            var fileName = System.IO.Path.GetFileName(entry.FullName);
            if (string.IsNullOrEmpty(fileName)) continue;

            var ext = System.IO.Path.GetExtension(fileName).ToLowerInvariant();
            if (ext is not (".jpg" or ".jpeg" or ".png" or ".webp")) continue;

            var baseName = System.IO.Path.GetFileNameWithoutExtension(fileName);
            var dniKey   = ExtractDniKey(baseName);

            if (!dniToStudentId.TryGetValue(dniKey, out var studentId))
            {
                notFound.Add(fileName);
                continue;
            }

            try
            {
                using var entryStream = entry.Open();
                var ms = new System.IO.MemoryStream();
                await entryStream.CopyToAsync(ms);
                ms.Position = 0;

                var contentType = ext is ".png" ? "image/png" : ext is ".webp" ? "image/webp" : "image/jpeg";
                var saved = await SaveStudentFotoAsync(studentId, ms, contentType);
                if (saved) imported++;
                else errors.Add($"{fileName}: error en desar.");
            }
            catch (Exception ex)
            {
                errors.Add($"{fileName}: {ex.Message}");
            }
        }

        return (imported, notFound, errors);
    }

    public bool DeleteStudentFoto(int studentId)
    {
        var path = System.IO.Path.Combine(_basePath, "alumnes", $"{studentId}.jpg");
        if (!System.IO.File.Exists(path)) return false;
        System.IO.File.Delete(path);
        return true;
    }

    public bool DeleteProfessorFoto(int professorId)
    {
        var path = System.IO.Path.Combine(_basePath, "professors", $"{professorId}.jpg");
        if (!System.IO.File.Exists(path)) return false;
        System.IO.File.Delete(path);
        return true;
    }

    private static async Task<bool> SaveImageAsync(Stream data, string contentType, string destPath)
    {
        if (contentType is not ("image/jpeg" or "image/png" or "image/webp" or "image/gif"))
            return false;
        try
        {
            using var image = await Image.LoadAsync(data);
            image.Mutate(ctx => ctx.Resize(new ResizeOptions
            {
                Size     = new Size(400, 400),
                Mode     = ResizeMode.Crop,
                Position = AnchorPositionMode.Center
            }));
            await image.SaveAsJpegAsync(destPath, new JpegEncoder { Quality = 85 });
            return true;
        }
        catch { return false; }
    }

    // Extreu la part numèrica del DNI (e.g. "53971108X" → "53971108", "53971108" → "53971108")
    private static string ExtractDniKey(string baseName)
    {
        var match = System.Text.RegularExpressions.Regex.Match(baseName, @"^\d+");
        return match.Success ? match.Value : baseName.ToUpperInvariant();
    }
}
