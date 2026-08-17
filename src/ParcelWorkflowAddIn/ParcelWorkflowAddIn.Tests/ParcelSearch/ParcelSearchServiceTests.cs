using System.IO;
using ParcelWorkflowAddIn.Compare;
using ParcelWorkflowAddIn.Enterprise.PortalAuth;
using ParcelWorkflowAddIn.Innola;
using ParcelWorkflowAddIn.ParcelSearch;
using System.Net;

namespace ParcelWorkflowAddIn.Tests.ParcelSearch;

internal static class ParcelSearchServiceTests
{
    public static void CadastralScopePlansFiscalSource()
    {
        var plan = ParcelSearchQueryPlanner.Build(
            new ParcelSearchCriteria { LayerScope = ParcelSearchLayerScope.Cadastral, LandValuationNumber = "3*" },
            CreateSettings());

        TestAssert.True(plan.ShouldExecute, "Cadastral search should execute when criteria are present.");
        TestAssert.Equal(1, plan.SourceRequests.Count, "Cadastral should map to one source.");
        TestAssert.Equal(CompareEnterpriseCadasterSourceKind.Fiscal, plan.SourceRequests[0].SourceKind, "Cadastral must map to Fiscal internally.");
        TestAssert.Equal("Cadastral", plan.SourceRequests[0].SourceDisplayName, "Fiscal should be presented as Cadastral.");
    }

    public static void AllScopePlansLegalFiscalAndSurvey()
    {
        var plan = ParcelSearchQueryPlanner.Build(
            new ParcelSearchCriteria { LayerScope = ParcelSearchLayerScope.All, ParishNames = new[] { "Kingston" } },
            CreateSettings());

        TestAssert.True(plan.ShouldExecute, "Parish filter is a valid search filter.");
        TestAssert.Equal(3, plan.SourceRequests.Count, "All should plan one query per enabled source.");
        TestAssert.True(plan.ParishFilterRequest is not null, "Specific parish should create a parish spatial filter request.");
        TestAssert.Equal(CompareEnterpriseCadasterSourceKind.Legal, plan.SourceRequests[0].SourceKind, "Legal source order mismatch.");
        TestAssert.Equal(CompareEnterpriseCadasterSourceKind.Fiscal, plan.SourceRequests[1].SourceKind, "Fiscal source order mismatch.");
        TestAssert.Equal(CompareEnterpriseCadasterSourceKind.Survey, plan.SourceRequests[2].SourceKind, "Survey source order mismatch.");
    }

    public static void MultipleSelectedScopesPlanOnlyThoseSources()
    {
        var plan = ParcelSearchQueryPlanner.Build(
            new ParcelSearchCriteria
            {
                LayerScopes = new[] { ParcelSearchLayerScope.Legal, ParcelSearchLayerScope.Survey },
                PeNumber = "3*"
            },
            CreateSettings());

        TestAssert.True(plan.ShouldExecute, "Multi-source search should execute when criteria are present.");
        TestAssert.Equal(2, plan.SourceRequests.Count, "Only selected sources should be queried.");
        TestAssert.Equal(CompareEnterpriseCadasterSourceKind.Legal, plan.SourceRequests[0].SourceKind, "Legal source should be included.");
        TestAssert.Equal(CompareEnterpriseCadasterSourceKind.Survey, plan.SourceRequests[1].SourceKind, "Survey source should be included.");
    }

    public static void EmptyExplicitScopeBlocksSearch()
    {
        var plan = ParcelSearchQueryPlanner.Build(
            new ParcelSearchCriteria
            {
                LayerScopes = new[] { "none" },
                PeNumber = "3*"
            },
            CreateSettings());

        TestAssert.False(plan.ShouldExecute, "Search should not execute when the UI has no selected source.");
        TestAssert.Equal(0, plan.SourceRequests.Count, "No source requests should be planned for an empty explicit source selection.");
    }

    public static void EmptyCriteriaBlocksSearch()
    {
        var plan = ParcelSearchQueryPlanner.Build(
            new ParcelSearchCriteria { LayerScope = ParcelSearchLayerScope.All },
            CreateSettings());

        TestAssert.False(plan.ShouldExecute, "Search should not execute without criteria or filters.");
        TestAssert.True(plan.StatusMessage.Contains("criterion or filter", StringComparison.OrdinalIgnoreCase), "Prompt should tell the user what is missing.");
        TestAssert.Equal(0, plan.SourceRequests.Count, "No source requests should be planned for empty criteria.");
    }

    public static void WildcardsAndCaseInsensitiveNameBuildExpectedWhereClause()
    {
        var plan = ParcelSearchQueryPlanner.Build(
            new ParcelSearchCriteria
            {
                LayerScope = ParcelSearchLayerScope.Legal,
                Name = "*smith*",
                PeNumber = "12??344?99",
                LandValuationNumber = "?23????"
            },
            CreateSettings());

        TestAssert.True(plan.ShouldExecute, "Configured name and number criteria should execute.");
        var where = plan.SourceRequests[0].WhereClause;
        TestAssert.True(where.Contains("UPPER(owner) LIKE '%SMITH%'", StringComparison.Ordinal), "Name search should be case-insensitive and translate *.");
        TestAssert.True(where.Contains("pe_no LIKE '12__344_99'", StringComparison.Ordinal), "PE search should treat ? as single-character wildcard.");
        TestAssert.True(where.Contains("landval LIKE '_23____'", StringComparison.Ordinal), "LandVal search should treat ? as single-character wildcard.");
        TestAssert.True(where.Contains(" AND ", StringComparison.Ordinal), "Criteria should be combined with AND.");
    }

