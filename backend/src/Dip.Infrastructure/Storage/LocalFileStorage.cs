using System.IO;
using System.Security.Cryptography;
using Dip.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Dip.Infrastructure.Storage;

// Writes to {Storage:LocalRoot}/{folderFileId}{ext}. Default root is
// ./App_Data/files (matches ASPMonster IIS convention).
public sealed class LocalFileStorage : ILocalFileStorage
{
    private readonly string _root;
    private readonly ILogger<LocalFileStorage> _logger;

    public LocalFileStorage(IConfiguration configuration, ILogger<LocalFileStorage> logger)
    {
        _root = configuration["Storage:LocalRoot"] ?? Path.Combine("App_Data", "files");
        Directory.CreateDirectory(_root);
        _logger = logger;
    }

    public async Task<StoredFile> SaveAsync(Guid folderFileId, string sourceFileName, Stream content, CancellationToken ct)
    {
        var ext = Path.GetExtension(sourceFileName);
        var target = Path.Combine(_root, folderFileId.ToString("N") + ext);
        Directory.CreateDirectory(_root);

        using var md5 = MD5.Create();
        long total = 0;
        await using (var file = File.Create(target))
        {
            var buffer = new byte[81920];
            int read;
            while ((read = await content.ReadAsync(buffer, ct)) > 0)
            {
                md5.TransformBlock(buffer, 0, read, null, 0);
                await file.WriteAsync(buffer.AsMemory(0, read), ct);
                total += read;
            }
        }
        md5.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        var md5Hex = Convert.ToHexString(md5.Hash ?? Array.Empty<byte>()).ToLowerInvariant();

        _logger.LogDebug("Stored {Size} bytes to {Path}", total, target);
        return new StoredFile(target, total, md5Hex);
    }

    public void Delete(string storagePath)
    {
        if (File.Exists(storagePath))
        {
            try { File.Delete(storagePath); }
            catch (IOException ex)
            {
                _logger.LogWarning(ex, "Could not delete {Path}", storagePath);
            }
        }
    }
}
