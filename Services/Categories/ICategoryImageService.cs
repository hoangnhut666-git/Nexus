using Nexus.Services.Categories.Models;

namespace Nexus.Services.Categories;

public interface ICategoryImageService
{
    Task<ServiceResult<string>> SaveUploadedFileAsync(Stream file, string fileName, string contentType, long fileLength);

    Task DeleteFileIfLocalAsync(string? imageUrl);

    bool IsLocalUploadPath(string? imageUrl);
}