    public static void LiteralSqlLikeWildcardsAreEscaped()
    {
        var plan = ParcelSearchQueryPlanner.Build(
            new ParcelSearchCriteria
            {
                LayerScope = ParcelSearchLayerScope.Legal,
                Name = "50%_smith"
            },
            CreateSettings());

        TestAssert.True(plan.ShouldExecute, "Literal LIKE wildcard characters should not block search.");
        TestAssert.True(plan.SourceRequests[0].WhereClause.Contains("UPPER(owner) LIKE '50[%][_]SMITH'", StringComparison.Ordinal), "Percent and underscore should be treated as literals.");
    }

    public static void MissingFieldMappingExcludesSourceWithWarning()
    {
        var settings = CreateSettings() with
        {
            Survey = CreateSettings().Survey with { PeNumberField = null }
        };

        var plan = ParcelSearchQueryPlanner.Build(
            new ParcelSearchCriteria { LayerScope = ParcelSearchLayerScope.Survey, PeNumber = "3*" },
            settings);

        TestAssert.False(plan.ShouldExecute, "Survey PE search should not execute without a PE field.");
        TestAssert.Equal(0, plan.SourceRequests.Count, "Source with missing requested field should be excluded.");
        TestAssert.True(plan.Diagnostics.Any(message => message.Contains("PE Number", StringComparison.OrdinalIgnoreCase)), "Missing PE field warning should be visible.");
    }

    public static void WorkingGdbPathUsesUserScopedName()
    {
        var path = ParcelSearchWorkspaceResolver.ResolveWorkingGeodatabasePath(
            Path.Combine("C:", "ParcelWorkflowCases"),
            "Jane Doe");

        TestAssert.True(path.EndsWith(Path.Combine("ParcelWorkflowCases", "GDB_Jane_Doe_working.gdb"), StringComparison.OrdinalIgnoreCase), "Working GDB path should use sanitized user name.");
    }

    public static void ResultLayerContractIncludesRequiredMetadata()
    {
        TestAssert.Equal("Parcel Search Results", ParcelSearchResultLayerContract.LayerName, "Layer name contract mismatch.");
        TestAssert.True(ParcelSearchResultLayerContract.MetadataFields.Contains("source_layer"), "SourceLayer metadata field missing.");
        TestAssert.True(ParcelSearchResultLayerContract.MetadataFields.Contains("search_run_id"), "SearchRunId metadata field missing.");
        TestAssert.True(ParcelSearchResultLayerContract.MetadataFields.Contains("search_timestamp"), "SearchTimestamp metadata field missing.");
        TestAssert.True(ParcelSearchResultLayerContract.MetadataFields.Contains("search_label"), "Search label metadata field missing.");
        TestAssert.True(ParcelSearchResultLayerContract.ChildLayerNames.SequenceEqual(new[] { "Legal", "Cadastral", "Survey", "Other" }), "Result child layer names should support per-source visibility toggles.");
        TestAssert.Equal("source_display_name = 'Legal'", ParcelSearchResultLayerContract.BuildSourceDefinitionQuery("Legal"), "Legal child layer definition query mismatch.");
        TestAssert.Equal("source_display_name NOT IN ('Legal', 'Cadastral', 'Survey')", ParcelSearchResultLayerContract.BuildOtherDefinitionQuery(), "Other child layer definition query mismatch.");
    }

    public static void ClearSearchKeepsReusableLayer()
    {
        var service = new RecordingParcelSearchMapIntegrationService();
        service.ClearSearchAsync(CancellationToken.None).GetAwaiter().GetResult();

        TestAssert.True(service.ClearSelectionRequested, "Clear Search should clear map selection.");
        TestAssert.True(service.ClearResultFeaturesRequested, "Clear Search should clear result features.");
        TestAssert.False(service.RemoveResultLayerRequested, "Clear Search should not remove the reusable result layer.");
    }

    public static void SettingsParseSublayersSourceSpecificFieldsAndParishSource()
    {
        using var settingsFile = new TempFile();
        File.WriteAllText(settingsFile.Path,
            """
            {
              "compare_enterprise_cadaster": {
                "enabled": true,
                "legal": {
                  "enabled": true,
                  "source_name": "Legal Cadastre",
                  "display_name": "Legal",
                  "layer_url": "https://example.test/legal/FeatureServer/0",
                  "sublayer_name": "Legal_Parcel",
                  "parcel_id_field": "lot_number",
                  "combined_volume_folio_field": "vol_folio",
                  "pe_number_field": "pe_number",
                  "dp_number_field": "dp_number",
                  "r_number_field": "r_number",
                  "parish_field": "parish"
                },
                "fiscal": {
                  "enabled": true,
                  "source_name": "Fiscal Cadastre",
                  "display_name": "Cadastral",
                  "layer_url": "https://example.test/fiscal/FeatureServer/0",
                  "sublayer_name": "Parcels",
                  "parcel_id_field": "Lv_number",
                  "volume_field": "LT_Volume",
                  "folio_field": "LT_Folio",
                  "combined_volume_folio_field": "Title_Reference",
                  "land_valuation_number_field": "Lv_number",
                  "dp_number_field": "dp_number",
                  "r_number_field": "R_Number"
                },
                "survey": {
                  "enabled": true,
                  "source_name": "Survey Cadastre",
                  "display_name": "Survey",
                  "layer_url": "https://example.test/survey/FeatureServer/0",
                  "sublayer_name": "COGO_Fabric",
                  "parcel_id_field": "PE_number",
                  "pe_number_field": "PE_number"
                },
                "parish_source": {
                  "enabled": true,
                  "source_kind": "fiscal",
                  "source_name": "Fiscal Cadastre Parishes",
                  "layer_url": "https://example.test/fiscal/FeatureServer/2",
                  "sublayer_name": "Parishes",
                  "parish_name_field": "Parish_nam"
                },
                "popup_fields": [
                  { "field_name": "PID", "alias": "PID", "visible": true },
                  { "field_name": "Lv_number", "alias": "LandVal No.", "visible": true },
                  { "field_name": "search_run_id", "alias": "Run", "visible": false }
                ]
              }
            }
            """);

        var settings = InnolaTransactionSettings.Load(settingsFile.Path).CompareEnterpriseCadaster;

        TestAssert.Equal("Legal_Parcel", settings.Legal.SublayerName, "Legal sublayer mismatch.");
        TestAssert.Equal("vol_folio", settings.Legal.CombinedVolumeFolioField, "Legal combined vol/folio field mismatch.");
        TestAssert.Equal("r_number", settings.Legal.RNumberField, "Legal R number field mismatch.");
        TestAssert.Equal("Parcels", settings.Fiscal.SublayerName, "Fiscal sublayer mismatch.");
        TestAssert.Equal("Title_Reference", settings.Fiscal.CombinedVolumeFolioField, "Fiscal title reference field mismatch.");
        TestAssert.Equal("R_Number", settings.Fiscal.RNumberField, "Fiscal R number field mismatch.");
        TestAssert.Equal("COGO_Fabric", settings.Survey.SublayerName, "Survey sublayer mismatch.");
        TestAssert.Equal("PE_number", settings.Survey.PeNumberField, "Survey PE field mismatch.");
        TestAssert.Equal("Parishes", settings.ParishSource.SublayerName, "Parish source sublayer mismatch.");
        TestAssert.Equal("Parish_nam", settings.ParishSource.ParishNameField, "Parish source field mismatch.");
        TestAssert.Equal(3, settings.PopupFields.Count, "Popup field count mismatch.");
        TestAssert.Equal("landval_number", settings.PopupFields[1].FieldName, "Popup field name mismatch.");
        TestAssert.Equal("LandVal No.", settings.PopupFields[1].Alias, "Popup field alias mismatch.");
        TestAssert.False(settings.PopupFields[2].Visible, "Popup field visibility mismatch.");
    }

