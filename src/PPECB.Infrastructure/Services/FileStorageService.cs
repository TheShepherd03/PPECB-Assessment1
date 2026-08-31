using Microsoft.Extensions.Logging;
using PPECB.Application.Abstractions;
using PPECB.Domain.Exceptions;

namespace PPECB.Infrastructure.Services;

public class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    /// <summary>Absolute or content-root-relative directory that backs the web root.</summary>
    public string WebRootPath { get; set; } = "wwwroot";

    /// <summary>Where product images live, relative to the web root.</summary>
    public string ProductImageFolder { get; set; } = "uploads/products";

    public long MaxImageBytes { get; set; } = 5 * 1024 * 1024;

    public string[] AllowedExtensions { get; set; } = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
}

/// <summary>
/// Saves product images to the local web root. The stored name is always a fresh GUID:
/// the client's filename is never used as a path, which removes traversal and collision
/// risk in one step.
/// </summary>
public class FileStorageService : IFileStorageService
{
    /// <summary>Magic-number prefixes for the image types we accept.</summary>
    private static readonly (byte[] Signature, int Offset)[] ImageSignatures =
    {
        (new byte[] { 0xFF, 0xD8, 0xFF }, 0),                                     // JPEG
        (new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, 0),       // PNG
        (new byte[] { 0x47, 0x49, 0x46, 0x38 }, 0),                               // GIF
        (new byte[] { 0x52, 0x49, 0x46, 0x46 }, 0)                                // WEBP (RIFF)
    };

    private readonly FileStorageOptions _options;
    private readonly ILogger<FileStorageService> _logger;

    public FileStorageService(FileStorageOptions options, ILogger<FileStorageService> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task<string> SaveProductImageAsync(
        Stream content, string originalFileName, CancellationToken ct = default)
    {
        var extension = Path.GetExtension(originalFileName)?.ToLowerInvariant() ?? string.Empty;

        if (!_options.AllowedExtensions.Contains(extension))
        {
            throw new Domain.Exceptions.ValidationException(
                "Image",
                $"Unsupported image type '{extension}'. Allowed: {string.Join(", ", _options.AllowedExtensions)}.");
        }

        // Buffer first so the size and signature can be checked before anything is written.
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);

        if (buffer.Length == 0)
        {
            throw new Domain.Exceptions.ValidationException("Image", "The uploaded file is empty.");
        }

        if (buffer.Length > _options.MaxImageBytes)
        {
            var limitMb = _options.MaxImageBytes / 1024d / 1024d;
            throw new Domain.Exceptions.ValidationException(
                "Image", $"The image must be {limitMb:0.#} MB or smaller.");
        }

        var bytes = buffer.ToArray();
        if (!LooksLikeImage(bytes))
        {
            // An attacker-supplied extension proves nothing; check the content itself.
            throw new Domain.Exceptions.ValidationException(
                "Image", "The uploaded file does not appear to be a valid image.");
        }

        var targetDirectory = Path.Combine(_options.WebRootPath, _options.ProductImageFolder);
        Directory.CreateDirectory(targetDirectory);

        var fileName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(targetDirectory, fileName);

        await File.WriteAllBytesAsync(fullPath, bytes, ct);

        return $"/{_options.ProductImageFolder.Replace('\\', '/')}/{fileName}";
    }

    public void DeleteProductImage(string? webRelativePath)
    {
        if (string.IsNullOrWhiteSpace(webRelativePath)) return;

        try
        {
            var expectedPrefix = $"/{_options.ProductImageFolder.Replace('\\', '/')}/";
            if (!webRelativePath.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
            {
                // Refuse to delete anything outside the managed upload folder.
                _logger.LogWarning("Refused to delete image outside the upload folder: {Path}", webRelativePath);
                return;
            }

            var fileName = Path.GetFileName(webRelativePath);
            var fullPath = Path.Combine(_options.WebRootPath, _options.ProductImageFolder, fileName);

            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }
        catch (IOException ex)
        {
            // An orphaned file is not worth failing the user's request over.
            _logger.LogWarning(ex, "Could not delete product image {Path}", webRelativePath);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Access denied deleting product image {Path}", webRelativePath);
        }
    }

    private static bool LooksLikeImage(byte[] bytes) =>
        ImageSignatures.Any(candidate =>
            bytes.Length >= candidate.Offset + candidate.Signature.Length &&
            bytes.Skip(candidate.Offset).Take(candidate.Signature.Length).SequenceEqual(candidate.Signature));
}
