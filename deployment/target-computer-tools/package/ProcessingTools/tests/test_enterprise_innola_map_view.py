import importlib.util
import io
import json
import os
import sys
import tempfile
import types
import unittest
from contextlib import redirect_stdout
from pathlib import Path


SCRIPT_PATH = Path(__file__).resolve().parents[1] / "admin" / "provision_innola_compute_review_web_map.py"
SPEC = importlib.util.spec_from_file_location("provision_innola_compute_review_web_map", SCRIPT_PATH)
admin_script = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(admin_script)


def _settings():
    return {
        "enterprise_working_review": {
            "transaction_scope_field": "transaction_number",
            "layers": {
                "points": "https://enterprise.local/server/rest/services/Hosted/working_review/FeatureServer/1",
                "lines": "https://enterprise.local/server/rest/services/Hosted/working_review/FeatureServer/2",
                "polygons": "https://enterprise.local/server/rest/services/Hosted/working_review/FeatureServer/3",
                "issues": "https://enterprise.local/server/rest/services/Hosted/working_review/FeatureServer/0",
                "case_index": "https://enterprise.local/server/rest/services/Hosted/working_review/FeatureServer/4",
            },
        },
        "enterprise_working_admin": {
            "portal_url": "https://enterprise.local/portal",
        },
        "enterprise_innola_map_view": {
            "feature_layer_view_name": "working_review_innola_view",
            "feature_layer_view_url": "https://enterprise.local/server/rest/services/Hosted/working_review_innola_view/FeatureServer",
            "web_map_name": "innola_compute_review_map",
            "completion_filter": "is_active = 1 AND case_status = 'review_closed'",
            "reference_layers": {
                "survey_cadastre": "https://enterprise.local/server/rest/services/Survey_Cadastre/FeatureServer/0",
                "legal_cadastre": "https://enterprise.local/server/rest/services/Legal_Cadastre/FeatureServer/0",
                "fiscal_cadastre": "https://enterprise.local/server/rest/services/Fiscal_Cadastre/FeatureServer/0",
                "parish": "https://enterprise.local/server/rest/services/Parish/FeatureServer/0",
            },
        },
    }