    public static void SettingsDefaultMissingLegalLandValFieldToLiveName()
    {
        using var settingsFile = new TempFile();
        File.WriteAllText(settingsFile.Path,
            """
            {
              "compare_enterprise_cadaster": {
                "enabled": true,
                "legal": {
                  "enabled": true,
                  "source_name": "Legal Cadastre",
                  "layer_url": "https://example.test/legal/FeatureServer/15",
                  "sublayer_name": "Legal_Parcel",
                  "parcel_id_field": "lot_number",
                  "combined_volume_folio_field": "vol_folio",
                  "land_valuation_number_field": ""
                }
              }
            }
            """);

        var settings = InnolaTransactionSettings.Load(settingsFile.Path).CompareEnterpriseCadaster;
        var plan = ParcelSearchQueryPlanner.Build(
            new ParcelSearchCriteria { LayerScope = ParcelSearchLayerScope.Legal, LandValuationNumber = "149020*" },
            settings);

        TestAssert.Equal("Lv_NUMBER", settings.Legal.LandValuationNumberField, "Blank Legal LandVal mapping should migrate to the live Legal_Parcel field.");
        TestAssert.True(plan.ShouldExecute, "Legal LandVal search should execute after mapping migration.");
        TestAssert.True(plan.SourceRequests[0].WhereClause.Contains("Lv_NUMBER LIKE '149020%'", StringComparison.Ordinal), "Legal LandVal query should use Lv_NUMBER.");
    }

    public static void SettingsDefaultMissingFiscalRNumberFieldToLiveName()
    {
        using var settingsFile = new TempFile();
        File.WriteAllText(settingsFile.Path,
            """
            {
              "compare_enterprise_cadaster": {
                "enabled": true,
                "fiscal": {
                  "enabled": true,
                  "source_name": "Fiscal Cadastre",
                  "display_name": "Cadastral",
                  "layer_url": "https://example.test/fiscal/FeatureServer/1",
                  "sublayer_name": "Parcels",
                  "parcel_id_field": "Lv_number",
                  "volume_field": "LT_Volume",
                  "folio_field": "LT_Folio",
                  "combined_volume_folio_field": "Title_Reference"
                }
              }
            }
            """);

        var settings = InnolaTransactionSettings.Load(settingsFile.Path).CompareEnterpriseCadaster;
        var plan = ParcelSearchQueryPlanner.Build(
            new ParcelSearchCriteria
            {
                LayerScope = ParcelSearchLayerScope.Cadastral,
                RNumber = "39700"
            },
            settings);

        TestAssert.True(plan.ShouldExecute, "Fiscal R number search should execute when r_number_field is omitted.");
        TestAssert.Equal("R_Number", settings.Fiscal.RNumberField, "Missing Fiscal r_number_field should default to live R_Number field.");
        TestAssert.True(plan.SourceRequests[0].WhereClause.Contains("R_Number LIKE '39700'", StringComparison.Ordinal), "Fiscal R number fallback should use R_Number.");
    }

