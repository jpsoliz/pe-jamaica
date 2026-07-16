using System.IO;
using ParcelWorkflowAddIn.Compare;
using ParcelWorkflowAddIn.Innola;

namespace ParcelWorkflowAddIn.Tests.Compare;

internal static class CompareEnterpriseCadasterEvidenceTests
{
    public static void SettingsLoadEnterpriseLegalAndFiscalSources()
    {
        using var settingsFile = new TempFile();
        File.WriteAllText(settingsFile.Path,
            """
            {
              "compare_enterprise_cadaster": {
                "enabled": true,
                "relationship_tolerance_meters": 0.12,
                "result_limit": 250,
                "page_size": 75,
                "legal": {
                  "enabled": true,
                  "source_name": "Legal Cadastre",
                  "layer_url": "https://example.test/legal/FeatureServer/0",
                  "parcel_id_field": "parcel_no",
                  "pid_field": "pid",
                  "volume_field": "vol",
                  "folio_field": "fol",
                  "land_valuation_number_field": "landval",
                  "owner_field": "registered_owner",
                  "parish_field": "parish_name",
                  "suid_field": "suid",
                  "object_id_field": "OBJECTID",
                  "global_id_field": "globalid"
                },
                "fiscal": {
                  "enabled": true,
                  "source_name": "Fiscal Cadastre",
                  "layer_url": "https://example.test/fiscal/FeatureServer/0",
                  "parcel_id_field": "fiscal_pin",
                  "pid_field": "pid",
                  "land_valuation_number_field": "landval",
                  "occupant_field": "occupant",
                  "taxpayer_field": "taxpayer",
                  "parish_field": "parish",
                  "suid_field": "suid",
                  "object_id_field": "objectid",
                  "global_id_field": "globalid"
                }
              }
            }
            """);

        var settings = InnolaTransactionSettings.Load(settingsFile.Path);

        TestAssert.True(settings.CompareEnterpriseCadaster.Enabled, "Enterprise cadaster evidence should be enabled.");
        TestAssert.Equal(0.12, settings.CompareEnterpriseCadaster.RelationshipToleranceMeters, "Tolerance mismatch.");
        TestAssert.Equal(250, settings.CompareEnterpriseCadaster.ResultLimit, "Result limit mismatch.");
        TestAssert.Equal(75, settings.CompareEnterpriseCadaster.PageSize, "Page size mismatch.");
        TestAssert.True(settings.CompareEnterpriseCadaster.Legal.Enabled, "Legal source should be enabled.");
        TestAssert.Equal("https://example.test/legal/FeatureServer/0", settings.CompareEnterpriseCadaster.Legal.LayerUrl, "Legal layer URL mismatch.");
        TestAssert.Equal("registered_owner", settings.CompareEnterpriseCadaster.Legal.OwnerField, "Legal owner field mismatch.");
        TestAssert.True(settings.CompareEnterpriseCadaster.Fiscal.Enabled, "Fiscal source should be enabled.");
        TestAssert.Equal("taxpayer", settings.CompareEnterpriseCadaster.Fiscal.TaxpayerField, "Fiscal taxpayer field mismatch.");
        TestAssert.Equal(null, settings.CompareEnterpriseCadaster.Warning, "Complete config should not warn.");
    }

    public static void QueryPlanUsesSpatialFilterAndSelectedFields()
    {
        var settings = CreateEnterpriseSettings();
        var plan = CompareEnterpriseCadasterEvidenceService.BuildQueryPlan(
            CreateTransaction(),
            ReadyGeometryPlan(),
            settings);

        TestAssert.True(plan.IsValid, "Query plan should be valid.");
        TestAssert.Equal(2, plan.LayerRequests.Count, "Both legal and fiscal requests should be included.");
        TestAssert.True(plan.LayerRequests.All(layer => layer.RequiresGeometry), "Cadaster requests must be spatial queries, not full-layer loads.");
        TestAssert.True(plan.LayerRequests.All(layer => layer.ReturnGeometry), "Cadaster evidence must return geometry for classification.");
        TestAssert.True(plan.LayerRequests.All(layer => layer.OutFields.Contains("pid")), "PID field should be requested.");
        TestAssert.True(plan.LayerRequests.All(layer => layer.ResultLimit == 100), "Result limit should be applied per source.");
        TestAssert.True(plan.LayerRequests.All(layer => layer.PageSize == 50), "Page size should be applied per source.");
    }

