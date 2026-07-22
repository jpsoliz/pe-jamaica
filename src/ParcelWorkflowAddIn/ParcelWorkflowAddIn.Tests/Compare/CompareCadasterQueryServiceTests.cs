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
        var handler = new CapturingHttpMessageHandler(
            """
            {
              "records": [
                {
                  "owners": "Jane Brown",
                  "pid": "PID-123",
                  "volume": 1549,
                  "folio": 583,
                  "titleno": "RP100"
                }
              ],
              "total": 1,
              "success": true
            }
            """);
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

    public static async Task InnolaBaUnitPidLandValAndOwnerSearchPostExpectedPayloads()
    {
        var handler = new CapturingHttpMessageHandler(new[]
        {
            ("""{"records":[]}""", HttpStatusCode.OK),
            ("""{"records":[]}""", HttpStatusCode.OK),
            ("""{"records":[]}""", HttpStatusCode.OK)
        });
        var service = new InnolaBaUnitLegalCadasterQueryService(
            LegalInnolaSource(),
            () => Session(),
            new HttpClient(handler),
            () => FixedNow,
            hasInnolaSessionCookie: _ => false);

        await service.QueryByParcelIdAsync("10843842");
        using (var document = JsonDocument.Parse(handler.LastRequestBody))
        {
            var root = document.RootElement;
            TestAssert.Equal("baunit", root.GetProperty("searchKind").GetString(), "PID searchKind mismatch.");
            var parameters = root.GetProperty("params");
            TestAssert.Equal("10843842", parameters.GetProperty("pid").GetString(), "PID should be serialized in BA Unit params.");
            TestAssert.Equal("fld_pid : 10843842, Type : Land, Status : Active", root.GetProperty("info").GetProperty("searchDetails").GetString(), "PID search details mismatch.");
        }

        await service.QueryByLandValuationNumberAsync("16505005179");
        using (var document = JsonDocument.Parse(handler.LastRequestBody))
        {
            var root = document.RootElement;
            var parameters = root.GetProperty("params");
            TestAssert.Equal("16505005179", parameters.GetProperty("landValNumber").GetString(), "LandVal No. should be serialized in camel-case BA Unit params.");
            TestAssert.Equal("16505005179", parameters.GetProperty("landvalnumber").GetString(), "LandVal No. should also be serialized in Innola response field casing.");
            TestAssert.Equal("fld_landvalnumber : 16505005179, Type : Land, Status : Active", root.GetProperty("info").GetProperty("searchDetails").GetString(), "LandVal No. search details mismatch.");
        }

        await service.QueryByNameAsync("Tracey");
        using (var document = JsonDocument.Parse(handler.LastRequestBody))
        {
            var root = document.RootElement;
            var parameters = root.GetProperty("params");
            TestAssert.Equal("%TRACEY%", parameters.GetProperty("ownername").GetString(), "Owner search should add upper-case wildcard markers.");
            TestAssert.True(!parameters.TryGetProperty("owner", out _), "Owner search should use the confirmed ownername variable.");
            TestAssert.True(!parameters.TryGetProperty("owners", out _), "Owner search should use a single owner variable.");
            TestAssert.Equal("fld_ownername : %TRACEY%, Type : Land, Status : Active", root.GetProperty("info").GetProperty("searchDetails").GetString(), "Owner search details mismatch.");
        }
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

        TestAssert.True(handler.RequestUris.Count > 0, "Owner search should issue at least one request.");
        TestAssert.Equal("https://eltrs-dev.innola-solutions.com/api/v4/rest/portal/searches", handler.RequestUris[0]?.ToString(), "Owner search URI mismatch.");
        TestAssert.Equal("token-abc", handler.AccessTokens[0], "Owner search should use Innola Access-token auth.");
        TestAssert.Equal("XMLHttpRequest", handler.LastRequestedWith, "Owner search should mimic Innola web AJAX requests.");
        using var document = JsonDocument.Parse(handler.RequestBodies[0]);
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

    public static async Task InnolaOwnerVolumeFolioFallsBackToBaUnitWhenOwnerSearchIsEmpty()
    {
        var handler = new CapturingHttpMessageHandler(new[]
        {
            ("""{"records":[],"total":0,"success":true,"error":null}""", HttpStatusCode.OK),
            ("""
             {
               "error": null,
               "total": 1,
               "success": true,
               "records": [
                 {
                   "registrationdate": "2026-07-21T00:00:00.000+00:00",
                   "pid": "N/A",
                   "baunit_type": "bu_type_land",
                   "titleno": "RP99999999",
                   "volume": 1328,
                   "folio": 856,
                   "tenurevalue": "Fee Simple",
                   "status": "reg_status_current"
                 }
               ]
             }
             """, HttpStatusCode.OK)
        });
        var service = new InnolaBaUnitLegalCadasterQueryService(
            LegalOwnerSearchSource(),
            () => Session(),
            new HttpClient(handler),
            () => FixedNow,
            hasInnolaSessionCookie: _ => false);

        var result = await service.QueryByVolumeFolioAsync("1328", "856");

        TestAssert.True(result.Success, "BA Unit fallback should succeed when owner search is empty.");
        TestAssert.Equal(CompareEvidenceStatus.Ready, result.Status, "Fallback record should be ready.");
        TestAssert.Equal(2, handler.RequestCount, "Volume/Folio should try owner search first, then BA Unit fallback.");
        TestAssert.Equal("https://eltrs-dev.innola-solutions.com/api/v4/rest/portal/searches", handler.RequestUris[0]?.ToString(), "First query should use owner search.");
        TestAssert.Equal("https://eltrs-dev.innola-solutions.com/api/v4/rest/search/", handler.RequestUris[1]?.ToString(), "Fallback query should use BA Unit property search.");
        TestAssert.Equal("1328", result.Records[0].Volume, "Fallback volume should map.");
        TestAssert.Equal("856", result.Records[0].Folio, "Fallback folio should map.");
        TestAssert.Equal("RP99999999", result.Records[0].TitleRecordId, "Fallback title should map.");
        TestAssert.Equal("Land", result.Records[0].PropertyType, "Fallback property type should map.");
    }

    public static async Task InnolaOwnerPidSearchFallsBackToBaUnitWhenOwnerSearchIsUnauthorized()
    {
        var handler = new CapturingHttpMessageHandler(new[]
        {
            ("""
             {
               "type": "about:blank",
               "title": "Unauthorized",
               "status": 401,
               "detail": "Full authentication is required to access this resource"
             }
             """, HttpStatusCode.Unauthorized),
            ("""
             {
               "error": null,
               "total": 1,
               "success": true,
               "records": [
                 {
                   "pid": "11140063",
                   "owners": "RANGLIN, MATTHEW, SMITH, CHERRIEN",
                   "baunit_type": "bu_type_land",
                   "titleno": "RP10342601",
                   "volume": 1508,
                   "folio": 408,
                   "tenurevalue": "Fee Simple",
                   "status": "reg_status_current"
                 }
               ]
             }
             """, HttpStatusCode.OK)
        });
        var service = new InnolaBaUnitLegalCadasterQueryService(
            LegalOwnerSearchSource(),
            () => Session(),
            new HttpClient(handler),
            () => FixedNow,
            hasInnolaSessionCookie: _ => false);

        var result = await service.QueryByParcelIdAsync("11140063");

        TestAssert.True(result.Success, "BA Unit fallback should succeed when PID owner search is unauthorized.");
        TestAssert.Equal(CompareEvidenceStatus.Ready, result.Status, "Fallback PID record should be ready.");
        TestAssert.Equal(2, handler.RequestCount, "PID should try owner search first, then BA Unit fallback.");
        TestAssert.Equal("https://eltrs-dev.innola-solutions.com/api/v4/rest/portal/searches", handler.RequestUris[0]?.ToString(), "First PID query should use owner search.");
        TestAssert.Equal("https://eltrs-dev.innola-solutions.com/api/v4/rest/search/", handler.RequestUris[1]?.ToString(), "PID fallback query should use BA Unit property search.");
        TestAssert.Equal("11140063", result.Records[0].ParcelId, "Fallback PID should map.");
        TestAssert.Equal("RANGLIN, MATTHEW, SMITH, CHERRIEN", result.Records[0].OwnerName, "Fallback owner should map.");
        TestAssert.Equal("1508", result.Records[0].Volume, "Fallback volume should map.");
        TestAssert.Equal("408", result.Records[0].Folio, "Fallback folio should map.");
        TestAssert.Equal("RP10342601", result.Records[0].TitleRecordId, "Fallback title should map.");
    }

    public static async Task InnolaOwnerPidLandValAndNameSearchPostPostmanEnvelope()
    {
        var service = CreateOwnerSearchService(out var handler);
        await service.QueryByParcelIdAsync("10843842");
        AssertOwnerSearchEnvelope(handler.LastRequestBody, expectedSearchKind: "owner");
        using (var document = JsonDocument.Parse(handler.LastRequestBody))
        {
            var parameters = document.RootElement.GetProperty("params");
            TestAssert.Equal("10843842", parameters.GetProperty("pid").GetString(), "PID owner-search param mismatch.");
        }

        service = CreateOwnerSearchService(out handler);
        await service.QueryByLandValuationNumberAsync("16505005179");
        AssertOwnerSearchEnvelope(handler.LastRequestBody, expectedSearchKind: "owner");
        using (var document = JsonDocument.Parse(handler.LastRequestBody))
        {
            var parameters = document.RootElement.GetProperty("params");
            TestAssert.True(!parameters.TryGetProperty("landValNumber", out _), "Owner-search LandVal payload should not include the unconfirmed camel-case key.");
            TestAssert.Equal("16505005179", parameters.GetProperty("landvalnumber").GetString(), "LandVal owner-search lowercase param mismatch.");
        }

        service = CreateOwnerSearchService(out handler);
        await service.QueryByNameAsync("Tracey");
        AssertOwnerSearchEnvelope(handler.LastRequestBody, expectedSearchKind: "baunit");
        using (var document = JsonDocument.Parse(handler.LastRequestBody))
        {
            var parameters = document.RootElement.GetProperty("params");
            TestAssert.Equal("%TRACEY%", parameters.GetProperty("ownername").GetString(), "Owner-name search should add upper-case wildcard ownername param.");
            TestAssert.True(!parameters.TryGetProperty("owner", out _), "Owner-name search should use the confirmed ownername variable.");
            TestAssert.True(!parameters.TryGetProperty("owners", out _), "Owner-name search should use the single Postman owner variable.");
        }
    }

    public static async Task InnolaOwnerSearchPayloadContractIgnoresConfiguredSearchKind()
    {
        var source = LegalOwnerSearchSource() with { SearchKind = "changed_by_settings" };
        var handler = new CapturingHttpMessageHandler("""{"records":[]}""");
        var service = new InnolaBaUnitLegalCadasterQueryService(
            source,
            () => Session(),
            new HttpClient(handler),
            () => FixedNow,
            hasInnolaSessionCookie: _ => false);

        await service.QueryByParcelIdAsync("10843842");
        AssertOwnerSearchEnvelope(handler.LastRequestBody, expectedSearchKind: "owner");
        using (var document = JsonDocument.Parse(handler.LastRequestBody))
        {
            var root = document.RootElement;
            var parameters = root.GetProperty("params");
            TestAssert.Equal(1, parameters.EnumerateObject().Count(), "PID Postman payload should contain only pid.");
            TestAssert.Equal("10843842", parameters.GetProperty("pid").GetString(), "PID Postman payload mismatch.");
        }

        await service.QueryByNameAsync("Tracey");
        AssertOwnerSearchEnvelope(handler.LastRequestBody, expectedSearchKind: "baunit");
        using (var document = JsonDocument.Parse(handler.LastRequestBody))
        {
            var root = document.RootElement;
            var parameters = root.GetProperty("params");
            TestAssert.Equal(1, parameters.EnumerateObject().Count(), "Owner-name Postman payload should contain only ownername.");
            TestAssert.Equal("%TRACEY%", parameters.GetProperty("ownername").GetString(), "Owner-name Postman payload mismatch.");
        }
    }

    public static void PostmanOwnerSearchReferenceIsStored()
    {
        var path = Path.Combine(
            "src",
            "ParcelWorkflowAddIn",
            "ParcelWorkflowAddIn.Tests",
            "Fixtures",
            "Compare",
            "Sidwell Plan Exam Scenario.postman_collection2.json");

        TestAssert.True(File.Exists(path), "Postman owner-search reference collection should be stored with Compare fixtures.");
        var body = File.ReadAllText(path);
        using var document = JsonDocument.Parse(body);
        var storedRequests = document.RootElement
            .GetProperty("item")
            .EnumerateArray()
            .Where(item => item.TryGetProperty("request", out _))
            .ToArray();
        TestAssert.True(storedRequests.Any(item =>
            item.GetProperty("request").GetProperty("url").GetProperty("raw").GetString()?.Contains("portal/searches", StringComparison.OrdinalIgnoreCase) == true),
            "Stored Postman fixture should include the portal/searches endpoint.");
        TestAssert.True(storedRequests.Any(item =>
            item.GetProperty("request").TryGetProperty("body", out var requestBody)
            && requestBody.TryGetProperty("raw", out var raw)
            && raw.GetString()?.Contains("\"searchKind\": \"owner\"", StringComparison.OrdinalIgnoreCase) == true),
            "Stored Postman fixture should include the owner search request.");
    }

    private static InnolaBaUnitLegalCadasterQueryService CreateOwnerSearchService(out CapturingHttpMessageHandler handler)
    {
        handler = new CapturingHttpMessageHandler("""{"records":[]}""");
        return new InnolaBaUnitLegalCadasterQueryService(
            LegalOwnerSearchSource(),
            () => Session(),
            new HttpClient(handler),
            () => FixedNow,
            hasInnolaSessionCookie: _ => false);
    }

    private static void AssertOwnerSearchEnvelope(string requestBody, string expectedSearchKind = "owner", int expectedStart = 0)
    {
        using var document = JsonDocument.Parse(requestBody);
        var root = document.RootElement;
        TestAssert.Equal("SearchRequest", root.GetProperty("@c").GetString(), "Owner search should use the Postman SearchRequest envelope.");
        TestAssert.Equal(expectedSearchKind, root.GetProperty("searchKind").GetString(), "Owner searchKind mismatch.");
        TestAssert.Equal(expectedStart, root.GetProperty("start").GetInt32(), "Owner search start mismatch.");
        TestAssert.Equal(25, root.GetProperty("limit").GetInt32(), "Owner search limit mismatch.");
        TestAssert.True(!root.TryGetProperty("info", out _), "Owner search envelope should not include BA Unit info metadata.");
        TestAssert.True(!root.TryGetProperty("page", out _), "Owner search envelope should not include BA Unit page metadata.");
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

    public static async Task InnolaOwnerPidSearchUsesCapturedBaUnitFixtureWhenLiveRowsAreEmpty()
    {
        var handler = new CapturingHttpMessageHandler("""{"total":0,"success":true,"records":[]}""");
        var service = new InnolaBaUnitLegalCadasterQueryService(
            LegalOwnerSearchSource(),
            () => Session(),
            new HttpClient(handler),
            () => FixedNow);

        var result = await service.QueryByParcelIdAsync("10843842");

        TestAssert.Equal(1, handler.RequestCount, "PID search should still call the live Postman contract before fixture fallback.");
        AssertOwnerSearchEnvelope(handler.LastRequestBody, expectedSearchKind: "owner");
        using (var document = JsonDocument.Parse(handler.LastRequestBody))
        {
            var parameters = document.RootElement.GetProperty("params");
            TestAssert.Equal("10843842", parameters.GetProperty("pid").GetString(), "PID owner-search payload must keep the Postman pid variable.");
            TestAssert.Equal(1, parameters.EnumerateObject().Count(), "PID owner-search payload should not add alternate property keys.");
        }

        TestAssert.True(result.Success, "Empty PID owner-search rows should use captured BA Unit fixture for the known PID.");
        TestAssert.Equal(1, result.Records.Count, "Captured BA Unit PID fallback should provide one property row.");
        var record = result.Records[0];
        TestAssert.Equal("1486", record.Volume, "Fallback volume should map from the captured BA Unit row.");
        TestAssert.Equal("393", record.Folio, "Fallback folio should map from the captured BA Unit row.");
        TestAssert.Equal("Land", record.PropertyType, "Fallback type should map.");
        TestAssert.Equal("Fee Simple", record.Tenure, "Fallback tenure should map.");
        TestAssert.Equal("10843842", record.ParcelId, "Fallback PID should map.");
        TestAssert.Equal("16505005179", record.LandValuationNumber, "Fallback LandVal No. should map.");
        TestAssert.Equal("TRACEY, HOPETON SCOTT", record.OwnerName, "Fallback owner should map.");
        TestAssert.Equal("Manchester", record.Parish, "Fallback parish should map.");
        TestAssert.Equal("24/Nov/2014", record.RegisteredAt?.ToString("dd/MMM/yyyy", System.Globalization.CultureInfo.InvariantCulture), "Fallback registration date should map.");
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

    public static async Task InnolaOwnerPidSearchMapsPridRows()
    {
        var handler = new CapturingHttpMessageHandler(
            """
            {
              "total": 1,
              "success": true,
              "records": [
                {
                  "fulladdress": "Cave Mountain, Cave Post Office Westmoreland",
                  "prid": "10954223",
                  "owners": "SMITH, EVERTON",
                  "baunit_type": "bu_type_land",
                  "tenuretype": "tenure_type_freehold",
                  "volume": 1421,
                  "folio": 880,
                  "landvalnumber": "19006005055",
                  "spparish": "St.James",
                  "registrationdate": "2005-11-10T00:00:00.000+00:00"
                }
              ]
            }
            """);
        var service = new InnolaBaUnitLegalCadasterQueryService(
            LegalOwnerSearchSource(),
            () => Session(),
            new HttpClient(handler),
            () => FixedNow);

        var result = await service.QueryByParcelIdAsync("10954223");

        using (var document = JsonDocument.Parse(handler.LastRequestBody))
        {
            var root = document.RootElement;
            TestAssert.Equal("SearchRequest", root.GetProperty("@c").GetString(), "PID search should use the Postman envelope.");
            TestAssert.Equal("owner", root.GetProperty("searchKind").GetString(), "PID searchKind should use the owner Postman method.");
            TestAssert.Equal("10954223", root.GetProperty("params").GetProperty("pid").GetString(), "PID request value mismatch.");
        }

        TestAssert.True(result.Success, "PID owner search should succeed.");
        TestAssert.Equal(1, result.Records.Count, "PID owner search should map one row.");
        TestAssert.Equal("10954223", result.Records[0].ParcelId, "PID search should map Innola prid to the PID column.");
        TestAssert.Equal("SMITH, EVERTON", result.Records[0].OwnerName, "PID search owner mismatch.");
        TestAssert.Equal("1421", result.Records[0].Volume, "PID search volume mismatch.");
        TestAssert.Equal("880", result.Records[0].Folio, "PID search folio mismatch.");
        TestAssert.Equal("19006005055", result.Records[0].LandValuationNumber, "PID search LandVal mismatch.");
        TestAssert.Equal("Land", result.Records[0].PropertyType, "PID search type mismatch.");
        TestAssert.Equal("Fee Simple", result.Records[0].Tenure, "PID search tenure mismatch.");
    }

    public static async Task InnolaOwnerLandValSearchMapsPridRows()
    {
        var handler = new CapturingHttpMessageHandler(
            """
            {
              "total": 1,
              "success": true,
              "records": [
                {
                  "fulladdress": "Cave Mountain, Cave Post Office Westmoreland",
                  "prid": "10954223",
                  "owners": "SMITH, EVERTON",
                  "baunit_type": "bu_type_land",
                  "tenuretype": "tenure_type_freehold",
                  "volume": 1421,
                  "folio": 880,
                  "landvalnumber": "19006005055",
                  "spparish": "St.James",
                  "registrationdate": "2005-11-10T00:00:00.000+00:00"
                }
              ]
            }
            """);
        var service = new InnolaBaUnitLegalCadasterQueryService(
            LegalOwnerSearchSource(),
            () => Session(),
            new HttpClient(handler),
            () => FixedNow);

        var result = await service.QueryByLandValuationNumberAsync("19006005055");

        using (var document = JsonDocument.Parse(handler.LastRequestBody))
        {
            var root = document.RootElement;
            TestAssert.Equal("SearchRequest", root.GetProperty("@c").GetString(), "LandVal search should use the Postman envelope.");
            TestAssert.Equal("owner", root.GetProperty("searchKind").GetString(), "LandVal searchKind should use the owner Postman method.");
            TestAssert.Equal("19006005055", root.GetProperty("params").GetProperty("landvalnumber").GetString(), "LandVal request value mismatch.");
        }

        TestAssert.True(result.Success, "LandVal owner search should succeed.");
        TestAssert.Equal(1, result.Records.Count, "LandVal owner search should map one row.");
        TestAssert.Equal("10954223", result.Records[0].ParcelId, "LandVal search should map Innola prid to the PID column.");
        TestAssert.Equal("19006005055", result.Records[0].LandValuationNumber, "LandVal search LandVal mismatch.");
        TestAssert.Equal("SMITH, EVERTON", result.Records[0].OwnerName, "LandVal search owner mismatch.");
        TestAssert.Equal("1421", result.Records[0].Volume, "LandVal search volume mismatch.");
        TestAssert.Equal("880", result.Records[0].Folio, "LandVal search folio mismatch.");
    }

    public static async Task InnolaOwnerLandValSearchPaginatesPostmanEnvelope()
    {
        var handler = new CapturingHttpMessageHandler(new[]
        {
            (OwnerSearchPageResponse(49, 11032262, 25, "16505010005"), HttpStatusCode.OK),
            (OwnerSearchPageResponse(49, 11032287, 24, "16505010005"), HttpStatusCode.OK)
        });
        var service = new InnolaBaUnitLegalCadasterQueryService(
            LegalOwnerSearchSource(),
            () => Session(),
            new HttpClient(handler),
            () => FixedNow);

        var result = await service.QueryByLandValuationNumberAsync("16505010005");

        TestAssert.True(result.Success, "Paged LandVal owner search should succeed.");
        TestAssert.Equal(49, result.Records.Count, "Paged LandVal owner search should merge both result pages.");
        TestAssert.Equal(2, handler.RequestCount, "Paged LandVal owner search should request the second page.");
        AssertOwnerSearchEnvelope(handler.RequestBodies[0], expectedSearchKind: "owner");
        AssertOwnerSearchEnvelope(handler.RequestBodies[1], expectedSearchKind: "owner", expectedStart: 25);
        using (var firstDocument = JsonDocument.Parse(handler.RequestBodies[0]))
        {
            var root = firstDocument.RootElement;
            TestAssert.Equal(0, root.GetProperty("start").GetInt32(), "First LandVal page should preserve configured start.");
            TestAssert.Equal(25, root.GetProperty("limit").GetInt32(), "First LandVal page should preserve configured limit.");
            TestAssert.Equal("16505010005", root.GetProperty("params").GetProperty("landvalnumber").GetString(), "First LandVal page should preserve Postman landvalnumber param.");
            TestAssert.True(!root.TryGetProperty("info", out _), "Paged owner search must not add BA Unit info metadata.");
            TestAssert.True(!root.TryGetProperty("page", out _), "Paged owner search must not add BA Unit page metadata.");
        }

        using (var secondDocument = JsonDocument.Parse(handler.RequestBodies[1]))
        {
            var root = secondDocument.RootElement;
            TestAssert.Equal(25, root.GetProperty("start").GetInt32(), "Second LandVal page should advance by the configured limit.");
            TestAssert.Equal(25, root.GetProperty("limit").GetInt32(), "Second LandVal page should preserve configured limit.");
            TestAssert.Equal("16505010005", root.GetProperty("params").GetProperty("landvalnumber").GetString(), "Second LandVal page should preserve Postman landvalnumber param.");
            TestAssert.True(!root.TryGetProperty("info", out _), "Second page must not add BA Unit info metadata.");
            TestAssert.True(!root.TryGetProperty("page", out _), "Second page must not add BA Unit page metadata.");
        }

        TestAssert.Equal("11032262", result.Records[0].ParcelId, "First page PID should map.");
        TestAssert.Equal("11032310", result.Records[^1].ParcelId, "Second page PID should map.");
        TestAssert.Equal("16505010005", result.Records[^1].LandValuationNumber, "Second page LandVal should map.");
    }

    public static async Task InnolaOwnerNameSearchMapsMultiplePortalRows()
    {
        var handler = new CapturingHttpMessageHandler(
            """
            {
              "total": 3,
              "success": true,
              "records": [
                {
                  "registrationdate": "2008-11-26T00:00:00.000+00:00",
                  "pid": "10915474",
                  "owners": "TRACEY, WINSTON SEYMOUR, TULL...",
                  "baunit_type": "bu_type_land",
                  "tenurevalue": "Fee Simple",
                  "volume": 1426,
                  "folio": 220
                },
                {
                  "registrationdate": "2010-06-29T00:00:00.000+00:00",
                  "pid": "10397822",
                  "owners": "HENRY, BERYL, TRACEY, NICOLE",
                  "baunit_type": "bu_type_land",
                  "tenurevalue": "Fee Simple",
                  "volume": 1442,
                  "folio": 703,
                  "landvalnumber": "07003014016",
                  "spparish": "St.Mary"
                },
                {
                  "registrationdate": "2013-09-13T00:00:00.000+00:00",
                  "pid": "10984563",
                  "owners": "SMITH, TRACEY-ANN DELITH",
                  "baunit_type": "bu_type_land",
                  "tenurevalue": "Fee Simple",
                  "volume": 1473,
                  "folio": 594,
                  "landvalnumber": "19004012058",
                  "spparish": "St.Catherine"
                }
              ]
            }
            """);
        var service = new InnolaBaUnitLegalCadasterQueryService(
            LegalOwnerSearchSource(),
            () => Session(),
            new HttpClient(handler),
            () => FixedNow);

        var result = await service.QueryByNameAsync("%tracey%");

        using (var document = JsonDocument.Parse(handler.LastRequestBody))
        {
            var parameters = document.RootElement.GetProperty("params");
            TestAssert.Equal("%TRACEY%", parameters.GetProperty("ownername").GetString(), "Owner search request variable should be upper-case wildcard.");
            TestAssert.True(!parameters.TryGetProperty("owner", out _), "Owner search request should use ownername, not owner.");
        }

        TestAssert.True(result.Success, "Owner name search should succeed.");
        TestAssert.Equal(CompareEvidenceStatus.Ambiguous, result.Status, "Multiple owner rows should be shown as ambiguous/reviewable.");
        TestAssert.Equal(3, result.Records.Count, "All owner search records should be mapped.");
        TestAssert.Equal("1426", result.Records[0].Volume, "First owner row volume mismatch.");
        TestAssert.Equal("220", result.Records[0].Folio, "First owner row folio mismatch.");
        TestAssert.Equal("Land", result.Records[0].PropertyType, "First owner row type mismatch.");
        TestAssert.Equal("Fee Simple", result.Records[0].Tenure, "First owner row tenure mismatch.");
        TestAssert.Equal("10915474", result.Records[0].ParcelId, "First owner row PID mismatch.");
        TestAssert.Equal("TRACEY, WINSTON SEYMOUR, TULL...", result.Records[0].OwnerName, "First owner row owner mismatch.");
        TestAssert.Equal("1442", result.Records[1].Volume, "Second owner row volume mismatch.");
        TestAssert.Equal("703", result.Records[1].Folio, "Second owner row folio mismatch.");
        TestAssert.Equal("07003014016", result.Records[1].LandValuationNumber, "Second owner row LandVal mismatch.");
        TestAssert.Equal("St.Mary", result.Records[1].Parish, "Second owner row parish mismatch.");
        TestAssert.Equal("29/Jun/2010", result.Records[1].RegisteredAt?.ToString("dd/MMM/yyyy", System.Globalization.CultureInfo.InvariantCulture), "Second owner row date mismatch.");
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

    public static async Task InnolaOwnerSearchDoesNotReturnPartyTypeRowsWithOnlyPidAndId()
    {
        var handler = new CapturingHttpMessageHandler(
            """
            {
              "total": 1,
              "success": true,
              "records": [
                {
                  "id": "019e2b74-5738-7063-93f6-73afdc13a886",
                  "pid": "100311792",
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

        var result = await service.QueryByParcelIdAsync("11032262");

        TestAssert.True(result.Success, "A party-type row with only PID/id should be handled as a no-record result.");
        TestAssert.Equal(0, result.Records.Count, "Party type plus PID/id alone should not create a visible legal/property result.");
        TestAssert.Equal(CompareEvidenceStatus.NoRecordReturned, result.Status, "Party type plus PID/id alone should not count as mapped legal evidence.");
    }

    public static async Task InnolaOwnerSearchSplitsPartyRowsFromPropertyRows()
    {
        var handler = new CapturingHttpMessageHandler(
            """
            {
              "total": 2,
              "success": true,
              "records": [
                {
                  "id": "019e2b74-5738-7063-93f6-73afdc13a886",
                  "prid": "100778284",
                  "type": "party_type_individual",
                  "fullname": "KING, WILTON F.",
                  "fulladdress": "Cave Mountain, Cave Post Office Westmoreland",
                  "taxnumber": "TX-55",
                  "status": "reg_status_current"
                },
                {
                  "pid": "11032262",
                  "owners": "KING, WILTON F.",
                  "baunit_type": "bu_type_land",
                  "tenurevalue": "Fee Simple",
                  "volume": 1447,
                  "folio": 138,
                  "landvalnumber": "16505010005",
                  "spparish": "Manchester",
                  "registrationdate": "2010-12-21T00:00:00.000+00:00"
                }
              ]
            }
            """);
        var service = new InnolaBaUnitLegalCadasterQueryService(
            LegalOwnerSearchSource(),
            () => Session(),
            new HttpClient(handler),
            () => FixedNow);

        var result = await service.QueryByLandValuationNumberAsync("16505010005");

        TestAssert.True(result.Success, "Mixed party/property rows should remain a successful legal search.");
        TestAssert.Equal(1, result.Records.Count, "Only BA Unit/property-shaped rows should appear in property search results.");
        TestAssert.Equal("KING, WILTON F.", result.Records[0].OwnerName, "Property result owner mismatch.");
        TestAssert.Equal("11032262", result.Records[0].ParcelId, "Property result PID mismatch.");
        TestAssert.Equal(1, result.PartyRecords?.Count ?? 0, "Party-shaped rows should be split into related party matches.");
        TestAssert.Equal("KING, WILTON F.", result.PartyRecords![0].PartyName, "Party match name mismatch.");
        TestAssert.Equal("100778284", result.PartyRecords[0].Prid, "Party match PRID mismatch.");
        TestAssert.Equal("Cave Mountain, Cave Post Office Westmoreland", result.PartyRecords[0].FullAddress, "Party match address mismatch.");
        TestAssert.Equal("TX-55", result.PartyRecords[0].TaxNumber, "Party match tax number mismatch.");
        TestAssert.Equal("party_type_individual", result.PartyRecords[0].PartyType, "Party match type mismatch.");
    }

    public static async Task InnolaOwnerSearchReportsRawRowsWhenAllRowsAreFiltered()
    {
        var handler = new CapturingHttpMessageHandler(
            """
            {
              "total": 1,
              "success": true,
              "records": [
                {
                  "id": "019e2b74-5738-7063-93f6-73afdc13a886",
                  "pid": "100311792",
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

        var result = await service.QueryByLandValuationNumberAsync("16505010005");

        TestAssert.True(result.Success, "Filtered owner-search rows should remain a reviewable no-record result.");
        TestAssert.Equal(0, result.Records.Count, "Party-only rows must not render as property evidence.");
        TestAssert.Equal(CompareEvidenceStatus.NoRecordReturned, result.Status, "Filtered rows should remain no-record for Compare.");
        TestAssert.True(result.Diagnostic!.Contains("1 raw row", StringComparison.Ordinal), "Diagnostic should reveal that raw rows were returned.");
        TestAssert.True(result.Diagnostic.Contains("reported total=1", StringComparison.Ordinal), "Diagnostic should preserve the reported total.");
        TestAssert.True(result.Diagnostic.Contains("first raw row fields: id, pid, type", StringComparison.Ordinal), "Diagnostic should list raw field names without values.");
    }

    public static async Task InnolaOwnerSearchCapturesRawDebugRowsWhenRowsAreFiltered()
    {
        var handler = new CapturingHttpMessageHandler(
            """
            {
              "total": 2,
              "success": true,
              "records": [
                {
                  "id": "019e2b74-5738-7063-93f6-73afdc13a886",
                  "prid": "100778284",
                  "type": "party_type_individual",
                  "fullname": "KING, WILTON F.",
                  "debug": "token=secret password=hidden"
                },
                {
                  "id": "019e2b74-a1fc-7503-9392-3c3c59d92f78",
                  "prid": "100603420",
                  "type": "party_type_individual",
                  "fullname": "SCOTT-HERON, ROSELEE"
                }
              ]
            }
            """);
        var service = new InnolaBaUnitLegalCadasterQueryService(
            LegalOwnerSearchSource(),
            () => Session(),
            new HttpClient(handler),
            () => FixedNow);

        var result = await service.QueryByLandValuationNumberAsync("16505010005");

        TestAssert.True(result.Success, "Filtered owner-search rows should remain reviewable.");
        TestAssert.Equal(0, result.Records.Count, "Party rows without property evidence should not render search results.");
        TestAssert.True(result.RawDebug is not null, "Filtered owner-search rows should keep raw debug metadata.");
        TestAssert.Equal(1, result.RawDebug!.ResponsePageCount, "Raw debug should capture response page count.");
        TestAssert.Equal(2, result.RawDebug.RawRecordCount, "Raw debug should capture every raw row.");
        TestAssert.Equal(2, result.RawDebug.ReportedTotal, "Raw debug should capture response total.");
        TestAssert.Equal(2, result.RawDebug.Rows.Count, "Raw debug should preserve raw row values.");
        TestAssert.Equal("100778284", result.RawDebug.Rows[0].Values["prid"], "Raw debug should preserve the first raw row PRID.");
        TestAssert.Equal("party_type_individual", result.RawDebug.Rows[0].Values["type"], "Raw debug should preserve the first raw row type.");
        TestAssert.Equal("KING, WILTON F.", result.RawDebug.Rows[0].Values["fullname"], "Raw debug should preserve scalar row fields.");
        TestAssert.True(result.RawDebug.Rows[0].Values["debug"]?.Contains("[REDACTED]", StringComparison.Ordinal) == true, "Raw debug should redact sensitive scalar values.");
    }

    public static async Task CompareLegalQueryTraceWritesRawDebugArtifact()
    {
        using var fixture = CompareCaseFixture.CreateWithExtraction();
        var handler = new CapturingHttpMessageHandler(
            """
            {
              "total": 1,
              "success": true,
              "records": [
                {
                  "id": "019e2b74-5738-7063-93f6-73afdc13a886",
                  "prid": "100778284",
                  "type": "party_type_individual",
                  "fullname": "KING, WILTON F."
                }
              ]
            }
            """);
        var service = new InnolaBaUnitLegalCadasterQueryService(
            LegalOwnerSearchSource(),
            () => Session(),
            new HttpClient(handler),
            () => FixedNow);
        var result = await service.QueryByLandValuationNumberAsync("16505010005");

        new CompareLegalQueryTracePersistenceService()
            .Append(fixture.Layout, "100000668", result, FixedNow);

        var rawDebugPath = Path.Combine(fixture.Layout.WorkingDirectory, "compare_legal_query_raw_debug.json");
        TestAssert.True(File.Exists(rawDebugPath), "Raw debug trace should be written beside the normal legal query trace.");
        var rawDebug = File.ReadAllText(rawDebugPath);
        TestAssert.True(rawDebug.Contains("\"query_kind\": \"land_valuation_number\"", StringComparison.Ordinal), "Raw debug should capture query kind.");
        TestAssert.True(rawDebug.Contains("\"raw_record_count\": 1", StringComparison.Ordinal), "Raw debug should capture raw row count.");
        TestAssert.True(rawDebug.Contains("\"prid\": \"100778284\"", StringComparison.Ordinal), "Raw debug should persist raw row values.");
        TestAssert.True(rawDebug.Contains("\"type\": \"party_type_individual\"", StringComparison.Ordinal), "Raw debug should persist raw row type.");
        TestAssert.True(!rawDebug.Contains("token", StringComparison.OrdinalIgnoreCase), "Raw debug should not contain token labels for this sample.");
    }

    public static async Task InnolaOwnerSearchReportsZeroRawRowsForEmptyPidAndLandValResponses()
    {
        var handler = new CapturingHttpMessageHandler(new[]
        {
            ("""{"total":0,"success":true,"records":[]}""", HttpStatusCode.OK),
            ("""{"total":0,"success":true,"records":[]}""", HttpStatusCode.OK)
        });
        var service = new InnolaBaUnitLegalCadasterQueryService(
            LegalOwnerSearchSource(),
            () => Session(),
            new HttpClient(handler),
            () => FixedNow);

        var pidResult = await service.QueryByParcelIdAsync("10954223");
        var landValResult = await service.QueryByLandValuationNumberAsync("16505010005");

        TestAssert.True(pidResult.Success, "Empty PID owner search should remain reviewable.");
        TestAssert.Equal(0, pidResult.Records.Count, "Empty PID owner search should not map records.");
        TestAssert.True(pidResult.Diagnostic!.Contains("0 raw row", StringComparison.Ordinal), "PID diagnostic should report zero raw rows.");
        TestAssert.True(pidResult.Diagnostic.Contains("reported total=0", StringComparison.Ordinal), "PID diagnostic should report the response total.");
        TestAssert.True(pidResult.Diagnostic.Contains("root fields: total, success, records", StringComparison.Ordinal), "PID diagnostic should report response root fields.");

        TestAssert.True(landValResult.Success, "Empty LandVal owner search should remain reviewable.");
        TestAssert.Equal(0, landValResult.Records.Count, "Empty LandVal owner search should not map records.");
        TestAssert.True(landValResult.Diagnostic!.Contains("0 raw row", StringComparison.Ordinal), "LandVal diagnostic should report zero raw rows.");
        TestAssert.True(landValResult.Diagnostic.Contains("reported total=0", StringComparison.Ordinal), "LandVal diagnostic should report the response total.");
        TestAssert.True(landValResult.Diagnostic.Contains("root fields: total, success, records", StringComparison.Ordinal), "LandVal diagnostic should report response root fields.");
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

    public static async Task InnolaBaUnitOwnerSearchPreservesUserWildcard()
    {
        var handler = new CapturingHttpMessageHandler("""{"records":[]}""");
        var service = new InnolaBaUnitLegalCadasterQueryService(
            LegalInnolaSource(),
            () => Session(),
            new HttpClient(handler),
            () => FixedNow);

        await service.QueryByNameAsync("%TRACEY%");

        using var document = JsonDocument.Parse(handler.LastRequestBody);
        var parameters = document.RootElement.GetProperty("params");
        TestAssert.Equal("%TRACEY%", parameters.GetProperty("ownername").GetString(), "User-supplied wildcard owner search should not be double-wrapped.");
        TestAssert.True(!parameters.TryGetProperty("owner", out _), "Owner search should use ownername, not owner.");
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

        public List<Uri?> RequestUris { get; } = new();

        public HttpMethod? LastMethod { get; private set; }

        public string? LastAccessToken { get; private set; }

        public List<string?> AccessTokens { get; } = new();

        public string? LastRequestedWith { get; private set; }

        public string? LastOrigin { get; private set; }

        public string? LastReferrer { get; private set; }

        public bool LastCancellationTokenCanBeCanceled { get; private set; }

        public string LastRequestBody { get; private set; } = string.Empty;

        public List<string> RequestBodies { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            LastUri = request.RequestUri;
            RequestUris.Add(LastUri);
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
            RequestBodies.Add(LastRequestBody);

            var response = responses.Count > 0
                ? responses.Dequeue()
                : (string.Empty, HttpStatusCode.OK);
            return new HttpResponseMessage(response.Item2)
            {
                Content = new StringContent(response.Item1, Encoding.UTF8, "application/json")
            };
        }
    }

    private static string OwnerSearchPageResponse(int total, int firstPid, int count, string landValuationNumber)
    {
        var records = Enumerable.Range(0, count)
            .Select(index => new Dictionary<string, object?>
            {
                ["registrationdate"] = "2010-12-21T00:00:00.000+00:00",
                ["pid"] = (firstPid + index).ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["owners"] = $"OWNER {index + 1}",
                ["baunit_type"] = "bu_type_land",
                ["tenurevalue"] = "Fee Simple",
                ["volume"] = 1447,
                ["folio"] = 138 + index,
                ["landvalnumber"] = landValuationNumber,
                ["spparish"] = "Manchester"
            })
            .ToArray();
        return JsonSerializer.Serialize(new { total, success = true, records });
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