    public static void SettingsMigrateLegacyParcelSearchFieldNames()
    {
        using var settingsFile = new TempFile();
        File.WriteAllText(settingsFile.Path,
            """
            {
              "compare_enterprise_cadaster": {
                "enabled": true,
                "legal": {
                  "enabled": true,
                  "source_name": "Legal Cadastre",
                  "layer_url": "https://example.test/legal/FeatureServer/15",
                  "parcel_id_field": "cad_number",
                  "pid_field": "cad_number",
                  "volume_field": "vol_fol",
                  "folio_field": ""
                },
                "fiscal": {
                  "enabled": true,
                  "source_name": "Fiscal Cadastre",
                  "layer_url": "https://example.test/fiscal/FeatureServer/1",
                  "parcel_id_field": "lv_number",
                  "pid_field": "lv_number",
                  "volume_field": "lt_volume",
                  "folio_field": "lt_folio",
                  "combined_volume_folio_field": "title_reference",
                  "land_valuation_number_field": "lv_number",
                  "r_number_field": "r_number"
                },
                "survey": {
                  "enabled": true,
                  "source_name": "Survey Cadastre",
                  "layer_url": "https://example.test/survey/FeatureServer/0",
                  "parcel_id_field": "parcel_id",
                  "pid_field": "pid",
                  "pe_number_field": "pe_number"
                }
              }
            }
            """);

        var settings = InnolaTransactionSettings.Load(settingsFile.Path).CompareEnterpriseCadaster;

        TestAssert.Equal("lot_number", settings.Legal.ParcelIdField, "Legacy Legal parcel ID should migrate to lot_number.");
        TestAssert.Equal("vol_folio", settings.Legal.CombinedVolumeFolioField, "Legacy Legal vol_fol should migrate to vol_folio.");
        TestAssert.Equal("LT_Volume", settings.Fiscal.VolumeField, "Legacy Fiscal volume casing should migrate.");
        TestAssert.Equal("LT_Folio", settings.Fiscal.FolioField, "Legacy Fiscal folio casing should migrate.");
        TestAssert.Equal("Title_Reference", settings.Fiscal.CombinedVolumeFolioField, "Legacy Fiscal title reference casing should migrate.");
        TestAssert.Equal("Lv_number", settings.Fiscal.LandValuationNumberField, "Legacy Fiscal LandVal casing should migrate.");
        TestAssert.Equal("R_Number", settings.Fiscal.RNumberField, "Legacy Fiscal R number casing should migrate.");
        TestAssert.Equal("PE_number", settings.Survey.PeNumberField, "Legacy Survey PE field should migrate.");
    }

    public static void CombinedVolumeFolioUsesConfiguredLegalField()
    {
        var settings = CreateSettings() with
        {
            Legal = CreateSettings().Legal with
            {
                VolumeField = null,
                FolioField = null,
                CombinedVolumeFolioField = "vol_folio"
            }
        };

        var plan = ParcelSearchQueryPlanner.Build(
            new ParcelSearchCriteria { LayerScope = ParcelSearchLayerScope.Legal, Volume = "1234", Folio = "344" },
            settings);

        TestAssert.True(plan.ShouldExecute, "Legal combined volume/folio should execute.");
        TestAssert.True(plan.SourceRequests[0].WhereClause.Contains("vol_folio LIKE '1234/344'", StringComparison.Ordinal), "Combined vol_folio clause mismatch.");
    }

    public static void CombinedVolumeOnlyUsesWildcardForMissingFolio()
    {
        var settings = CreateSettings() with
        {
            Fiscal = CreateSettings().Fiscal with
            {
                VolumeField = "LT_Volume",
                FolioField = "LT_Folio",
                CombinedVolumeFolioField = "Title_Reference"
            }
        };

        var plan = ParcelSearchQueryPlanner.Build(
            new ParcelSearchCriteria { LayerScope = ParcelSearchLayerScope.Cadastral, Volume = "123" },
            settings);

        TestAssert.True(plan.ShouldExecute, "Fiscal title reference volume-only search should execute.");
        TestAssert.True(plan.SourceRequests[0].WhereClause.Contains("Title_Reference LIKE '123/%'", StringComparison.Ordinal), "Missing folio should become wildcard in combined title reference.");
    }

    public static void FiscalVolumeAndFolioPreferSeparateConfiguredFields()
    {
        var settings = CreateSettings() with
        {
            Fiscal = CreateSettings().Fiscal with
            {
                VolumeField = "LT_Volume",
                FolioField = "LT_Folio",
                CombinedVolumeFolioField = "Title_Reference"
            }
        };

        var plan = ParcelSearchQueryPlanner.Build(
            new ParcelSearchCriteria { LayerScope = ParcelSearchLayerScope.Cadastral, Volume = "999", Folio = "2*" },
            settings);

        TestAssert.True(plan.ShouldExecute, "Fiscal volume/folio search should execute.");
        var where = plan.SourceRequests[0].WhereClause;
        TestAssert.True(where.Contains("LT_Volume LIKE '999'", StringComparison.Ordinal), "Fiscal volume should use LT_Volume.");
        TestAssert.True(where.Contains("LT_Folio LIKE '2%'", StringComparison.Ordinal), "Fiscal folio wildcard should use LT_Folio.");
        TestAssert.False(where.Contains("Title_Reference", StringComparison.Ordinal), "Fiscal should prefer separate fields when both volume and folio are configured.");
    }

    public static void SourceSpecificIdentifierFieldsAreIncludedInOutFields()
    {
        var settings = CreateSettings() with
        {
            Legal = CreateSettings().Legal with
            {
                LotNumberField = "lot_number",
                DpNumberField = "dp_number",
                RNumberField = "r_number",
                CombinedVolumeFolioField = "vol_folio"
            }
        };

        var plan = ParcelSearchQueryPlanner.Build(
            new ParcelSearchCriteria { LayerScope = ParcelSearchLayerScope.Legal, PeNumber = "3*" },
            settings);

        var outFields = plan.SourceRequests[0].OutFields;
        TestAssert.True(outFields.Contains("lot_number"), "Lot number should be returned.");
        TestAssert.True(outFields.Contains("dp_number"), "DP number should be returned.");
        TestAssert.True(outFields.Contains("r_number"), "R number should be returned.");
        TestAssert.True(outFields.Contains("vol_folio"), "Combined volume/folio should be returned.");
        TestAssert.False(outFields.Contains("globalid"), "GlobalID should not be requested because JSONToFeatures rejects the service GlobalID field.");
    }

