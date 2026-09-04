using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dip.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dip.Infrastructure.Drive;

// Talks to the Google Drive v3 REST API with an API key. Requires that the
// shared folder is set to "Anyone with the link — Viewer". The 403 case is
// intercepted and reworded so operators get an actionable message.
//
// Endpoints used:
//   GET https://www.googleapis.com/drive/v3/files?q=... &fields=... &key=...
//   GET https://www.googleapis.com/drive/v3/files/{id}?alt=media&key=...
public sealed class ApiKeyDriveClient : IDriveClient
{
    private readonly HttpClient _http;
    private readonly GoogleDriveOptions _options;
    private readonly ILogger<ApiKeyDriveClient> _logger;

    private const string DriveBaseUrl = "https://www.googleapis.com/drive/v3/";
    private const string FolderFieldsMask = "files(id,name,mimeType,modifiedTime,md5Checksum,size),nextPageToken";

    public ApiKeyDriveClient(HttpClient http, IOptions<GoogleDriveOptions> options, ILogger<ApiKeyDriveClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;

        if (_http.BaseAddress is null)
        {
            _http.BaseAddress = new Uri(DriveBaseUrl);
        }
        _http.Timeout = TimeSpan.FromSeconds(_options.RequestTimeoutSeconds);
    }

    public async Task<IReadOnlyList<DriveEntry>> ListChildrenAsync(string folderId, CancellationToken ct)
    {
        EnsureConfigured();
        var results = new List<DriveEntry>();
        string? pageToken = null;
        do
        {
            var url = BuildListUrl(folderId, pageToken);
            using var response = await _http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                await ThrowFriendlyAsync(response, $"list children of {folderId}");
            }

            var payload = await response.Content.ReadFromJsonAsync<FilesListResponse>(SerializerOptions, ct)
                ?? throw new InvalidOperationException("Empty response from Drive files.list");

            foreach (var file in payload.Files ?? Array.Empty<DriveFileDto>())
            {
                results.Add(new DriveEntry(
                    file.Id,
                    file.Name,
                    file.MimeType,
                    file.ModifiedTime,
                    file.Md5Checksum,
                    ParseSize(file.Size)));
            }

            pageToken = payload.NextPageToken;
        }
        while (!string.IsNullOrEmpty(pageToken));

        _logger.LogDebug("Drive listed {Count} children of {FolderId}", results.Count, folderId);
        return results;
    }

    public async Task<Stream> DownloadAsync(string fileId, CancellationToken ct)
    {
        EnsureConfigured();
        var url = $"files/{Uri.EscapeDataString(fileId)}?alt=media&key={Uri.EscapeDataString(_options.ApiKey)}";
        var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
        {
            await ThrowFriendlyAsync(response, $"download file {fileId}");
        }
        return await response.Content.ReadAsStreamAsync(ct);
    }

    private string BuildListUrl(string folderId, string? pageToken)
    {
        var query = $"'{folderId}' in parents and trashed = false";
        var url = $"files?q={Uri.EscapeDataString(query)}"
            + $"&fields={Uri.EscapeDataString(FolderFieldsMask)}"
            + $"&pageSize={_options.PageSize.ToString(CultureInfo.InvariantCulture)}"
            + "&supportsAllDrives=true"
            + "&includeItemsFromAllDrives=true"
            + $"&key={Uri.EscapeDataString(_options.ApiKey)}";
        if (!string.IsNullOrEmpty(pageToken))
        {
            url += $"&pageToken={Uri.EscapeDataString(pageToken)}";
        }
        return url;
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException("GoogleDrive:ApiKey is not configured");
        }
    }

    private static long? ParseSize(string? size) =>
        long.TryParse(size, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;

    private static async Task ThrowFriendlyAsync(HttpResponseMessage response, string action)
    {
        var body = await response.Content.ReadAsStringAsync();
        var message = response.StatusCode switch
        {
            HttpStatusCode.Forbidden =>
                $"Cannot {action}: the folder is not shared with 'Anyone with the link' or the API key is missing 'Drive API' access. Response: {Truncate(body)}",
            HttpStatusCode.NotFound =>
                $"Cannot {action}: not found on Drive (check the folder id and sharing). Response: {Truncate(body)}",
            _ => $"Drive request to {action} failed with {(int)response.StatusCode} {response.ReasonPhrase}. Response: {Truncate(body)}",
        };
        throw new DriveClientException(message, response.StatusCode);
    }

    private static string Truncate(string? value) =>
        string.IsNullOrEmpty(value) ? string.Empty : (value.Length <= 500 ? value : value[..500] + "…");

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private sealed class FilesListResponse
    {
        [JsonPropertyName("files")] public DriveFileDto[]? Files { get; set; }
        [JsonPropertyName("nextPageToken")] public string? NextPageToken { get; set; }
    }

    private sealed class DriveFileDto
    {
        [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
        [JsonPropertyName("mimeType")] public string MimeType { get; set; } = string.Empty;
        [JsonPropertyName("modifiedTime")] public DateTime? ModifiedTime { get; set; }
        [JsonPropertyName("md5Checksum")] public string? Md5Checksum { get; set; }
        [JsonPropertyName("size")] public string? Size { get; set; }
    }
}

public sealed class DriveClientException : Exception
{
    public HttpStatusCode StatusCode { get; }
    public DriveClientException(string message, HttpStatusCode statusCode) : base(message)
    {
        StatusCode = statusCode;
    }
}
