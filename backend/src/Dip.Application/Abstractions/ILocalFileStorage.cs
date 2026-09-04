using System.IO;

namespace Dip.Application.Abstractions;

// Abstracts the "download from Drive to App_Data/files/{id}.xlsx" step so
// handlers don't hard-code paths and tests can swap in an in-memory store.
public interface ILocalFileStorage
{
    Task<StoredFile> SaveAsync(Guid folderFileId, string sourceFileName, Stream content, CancellationToken ct);
    void Delete(string storagePath);
}

public sealed record StoredFile(string StoragePath, long SizeBytes, string Md5);