    public static void DpAndRNumberCriteriaUseConfiguredFields()
    {
        var settings = CreateSettings() with
        {
            Legal = CreateSettings().Legal with
            {
                DpNumberField = "dp_number",
                RNumberField = "r_number"
            }
        };

        var plan = ParcelSearchQueryPlanner.Build(
            new ParcelSearchCriteria
            {
                LayerScope = ParcelSearchLayerScope.Legal,
                DpNumber = "55*",
                RNumber = "?12"
            },
            settings);

        TestAssert.True(plan.ShouldExecute, "DP/R number search should execute.");
        var where = plan.SourceRequests[0].WhereClause;
        TestAssert.True(where.Contains("dp_number LIKE '55%'", StringComparison.Ordinal), "DP Number should use the configured DP field.");
        TestAssert.True(where.Contains("r_number LIKE '_12'", StringComparison.Ordinal), "R Number should use the configured R field with ? wildcard.");
        TestAssert.True(plan.SourceRequests[0].LabelFields.Any(field => field.Label == "DP No." && field.FieldNames.Contains("dp_number")), "DP label field should use configured DP field.");
        TestAssert.True(plan.SourceRequests[0].LabelFields.Any(field => field.Label == "R No." && field.FieldNames.Contains("r_number")), "R label field should use configured R field.");
    }

    public static void FiscalVolumeFolioAndRNumberUseLiveLayerFieldNames()
    {
        var settings = CreateSettings() with
        {
            Fiscal = CreateSettings().Fiscal with
            {
                RNumberField = "R_Number"
            }
        };

        var plan = ParcelSearchQueryPlanner.Build(
            new ParcelSearchCriteria
            {
                LayerScope = ParcelSearchLayerScope.Cadastral,
                Volume = "1374",
                Folio = "140",
                RNumber = "39700"
            },
            settings);

        TestAssert.True(plan.ShouldExecute, "Fiscal volume/folio/R number search should execute.");
        var where = plan.SourceRequests[0].WhereClause;
        TestAssert.True(where.Contains("LT_Volume LIKE '1374'", StringComparison.Ordinal), "Fiscal volume should use LT_Volume.");
        TestAssert.True(where.Contains("LT_Folio LIKE '140'", StringComparison.Ordinal), "Fiscal folio should use LT_Folio.");
        TestAssert.True(where.Contains("R_Number LIKE '39700'", StringComparison.Ordinal), "Fiscal R number should use R_Number.");
        TestAssert.True(plan.SourceRequests[0].OutFields.Contains("R_Number"), "Fiscal R number should be returned for materialization.");
    }

    public static void ActiveSearchCriteriaPlanPerParcelLabelFields()
    {
        var plan = ParcelSearchQueryPlanner.Build(
            new ParcelSearchCriteria
            {
                LayerScope = ParcelSearchLayerScope.Cadastral,
                Volume = "999",
                Folio = "2*",
                Name = "*smith*",
                PeNumber = "3*",
                LandValuationNumber = "323232"
            },
            CreateSettings());

        TestAssert.True(plan.ShouldExecute, "Configured criteria should execute.");
        var labelFields = plan.SourceRequests[0].LabelFields;
        TestAssert.True(labelFields.Any(field => field.Label == "Vol/Folio" && field.FieldNames.SequenceEqual(new[] { "LT_Volume", "LT_Folio" })), "Volume/folio label should use returned parcel volume and folio fields.");
        TestAssert.True(labelFields.Any(field => field.Label == "Name" && field.FieldNames.SequenceEqual(new[] { "occupant" })), "Name label should use the configured returned parcel name field.");
        TestAssert.True(labelFields.Any(field => field.Label == "PE No." && field.FieldNames.SequenceEqual(new[] { "pe_no" })), "PE label should use the configured returned parcel PE field.");
        TestAssert.True(labelFields.Any(field => field.Label == "LandVal No." && field.FieldNames.SequenceEqual(new[] { "lv_number" })), "LandVal label should use the configured returned parcel LandVal field.");
        TestAssert.False(labelFields.Any(field => field.Label.Contains("Parish", StringComparison.OrdinalIgnoreCase)), "Parish must remain a filter only and not become a result label.");
    }

