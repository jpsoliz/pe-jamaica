using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Innola;
using ParcelWorkflowAddIn.Workflow.Disposition;

namespace ParcelWorkflowAddIn.Tests.Innola;

internal static class InnolaSpatialUnitServiceTests
{
    public static async Task CreatesDefaultsThenSavesPopulatedSpatialUnit()
    {
        using var tempRoot = new TempDirectory();
        var layout = CreateLayout(tempRoot.Path);
        var handler = new RecordingHandler(new[]
        {
            "[{\"@c\":\"SpatialUnitExt\",\"id\":\"draft-su\",\"uid\":\"draft-uid\",\"address\":{\"id\":\"addr-1\"},\"link\":{\"id\":\"link-1\"}}]",
            "[{\"@c\":\"SpatialUnitExt\",\"id\":\"su-100000004\",\"uid\":\"saved-uid\"}]"
        });
        var service = new InnolaSpatialUnitService(new HttpClient(handler));

        var result = await service.CreateOrUpdateAsync(
            Session(),
            Transaction(),
            layout.RootDirectory,
            Disposition(layout));

        TestAssert.True(result.Success, "Spatial Unit save should succeed.");
        TestAssert.Equal("su-100000004", result.SpatialUnitId, "Returned Spatial Unit id mismatch.");
        TestAssert.Equal(2, handler.Requests.Count, "Spatial Unit service should make default and save calls.");
        TestAssert.True(handler.Requests[0].PathAndQuery!.Contains("/api/v4/rest/administrative/ladm-objects/create/multi", StringComparison.OrdinalIgnoreCase), "Create defaults route mismatch.");
        TestAssert.True(handler.Requests[0].PathAndQuery.Contains("transactionId=100000004", StringComparison.OrdinalIgnoreCase), "Create defaults transaction binding missing.");
        TestAssert.True(handler.Requests[1].PathAndQuery!.Contains("/api/v4/rest/administrative/ladm-objects?typeKeyId=spatialunit", StringComparison.OrdinalIgnoreCase), "Save route mismatch.");

        using var savePayload = JsonDocument.Parse(handler.Bodies[1]);
        var savedObject = savePayload.RootElement[0];
        TestAssert.Equal("SpatialUnitExt", savedObject.GetProperty("@c").GetString(), "Spatial Unit class should be preserved.");
        TestAssert.Equal("spatial_unit_type_land", savedObject.GetProperty("type").GetString(), "Spatial Unit type mismatch.");
        TestAssert.Equal("reg_status_pending", savedObject.GetProperty("status").GetString(), "Spatial Unit status mismatch.");
        TestAssert.Equal("spatialunit", savedObject.GetProperty("idMarkupType").GetString(), "Spatial Unit markup type mismatch.");
        TestAssert.Equal("approved", savedObject.GetProperty("reviewDecision").GetString(), "Review decision should be sent.");
        TestAssert.Equal("addr-1", savedObject.GetProperty("address").GetProperty("id").GetString(), "API generated address object should be preserved.");
        TestAssert.Equal("link-1", savedObject.GetProperty("link").GetProperty("id").GetString(), "API generated link object should be preserved.");
    }

    public static async Task ReturnsFailureForUnauthorizedSession()
    {
        using var tempRoot = new TempDirectory();
        var layout = CreateLayout(tempRoot.Path);
        var handler = new RecordingHandler(Array.Empty<string>());
        var service = new InnolaSpatialUnitService(new HttpClient(handler));
        var session = Session() with { AccessToken = string.Empty };

        var result = await service.CreateOrUpdateAsync(session, Transaction(), layout.RootDirectory, Disposition(layout));

        TestAssert.True(!result.Success, "Unauthorized session should fail.");
        TestAssert.Equal("unauthorized", result.ErrorCategory, "Unauthorized error category mismatch.");
        TestAssert.Equal(0, handler.Requests.Count, "Unauthorized service must not issue HTTP requests.");
    }

    private static InnolaSession Session()
    {
        return new InnolaSession(
            InnolaSessionStatus.LoggedIn,
            "https://eltrs-dev.innola-solutions.com/",
            "tester",
            "secret-password",
            "token-abc",
            new InnolaUserContext("tester", "Test User", Array.Empty<string>(), Array.Empty<string>()),
            null);
    }

    private static SelectedInnolaTransaction Transaction()
    {
        return new SelectedInnolaTransaction(
            "task-100000004",
            "100000004",
            "TR100000004",
            "Computation Check",
            "parcel_workflow",
            new DateTimeOffset(2026, 6, 10, 12, 0, 0, TimeSpan.Zero),
            null,
            "Plan Examination");
    }

    private static CaseFolderLayout CreateLayout(string root)
    {
        var layout = CaseFolderLayout.For(root, "TR100000004");
        Directory.CreateDirectory(layout.WorkingDirectory);
        Directory.CreateDirectory(layout.OutputDirectory);
        return layout;
    }

    private static ComputeReviewDispositionDocument Disposition(CaseFolderLayout layout)
    {
        return new ComputeReviewDispositionDocument(
            "compute_review_disposition_v1",
            "100000004",
            "TR100000004",
            "task-100000004",
            "approved",
            "Approved for closeout.",
            "tester",
            "2026-06-10T12:00:00.0000000Z",
            Path.Combine(layout.OutputDirectory, "output_summary.json"),
            Path.Combine(layout.OutputDirectory, "enterprise_working_publish.json"),
            "run-output",
            "written",
            Path.Combine(layout.OutputDirectory, "enterprise_working_disposition.json"),
            null,
            null,
            null,
            null,
            null);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Queue<string> responses;

        public RecordingHandler(IEnumerable<string> responses)
        {
            this.responses = new Queue<string>(responses);
        }

        public List<Uri> Requests { get; } = new();

        public List<string> Bodies { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            Bodies.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));
            var response = responses.Count > 0 ? responses.Dequeue() : "{}";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response, Encoding.UTF8, "application/json")
            };
        }
    }
}