    public static void DisabledSourceDoesNotBlockEnabledSource()
    {
        var settings = CreateEnterpriseSettings() with
        {
            Fiscal = CreateEnterpriseSettings().Fiscal with { Enabled = false }
        };

        var plan = CompareEnterpriseCadasterEvidenceService.BuildQueryPlan(
            CreateTransaction(),
            ReadyGeometryPlan(),
            settings);

        TestAssert.True(plan.IsValid, "Plan should remain valid when one source is disabled.");
        TestAssert.Equal(1, plan.LayerRequests.Count, "Only enabled source should be queried.");
        TestAssert.Equal(CompareEnterpriseCadasterSourceKind.Legal, plan.LayerRequests[0].SourceKind, "Legal source should remain.");
        TestAssert.True(plan.Diagnostics.Any(message => message.Contains("Fiscal", StringComparison.OrdinalIgnoreCase)), "Disabled source diagnostic should be visible.");
    }

    public static void ClassifierSortsOverlapsBeforeTouchesAndExcludedRowsLast()
    {
        var now = DateTimeOffset.Parse("2026-07-15T00:00:00Z");
        var rows = new[]
        {
            Row("fiscal-touch", CompareSpatialRelationship.Touches, included: true, now),
            Row("legal-overlap", CompareSpatialRelationship.Overlaps, included: true, now),
            Row("legal-excluded", CompareSpatialRelationship.Overlaps, included: false, now),
            Row("same", CompareSpatialRelationship.SameReviewMatch, included: true, now)
        };

        var sorted = CompareEnterpriseCadasterEvidenceClassifier.Sort(rows).ToArray();

        TestAssert.Equal("same", sorted[0].ParcelId, "Same/review match should sort first.");
        TestAssert.Equal("legal-overlap", sorted[1].ParcelId, "Included overlap should sort before touch.");
        TestAssert.Equal("fiscal-touch", sorted[2].ParcelId, "Touch should sort after overlap.");
        TestAssert.Equal("legal-excluded", sorted[3].ParcelId, "Excluded rows should sort last.");
    }

    public static async Task ViewModelLoadsSpatialRowsAndSeedsManualSearch()
    {
        using var fixture = CompareWorkspaceViewModelTestHelpers.CreateCaseFolderWithSource();
        var service = new StubEnterpriseCadasterEvidenceService(new[]
        {
            Row("10843842", CompareSpatialRelationship.Touches, included: true, DateTimeOffset.Parse("2026-07-15T00:00:00Z"))
                with { Volume = "1486", Folio = "393", LandValuationNumber = "16505005179", Parish = "Manchester" }
        });
        var viewModel = CompareWorkspaceViewModelTestHelpers.CreateViewModel(
            legalService: new MockLegalCadasterQueryService(),
            enterpriseCadasterEvidenceService: service);
        viewModel.ApplyLoadState(CompareWorkspaceViewModelTestHelpers.ReadyState(fixture.Layout.RootDirectory), fixture.Reopen());

        await viewModel.QueryEnterpriseCadasterEvidenceAsync();
        viewModel.SeedSearchFromEnterpriseEvidenceCommand.Execute(viewModel.EnterpriseCadasterEvidenceRows[0]);

        TestAssert.Equal(1, viewModel.EnterpriseCadasterEvidenceRows.Count, "Spatial evidence row should be visible.");
        TestAssert.Equal(CompareEvidenceSearchMode.VolumeFolio, viewModel.SelectedEvidenceSearchMode, "Volume/Folio should be selected when row has title values.");
        TestAssert.Equal("1486", viewModel.SearchVolume, "Volume should be seeded from spatial evidence row.");
        TestAssert.Equal("393", viewModel.SearchFolio, "Folio should be seeded from spatial evidence row.");
        TestAssert.True(viewModel.FiscalEvidenceReviewed, "Spatial evidence refresh should satisfy fiscal evidence review.");
    }