    public static void LandValOnlySearchPlansOneLandValLabelField()
    {
        var plan = ParcelSearchQueryPlanner.Build(
            new ParcelSearchCriteria
            {
                LayerScope = ParcelSearchLayerScope.Cadastral,
                LandValuationNumber = "149020*"
            },
            CreateSettings());

        TestAssert.True(plan.ShouldExecute, "LandVal-only search should execute.");
        var landValLabelFields = plan.SourceRequests[0].LabelFields
            .Where(field => string.Equals(field.Label, "LandVal No.", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        TestAssert.Equal(1, landValLabelFields.Length, "LandVal-only search should produce one LandVal label line.");
        TestAssert.True(landValLabelFields[0].FieldNames.SequenceEqual(new[] { "lv_number" }), "LandVal label should use only the configured LandVal field once.");
    }

    public static void ResultLabelsResolveConfiguredFieldsToActualReturnedFields()
    {
        var configured = new[]
        {
            new ParcelSearchLabelField("LandVal No.", new[] { "Lv_number" }, string.Empty),
            new ParcelSearchLabelField("R No.", new[] { "R_Number" }, string.Empty)
        };
        var available = new HashSet<string>(new[] { "LV_NUMBER", "r_number", "OBJECTID" }, StringComparer.OrdinalIgnoreCase);

        var resolved = ArcGisParcelSearchResultMaterializer.ResolveAvailableLabelFields(configured, available);

        TestAssert.Equal(2, resolved.Count, "Configured label fields should resolve when only casing differs.");
        TestAssert.True(resolved[0].FieldNames.SequenceEqual(new[] { "LV_NUMBER" }), "LandVal label should use the actual returned field name.");
        TestAssert.True(resolved[1].FieldNames.SequenceEqual(new[] { "r_number" }), "R number label should use the actual returned field name.");
    }

    public static void ResultLabelDiagnosticsShowConfiguredActualAndSampleValues()
    {
        var configured = new[]
        {
            new ParcelSearchLabelField("LandVal No.", new[] { "Lv_number" }, string.Empty)
        };
        var actual = new[]
        {
            new ParcelSearchLabelField("LandVal No.", new[] { "LV_NUMBER" }, string.Empty)
        };

        var diagnostic = ArcGisParcelSearchResultMaterializer.BuildSearchLabelDiagnostic(
            "Cadastral",
            configured,
            actual,
            "LandVal No.: 14902009004");

        TestAssert.True(diagnostic.Contains("source_display_name=Cadastral", StringComparison.Ordinal), "Diagnostic should include the source display name.");
        TestAssert.True(diagnostic.Contains("configured_label_fields=LandVal No.=[Lv_number]", StringComparison.Ordinal), "Diagnostic should include configured label fields.");
        TestAssert.True(diagnostic.Contains("actual_label_fields=LandVal No.=[LV_NUMBER]", StringComparison.Ordinal), "Diagnostic should include actual returned label fields.");
        TestAssert.True(diagnostic.Contains("sample_search_label=LandVal No.: 14902009004", StringComparison.Ordinal), "Diagnostic should include a sample produced label.");
    }

    public static void MapIntegrationQueriesSelectedSourcesAndMaterializesResults()
    {
        var settings = CreateSettings();
        var plan = ParcelSearchQueryPlanner.Build(
            new ParcelSearchCriteria
            {
                LayerScopes = new[] { ParcelSearchLayerScope.Legal, ParcelSearchLayerScope.Cadastral },
                PeNumber = "3*"
            },
            settings);
        var queryClient = new RecordingParcelSearchFeatureQueryClient();
        var materializer = new RecordingParcelSearchResultMaterializer();
        var service = new ParcelSearchMapIntegrationService(queryClient, materializer, () => true);

        var result = service.UpdateResultsAsync(plan, Path.Combine("C:", "ParcelWorkflowCases", "GDB_test_working.gdb")).GetAwaiter().GetResult();

        TestAssert.True(result.Success, "Search integration should succeed when queries and materialization succeed.");
        TestAssert.Equal(2, queryClient.Requests.Count, "Each selected source should be queried.");
        TestAssert.Equal(2, materializer.FeatureSets.Count, "Feature sets returned from selected sources should be materialized.");
        TestAssert.Equal(2, result.ResultCount, "Result count should come from materialized feature count.");
    }

    public static void MapIntegrationRequiresActiveMapBeforeQuerying()
    {
        var plan = ParcelSearchQueryPlanner.Build(
            new ParcelSearchCriteria { LayerScope = ParcelSearchLayerScope.Cadastral, Volume = "999", Folio = "2*" },
            CreateSettings());
        var queryClient = new RecordingParcelSearchFeatureQueryClient();
        var materializer = new RecordingParcelSearchResultMaterializer();
        var service = new ParcelSearchMapIntegrationService(queryClient, materializer, () => false);

        var result = service.UpdateResultsAsync(plan, Path.Combine("C:", "ParcelWorkflowCases", "GDB_test_working.gdb")).GetAwaiter().GetResult();

        TestAssert.False(result.Success, "Search should fail before querying when no active map exists.");
        TestAssert.True(result.Message.Contains("activate an ArcGIS Pro map", StringComparison.OrdinalIgnoreCase), "Message should guide the user to activate a map.");
        TestAssert.Equal(0, queryClient.Requests.Count, "No FeatureServer query should run without an active map.");
    }

    public static void SpecificParishSpatialFilterIsPassedToSourceQueries()
    {
        var plan = ParcelSearchQueryPlanner.Build(
            new ParcelSearchCriteria
            {
                LayerScope = ParcelSearchLayerScope.Cadastral,
                Volume = "999",
                Folio = "2*",
                ParishNames = new[] { "Kingston" }
            },
            CreateSettings());
        var queryClient = new RecordingParcelSearchFeatureQueryClient();
        var materializer = new RecordingParcelSearchResultMaterializer();
        var service = new ParcelSearchMapIntegrationService(queryClient, materializer, () => true);

        var result = service.UpdateResultsAsync(plan, Path.Combine("C:", "ParcelWorkflowCases", "GDB_test_working.gdb")).GetAwaiter().GetResult();

        TestAssert.True(result.Success, "Search should succeed with parish spatial filter.");
        TestAssert.True(queryClient.ParishFilterRequested, "Parish geometry should be queried before source queries.");
        TestAssert.True(queryClient.SpatialFilters.Count == 1 && queryClient.SpatialFilters[0] is not null, "Source query should receive the parish spatial filter.");
        TestAssert.True(result.Diagnostics.Any(message => message.Contains("Parish spatial filter where", StringComparison.OrdinalIgnoreCase)), "Diagnostics should show parish filter where clause.");
    }

    public static void SaintParishSpatialFilterUsesStAliases()
    {
        var plan = ParcelSearchQueryPlanner.Build(
            new ParcelSearchCriteria
            {
                LayerScope = ParcelSearchLayerScope.Cadastral,
                Volume = "999",
                Folio = "22*",
                ParishNames = new[] { "Saint Thomas" }
            },
            CreateSettings());

        TestAssert.True(plan.ParishFilterRequest is not null, "Specific Saint parish should create a spatial parish request.");
        TestAssert.True(plan.ParishFilterRequest!.WhereClause.Contains("'SAINT THOMAS'", StringComparison.OrdinalIgnoreCase), "Parish query should include the full Saint value.");
        TestAssert.True(plan.ParishFilterRequest.WhereClause.Contains("'ST.THOMAS'", StringComparison.OrdinalIgnoreCase), "Parish query should include the compact St. alias used by service data.");
    }

    public static void AttributeParishFallbackUsesStAliases()
    {
        var settings = CreateSettings() with
        {
            ParishSource = CreateSettings().ParishSource with { Enabled = false }
        };

        var plan = ParcelSearchQueryPlanner.Build(
            new ParcelSearchCriteria
            {
                LayerScope = ParcelSearchLayerScope.Legal,
                ParishNames = new[] { "Saint Thomas" }
            },
            settings);

        TestAssert.True(plan.ShouldExecute, "Attribute parish fallback should execute.");
        var where = plan.SourceRequests[0].WhereClause;
        TestAssert.True(where.Contains("'SAINT THOMAS'", StringComparison.OrdinalIgnoreCase), "Fallback parish query should include the full Saint value.");
        TestAssert.True(where.Contains("'ST.THOMAS'", StringComparison.OrdinalIgnoreCase), "Fallback parish query should include the compact St. alias.");
    }

    public static void AllFailedSourceQueriesReturnFailure()
    {
        var plan = ParcelSearchQueryPlanner.Build(
            new ParcelSearchCriteria
            {
                LayerScope = ParcelSearchLayerScope.Cadastral,
                Volume = "999",
                Folio = "2*"
            },
            CreateSettings());
        var queryClient = new RecordingParcelSearchFeatureQueryClient { ThrowOnQuery = true };
        var materializer = new RecordingParcelSearchResultMaterializer();
        var service = new ParcelSearchMapIntegrationService(queryClient, materializer, () => true);

        var result = service.UpdateResultsAsync(plan, Path.Combine("C:", "ParcelWorkflowCases", "GDB_test_working.gdb")).GetAwaiter().GetResult();

        TestAssert.False(result.Success, "All failed source queries should not be reported as a successful empty search.");
        TestAssert.Equal(0, materializer.FeatureSets.Count, "Materializer should not clear valid results after all source queries fail.");
        TestAssert.True(result.Message.Contains("failed", StringComparison.OrdinalIgnoreCase), "Failure message should be visible.");
    }

    public static void ParishOptionsProviderReadsDistinctConfiguredNames()
    {
        var handler = new StaticHttpMessageHandler(
            """
            {
              "features": [
                { "attributes": { "Parish_nam": "KINGSTON" } },
                { "attributes": { "Parish_nam": "CLARENDON" } },
                { "attributes": { "Parish_nam": "KINGSTON" } }
              ]
            }
            """);
        var provider = new ArcGisParcelSearchParishOptionsProvider(new HttpClient(handler), new NoTokenPortalAuthProvider());

        var names = provider.LoadParishOptionsAsync(new ParcelSearchParishSourceSettings(
            true,
            "fiscal",
            "Parishes",
            "https://example.test/server/rest/services/Fiscal/FeatureServer/2",
            "Parishes",
            "Parish_nam",
            null)).GetAwaiter().GetResult();

        TestAssert.Equal(2, names.Count, "Parish provider should return distinct parish names.");
        TestAssert.True(names.Contains("CLARENDON"), "Configured parish source should provide Clarendon.");
        TestAssert.True(names.Contains("KINGSTON"), "Configured parish source should provide Kingston.");
    }

    public static void FeatureServerQueryUsesWildcardOutFieldsForServiceCompatibility()
    {
        var settings = CreateSettings();
        var request = new ParcelSearchSourceRequest(
            CompareEnterpriseCadasterSourceKind.Fiscal,
            "Fiscal Cadastre",
            "Cadastral",
            "https://example.test/server/rest/services/Fiscal/FeatureServer/1",
            "Parcels",
            "LT_Volume LIKE '999' AND LT_Folio LIKE '22%'",
            new[] { "Lv_number", "LT_Volume", "LT_Folio", "R_Number", "objectid" },
            Array.Empty<ParcelSearchLabelField>(),
            50,
            10,
            settings.Fiscal);
        var handler = new StaticHttpMessageHandler("""{"features":[]}""");
        var client = new ArcGisFeatureServerParcelSearchClient(new HttpClient(handler), new NoTokenPortalAuthProvider());

        client.QueryAsync(request).GetAwaiter().GetResult();

        var query = Uri.UnescapeDataString(handler.LastRequestUri?.Query ?? string.Empty);
        TestAssert.True(query.Contains("where=LT_Volume LIKE '999' AND LT_Folio LIKE '22%'", StringComparison.Ordinal), "FeatureServer WHERE should use configured live fields.");
        TestAssert.True(query.Contains("outFields=*", StringComparison.Ordinal), "FeatureServer query should use wildcard out fields so invalid optional configured fields do not break service queries.");
    }

    public static void MissingParishGeometryStopsUnfilteredSourceQueries()
    {
        var plan = ParcelSearchQueryPlanner.Build(
            new ParcelSearchCriteria
            {
                LayerScope = ParcelSearchLayerScope.Cadastral,
                Volume = "999",
                Folio = "22*",
                ParishNames = new[] { "Saint Thomas" }
            },
            CreateSettings());
        var queryClient = new RecordingParcelSearchFeatureQueryClient { ReturnParishSpatialFilter = false };
        var materializer = new RecordingParcelSearchResultMaterializer();
        var service = new ParcelSearchMapIntegrationService(queryClient, materializer, () => true);

        var result = service.UpdateResultsAsync(plan, Path.Combine("C:", "ParcelWorkflowCases", "GDB_test_working.gdb")).GetAwaiter().GetResult();

        TestAssert.False(result.Success, "Search should fail when a requested parish filter cannot be applied.");
        TestAssert.Equal(0, queryClient.Requests.Count, "Source queries must not run unfiltered after a parish lookup miss.");
        TestAssert.Equal(0, materializer.FeatureSets.Count, "Materializer should clear with an empty result set.");
    }

    private static CompareEnterpriseCadasterSettings CreateSettings()
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
            null)
        {
            PeNumberField = "pe_no",
            SublayerName = "Legal_Parcel",
            DisplayName = "Legal",
            CombinedVolumeFolioSeparator = "/"
        },
            new CompareEnterpriseCadasterSourceSettings(
                true,
                "Fiscal Cadastre",
                "https://example.test/fiscal/FeatureServer/0",
                "parcel_id",
                "pid",
                "LT_Volume",
                "LT_Folio",
                "lv_number",
                null,
                "occupant",
                "taxpayer",
                "parish",
                "suid",
                "objectid",
                "globalid",
                null)
        {
            PeNumberField = "pe_no",
            SublayerName = "Parcels",
            DisplayName = "Cadastral",
            CombinedVolumeFolioSeparator = "/"
        },
            new CompareEnterpriseCadasterSourceSettings(
                true,
                "Survey Cadastre",
                "https://example.test/survey/FeatureServer/0",
                "parcel_id",
                "pid",
                null,
                null,
                null,
                null,
                null,
                null,
                "parish",
                "suid",
                "objectid",
                "globalid",
                null)
        {
            PeNumberField = "pe_no",
            SublayerName = "COGO_Fabric",
            DisplayName = "Survey"
        },
            null)
        {
            ParishSource = new ParcelSearchParishSourceSettings(
                true,
                "fiscal",
                "Fiscal Cadastre Parishes",
                "https://example.test/fiscal/FeatureServer/2",
                "Parishes",
                "Parish_nam",
                null)
        };
    }

    private sealed class RecordingParcelSearchMapIntegrationService : IParcelSearchMapIntegrationService
    {
        public bool ClearSelectionRequested { get; private set; }
        public bool ClearResultFeaturesRequested { get; private set; }
        public bool RemoveResultLayerRequested { get; private set; }

        public Task<ParcelSearchMapUpdateResult> UpdateResultsAsync(
            ParcelSearchQueryPlan plan,
            string workingGeodatabasePath,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ParcelSearchMapUpdateResult.Ready(0, false, "No records."));
        }

        public Task ClearSearchAsync(CancellationToken cancellationToken = default)
        {
            ClearSelectionRequested = true;
            ClearResultFeaturesRequested = true;
            RemoveResultLayerRequested = false;
            return Task.CompletedTask;
        }

        public Task ZoomToResultsAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingParcelSearchFeatureQueryClient : IParcelSearchFeatureQueryClient
    {
        public List<ParcelSearchSourceRequest> Requests { get; } = new();
        public List<ParcelSearchSpatialFilter?> SpatialFilters { get; } = new();
        public bool ParishFilterRequested { get; private set; }
        public bool ReturnParishSpatialFilter { get; init; } = true;
        public bool ThrowOnQuery { get; init; }

        public Task<ParcelSearchSpatialFilter?> ResolveParishSpatialFilterAsync(
            ParcelSearchParishFilterRequest request,
            CancellationToken cancellationToken = default)
        {
            ParishFilterRequested = true;
            if (!ReturnParishSpatialFilter)
            {
                return Task.FromResult<ParcelSearchSpatialFilter?>(null);
            }

            return Task.FromResult<ParcelSearchSpatialFilter?>(new ParcelSearchSpatialFilter(
                """{"rings":[[[0,0],[1,0],[1,1],[0,1],[0,0]]]}""",
                "esriGeometryPolygon",
                """{"wkid":3448}""",
                new[] { $"Parish spatial filter where: {request.WhereClause}" }));
        }

        public Task<IReadOnlyList<ParcelSearchFeatureSet>> QueryAsync(
            ParcelSearchSourceRequest request,
            ParcelSearchSpatialFilter? spatialFilter = null,
            CancellationToken cancellationToken = default)
        {
            if (ThrowOnQuery)
            {
                throw new HttpRequestException("Simulated FeatureServer outage.");
            }

            Requests.Add(request);
            SpatialFilters.Add(spatialFilter);
            return Task.FromResult<IReadOnlyList<ParcelSearchFeatureSet>>(new[]
            {
                new ParcelSearchFeatureSet(
                    request,
                    """{"features":[{"attributes":{"objectid":1},"geometry":{"x":1,"y":2}}]}""",
                    1,
                    false,
                    Array.Empty<string>())
            });
        }
    }

    private sealed class RecordingParcelSearchResultMaterializer : IParcelSearchResultMaterializer
    {
        public IReadOnlyList<ParcelSearchFeatureSet> FeatureSets { get; private set; } = Array.Empty<ParcelSearchFeatureSet>();

        public Task<ParcelSearchMaterializationResult> MaterializeAsync(
            ParcelSearchQueryPlan plan,
            IReadOnlyList<ParcelSearchFeatureSet> featureSets,
            string workingGeodatabasePath,
            CancellationToken cancellationToken = default)
        {
            FeatureSets = featureSets;
            return Task.FromResult(new ParcelSearchMaterializationResult(
                true,
                featureSets.Sum(set => set.FeatureCount),
                false,
                "Materialized.",
                Array.Empty<string>()));
        }

        public Task ClearAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task ZoomToResultsAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class StaticHttpMessageHandler : HttpMessageHandler
    {
        private readonly string responseJson;

        public Uri? LastRequestUri { get; private set; }

        public StaticHttpMessageHandler(string responseJson)
        {
            this.responseJson = responseJson;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson)
            });
        }
    }

    private sealed class NoTokenPortalAuthProvider : IPortalAuthProvider
    {
        public Task<PortalAuthResult> GetTokenAsync(PortalAuthRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(PortalAuthResult.Failed("test", "No token required."));
        }
    }
}
