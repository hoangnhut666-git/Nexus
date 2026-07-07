using Nexus.Services.Categories.Models;

namespace Nexus.Services.Products;

public sealed class ProductImageService(IWebHostEnvironment environment) : IProductImageService
{
    public const string UploadRelativePath = "uploads/products";
    public const long MaxFileSizeBytes = 2 * 1024 * 1024;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif"
    };

    private static readonly Dictionary<string, string> ContentTypeToExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp",
        ["image/gif"] = ".gif"
    };

    public async Task<ServiceResult<string>> SaveUploadedFileAsync(
        Stream file,
        string fileName,
        string contentType,
        long fileLength)
    {
        if (fileLength <= 0)
            return ServiceResult<string>.Fail("The uploaded file is empty.");

        if (fileLength > MaxFileSizeBytes)
            return ServiceResult<string>.Fail("Image must be 2 MB or smaller.");

        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(extension) && ContentTypeToExtension.TryGetValue(contentType, out var mappedExtension))
            extension = mappedExtension;

        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
            return ServiceResult<string>.Fail("Only JPG, PNG, WEBP, and GIF images are allowed.");

        var uploadDirectory = GetUploadDirectory();
        Directory.CreateDirectory(uploadDirectory);

        var storedFileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var fullPath = Path.Combine(uploadDirectory, storedFileName);

        await using var output = File.Create(fullPath);
        await file.CopyToAsync(output);

        return ServiceResult<string>.Ok($"/{UploadRelativePath}/{storedFileName}");
    }

    public Task DeleteFileIfLocalAsync(string? imageUrl)
    {
        if (!IsLocalUploadPath(imageUrl))
            return Task.CompletedTask;

        var relativePath = imageUrl!.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.Combine(environment.WebRootPath, relativePath);

        if (File.Exists(fullPath))
            File.Delete(fullPath);

        return Task.CompletedTask;
    }

    public bool IsLocalUploadPath(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            return false;

        var normalized = imageUrl.Trim().Replace('\\', '/');
        return normalized.StartsWith($"/{UploadRelativePath}/", StringComparison.OrdinalIgnoreCase);
    }

    private string GetUploadDirectory() =>
        Path.Combine(environment.WebRootPath, UploadRelativePath);
}
