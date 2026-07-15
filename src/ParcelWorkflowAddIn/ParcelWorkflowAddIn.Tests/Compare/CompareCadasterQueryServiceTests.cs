using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Compare;
using ParcelWorkflowAddIn.Innola;
using ParcelWorkflowAddIn.Tests;
using ParcelWorkflowAddIn.Workflow.Review;

namespace ParcelWorkflowAddIn.Tests.Compare;

internal static class CompareCadasterQueryServiceTests
{
    private static readonly DateTimeOffset FixedNow = DateTimeOffset.Parse("2026-07-14T00:00:00Z");

    public static async Task LegalParcelIdQueryMapsRecord()
    {
        var service = new MockLegalCadasterQueryService(new[]
        {
            LegalRecord("Jane Brown", "parcel-001", "123", "45", "title-9")
        });

        var result = await service.QueryByParcelIdAsync("parcel-001");

        TestAssert.True(result.Success, "Parcel ID query should succeed.");
        TestAssert.Equal("parcel_id=parcel-001", LegalCadasterQueryResult.BuildLegalQueryKey(result.Query), "Parcel query key mismatch.");
        TestAssert.Equal("Jane Brown", result.Records[0].OwnerName, "Owner mapping mismatch.");
        TestAssert.Equal("title-9", result.Records[0].TitleRecordId, "Title record mapping mismatch.");
    }

    public static async Task LegalVolumeFolioQueryMapsRecord()
    {
        var service = new MockLegalCadasterQueryService(new[]
        {
            LegalRecord("Jane Brown", "parcel-001", "123", "45", "title-9")
        });

        var result = await service.QueryByVolumeFolioAsync("123", "45");

        TestAssert.True(result.Success, "Volume/folio query should succeed.");
        TestAssert.Equal("volume=123;folio=45", LegalCadasterQueryResult.BuildLegalQueryKey(result.Query), "Volume/folio query key mismatch.");
        TestAssert.Equal("parcel-001", result.Records[0].ParcelId, "Parcel ID mapping mismatch.");
    }

    public static async Task FiscalNeighborQueryMapsRecords()
    {
        var service = new MockFiscalCadasterQueryService(new[]
        {
            FiscalRecord("neighbor-1", "touches", "north", "Nadia Neighbor")
        });
        var transaction = Transaction();
        var plan = GeometryPlan();

        var result = await service.QueryNeighborsAsync(transaction, plan);

        TestAssert.True(result.Success, "Fiscal neighbor query should succeed.");
        TestAssert.Equal("transaction_number", result.Query.GeometryScopeField, "Geometry scope field mismatch.");
        TestAssert.Equal("100000674", result.Query.GeometryScopeValue, "Geometry scope value mismatch.");
        TestAssert.Equal("Nadia Neighbor", result.Records[0].OwnerOrTaxpayerDisplay, "Neighbor display mapping mismatch.");
    }

    public static async Task FactoryUsesMockServicesInMockMode()
    {
        var settings = InnolaTransactionSettings.Default with { Mode = "mock" };

        var legal = CompareCadasterQueryServiceFactory.CreateLegal(settings);
        var fiscal = CompareCadasterQueryServiceFactory.CreateFiscal(settings);
        var legalResult = await legal.QueryByParcelIdAsync("parcel-001");
        var fiscalResult = await fiscal.QueryNeighborsAsync(Transaction(), GeometryPlan());

        TestAssert.True(legalResult.Success, "Mock legal service should return a reviewable result.");
        TestAssert.Equal(CompareEvidenceStatus.NoRecordReturned, legalResult.Status, "Empty mock legal service should return no record.");
        TestAssert.True(fiscalResult.Success, "Mock fiscal service should return a reviewable result.");
        TestAssert.Equal(CompareEvidenceStatus.NoRecordReturned, fiscalResult.Status, "Empty mock fiscal service should return no record.");
    }

