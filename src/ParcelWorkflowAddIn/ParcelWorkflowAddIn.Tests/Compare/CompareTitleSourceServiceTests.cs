using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Compare;
using ParcelWorkflowAddIn.Contracts;
using ParcelWorkflowAddIn.Innola;

namespace ParcelWorkflowAddIn.Tests.Compare;

internal static class CompareTitleSourceServiceTests
{
    public static async Task SearchCurrentTitlesUsesExpectedInnolaPayload()
    {
        var handler = new RecordingHandler(new[]
        {
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"records":[{"id":"source-1","referenceNo":"2000/1","documentType":"st_title","status":"reg_status_current","name":"Title 2000/1"}]}""")
            }
        });
        var service = CreateService(handler);

        var result = await service.SearchCurrentTitlesAsync("2000", "1");

        TestAssert.True(result.Success, "Search should succeed.");
        TestAssert.Equal(1, result.Sources.Count, "One source should be returned.");
        TestAssert.Equal("source-1", result.Sources[0].SourceId, "Source id should map from the search response.");
        TestAssert.Equal("/api/v4/rest/search/", handler.Requests[0].RequestUri!.AbsolutePath, "Search should call the v4 source search endpoint.");
        TestAssert.Equal(HttpMethod.Post, handler.Requests[0].Method, "Search should use POST.");
        TestAssert.Equal("token-1", handler.Requests[0].Headers.GetValues("Access-Token").Single(), "Search should apply Innola auth headers.");

        using var document = JsonDocument.Parse(handler.Bodies[0]);
        var root = document.RootElement;
        TestAssert.Equal("SearchRequest", root.GetProperty("@c").GetString(), "Source search should use the Innola search request envelope.");
        TestAssert.Equal("source", root.GetProperty("searchKind").GetString(), "Search kind mismatch.");
        var parameters = root.GetProperty("params");
        TestAssert.True(parameters.GetProperty("statusLatest").GetBoolean(), "Search should request the latest current source.");
        TestAssert.Equal("reg_status_current", parameters.GetProperty("status").GetString(), "Status filter mismatch.");
        TestAssert.Equal("st_title", parameters.GetProperty("documentTypes")[0].GetString(), "Document type filter mismatch.");
        TestAssert.Equal("2000/1", parameters.GetProperty("referenceNo").GetString(), "Reference number mismatch.");
        TestAssert.Equal(25, root.GetProperty("limit").GetInt32(), "Search limit mismatch.");
    }

    public static async Task DownloadWritesRepoVolumeFolioAndAvoidsOverwrite()
    {
        using var fixture = CreateCaseFolder();
        File.WriteAllText(Path.Combine(fixture.Layout.SourceDirectory, "Repo_2000_1.pdf"), "existing");
        var handler = new RecordingHandler(new[]
        {
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"body":{"id":"body-1"}}""") },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Encoding.ASCII.GetBytes("%PDF-1.4 body")) }
        });
        var ensureCalls = 0;
        var service = new InnolaCompareTitleSourceService(
            new HttpClient(handler),
            (_, _) =>
            {
                ensureCalls++;
                return Task.FromResult(InnolaSessionEnsureResult.Succeeded(CreateSession(), "active"));
            },
            () => DateTimeOffset.Parse("2026-09-12T00:00:00Z"));

        var result = await service.DownloadAsync(
            new CompareTitleSourceRecord("source-1", "Title", "2000/1", "st_title", "reg_status_current"),
            "2000",
            "1",
            fixture.Layout);

        TestAssert.True(result.Success, "Download should succeed.");
        TestAssert.True(result.FilePath!.EndsWith("Repo_2000_1_2.pdf", StringComparison.OrdinalIgnoreCase), "Duplicate downloads should use deterministic suffix.");
        TestAssert.True(File.Exists(result.FilePath), "Downloaded PDF should be written.");
        TestAssert.Equal("/api/v4/rest/portal/ladm-objects/source-1", handler.Requests[0].RequestUri!.AbsolutePath, "Source object endpoint mismatch.");
        TestAssert.Equal("typeKeyId=source", handler.Requests[0].RequestUri!.Query.TrimStart('?'), "Source object query mismatch.");
        TestAssert.Equal("/api/v4/rest/source/download", handler.Requests[1].RequestUri!.AbsolutePath, "Download endpoint mismatch.");
        TestAssert.True(handler.Requests[1].RequestUri!.Query.Contains("noredirect=1", StringComparison.Ordinal), "Download should request no redirect.");
        TestAssert.True(handler.Requests[1].RequestUri!.Query.Contains("bodyId=body-1", StringComparison.Ordinal), "Download should use returned body id.");
        TestAssert.Equal(2, ensureCalls, "Source object read and body download should each ensure a current Innola session.");

        var reopened = new CaseFolderStore().ReopenCaseFolder(fixture.Layout.RootDirectory);
        TestAssert.True(reopened.SourceFiles.Any(item => item.FileName.Equals("Repo_2000_1_2.pdf", StringComparison.OrdinalIgnoreCase)), "Downloaded title should appear after case reopen.");
    }

    public static async Task DownloadMissingBodyDoesNotWriteFile()
    {
        using var fixture = CreateCaseFolder();
        var handler = new RecordingHandler(new[]
        {
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"id":"source-1"}""") }
        });
        var service = CreateService(handler);

        var result = await service.DownloadAsync(
            new CompareTitleSourceRecord("source-1", "Title", "2000/1", "st_title", "reg_status_current"),
            "2000",
            "1",
            fixture.Layout);

        TestAssert.False(result.Success, "Missing body id should fail safely.");
        TestAssert.False(File.Exists(Path.Combine(fixture.Layout.SourceDirectory, "Repo_2000_1.pdf")), "Missing body id should not create an empty title file.");
    }

    public static async Task SearchFailureRedactsSensitiveDiagnostics()
    {
        var handler = new RecordingHandler(new[]
        {
            new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("""{"error":"token=abc password=secret"}""")
            }
        });
        var service = CreateService(handler);

        var result = await service.SearchCurrentTitlesAsync("2000", "1");

        TestAssert.False(result.Success, "Unauthorized search should fail.");
        TestAssert.False(result.Message.Contains("abc", StringComparison.OrdinalIgnoreCase), "Failure message must not expose tokens.");
        TestAssert.False((result.Diagnostic ?? string.Empty).Contains("secret", StringComparison.OrdinalIgnoreCase), "Failure diagnostic must not expose passwords.");
    }

    public static async Task SearchUnauthorizedReportsTitleSourceRejection()
    {
        var handler = new RecordingHandler(new[]
        {
            new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("""{"detail":"Full authentication is required to access this resource"}""")
            },
            new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("""{"detail":"Full authentication is required to access this resource"}""")
            }
        });
        var service = new InnolaCompareTitleSourceService(
            new HttpClient(handler),
            (_, _) => Task.FromResult(InnolaSessionEnsureResult.Succeeded(CreateSession(), "active")),
            () => DateTimeOffset.Parse("2026-09-12T00:00:00Z"),
            _ => false);

        var result = await service.SearchCurrentTitlesAsync("1284", "27");

        TestAssert.False(result.Success, "Unauthorized source search should fail.");
        TestAssert.True(result.Message.Contains("rejected the title source search", StringComparison.OrdinalIgnoreCase), $"Unauthorized source search should not look like a generic connection failure. Message={result.Message}");
        TestAssert.True(result.Diagnostic?.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase) == true, "Diagnostic should preserve safe HTTP status evidence.");
    }

    public static async Task SearchRetriesCookieOnlyWhenAccessTokenRejected()
    {
        var handler = new RecordingHandler(new[]
        {
            new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("""{"detail":"Full authentication is required"}""")
            },
            new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("""{"detail":"Full authentication is required"}""")
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"records":[{"id":"source-27","referenceNo":"1284/27","documentType":"st_title","status":"reg_status_current"}]}""")
            }
        });
        var ensureCalls = 0;
        var service = new InnolaCompareTitleSourceService(
            new HttpClient(handler),
            (_, _) =>
            {
                ensureCalls++;
                return Task.FromResult(InnolaSessionEnsureResult.Succeeded(CreateSession() with { AccessToken = $"token-{ensureCalls}" }, "active"));
            },
            () => DateTimeOffset.Parse("2026-09-12T00:00:00Z"),
            _ => true);

        var result = await service.SearchCurrentTitlesAsync("1284", "27");

        TestAssert.True(result.Success, "Cookie-only retry should recover title source search.");
        TestAssert.Equal(1, result.Sources.Count, "Retry response should be mapped.");
        TestAssert.Equal(3, handler.Requests.Count, "Search should refresh once before falling back to cookie-only auth.");
        TestAssert.True(handler.Requests[0].Headers.Contains("Access-Token"), "First request should include Access-Token.");
        TestAssert.True(handler.Requests[1].Headers.Contains("Access-Token"), "Auth refresh retry should include the refreshed Access-Token.");
        TestAssert.False(handler.Requests[2].Headers.Contains("Access-Token"), "Cookie-only retry should omit Access-Token.");
        TestAssert.True(handler.Requests[2].Headers.Contains("X-Requested-With"), "Retry should preserve Innola web-search headers.");
    }

    public static async Task SearchRefreshesSessionAfterAuthFailure()
    {
        var handler = new RecordingHandler(new[]
        {
            new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("""{"detail":"Full authentication is required"}""")
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"records":[{"id":"source-27","referenceNo":"1284/27","documentType":"st_title","status":"reg_status_current"}]}""")
            }
        });
        var ensureCalls = 0;
        var service = new InnolaCompareTitleSourceService(
            new HttpClient(handler),
            (_, _) =>
            {
                ensureCalls++;
                var token = ensureCalls == 1 ? "token-stale" : "token-refreshed";
                return Task.FromResult(InnolaSessionEnsureResult.Succeeded(CreateSession() with { AccessToken = token }, "active"));
            },
            () => DateTimeOffset.Parse("2026-09-12T00:00:00Z"),
            _ => false);

        var result = await service.SearchCurrentTitlesAsync("1284", "27");

        TestAssert.True(result.Success, "Title source search should refresh session and retry once after an auth failure.");
        TestAssert.Equal(2, ensureCalls, "Title source search should ensure before sending and again after auth failure.");
        TestAssert.Equal(2, handler.Requests.Count, "Title source search should resend the safe read once.");
        TestAssert.Equal("token-stale", handler.AccessTokens[0], "Initial source search should use the first ensured token.");
        TestAssert.Equal("token-refreshed", handler.AccessTokens[1], "Auth retry should use the refreshed token.");
    }

    public static async Task DownloadRefreshesSessionForObjectReadAndBodyDownloadAuthFailures()
    {
        using var fixture = CreateCaseFolder();
        var handler = new RecordingHandler(new[]
        {
            new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("""{"detail":"source object token expired"}""")
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"body":{"id":"body-1"}}""")
            },
            new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("""{"detail":"download token expired"}""")
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(Encoding.ASCII.GetBytes("%PDF-1.4 body"))
            }
        });
        var ensureCalls = 0;
        var service = new InnolaCompareTitleSourceService(
            new HttpClient(handler),
            (_, _) =>
            {
                ensureCalls++;
                return Task.FromResult(InnolaSessionEnsureResult.Succeeded(CreateSession() with { AccessToken = $"token-{ensureCalls}" }, "active"));
            },
            () => DateTimeOffset.Parse("2026-09-12T00:00:00Z"),
            _ => false);

        var result = await service.DownloadAsync(
            new CompareTitleSourceRecord("source-1", "Title", "1284/27", "st_title", "reg_status_current"),
            "1284",
            "27",
            fixture.Layout);

        TestAssert.True(result.Success, "Title download should recover from source-object and body-download auth failures.");
        TestAssert.Equal(4, ensureCalls, "Object read and body download should each ensure before sending and refresh after auth failure.");
        TestAssert.Equal(4, handler.Requests.Count, "Object read and body download should each retry exactly once.");
        TestAssert.Equal("token-1", handler.AccessTokens[0], "Initial object read should use token 1.");
        TestAssert.Equal("token-2", handler.AccessTokens[1], "Object-read retry should use token 2.");
        TestAssert.Equal("token-3", handler.AccessTokens[2], "Initial body download should use token 3.");
        TestAssert.Equal("token-4", handler.AccessTokens[3], "Body-download retry should use token 4.");
        TestAssert.True(File.Exists(result.FilePath), "Recovered title download should write the local PDF.");
    }

    public static void TraceAppendWritesTitleSearchEvidence()
    {
        using var fixture = CreateCaseFolder();
        var trace = new CompareTitleSourceTracePersistenceService();
        var search = new CompareTitleSourceSearchResult(
            true,
            new[] { new CompareTitleSourceRecord("source-27", "Title 1284/27", "1284/27", "st_title", "reg_status_current") },
            "1 current title source record(s) found for 1284/27.",
            null);
        var download = new CompareTitleDownloadResult(
            true,
            "Downloaded title document Repo_1284_27.pdf.",
            Path.Combine(fixture.Layout.SourceDirectory, "Repo_1284_27.pdf"),
            null);

        trace.Append(fixture.Layout, "100000896", "1284", "27", search, download, DateTimeOffset.Parse("2026-09-12T22:00:00Z"));

        var path = trace.GetTracePath(fixture.Layout);
        TestAssert.True(File.Exists(path), "Trace file should be written.");
        var text = File.ReadAllText(path);
        TestAssert.True(text.Contains("\"reference_no\": \"1284/27\"", StringComparison.Ordinal), "Trace should record the requested title reference.");
        TestAssert.True(text.Contains("\"source_count\": 1", StringComparison.Ordinal), "Trace should record mapped source count.");
        TestAssert.True(text.Contains("Repo_1284_27.pdf", StringComparison.Ordinal), "Trace should record downloaded file path.");
    }

    private static InnolaCompareTitleSourceService CreateService(RecordingHandler handler)
    {
        return new InnolaCompareTitleSourceService(
            new HttpClient(handler),
            (_, _) => Task.FromResult(InnolaSessionEnsureResult.Succeeded(CreateSession(), "active")),
            () => DateTimeOffset.Parse("2026-09-12T00:00:00Z"));
    }

    private static InnolaSession CreateSession()
    {
        return new InnolaSession(
            InnolaSessionStatus.LoggedIn,
            "https://innola.example/",
            "tester",
            "session-password",
            "token-1",
            new InnolaUserContext("tester", "Tester", Array.Empty<string>(), Array.Empty<string>()),
            null);
    }

    private static CompareCaseFixture CreateCaseFolder()
    {
        var temp = new TempDirectory();
        var store = new CaseFolderStore(
            () => DateTimeOffset.Parse("2026-09-12T00:00:00Z"),
            () => "run-title");
        var created = store.CreateCase(temp.Path, "TR100000674", "tester");
        if (!created.Success || created.Layout is null)
        {
            throw new InvalidOperationException(created.ErrorMessage);
        }

        return new CompareCaseFixture(temp, created.Layout);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> responses;

        public RecordingHandler(IEnumerable<HttpResponseMessage> responses)
        {
            this.responses = new Queue<HttpResponseMessage>(responses);
        }

        public List<HttpRequestMessage> Requests { get; } = new();

        public List<string> Bodies { get; } = new();

        public List<string?> AccessTokens { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            Bodies.Add(request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken));
            AccessTokens.Add(request.Headers.TryGetValues("Access-Token", out var values)
                ? values.SingleOrDefault()
                : request.Headers.TryGetValues("Access-token", out values)
                    ? values.SingleOrDefault()
                    : null);
            return responses.Count > 0
                ? responses.Dequeue()
                : new HttpResponseMessage(HttpStatusCode.NotFound);
        }
    }

    private sealed class CompareCaseFixture : IDisposable
    {
        private readonly TempDirectory tempDirectory;

        public CompareCaseFixture(TempDirectory tempDirectory, CaseFolderLayout layout)
        {
            this.tempDirectory = tempDirectory;
            Layout = layout;
        }

        public CaseFolderLayout Layout { get; }

        public void Dispose()
        {
            tempDirectory.Dispose();
        }
    }
}
