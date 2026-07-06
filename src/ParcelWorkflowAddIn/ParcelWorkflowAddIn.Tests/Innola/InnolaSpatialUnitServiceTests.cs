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
        WriteOutputSummary(layout);
        var handler = new RecordingHandler(new[]
        {
            "[{\"@c\":\"SpatialUnitExt\",\"id\":\"draft-su-1\",\"uid\":\"draft-uid-1\",\"address\":{\"id\":\"addr-1\"},\"link\":{\"id\":\"link-1\"}},{\"@c\":\"SpatialUnitExt\",\"id\":\"draft-su-2\",\"uid\":\"draft-uid-2\",\"address\":{\"id\":\"addr-2\"},\"link\":{\"id\":\"link-2\"}}]",
            "[{\"@c\":\"SpatialUnitExt\",\"id\":\"su-100000004\",\"uid\":\"saved-uid\"},{\"@c\":\"SpatialUnitExt\",\"id\":\"su-100000005\",\"uid\":\"saved-uid-2\"}]"
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
        using var createPayload = JsonDocument.Parse(handler.Bodies[0]);
        TestAssert.Equal(2, createPayload.RootElement.GetArrayLength(), "Spatial Unit defaults should be requested once per computed polygon.");

        using var savePayload = JsonDocument.Parse(handler.Bodies[1]);
        TestAssert.Equal(2, savePayload.RootElement.GetArrayLength(), "Saved Spatial Unit payload should contain one object per computed polygon.");
        var savedObject = savePayload.RootElement[0];
        TestAssert.Equal("SpatialUnitExt", savedObject.GetProperty("@c").GetString(), "Spatial Unit class should be preserved.");
        TestAssert.Equal("spatial_unit_type_land", savedObject.GetProperty("type").GetString(), "Spatial Unit type mismatch.");
        TestAssert.Equal("reg_status_pending", savedObject.GetProperty("status").GetString(), "Spatial Unit status mismatch.");
        TestAssert.Equal("spatialunit", savedObject.GetProperty("idMarkupType").GetString(), "Spatial Unit markup type mismatch.");
        TestAssert.Equal("approved", savedObject.GetProperty("reviewDecision").GetString(), "Review decision should be sent.");
        TestAssert.Equal("TR100000004-completed.zip", savedObject.GetProperty("workingPackageFileName").GetString(), "Working package file name should be sent.");
        TestAssert.Equal("sidwell_completed_package", savedObject.GetProperty("workingPackageSourceType").GetString(), "Working package source type should be sent.");
        TestAssert.Equal("pending", savedObject.GetProperty("workingPackageUploadStatus").GetString(), "Working package upload state should be sent.");
        TestAssert.Equal("output/enterprise_working_publish.json", savedObject.GetProperty("enterprisePublishRef").GetString(), "Enterprise publish reference should be sent.");
        TestAssert.Equal("output/output_summary.json", savedObject.GetProperty("outputSummaryRef").GetString(), "Output summary reference should be sent.");
        var workingLayer = savedObject.GetProperty("workingLayerReferences")[0];
        TestAssert.Equal("polygons", workingLayer.GetProperty("layerRole").GetString(), "Working layer reference should include layer role.");
        TestAssert.Equal("https://example.test/server/rest/services/working_review/FeatureServer/2", workingLayer.GetProperty("target").GetString(), "Working layer reference should include target.");
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
            "TR100000004-completed.zip",
            "sidwell_completed_package",
            "pending");
    }

    private static void WriteOutputSummary(CaseFolderLayout layout)
    {
        File.WriteAllText(
            Path.Combine(layout.OutputDirectory, "output_summary.json"),
            """
            {
              "schema_version": "output_summary_v1",
              "transaction_id": "100000004",
              "run_id": "run-output",
              "created_at": "2026-06-10T12:00:00.0000000Z",
              "created_by": "tester",
              "source_manifest_hash": "hash",
              "payload": {
                "status": "created",
                "review_workspace_mode": "enterprise_working_layers",
                "artifact_paths": [],
                "map_layer_paths": [],
                "built_parcel_count": 2,
                "built_line_count": 4,
                "built_point_count": 6,
                "point_count": 6,
                "line_count": 4,
                "polygon_count": 2,
                "parcel_record_name": "Lot 12",
                "parcel_record_id": "record-12",
                "parcel_type": "Plan Examination",
                "enterprise_working_publish": {
                  "status": "succeeded",
                  "message": "published",
                  "published_at": "2026-06-10T12:00:00.0000000Z",
                  "published_by": "tester",
                  "transaction_scope_field": "transaction_number",
                  "transaction_scope_value": "TR100000004",
                  "workflow_name": "parcel_workflow",
                  "workflow_stage": "spatial_review_pending",
                  "transaction_id": "100000004",
                  "transaction_number": "TR100000004",
                  "task_id": "task-100000004",
                  "transaction_type": "Plan Examination",
                  "assigned_user": "tester",
                  "assigned_group": "Plan Examination",
                  "last_saved_utc": "2026-06-10T12:00:00.0000000Z",
                  "published_layers": [
                    {
                      "layer_role": "polygons",
                      "target": "https://example.test/server/rest/services/working_review/FeatureServer/2",
                      "record_count": 2,
                      "replaced_existing": true
                    }
                  ],
                  "local_only_artifacts": [],
                  "warnings": [],
                  "errors": []
                }
              },
              "warnings": [],
              "errors": []
            }
            """);
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