    public static void DraftPersistsIncludedSpatialEvidence()
    {
        using var fixture = CompareWorkspaceViewModelTestHelpers.CreateCaseFolderWithSource();
        var row = Row("10843842", CompareSpatialRelationship.Overlaps, included: true, DateTimeOffset.Parse("2026-07-15T00:00:00Z"))
            with { SourceLabel = "Legal Cadastre", ObjectId = "42", Suid = "S-1" };
        var viewModel = CompareWorkspaceViewModelTestHelpers.CreateViewModel(
            enterpriseCadasterEvidenceService: new StubEnterpriseCadasterEvidenceService(new[] { row }));
        viewModel.ApplyLoadState(CompareWorkspaceViewModelTestHelpers.ReadyState(fixture.Layout.RootDirectory), fixture.Reopen());
        viewModel.QueryEnterpriseCadasterEvidenceAsync().GetAwaiter().GetResult();
        viewModel.SaveProgressCommand.Execute(null);

        var restored = CompareWorkspaceViewModelTestHelpers.CreateViewModel();
        restored.ApplyLoadState(CompareWorkspaceViewModelTestHelpers.ReadyState(fixture.Layout.RootDirectory), fixture.Reopen());

        TestAssert.Equal(1, restored.EnterpriseCadasterEvidenceRows.Count, "Included spatial evidence should restore from draft.");
        TestAssert.True(restored.EnterpriseCadasterEvidenceRows[0].IsIncluded, "Included state should persist.");
        TestAssert.Equal("S-1", restored.EnterpriseCadasterEvidenceRows[0].Suid, "SUID should persist.");
    }

    private static CompareEnterpriseCadasterSettings CreateEnterpriseSettings()
    {
        return new CompareEnterpriseCadasterSettings(
            true,
            0.05,
            100,
            50,
            new CompareEnterpriseCadasterSourceSettings(
                true,
                "Legal Cadastre",
                "https://example.test/legal/FeatureServer/0",
                "parcel_id",
                "pid",
                "volume",
                "folio",
                "landval",
                "owner",
                null,
                null,
                "parish",
                "suid",
                "objectid",
                "globalid",
                null),
            new CompareEnterpriseCadasterSourceSettings(
                true,
                "Fiscal Cadastre",
                "https://example.test/fiscal/FeatureServer/0",
                "parcel_id",
                "pid",
                null,
                null,
                "landval",
                null,
                "occupant",
                "taxpayer",
                "parish",
                "suid",
                "objectid",
                "globalid",
                null),
            null);
    }

    private static CompareWorkingGeometryLoadPlan ReadyGeometryPlan()
    {
        return new CompareWorkingGeometryLoadPlan(
            true,
            "100000668",
            "TR100000668",
            "https://jm-gis.innola-solutions.com/portal",
            "transaction_id",
            "100000668",
            "transaction_id = '100000668'",
            Array.Empty<CompareWorkingLayerRequest>(),
            null);
    }

    private static SelectedInnolaTransaction CreateTransaction()
    {
        return new SelectedInnolaTransaction(
            "task-1",
            "100000668",
            "TR100000668",
            "Compare Survey Plan",
            "Compare",
            DateTimeOffset.Parse("2026-07-15T00:00:00Z"));
    }

    private static CompareEnterpriseCadasterEvidenceRecord Row(
        string parcelId,
        string relationship,
        bool included,
        DateTimeOffset queriedAt)
    {
        return new CompareEnterpriseCadasterEvidenceRecord(
            CompareEnterpriseCadasterSourceKind.Legal,
            "Legal Cadastre",
            "https://example.test/legal/FeatureServer/0",
            "42",
            "global-42",
            null,
            parcelId,
            parcelId,
            null,
            null,
            null,
            "Owner",
            null,
            null,
            "Manchester",
            relationship,
            included,
            queriedAt,
            CompareEvidenceStatus.Ready,
            null);
    }

    private sealed class StubEnterpriseCadasterEvidenceService : ICompareEnterpriseCadasterEvidenceService
    {
        private readonly IReadOnlyList<CompareEnterpriseCadasterEvidenceRecord> records;

        public StubEnterpriseCadasterEvidenceService(IReadOnlyList<CompareEnterpriseCadasterEvidenceRecord> records)
        {
            this.records = records;
        }

        public Task<CompareEnterpriseCadasterEvidenceResult> QueryAsync(
            SelectedInnolaTransaction transaction,
            CompareWorkingGeometryLoadPlan? geometryPlan,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CompareEnterpriseCadasterEvidenceResult.Ready(
                new CompareEnterpriseCadasterEvidenceQuery(
                    transaction.TransactionNumber,
                    geometryPlan?.ScopeField,
                    geometryPlan?.ScopeValue),
                records,
                "Spatial evidence returned."));
        }
    }
}