    public static async Task FactoryReportsConfiguredLiveAdaptersAsUnsupportedUntilContractExists()
    {
        var settings = InnolaTransactionSettings.Default with
        {
            Mode = "live",
            CompareCadaster = new CompareCadasterQuerySettings(
                new CadasterSourceSettings(
                    true,
                    "Legal cadaster",
                    "https://example.test/legal/FeatureServer/0",
                    "parcel_id",
                    "volume",
                    "folio",
                    "owner_name",
                    null,
                    null,
                    null),
                new CadasterSourceSettings(
                    true,
                    "Fiscal cadaster",
                    "https://example.test/fiscal/FeatureServer/0",
                    "parcel_id",
                    null,
                    null,
                    "taxpayer_display",
                    "spatial_relationship",
                    "boundary_side",
                    null),
                30,
                null)
        };

        var legal = CompareCadasterQueryServiceFactory.CreateLegal(settings);
        var fiscal = CompareCadasterQueryServiceFactory.CreateFiscal(settings);
        var legalResult = await legal.QueryByParcelIdAsync("parcel-001");
        var fiscalResult = await fiscal.QueryNeighborsAsync(Transaction(), GeometryPlan());

        TestAssert.False(legalResult.Success, "Configured live legal service should remain blocked until the live adapter contract exists.");
        TestAssert.True(legalResult.Message.Contains("live adapter is not implemented", StringComparison.OrdinalIgnoreCase), "Legal diagnostic should name the missing adapter.");
        TestAssert.False(fiscalResult.Success, "Configured live fiscal service should remain blocked until the live adapter contract exists.");
        TestAssert.True(fiscalResult.Message.Contains("live adapter is not implemented", StringComparison.OrdinalIgnoreCase), "Fiscal diagnostic should name the missing adapter.");
    }

    public static async Task NoRecordCreatesReviewableDiscrepancy()
    {
        using var fixture = CompareCaseFixture.CreateWithExtraction();
        var viewModel = CreateViewModel(new MockLegalCadasterQueryService(getUtcNow: () => FixedNow));
        viewModel.ApplyLoadState(ReadyState(fixture.Layout.RootDirectory), fixture.Reopen());

        await viewModel.QueryParcelIdAsync();

        TestAssert.Equal("No record returned", viewModel.LegalEvidenceStatus, "No-record status should be reviewable.");
        TestAssert.True(viewModel.Discrepancies.Any(item => item.Title.Contains("No legal cadaster", StringComparison.OrdinalIgnoreCase)), "No-record discrepancy should be created.");
        TestAssert.False(viewModel.CanApproveCompare, "No-record discrepancy must block approval until resolved.");
    }

    public static async Task LegalMismatchCreatesSourceLabeledDiscrepancy()
    {
        using var fixture = CompareCaseFixture.CreateWithExtraction();
        var viewModel = CreateViewModel(new MockLegalCadasterQueryService(new[]
        {
            LegalRecord("Other Owner", "parcel-001", "123", "45", "title-9")
        }));
        viewModel.ApplyLoadState(ReadyState(fixture.Layout.RootDirectory), fixture.Reopen());

        await viewModel.QueryParcelIdAsync();

        TestAssert.True(viewModel.LegalCadasterSummary.Contains("Other Owner", StringComparison.Ordinal), "Legal result should render.");
        TestAssert.True(viewModel.Discrepancies.Any(item =>
            item.Source.Contains("Legal cadaster", StringComparison.OrdinalIgnoreCase)
            && item.Title.Contains("owner", StringComparison.OrdinalIgnoreCase)), "Owner mismatch discrepancy should be source-labeled.");
    }

    public static async Task FiscalMismatchCreatesSeparateNeighborDiscrepancy()
    {
        using var fixture = CompareCaseFixture.CreateWithExtraction(adjacentOwner: "Expected Neighbor");
        var viewModel = CreateViewModel(
            fiscalService: new MockFiscalCadasterQueryService(new[]
            {
                FiscalRecord("neighbor-1", "touches", "east", "Different Neighbor")
            }));
        viewModel.ApplyLoadState(ReadyState(fixture.Layout.RootDirectory), fixture.Reopen());

        await viewModel.QueryFiscalNeighborsAsync();

        TestAssert.True(viewModel.FiscalNeighborSummary.Contains("Different Neighbor", StringComparison.Ordinal), "Fiscal result should render separately.");
        TestAssert.True(viewModel.Discrepancies.Any(item =>
            item.Source.Contains("Fiscal cadaster", StringComparison.OrdinalIgnoreCase)
            && item.Title.Contains("adjacent owner", StringComparison.OrdinalIgnoreCase)), "Fiscal neighbor mismatch discrepancy should be source-labeled.");
    }

    public static void RedactsSensitiveDiagnostics()
    {
        var result = LegalCadasterQueryResult.Failed(
            new LegalCadasterQuery("parcel_id", "parcel-001", null, null),
            "Unauthorized token=abc123 password=secret raw body");

        TestAssert.True(!result.Diagnostic!.Contains("abc123", StringComparison.Ordinal), "Token must be redacted.");
        TestAssert.True(!result.Diagnostic.Contains("secret", StringComparison.Ordinal), "Password must be redacted.");
        TestAssert.True(result.Diagnostic.Contains("[REDACTED]", StringComparison.Ordinal), "Redaction marker should be present.");
    }

