using System.Net;
using System.Text;
using Dip.Application.Abstractions;
using Dip.Infrastructure.Drive;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Dip.Infrastructure.Tests;

public class ApiKeyDriveClientTests
{
    private static ApiKeyDriveClient CreateClient(HttpMessageHandler handler, string apiKey = "test-key")
    {
        var http = new HttpClient(handler);
        var options = Options.Create(new GoogleDriveOptions { ApiKey = apiKey, RootFolderId = "root-id" });
        return new ApiKeyDriveClient(http, options, NullLogger<ApiKeyDriveClient>.Instance);
    }

    [Fact]
    public async Task ListChildrenAsync_ParsesFilesAndPropagatesModifiedTime()
    {
        const string json = /*lang=json,strict*/ """
        {
          "files": [
            {
              "id": "f1", "name": "Blades", "mimeType": "application/vnd.google-apps.folder",
              "modifiedTime": "2026-01-15T10:20:30.123Z"
            },
            {
              "id": "f2", "name": "TIDP-STL.xlsx",
              "mimeType": "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
              "modifiedTime": "2026-02-01T00:00:00Z",
              "md5Checksum": "abc123def456", "size": "348160"
            }
          ]
        }
        """;
        var handler = new StubHandler((request) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        });

        var client = CreateClient(handler);
        var result = await client.ListChildrenAsync("parent-id", CancellationToken.None);

        result.Should().HaveCount(2);

        result[0].Id.Should().Be("f1");
        result[0].IsFolder.Should().BeTrue();

        result[1].Id.Should().Be("f2");
        result[1].IsFolder.Should().BeFalse();
        result[1].Md5Checksum.Should().Be("abc123def456");
        result[1].Size.Should().Be(348160);

        // Verify the request URL contained key + query + fields + pageSize.
        handler.Requests.Should().HaveCount(1);
        var url = handler.Requests[0].RequestUri!.ToString();
        url.Should().Contain("files?");
        url.Should().Contain("parent-id");
        url.Should().Contain("key=test-key");
        url.Should().Contain("pageSize=1000");
    }

    [Fact]
    public async Task ListChildrenAsync_PaginatesUntilNextPageTokenNull()
    {
        var responses = new Queue<HttpResponseMessage>();
        responses.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                /*lang=json*/ """{"files":[{"id":"a","name":"A","mimeType":"application/vnd.google-apps.folder"}],"nextPageToken":"tok-2"}""",
                Encoding.UTF8, "application/json"),
        });
        responses.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                /*lang=json*/ """{"files":[{"id":"b","name":"B","mimeType":"application/vnd.google-apps.folder"}]}""",
                Encoding.UTF8, "application/json"),
        });

        var handler = new StubHandler(_ => responses.Dequeue());
        var client = CreateClient(handler);
        var result = await client.ListChildrenAsync("parent-id", CancellationToken.None);

        result.Should().HaveCount(2);
        handler.Requests.Should().HaveCount(2);
        handler.Requests[1].RequestUri!.ToString().Should().Contain("pageToken=tok-2");
    }

    [Fact]
    public async Task ListChildrenAsync_Throws_FriendlyMessage_On403()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent("The caller does not have permission", Encoding.UTF8, "application/json"),
        });
        var client = CreateClient(handler);

        var act = async () => await client.ListChildrenAsync("f", CancellationToken.None);

        var ex = await act.Should().ThrowAsync<DriveClientException>();
        ex.Which.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        ex.Which.Message.Should().Contain("Anyone with the link");
    }

    [Fact]
    public async Task DownloadAsync_ReturnsStreamBytes()
    {
        var payload = new byte[] { 0x50, 0x4B, 0x03, 0x04 }; // PK zip magic
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(payload),
        });
        var client = CreateClient(handler);

        await using var stream = await client.DownloadAsync("file-id", CancellationToken.None);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        ms.ToArray().Should().Equal(payload);
    }

    [Fact]
    public async Task ListChildrenAsync_Throws_WhenApiKeyMissing()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = CreateClient(handler, apiKey: "");
        var act = async () => await client.ListChildrenAsync("f", CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*ApiKey*");
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
        public List<HttpRequestMessage> Requests { get; } = new();

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(_responder(request));
        }
    }
}
