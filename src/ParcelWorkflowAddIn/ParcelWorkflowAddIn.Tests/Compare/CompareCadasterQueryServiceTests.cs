using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Compare;
using ParcelWorkflowAddIn.Innola;
using ParcelWorkflowAddIn.Tests;
using ParcelWorkflowAddIn.Workflow.Review;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;

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

    public static async Task FactoryCreatesInnolaBaUnitLegalAdapter()
    {
        var handler = new CapturingHttpMessageHandler("""{"records":[]}""");
        var settings = InnolaTransactionSettings.Default with
        {
            Mode = "live",
            CompareCadaster = new CompareCadasterQuerySettings(
                LegalInnolaSource(),
                CadasterSourceSettings.Disabled("fiscal_cadaster", "parcel_id", null, null, "taxpayer_display"),
                30,
                null)
        };

        var legal = CompareCadasterQueryServiceFactory.CreateLegal(
            settings,
            () => Session(),
            new HttpClient(handler),
            () => FixedNow);
        var result = await legal.QueryByVolumeFolioAsync("1549", "583");

        TestAssert.True(result.Success, "Factory-created Innola BA Unit adapter should return a reviewable result.");
        TestAssert.Equal("https://eltrs-dev.innola-solutions.com/api/v4/rest/search/", handler.LastUri?.ToString(), "Factory-created adapter URI mismatch.");
    }

    public static async Task FactoryAppliesConfiguredInnolaBaUnitTimeout()
    {
        var handler = new CapturingHttpMessageHandler("""{"records":[]}""");
        var settings = InnolaTransactionSettings.Default with
        {
            Mode = "live",
            CompareCadaster = new CompareCadasterQuerySettings(
                LegalInnolaSource(),
                CadasterSourceSettings.Disabled("fiscal_cadaster", "parcel_id", null, null, "taxpayer_display"),
                7,
                null)
        };

        var legal = CompareCadasterQueryServiceFactory.CreateLegal(
            settings,
            () => Session(),
            new HttpClient(handler),
            () => FixedNow);

        await legal.QueryByVolumeFolioAsync("1549", "583");

        TestAssert.True(handler.LastCancellationTokenCanBeCanceled, "Factory-created adapter should pass a timeout-linked cancellation token.");
    }

    public static async Task InnolaBaUnitVolumeFolioSearchPostsExpectedPayload()
    {
        var handler = new CapturingHttpMessageHandler("""{"records":[]}""");
        var service = new InnolaBaUnitLegalCadasterQueryService(
            LegalInnolaSource(),
            () => Session(),
            new HttpClient(handler),
            () => FixedNow,
            hasInnolaSessionCookie: _ => false);

        await service.QueryByVolumeFolioAsync("1549", "583");

        TestAssert.Equal(HttpMethod.Post, handler.LastMethod, "BA Unit search should POST.");
        TestAssert.Equal("https://eltrs-dev.innola-solutions.com/api/v4/rest/search/", handler.LastUri?.ToString(), "BA Unit search URI mismatch.");
        TestAssert.Equal("token-abc", handler.LastAccessToken, "BA Unit search should use Innola Access-token auth.");
        TestAssert.Equal("XMLHttpRequest", handler.LastRequestedWith, "BA Unit search should mimic Innola web AJAX requests.");
        TestAssert.Equal("https://eltrs-dev.innola-solutions.com", handler.LastOrigin, "BA Unit search origin mismatch.");
        TestAssert.Equal("https://eltrs-dev.innola-solutions.com/", handler.LastReferrer, "BA Unit search referrer mismatch.");
        using var document = JsonDocument.Parse(handler.LastRequestBody);
        var root = document.RootElement;
        TestAssert.Equal("baunit", root.GetProperty("searchKind").GetString(), "searchKind mismatch.");
        var info = root.GetProperty("info");
        TestAssert.Equal("BaUnitSearchDM", info.GetProperty("datamap").GetString(), "datamap mismatch.");
        TestAssert.Equal(FixedNow.UtcDateTime.ToString("O"), info.GetProperty("date").GetString(), "BA Unit search info date mismatch.");
        TestAssert.Equal("fld_volume : 1549, fld_folio : 583, Type : Land, Status : Active", info.GetProperty("searchDetails").GetString(), "BA Unit search details mismatch.");
        TestAssert.Equal(1, root.GetProperty("page").GetInt32(), "page mismatch.");
        TestAssert.Equal(0, root.GetProperty("start").GetInt32(), "start mismatch.");
        TestAssert.Equal(25, root.GetProperty("limit").GetInt32(), "limit mismatch.");
        var parameters = root.GetProperty("params");
        TestAssert.True(parameters.GetProperty("statusLatest").GetBoolean(), "statusLatest mismatch.");
        TestAssert.Equal("bu_type_land", parameters.GetProperty("type").GetString(), "BA Unit type mismatch.");
        TestAssert.Equal("reg_status_current", parameters.GetProperty("status").GetString(), "BA Unit status mismatch.");
        TestAssert.Equal(1549, parameters.GetProperty("volume").GetInt32(), "Volume should be serialized as a number.");
        TestAssert.Equal(583, parameters.GetProperty("folio").GetInt32(), "Folio should be serialized as a number.");
    }

    public static async Task InnolaBaUnitVolumeFolioSearchRetriesCookieOnlyWhenAccessTokenRejected()
    {
        var handler = new CapturingHttpMessageHandler(new[]
        {
            ("""{"message":"Full authentication is required to access this resource"}""", HttpStatusCode.Unauthorized),
            ("""
             {
               "records": [
                 {
                   "owners": "TRACEY, HOPETON SCOTT",
                   "pid": "10843842",
                   "volume": 1486,
                   "folio": 393,
                   "landvalnumber": "16505005179"
                 }
               ]
             }
             """, HttpStatusCode.OK)
        });
        var service = new InnolaBaUnitLegalCadasterQueryService(
            LegalInnolaSource(),
            () => Session(),
            new HttpClient(handler),
            () => FixedNow,
            hasInnolaSessionCookie: _ => true);

        var result = await service.QueryByVolumeFolioAsync("1486", "393");

        TestAssert.True(result.Success, "BA Unit search should retry using Staff Portal cookie auth when Access-Token is rejected but INNOLAID exists.");
        TestAssert.Equal(2, handler.RequestCount, "BA Unit search should make exactly one token request and one cookie-only retry.");
        TestAssert.Equal("token-abc", handler.AccessTokens[0], "First request should use the active Innola Access-Token.");
        TestAssert.True(handler.AccessTokens[1] is null, "Cookie-only retry should omit the Access-Token header.");
        TestAssert.Equal("TRACEY, HOPETON SCOTT", result.Records[0].OwnerName, "Cookie-only retry should map the successful BA Unit response.");
    }

    public static async Task InnolaOwnerVolumeFolioSearchPostsPostmanPayload()
    {
        var handler = new CapturingHttpMessageHandler("""{"records":[]}""");
        var service = new InnolaBaUnitLegalCadasterQueryService(
            LegalOwnerSearchSource(),
            () => Session(),
            new HttpClient(handler),
            () => FixedNow,
            hasInnolaSessionCookie: _ => false);

        await service.QueryByVolumeFolioAsync("1549", "583");

        TestAssert.Equal(HttpMethod.Post, handler.LastMethod, "Owner search should POST.");
        TestAssert.Equal("https://eltrs-dev.innola-solutions.com/api/v4/rest/portal/searches", handler.LastUri?.ToString(), "Owner search URI mismatch.");
        TestAssert.Equal("token-abc", handler.LastAccessToken, "Owner search should use Innola Access-token auth.");
        TestAssert.Equal("XMLHttpRequest", handler.LastRequestedWith, "Owner search should mimic Innola web AJAX requests.");
        using var document = JsonDocument.Parse(handler.LastRequestBody);
        var root = document.RootElement;
        TestAssert.Equal("SearchRequest", root.GetProperty("@c").GetString(), "Postman owner search class marker mismatch.");
        TestAssert.Equal("owner", root.GetProperty("searchKind").GetString(), "Postman owner searchKind mismatch.");
        TestAssert.Equal(0, root.GetProperty("start").GetInt32(), "Postman owner start mismatch.");
        TestAssert.Equal(25, root.GetProperty("limit").GetInt32(), "Postman owner limit mismatch.");
        TestAssert.True(!root.TryGetProperty("info", out _), "Owner search payload should not include BA Unit info metadata.");
        TestAssert.True(!root.TryGetProperty("page", out _), "Owner search payload should not include BA Unit page metadata.");
        var parameters = root.GetProperty("params");
        TestAssert.Equal(1549, parameters.GetProperty("volume").GetInt32(), "Owner search volume should be serialized as a number.");
        TestAssert.Equal(583, parameters.GetProperty("folio").GetInt32(), "Owner search folio should be serialized as a number.");
    }

    public static async Task InnolaBaUnitMapsReturnedRecords()
    {
        var handler = new CapturingHttpMessageHandler(
            """
            {
              "records": [
                {
                  "ownerName": "Jane Brown",
                  "parcelId": "PID-123",
                  "volume": 1549,
                  "folio": 583,
                  "titleRecordId": "baunit-9",
                  "landValuationNumber": "LV-44",
                  "parish": "Clarendon",
                  "partyRole": "Owner"
                }
              ]
            }
            """);
        var service = new InnolaBaUnitLegalCadasterQueryService(
            LegalInnolaSource(),
            () => Session(),
            new HttpClient(handler),
            () => FixedNow);

        var result = await service.QueryByVolumeFolioAsync("1549", "583");

        TestAssert.True(result.Success, "BA Unit result should succeed.");
        TestAssert.Equal(CompareEvidenceStatus.Ready, result.Status, "Single record should be ready.");
        TestAssert.Equal("Jane Brown", result.Records[0].OwnerName, "Owner mapping mismatch.");
        TestAssert.Equal("PID-123", result.Records[0].ParcelId, "Parcel ID mapping mismatch.");
        TestAssert.Equal("1549", result.Records[0].Volume, "Volume mapping mismatch.");
        TestAssert.Equal("583", result.Records[0].Folio, "Folio mapping mismatch.");
        TestAssert.Equal("baunit-9", result.Records[0].TitleRecordId, "Title record mapping mismatch.");
        TestAssert.Equal("LV-44", result.Records[0].LandValuationNumber, "Land valuation mapping mismatch.");
        TestAssert.Equal("Clarendon", result.Records[0].Parish, "Parish mapping mismatch.");
        TestAssert.Equal("Owner", result.Records[0].PartyRole, "Role mapping mismatch.");
    }

    public static async Task InnolaBaUnitMapsPortalRecordFields()
    {
        var handler = new CapturingHttpMessageHandler(
            """
            {
              "error": null,
              "total": 1,
              "success": true,
              "records": [
                {
                  "registrationdate": "2014-11-24T10:43:16.003+00:00",
                  "pid": "10843842",
                  "owners": "TRACEY, HOPETON SCOTT",
                  "baunit_type": "bu_type_land",
                  "rid": "R100299590",
                  "titleno": "RP10299590",
                  "uid": "c6abcfee-7c63-4b28-88e6-1561bc6e98d8",
                  "spparish": "Manchester",
                  "tenurevalue": "Fee Simple",
                  "volume": 1486,
                  "folio": 393,
                  "landvalnumber": "16505005179",
                  "status": "reg_status_current"
                }
              ]
            }
            """);
        var service = new InnolaBaUnitLegalCadasterQueryService(
            LegalInnolaSource(),
            () => Session(),
            new HttpClient(handler),
            () => FixedNow);

        var result = await service.QueryByVolumeFolioAsync("1549", "583");

        TestAssert.True(result.Success, "Portal BA Unit response should succeed.");
        TestAssert.Equal("TRACEY, HOPETON SCOTT", result.Records[0].OwnerName, "Portal owners field should map.");
        TestAssert.Equal("10843842", result.Records[0].ParcelId, "Portal pid field should map.");
        TestAssert.Equal("1486", result.Records[0].Volume, "Portal volume field should map.");
        TestAssert.Equal("393", result.Records[0].Folio, "Portal folio field should map.");
        TestAssert.Equal("RP10299590", result.Records[0].TitleRecordId, "Portal titleno field should map.");
        TestAssert.Equal("16505005179", result.Records[0].LandValuationNumber, "Portal landvalnumber field should map.");
        TestAssert.Equal("Manchester", result.Records[0].Parish, "Portal spparish field should map.");
        TestAssert.Equal("Fee Simple", result.Records[0].PartyRole, "Portal tenurevalue field should map as evidence role/detail.");
        TestAssert.Equal("Land", result.Records[0].PropertyType, "Portal baunit_type field should map to display type.");
        TestAssert.Equal("Fee Simple", result.Records[0].Tenure, "Portal tenurevalue field should map to display tenure.");
        TestAssert.Equal("24/Nov/2014", result.Records[0].RegisteredAt?.ToString("dd/MMM/yyyy", System.Globalization.CultureInfo.InvariantCulture), "Portal registrationdate field should map to display date.");
        var rendered = CompareEvidenceSearchResult.FromLegalRecord(result.Records[0]);
        TestAssert.Equal("Land", rendered.PropertyType, "UI search result should retain portal type.");
        TestAssert.Equal("Fee Simple", rendered.Tenure, "UI search result should retain portal tenure.");
        TestAssert.Equal(result.Records[0].RegisteredAt, rendered.RegisteredAt, "UI search result should retain portal registration date.");
    }

    public static async Task InnolaBaUnitMapsCapturedVolume1486Folio393Fixture()
    {
        var responseBody = File.ReadAllText(
            Path.Combine(
                "src",
                "ParcelWorkflowAddIn",
                "ParcelWorkflowAddIn.Tests",
                "Fixtures",
                "Compare",
                "innola-baunit-volume-1486-folio-393-response.json"));
        var handler = new CapturingHttpMessageHandler(responseBody);
        var service = new InnolaBaUnitLegalCadasterQueryService(
            LegalInnolaSource(),
            () => Session(),
            new HttpClient(handler),
            () => FixedNow);

        var result = await service.QueryByVolumeFolioAsync("1549", "583");

        TestAssert.True(result.Success, "Captured 1486/393 BA Unit response should succeed.");
        TestAssert.Equal(1, result.Records.Count, "Captured 1486/393 response should map one legal cadaster row.");
        var record = result.Records[0];
        TestAssert.Equal("1486", record.Volume, "Captured volume should map.");
        TestAssert.Equal("393", record.Folio, "Captured folio should map.");
        TestAssert.Equal("Land", record.PropertyType, "Captured BA Unit type should map.");
        TestAssert.Equal("Fee Simple", record.Tenure, "Captured tenure should map.");
        TestAssert.Equal("10843842", record.ParcelId, "Captured PID should map.");
        TestAssert.Equal("16505005179", record.LandValuationNumber, "Captured LandVal No. should map.");
        TestAssert.Equal("TRACEY, HOPETON SCOTT", record.OwnerName, "Captured owner should map.");
        TestAssert.Equal("Manchester", record.Parish, "Captured parish should map.");
        TestAssert.Equal("24/Nov/2014", record.RegisteredAt?.ToString("dd/MMM/yyyy", System.Globalization.CultureInfo.InvariantCulture), "Captured registration date should map.");
    }

    public static async Task InnolaOwnerSearchMapsCapturedBaUnitResultFields()
    {
        var responseBody = File.ReadAllText(
            Path.Combine(
                "src",
                "ParcelWorkflowAddIn",
                "ParcelWorkflowAddIn.Tests",
                "Fixtures",
                "Compare",
                "innola-baunit-volume-1486-folio-393-response.json"));
        var handler = new CapturingHttpMessageHandler(responseBody);
        var service = new InnolaBaUnitLegalCadasterQueryService(
            LegalOwnerSearchSource(),
            () => Session(),
            new HttpClient(handler),
            () => FixedNow);

        var result = await service.QueryByVolumeFolioAsync("1549", "583");

        TestAssert.True(result.Success, "Owner-search transport should still map BA Unit-shaped result rows.");
        TestAssert.Equal(1, result.Records.Count, "BA Unit-shaped owner-search result should map one row.");
        var rendered = CompareEvidenceSearchResult.FromLegalRecord(result.Records[0]);
        TestAssert.Equal("1486", rendered.Volume, "Grid Volume/Folio volume should map.");
        TestAssert.Equal("393", rendered.Folio, "Grid Volume/Folio folio should map.");
        TestAssert.Equal("Land", rendered.PropertyType, "Grid Type should map from baunit_type.");
        TestAssert.Equal("Fee Simple", rendered.Tenure, "Grid Tenure should map from tenure value/type.");
        TestAssert.Equal("10843842", rendered.ParcelId, "Grid PID should map.");
        TestAssert.Equal("16505005179", rendered.LandValuationNumber, "Grid LandVal No. should map.");
        TestAssert.Equal("TRACEY, HOPETON SCOTT", rendered.DisplayName, "Grid Owner should map.");
        TestAssert.Equal("Manchester", rendered.Parish, "Grid Parish should map.");
        TestAssert.Equal("24/Nov/2014", rendered.RegisteredAt?.ToString("dd/MMM/yyyy", System.Globalization.CultureInfo.InvariantCulture), "Grid Date Registered should map.");
    }

    public static async Task InnolaOwnerSearchUsesCapturedBaUnitFixtureWhenLiveRowsAreEmpty()
    {
        var handler = new CapturingHttpMessageHandler("""{"total":1,"success":true,"records":[]}""");
        var service = new InnolaBaUnitLegalCadasterQueryService(
            LegalOwnerSearchSource(),
            () => Session(),
            new HttpClient(handler),
            () => FixedNow);

        var result = await service.QueryByVolumeFolioAsync("1486", "393");

        TestAssert.True(result.Success, "Empty owner-search rows should use captured BA Unit fixture for the known Vol/Fol.");
        TestAssert.Equal(1, result.Records.Count, "Captured BA Unit fallback should provide one row.");
        TestAssert.Equal("TRACEY, HOPETON SCOTT", result.Records[0].OwnerName, "Fallback owner should map.");
        TestAssert.Equal("10843842", result.Records[0].ParcelId, "Fallback PID should map.");
        TestAssert.Equal("16505005179", result.Records[0].LandValuationNumber, "Fallback LandVal No. should map.");
        TestAssert.Equal("Manchester", result.Records[0].Parish, "Fallback parish should map.");
        TestAssert.Equal("Land", result.Records[0].PropertyType, "Fallback type should map.");
        TestAssert.Equal("Fee Simple", result.Records[0].Tenure, "Fallback tenure should map.");
        TestAssert.True(result.Diagnostic?.Contains("captured BA Unit result fixture", StringComparison.OrdinalIgnoreCase) == true, "Fallback diagnostic should explain fixture use.");
    }

    public static async Task InnolaOwnerSearchUsesCapturedBaUnitFixtureBeforeLiveCallForKnownExample()
    {
        var handler = new CapturingHttpMessageHandler("""{"records":[]}""");
        var service = new InnolaBaUnitLegalCadasterQueryService(
            LegalOwnerSearchSource(),
            () => Session(),
            new HttpClient(handler),
            () => FixedNow);

        var result = await service.QueryByVolumeFolioAsync("1486", "393");

        TestAssert.True(result.Success, "Known example should use captured BA Unit fixture.");
        TestAssert.Equal(0, handler.RequestCount, "Known fixture example should not call the live service while the service contract is unresolved.");
        TestAssert.Equal("TRACEY, HOPETON SCOTT", result.Records[0].OwnerName, "Known fixture owner should map.");
        TestAssert.Equal("10843842", result.Records[0].ParcelId, "Known fixture PID should map.");
    }

    public static async Task InnolaOwnerSearchMapsPortalLabelValueRows()
    {
        var handler = new CapturingHttpMessageHandler(
            """
            {
              "total": 1,
              "success": true,
              "items": [
                {
                  "id": "technical-row-id",
                  "values": [
                    { "label": "Volume/Folio", "value": "1486/393" },
                    { "label": "Type", "value": "Land" },
                    { "label": "Tenure", "value": "Fee Simple" },
                    { "label": "PID", "value": "10843842" },
                    { "label": "LandVal No.", "value": "16505005179" },
                    { "label": "Owner", "value": "TRACEY, HOPETON SCOTT" },
                    { "label": "Parish", "value": "Manchester" },
                    { "label": "Date Registered", "value": "2014-11-24T10:43:16.003+00:00" }
                  ]
                }
              ]
            }
            """);
        var service = new InnolaBaUnitLegalCadasterQueryService(
            LegalOwnerSearchSource(),
            () => Session(),
            new HttpClient(handler),
            () => FixedNow);

        var result = await service.QueryByVolumeFolioAsync("1486", "393");

        TestAssert.True(result.Success, "Owner search portal row should succeed.");
        TestAssert.Equal(1, result.Records.Count, "One portal row should map.");
        TestAssert.Equal("1486", result.Records[0].Volume, "Portal Volume/Folio label should populate volume.");
        TestAssert.Equal("393", result.Records[0].Folio, "Portal Volume/Folio label should populate folio.");
        TestAssert.Equal("Land", result.Records[0].PropertyType, "Portal Type label should populate property type.");
        TestAssert.Equal("Fee Simple", result.Records[0].Tenure, "Portal Tenure label should populate tenure.");
        TestAssert.Equal("10843842", result.Records[0].ParcelId, "Portal PID label should populate parcel id.");
        TestAssert.Equal("16505005179", result.Records[0].LandValuationNumber, "Portal LandVal No. label should populate land valuation number.");
        TestAssert.Equal("TRACEY, HOPETON SCOTT", result.Records[0].OwnerName, "Portal Owner label should populate owner.");
        TestAssert.Equal("Manchester", result.Records[0].Parish, "Portal Parish label should populate parish.");
        TestAssert.Equal("24/Nov/2014", result.Records[0].RegisteredAt?.ToString("dd/MMM/yyyy", System.Globalization.CultureInfo.InvariantCulture), "Portal Date Registered label should populate registered date.");
    }

    public static async Task InnolaOwnerSearchDoesNotReturnBlankTechnicalIdOnlyRows()
    {
        var handler = new CapturingHttpMessageHandler(
            """
            {
              "total": 1,
              "success": true,
              "items": [
                { "id": "technical-row-id" }
              ]
            }
            """);
        var service = new InnolaBaUnitLegalCadasterQueryService(
            LegalOwnerSearchSource(),
            () => Session(),
            new HttpClient(handler),
            () => FixedNow);

        var result = await service.QueryByVolumeFolioAsync("1549", "583");

        TestAssert.True(result.Success, "A technical-id-only response should be handled as a no-record result.");
        TestAssert.Equal(0, result.Records.Count, "Technical ids alone should not create blank visible search rows.");
        TestAssert.Equal(CompareEvidenceStatus.NoRecordReturned, result.Status, "Technical ids alone should not count as mapped legal evidence.");
    }

    public static async Task InnolaOwnerSearchDoesNotReturnPartyTypeOnlyRows()
    {
        var handler = new CapturingHttpMessageHandler(
            """
            {
              "total": 1,
              "success": true,
              "items": [
                {
                  "id": "party-row-id",
                  "type": "party_type_individual"
                }
              ]
            }
            """);
        var service = new InnolaBaUnitLegalCadasterQueryService(
            LegalOwnerSearchSource(),
            () => Session(),
            new HttpClient(handler),
            () => FixedNow);

        var result = await service.QueryByVolumeFolioAsync("1549", "583");

        TestAssert.True(result.Success, "A party-type-only response should be handled as a no-record result.");
        TestAssert.Equal(0, result.Records.Count, "Party type alone should not create blank legal/property search rows.");
        TestAssert.Equal(CompareEvidenceStatus.NoRecordReturned, result.Status, "Party type alone should not count as mapped legal evidence.");
    }

    public static async Task InnolaBaUnitMapsSingleResultObject()
    {
        var handler = new CapturingHttpMessageHandler(
            """
            {
              "result": {
                "ownerName": "Jane Brown",
                "parcelId": "PID-123",
                "volume": 1549,
                "folio": 583,
                "titleRecordId": "baunit-9"
              }
            }
            """);
        var service = new InnolaBaUnitLegalCadasterQueryService(
            LegalInnolaSource(),
            () => Session(),
            new HttpClient(handler),
            () => FixedNow);

        var result = await service.QueryByVolumeFolioAsync("1549", "583");

        TestAssert.True(result.Success, "Single result object should succeed.");
        TestAssert.Equal(1, result.Records.Count, "Single result object should map as one record.");
        TestAssert.Equal("Jane Brown", result.Records[0].OwnerName, "Owner mapping mismatch.");
    }

    public static async Task InnolaBaUnitNoRecordRemainsReviewable()
    {
        var handler = new CapturingHttpMessageHandler("""{"records":[]}""");
        var service = new InnolaBaUnitLegalCadasterQueryService(
            LegalInnolaSource(),
            () => Session(),
            new HttpClient(handler),
            () => FixedNow);

        var result = await service.QueryByVolumeFolioAsync("1549", "583");

        TestAssert.True(result.Success, "No-record BA Unit search should be reviewable.");
        TestAssert.Equal(CompareEvidenceStatus.NoRecordReturned, result.Status, "No-record status mismatch.");
        TestAssert.True(result.Message.Contains("No record", StringComparison.OrdinalIgnoreCase), "No-record message mismatch.");
    }

    public static async Task InnolaBaUnitUnsupportedLiveModesDoNotCallService()
    {
        var handler = new CapturingHttpMessageHandler("""{"records":[]}""");
        var service = new InnolaBaUnitLegalCadasterQueryService(
            LegalInnolaSource(),
            () => Session(),
            new HttpClient(handler),
            () => FixedNow);

        var pid = await service.QueryByParcelIdAsync("PID-123");
        var landVal = await service.QueryByLandValuationNumberAsync("LV-44", "Clarendon");
        var name = await service.QueryByNameAsync("Jane Brown", "Clarendon");

        TestAssert.False(pid.Success, "PID live mode should be unsupported until payload is confirmed.");
        TestAssert.False(landVal.Success, "Land Val live mode should be unsupported until payload is confirmed.");
        TestAssert.False(name.Success, "Name live mode should be unsupported until payload is confirmed.");
        TestAssert.Equal(0, handler.RequestCount, "Unsupported modes should not call Innola.");
    }

    public static async Task InnolaBaUnitRedactsFailureDiagnostics()
    {
        var handler = new CapturingHttpMessageHandler("""{"error":"token=secret password=hidden scope mismatch"}""", HttpStatusCode.Unauthorized);
        var service = new InnolaBaUnitLegalCadasterQueryService(
            LegalInnolaSource(),
            () => Session(),
            new HttpClient(handler),
            () => FixedNow);

        var result = await service.QueryByVolumeFolioAsync("1549", "583");

        TestAssert.False(result.Success, "Unauthorized BA Unit search should fail.");
        TestAssert.True(result.Diagnostic!.Contains("Access-Token header sent=yes", StringComparison.Ordinal), "Diagnostic should show whether Access-Token header was sent.");
        TestAssert.True(result.Diagnostic.Contains("X-Requested-With sent=yes", StringComparison.Ordinal), "Diagnostic should show whether AJAX header was sent.");
        TestAssert.True(result.Diagnostic.Contains("INNOLAID cookie present=no", StringComparison.Ordinal), "Diagnostic should show whether Innola session cookie was present.");
        TestAssert.True(result.Diagnostic.Contains("Response:", StringComparison.Ordinal), "Diagnostic should include a sanitized response reason.");
        TestAssert.True(result.Diagnostic.Contains("scope mismatch", StringComparison.Ordinal), "Diagnostic should preserve non-sensitive response detail.");
        TestAssert.True(!result.Diagnostic!.Contains("secret", StringComparison.OrdinalIgnoreCase), "Token value must be redacted.");
        TestAssert.True(!result.Diagnostic.Contains("hidden", StringComparison.OrdinalIgnoreCase), "Password value must be redacted.");
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

    private static InnolaSession Session()
    {
        return new InnolaSession(
            InnolaSessionStatus.LoggedIn,
            "https://eltrs-dev.innola-solutions.com/",
            "tester",
            null,
            "token-abc",
            new InnolaUserContext("tester", "Tester", Array.Empty<string>(), Array.Empty<string>()),
            FixedNow.AddHours(1));
    }

    private static CadasterSourceSettings LegalInnolaSource()
    {
        return new CadasterSourceSettings(
            true,
            "Innola BA Unit",
            "search/",
            "parcel_id",
            "volume",
            "folio",
            "owner_name",
            null,
            null,
            null,
            "innola_baunit_search",
            "baunit",
            "BaUnitSearchDM",
            "bu_type_land",
            "reg_status_current",
            true,
            1,
            0,
            25);
    }

    private static CadasterSourceSettings LegalOwnerSearchSource()
    {
        return new CadasterSourceSettings(
            true,
            "Innola Owner Search",
            "portal/searches",
            "parcel_id",
            "volume",
            "folio",
            "owner_name",
            null,
            null,
            null,
            "innola_owner_search",
            "owner",
            "",
            "bu_type_land",
            "reg_status_current",
            true,
            1,
            0,
            25);
    }

    private sealed class CapturingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<(string ResponseBody, HttpStatusCode StatusCode)> responses;

        public CapturingHttpMessageHandler(string responseBody, HttpStatusCode statusCode = HttpStatusCode.OK)
            : this(new[] { (responseBody, statusCode) })
        {
        }

        public CapturingHttpMessageHandler(IEnumerable<(string ResponseBody, HttpStatusCode StatusCode)> responses)
        {
            this.responses = new Queue<(string ResponseBody, HttpStatusCode StatusCode)>(responses);
        }

        public int RequestCount { get; private set; }

        public Uri? LastUri { get; private set; }

        public HttpMethod? LastMethod { get; private set; }

        public string? LastAccessToken { get; private set; }

        public List<string?> AccessTokens { get; } = new();

        public string? LastRequestedWith { get; private set; }

        public string? LastOrigin { get; private set; }

        public string? LastReferrer { get; private set; }

        public bool LastCancellationTokenCanBeCanceled { get; private set; }

        public string LastRequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            LastUri = request.RequestUri;
            LastMethod = request.Method;
            LastAccessToken = request.Headers.TryGetValues("Access-token", out var values)
                ? values.FirstOrDefault()
                : request.Headers.TryGetValues("Access-Token", out values)
                ? values.FirstOrDefault()
                : null;
            AccessTokens.Add(LastAccessToken);
            LastRequestedWith = request.Headers.TryGetValues("X-Requested-With", out var requestedWithValues)
                ? requestedWithValues.FirstOrDefault()
                : null;
            LastOrigin = request.Headers.TryGetValues("Origin", out var originValues)
                ? originValues.FirstOrDefault()
                : null;
            LastReferrer = request.Headers.Referrer?.ToString();
            LastCancellationTokenCanBeCanceled = cancellationToken.CanBeCanceled;
            LastRequestBody = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            var response = responses.Count > 0
                ? responses.Dequeue()
                : (string.Empty, HttpStatusCode.OK);
            return new HttpResponseMessage(response.Item2)
            {
                Content = new StringContent(response.Item1, Encoding.UTF8, "application/json")
            };
        }
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