    private static CompareWorkspaceViewModel CreateViewModel(
        ILegalCadasterQueryService? legalService = null,
        IFiscalCadasterQueryService? fiscalService = null)
    {
        return new CompareWorkspaceViewModel(
            Transaction(),
            legalCadasterQueryService: legalService ?? new MockLegalCadasterQueryService(),
            fiscalCadasterQueryService: fiscalService ?? new MockFiscalCadasterQueryService(),
            surveyPlanEvidenceService: new CompareSurveyPlanEvidenceService(getUtcNow: () => FixedNow));
    }

    private static LegalCadasterRecord LegalRecord(string owner, string parcelId, string volume, string folio, string titleId)
    {
        return new LegalCadasterRecord(
            owner,
            parcelId,
            volume,
            folio,
            titleId,
            "Legal cadaster",
            FixedNow,
            $"parcel_id={parcelId}",
            CompareEvidenceStatus.Ready,
            null);
    }

    private static FiscalCadasterNeighborRecord FiscalRecord(string parcelId, string relationship, string side, string display)
    {
        return new FiscalCadasterNeighborRecord(
            parcelId,
            relationship,
            side,
            display,
            "Fiscal cadaster",
            FixedNow,
            "neighbors",
            CompareEvidenceStatus.Ready,
            null);
    }

    private static SelectedInnolaTransaction Transaction()
    {
        return new SelectedInnolaTransaction(
            "task-1",
            "100000674",
            "TR100000674",
            "Compare Survey Plan",
            "Compare",
            FixedNow);
    }

    private static CompareWorkingGeometryLoadPlan GeometryPlan()
    {
        return new CompareWorkingGeometryLoadPlan(
            true,
            "100000674",
            "TR100000674",
            null,
            "transaction_number",
            "100000674",
            "transaction_number = '100000674'",
            Array.Empty<CompareWorkingLayerRequest>(),
            null);
    }

    private static CompareWorkspaceLoadState ReadyState(string caseFolderPath)
    {
        return new CompareWorkspaceLoadState(
            CompareDocumentLoadState.Loaded("Documents ready.", caseFolderPath),
            CompareWorkingGeometryLoadResult.Ready(
                "Geometry ready.",
                GeometryPlan(),
                CompareMapIntegrationResult.Loaded("Geometry ready.", Array.Empty<string>(), "Compare Review - 100000674", 1)));
    }

    private sealed class CompareCaseFixture : IDisposable
    {
        private readonly TempDirectory tempDirectory;
        private readonly CaseFolderStore store;

        private CompareCaseFixture(TempDirectory tempDirectory, CaseFolderStore store, CaseFolderLayout layout)
        {
            this.tempDirectory = tempDirectory;
            this.store = store;
            Layout = layout;
        }

        public CaseFolderLayout Layout { get; }

        public static CompareCaseFixture CreateWithExtraction(string adjacentOwner = "Expected Neighbor")
        {
            var temp = new TempDirectory();
            var store = new CaseFolderStore(() => FixedNow, () => "run-compare");
            var created = store.CreateCase(temp.Path, "TR100000674", "tester");
            var layout = created.Layout!;
            var document = new ExtractionReviewDocument { TransactionNumber = "TR100000674" };
            document.SurveyMetadataFields.Add(new ExtractionReviewMetadataField { Key = "parcel_id", Value = "parcel-001" });
            document.Parties.Add(new ExtractionReviewNamedParty { Name = "Jane Brown", Role = "owner" });
            document.VolumeFolios.Add(new ExtractionReviewVolumeFolio { Volume = "123", Folio = "45" });
            document.AdjacentOwners.Add(new ExtractionReviewAdjacentOwner { Name = adjacentOwner });
            document.Rows.Add(new ExtractionReviewRow
            {
                RowId = "row-1",
                ParcelName = "parcel-001",
                PointIdentifier = "1",
                Easting = "1",
                Northing = "1"
            });
            var saved = new ExtractionReviewPersistenceService().Save(layout, document, "tester");
            if (!saved.Success)
            {
                throw new InvalidOperationException(saved.Message);
            }

            return new CompareCaseFixture(temp, store, layout);
        }

        public CaseFolderReopenResult Reopen()
        {
            return store.ReopenCaseFolder(Layout.RootDirectory);
        }

        public void Dispose()
        {
            tempDirectory.Dispose();
        }
    }
}