class EnterpriseInnolaMapViewTests(unittest.TestCase):
    def test_validate_requires_working_polygons_lines_and_points(self):
        args = admin_script.parse_args(
            [
                "validate",
                "--portal-url",
                "https://enterprise.local/portal",
                "--web-map-name",
                "innola_compute_review_map",
            ]
        )

        payload = admin_script.build_payload(args)

        self.assertEqual("failed", payload["status"])
        self.assertIn("working polygons layer URL is required.", payload["validation"]["errors"])
        self.assertIn("working lines layer URL is required.", payload["validation"]["errors"])
        self.assertIn("working points layer URL is required.", payload["validation"]["errors"])

    def test_export_config_builds_ordered_web_map_definition_from_settings(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            config_path = Path(temp_dir) / "WorkflowSettings.json"
            config_path.write_text(json.dumps(_settings()), encoding="utf-8")
            output_path = Path(temp_dir) / "map.json"

            with redirect_stdout(io.StringIO()):
                exit_code = admin_script.main(
                    [
                        "export-config",
                        "--config",
                        str(config_path),
                        "--output-json",
                        str(output_path),
                    ]
                )

            payload = json.loads(output_path.read_text(encoding="utf-8"))

        self.assertEqual(0, exit_code)
        self.assertEqual("exported", payload["status"])
        self.assertEqual(
            [
                "parish",
                "survey_cadastre",
                "legal_cadastre",
                "fiscal_cadastre",
                "polygons",
                "lines",
                "points",
                "issues",
            ],
            payload["layers"]["layer_order"],
        )
        layer_ids = [layer["id"] for layer in payload["web_map_definition"]["operationalLayers"]]
        self.assertLess(layer_ids.index("reference_parish"), layer_ids.index("working_polygons"))
        self.assertLess(layer_ids.index("working_polygons"), layer_ids.index("working_lines"))

    def test_working_layer_urls_use_feature_layer_view_when_configured(self):
        args = admin_script.parse_args(["export-config"])
        config = admin_script._resolve_config(args, _settings())

        urls = admin_script._working_layer_urls(config)

        self.assertEqual(
            "https://enterprise.local/server/rest/services/Hosted/working_review_innola_view/FeatureServer/3",
            urls["polygons"],
        )
        self.assertEqual(
            "https://enterprise.local/server/rest/services/Hosted/working_review_innola_view/FeatureServer/2",
            urls["lines"],
        )

    def test_line_label_expression_uses_only_length_and_distance_fallback(self):
        definition = admin_script.build_web_map_definition(admin_script._resolve_config(admin_script.parse_args(["export-config"]), _settings()))
        lines = next(layer for layer in definition["operationalLayers"] if layer["id"] == "working_lines")

        expression = lines["layerDefinition"]["drawingInfo"]["labelingInfo"][0]["labelExpressionInfo"]["expression"]

        self.assertIn("length_txt", expression)
        self.assertIn("distance_txt", expression)
        self.assertNotIn("bearing_txt", expression)
        self.assertNotIn("TextFormatting.NewLine", expression)

    def test_polygon_popup_exposes_suid_and_area(self):
        definition = admin_script.build_web_map_definition(admin_script._resolve_config(admin_script.parse_args(["export-config"]), _settings()))
        polygons = next(layer for layer in definition["operationalLayers"] if layer["id"] == "working_polygons")
        fields = [field["fieldName"] for field in polygons["popupInfo"]["fieldInfos"]]

        self.assertIn("SUID", fields)
        self.assertIn("area_sq_m", fields)
        self.assertIn("transaction_number", fields)

    def test_generated_settings_include_web_map_outputs(self):
        args = admin_script.parse_args(["export-config"])
        payload = admin_script.build_payload(args)

        settings = payload["generated_settings"]["enterprise_innola_map_view"]

        self.assertIn("outputs", settings)
        self.assertEqual("working_review_innola_view", settings["feature_layer_view_name"])
        self.assertEqual("innola_compute_review_map", settings["web_map_name"])

    def test_live_provision_requires_token(self):
        previous = os.environ.pop("ARCGIS_PORTAL_TOKEN", None)
        try:
            args = admin_script.parse_args(
                [
                    "provision",
                    "--portal-url",
                    "https://enterprise.local/portal",
                    "--feature-layer-view-url",
                    "https://enterprise.local/server/rest/services/Hosted/working_review_innola_view/FeatureServer",
                    "--no-dry-run",
                ]
            )
            config = _settings()
            config["enterprise_innola_map_view"]["outputs"] = {
                "feature_layer_view_url": "https://enterprise.local/server/rest/services/Hosted/working_review_innola_view/FeatureServer"
            }

            payload = admin_script.build_payload(args)
        finally:
            if previous is not None:
                os.environ["ARCGIS_PORTAL_TOKEN"] = previous

        self.assertEqual("failed", payload["status"])
        self.assertTrue(any("portal token" in error.lower() for error in payload["validation"]["errors"]))

    def test_live_provision_allows_source_service_map_fallback(self):
        previous = os.environ.get("ARCGIS_PORTAL_TOKEN")
        os.environ["ARCGIS_PORTAL_TOKEN"] = "token"
        settings = _settings()
        settings["enterprise_innola_map_view"].pop("feature_layer_view_url")
        original_load = admin_script._load_settings
        original_rest = admin_script._create_or_get_feature_layer_view_rest
        original_api = admin_script._create_or_get_feature_layer_view_with_arcgis_api
        original_post = admin_script._post_form
        calls = []

        def fake_post(url, form, token_env_var):
            calls.append((url, form))
            if url.endswith("/community/self"):
                return {"username": "GIS_Test"}
            if url.endswith("/search"):
                return {"results": []}
            if url.endswith("/addItem"):
                return {"success": True, "id": "web-map-item"}
            return {"success": True}

        try:
            admin_script._load_settings = lambda path: settings
            admin_script._create_or_get_feature_layer_view_rest = lambda *args, **kwargs: (_ for _ in ()).throw(RuntimeError("rest failed"))
            admin_script._create_or_get_feature_layer_view_with_arcgis_api = lambda **kwargs: (_ for _ in ()).throw(RuntimeError("api failed"))
            admin_script._post_form = fake_post
            args = admin_script.parse_args(
                [
                    "provision",
                    "--no-dry-run",
                    "--allow-source-service-map",
                    "--source-feature-layer-item-id",
                    "source-item",
                ]
            )

            payload = admin_script.build_payload(args)
        finally:
            admin_script._load_settings = original_load
            admin_script._create_or_get_feature_layer_view_rest = original_rest
            admin_script._create_or_get_feature_layer_view_with_arcgis_api = original_api
            admin_script._post_form = original_post
            if previous is None:
                os.environ.pop("ARCGIS_PORTAL_TOKEN", None)
            else:
                os.environ["ARCGIS_PORTAL_TOKEN"] = previous

        self.assertEqual("provisioned", payload["status"])
        self.assertEqual("web-map-item", payload["outputs"]["web_map_item_id"])
        self.assertEqual("source_service_fallback", payload["live_result"]["feature_layer_view"]["action"])
        self.assertTrue(any("source service" in warning for warning in payload["validation"]["warnings"]))

    def test_working_service_url_is_derived_from_child_layer_url(self):
        args = admin_script.parse_args(["export-config"])
        config = admin_script._resolve_config(args, _settings())

        service_url = admin_script._working_service_url(config)

        self.assertEqual(
            "https://enterprise.local/server/rest/services/Hosted/working_review/FeatureServer",
            service_url,
        )

    def test_feature_layer_view_creation_reads_source_service_item_id(self):
        settings = _settings()
        settings["enterprise_innola_map_view"].pop("feature_layer_view_url")
        args = admin_script.parse_args(["provision", "--no-dry-run"])
        config = admin_script._resolve_config(args, settings)
        config["_token_env_var"] = "ARCGIS_PORTAL_TOKEN"
        previous = os.environ.get("ARCGIS_PORTAL_TOKEN")
        os.environ["ARCGIS_PORTAL_TOKEN"] = "token"
        original_post = admin_script._post_form
        original_create = admin_script._create_or_get_feature_layer_view_with_arcgis_api
        try:
            admin_script._post_form = lambda url, form, token_env_var: {"serviceItemId": "source-item"}
            admin_script._create_or_get_feature_layer_view_with_arcgis_api = lambda **kwargs: {
                "action": "created",
                "source_feature_layer_item_id": kwargs["source_item_id"],
                "feature_layer_view_item_id": "view-item",
                "feature_layer_view_url": "https://enterprise.local/server/rest/services/Hosted/working_review_innola_view/FeatureServer",
            }
            errors = []
            warnings = []

            result = admin_script._ensure_feature_layer_view(config, errors, warnings)
        finally:
            admin_script._post_form = original_post
            admin_script._create_or_get_feature_layer_view_with_arcgis_api = original_create
            if previous is None:
                os.environ.pop("ARCGIS_PORTAL_TOKEN", None)
            else:
                os.environ["ARCGIS_PORTAL_TOKEN"] = previous

        self.assertFalse(errors)
        self.assertEqual("created", result["action"])
        self.assertEqual("source-item", result["source_feature_layer_item_id"])
        self.assertEqual("view-item", result["feature_layer_view_item_id"])

    def test_rest_feature_layer_view_creation_adds_source_children(self):
        settings = _settings()
        settings["enterprise_innola_map_view"].pop("feature_layer_view_url")
        args = admin_script.parse_args(["provision", "--no-dry-run"])
        config = admin_script._resolve_config(args, settings)
        calls = []

        def fake_post(url, form, token_env_var):
            calls.append((url, form))
            if url.endswith("/community/self"):
                return {"username": "GIS_Test"}
            if url.endswith("/search"):
                return {"results": []}
            if url.endswith("/FeatureServer"):
                return {
                    "serviceItemId": "source-item",
                    "spatialReference": {"wkid": 3448},
                    "initialExtent": {"xmin": 0, "ymin": 0, "xmax": 1, "ymax": 1, "spatialReference": {"wkid": 3448}},
                    "layers": [{"id": 1, "name": "working_points"}, {"id": 2, "name": "working_lines"}],
                    "tables": [{"id": 4, "name": "working_case_index"}],
                }
            if url.endswith("/createService"):
                return {"success": True, "itemId": "view-item"}
            if url.endswith("/content/items/view-item"):
                return {
                    "id": "view-item",
                    "url": "https://enterprise.local/server/rest/services/Hosted/working_review_innola_view/FeatureServer",
                }
            if url.endswith("/addToDefinition") or url.endswith("/updateDefinition"):
                return {"success": True}
            return {"success": True}

        original_post = admin_script._post_form
        try:
            admin_script._post_form = fake_post
            result = admin_script._create_or_get_feature_layer_view_rest(config, "source-item", "ARCGIS_PORTAL_TOKEN")
        finally:
            admin_script._post_form = original_post

        self.assertEqual("created", result["action"])
        self.assertEqual("view-item", result["feature_layer_view_item_id"])
        add_calls = [call for call in calls if call[0].endswith("/addToDefinition")]
        self.assertTrue(add_calls)
        add_definition = json.loads(add_calls[0][1]["addToDefinition"])
        self.assertEqual(2, len(add_definition["layers"]))
        self.assertEqual(1, len(add_definition["tables"]))
        self.assertEqual("working_review", add_definition["layers"][0]["adminLayerInfo"]["viewLayerDefinition"]["sourceServiceName"])

    def test_configured_feature_layer_view_is_reused_without_arcgis_api(self):
        args = admin_script.parse_args(["provision", "--no-dry-run"])
        config = admin_script._resolve_config(args, _settings())
        config["_token_env_var"] = "ARCGIS_PORTAL_TOKEN"
        original_post = admin_script._post_form

        try:
            admin_script._post_form = lambda url, form, token_env_var: {"layers": [{"id": 0}], "tables": []}
            result = admin_script._ensure_feature_layer_view(config, [], [])
        finally:
            admin_script._post_form = original_post

        self.assertEqual("configured", result["action"])
        self.assertEqual(
            "https://enterprise.local/server/rest/services/Hosted/working_review_innola_view/FeatureServer",
            result["feature_layer_view_url"],
        )

    def test_allow_source_service_map_falls_back_when_view_creation_fails(self):
        previous = os.environ.get("ARCGIS_PORTAL_TOKEN")
        os.environ["ARCGIS_PORTAL_TOKEN"] = "token"
        settings = _settings()
        settings["enterprise_innola_map_view"].pop("feature_layer_view_url")
        args = admin_script.parse_args(["provision", "--no-dry-run", "--allow-source-service-map"])
        config = admin_script._resolve_config(args, settings)
        config["_token_env_var"] = "ARCGIS_PORTAL_TOKEN"
        config["source_feature_layer_item_id"] = "source-item"
        warnings = []
        original_rest = admin_script._create_or_get_feature_layer_view_rest
        original_api = admin_script._create_or_get_feature_layer_view_with_arcgis_api

        try:
            admin_script._create_or_get_feature_layer_view_rest = lambda *args, **kwargs: (_ for _ in ()).throw(RuntimeError("rest failed"))
            admin_script._create_or_get_feature_layer_view_with_arcgis_api = lambda **kwargs: (_ for _ in ()).throw(RuntimeError("api failed"))
            result = admin_script._ensure_feature_layer_view(config, [], warnings)
        finally:
            admin_script._create_or_get_feature_layer_view_rest = original_rest
            admin_script._create_or_get_feature_layer_view_with_arcgis_api = original_api
            if previous is None:
                os.environ.pop("ARCGIS_PORTAL_TOKEN", None)
            else:
                os.environ["ARCGIS_PORTAL_TOKEN"] = previous

        self.assertEqual("source_service_fallback", result["action"])
        self.assertTrue(any("source service" in warning for warning in warnings))

    def test_arcgis_create_view_exception_reuses_view_created_by_portal(self):
        class FakeItem:
            id = "view-item"
            title = "working_review_innola_view"
            url = "https://enterprise.local/server/rest/services/Hosted/working_review_innola_view/FeatureServer"

            def share(self, groups):
                return {"success": True}

        class FakeContent:
            def __init__(self):
                self.search_count = 0

            def get(self, item_id):
                if item_id == "source-item":
                    return object()
                return None

            def search(self, query, item_type, max_items):
                self.search_count += 1
                return [] if self.search_count == 1 else [FakeItem()]

        class FakeGIS:
            def __init__(self, portal_url, token):
                self.content = FakeContent()

        class FakeCollectionManager:
            def create_view(self, name):
                raise KeyError("success")

            def update_definition(self, definition):
                return {"success": True}

        class FakeFeatureLayerCollection:
            manager = FakeCollectionManager()

            @staticmethod
            def fromitem(item):
                return FakeFeatureLayerCollection()

        arcgis_module = types.ModuleType("arcgis")
        gis_module = types.ModuleType("arcgis.gis")
        features_module = types.ModuleType("arcgis.features")
        gis_module.GIS = FakeGIS
        features_module.FeatureLayerCollection = FakeFeatureLayerCollection
        previous = {name: sys.modules.get(name) for name in ("arcgis", "arcgis.gis", "arcgis.features")}
        try:
            sys.modules["arcgis"] = arcgis_module
            sys.modules["arcgis.gis"] = gis_module
            sys.modules["arcgis.features"] = features_module

            result = admin_script._create_or_get_feature_layer_view_with_arcgis_api(
                portal_url="https://enterprise.local/portal",
                token="token",
                source_item_id="source-item",
                view_name="working_review_innola_view",
            )
        finally:
            for name, module in previous.items():
                if module is None:
                    sys.modules.pop(name, None)
                else:
                    sys.modules[name] = module

        self.assertEqual("reused_after_create_exception", result["action"])
        self.assertEqual("view-item", result["feature_layer_view_item_id"])

    def test_live_result_is_redacted(self):
        redacted = admin_script._redact({"token": "secret", "nested": {"password": "secret", "ok": "value"}})

        self.assertEqual("***REDACTED***", redacted["token"])
        self.assertEqual("***REDACTED***", redacted["nested"]["password"])
        self.assertEqual("value", redacted["nested"]["ok"])


if __name__ == "__main__":
    unittest.main()
