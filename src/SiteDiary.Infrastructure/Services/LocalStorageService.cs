using SiteDiary.Domain.Interfaces;

namespace SiteDiary.Infrastructure.Services;

/// <summary>
/// Local file system storage service — placeholder for development.
/// Replace with Azure Blob Storage implementation for production.
/// </summary>
public class LocalStorageService(IWebHostEnvironmentInfo env) : IStorageService
{
    private readonly string _root = Path.Combine(env.ContentRootPath, "uploads");

    public async Task<string> UploadAsync(Stream stream, string fileName, string contentType, CancellationToken ct = default)
    {
        Directory.CreateDirectory(_root);
        var safeFileName = $"{Guid.NewGuid()}_{Path.GetFileName(fileName)}";
        var path = Path.Combine(_root, safeFileName);
        await using var fs = File.Create(path);
        await stream.CopyToAsync(fs, ct);
        return $"/uploads/{safeFileName}";
    }

    public Task DeleteAsync(string fileUrl, CancellationToken ct = default)
    {
        var fileName = Path.GetFileName(fileUrl);
        var path = Path.Combine(_root, fileName);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }
}

public interface IWebHostEnvironmentInfo
{
    string ContentRootPath { get; }
}
