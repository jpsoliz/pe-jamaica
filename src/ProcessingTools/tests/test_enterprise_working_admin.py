import importlib.util
import io
import json
import os
import tempfile
import unittest
from contextlib import redirect_stdout
from pathlib import Path


SCRIPT_PATH = Path(__file__).resolve().parents[1] / "admin" / "provision_enterprise_working_layers.py"
SPEC = importlib.util.spec_from_file_location("provision_enterprise_working_layers", SCRIPT_PATH)
admin_script = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(admin_script)

TEMPLATE_SCRIPT_PATH = Path(__file__).resolve().parents[1] / "admin" / "create_enterprise_working_schema_template.py"
TEMPLATE_SPEC = importlib.util.spec_from_file_location("create_enterprise_working_schema_template", TEMPLATE_SCRIPT_PATH)
template_script = importlib.util.module_from_spec(TEMPLATE_SPEC)
TEMPLATE_SPEC.loader.exec_module(template_script)


def _valid_child_metadata(role: str = "points"):
    role_fields = {
        "points": admin_script.POINT_FIELDS,
        "lines": admin_script.LINE_FIELDS,
        "polygons": admin_script.POLYGON_FIELDS,
        "case_index": admin_script.CASE_INDEX_FIELDS,
        "issues": admin_script.ISSUE_FIELDS,
    }[role]
    metadata = {
        "fields": admin_script._field_definitions([*admin_script.SHARED_FIELDS, *role_fields]),
        "capabilities": "Query,Create,Update,Delete,Editing",
    }
    geometry_types = {
        "points": "esriGeometryPoint",
        "lines": "esriGeometryPolyline",
        "polygons": "esriGeometryPolygon",
        "issues": "esriGeometryPoint",
    }
    if role in geometry_types:
        metadata["geometryType"] = geometry_types[role]
    return metadata


class EnterpriseWorkingAdminTests(unittest.TestCase):
    def test_validate_requires_case_index_layer(self):
        args = admin_script.parse_args(
            [
                "--mode",
                "validate",
                "--points-layer",
                "https://enterprise.local/FeatureServer/0",
                "--lines-layer",
                "https://enterprise.local/FeatureServer/1",
                "--polygons-layer",
                "https://enterprise.local/FeatureServer/2",
            ]
        )

        payload = admin_script.build_payload(args)

        self.assertEqual("failed", payload["status"])
        self.assertIn("case_index layer URL is required for validate.", payload["validation"]["errors"])

    def test_cleanup_requires_explicit_scope(self):
        args = admin_script.parse_args(
            [
                "--mode",
                "cleanup",
                "--require-cleanup-scope",
                "--points-layer",
                "https://enterprise.local/FeatureServer/0",
                "--lines-layer",
                "https://enterprise.local/FeatureServer/1",
                "--polygons-layer",
                "https://enterprise.local/FeatureServer/2",
                "--case-index-layer",
                "https://enterprise.local/FeatureServer/3",
            ]
        )

        payload = admin_script.build_payload(args)

        self.assertEqual("failed", payload["status"])
        self.assertIn("cleanup_scope_value is required for cleanup.", payload["validation"]["errors"])

    def test_provision_dry_run_writes_generated_layer_urls(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            output_json = Path(temp_dir) / "provision.json"
            with redirect_stdout(io.StringIO()):
                exit_code = admin_script.main(
                    [
                        "--mode",
                        "provision",
                        "--service-root",
                        "https://enterprise.local/server/rest",
                        "--target-service-name",
                        "sidwell_working_review",
                        "--output-json",
                        str(output_json),
                    ]
                )

            payload = json.loads(output_json.read_text(encoding="utf-8"))

        self.assertEqual(0, exit_code)
        self.assertEqual("provision_ready", payload["status"])
        self.assertEqual(
            "https://enterprise.local/server/rest/services/Hosted/sidwell_working_review/FeatureServer/3",
            payload["layers"]["case_index"],
        )
        self.assertNotIn("password", json.dumps(payload).lower())

    def test_generated_urls_support_service_root_that_already_ends_with_services(self):
        args = admin_script.parse_args(
            [
                "--mode",
                "provision",
                "--service-root",
                "https://enterprise.local/server/rest/services",
                "--target-service-name",
                "working_review",
            ]
        )

        payload = admin_script.build_payload(args)

        self.assertEqual(
            "https://enterprise.local/server/rest/services/Hosted/working_review/FeatureServer/0",
            payload["layers"]["points"],
        )

    def test_live_provision_requires_token_instead_of_reporting_ready(self):
        previous = os.environ.pop("ARCGIS_PORTAL_TOKEN", None)
        try:
            args = admin_script.parse_args(
                [
                    "--mode",
                    "provision",
                    "--portal-url",
                    "https://enterprise.local/portal",
                    "--service-root",
                    "https://enterprise.local/server/rest",
                    "--target-service-name",
                    "sidwell_working_review",
                    "--no-dry-run",
                ]
            )

            payload = admin_script.build_payload(args)
        finally:
            if previous is not None:
                os.environ["ARCGIS_PORTAL_TOKEN"] = previous

        self.assertEqual("failed", payload["status"])
        self.assertIn("Live provisioning requires a portal token", payload["validation"]["errors"][0])
        self.assertEqual({}, payload["layers"])

    def test_add_to_definition_urls_include_public_and_admin_shapes(self):
        urls = admin_script._add_to_definition_urls(
            "https://jm-gis.innola-solutions.com/server/rest/services/Hosted/working_review/FeatureServer"
        )

        self.assertIn(
            "https://jm-gis.innola-solutions.com/server/rest/services/Hosted/working_review/FeatureServer/addToDefinition",
            urls,
        )
        self.assertIn(
            "https://jm-gis.innola-solutions.com/server/rest/admin/services/Hosted/working_review.FeatureServer/addToDefinition",
            urls,
        )
        self.assertIn(
            "https://jm-gis.innola-solutions.com/server/admin/services/Hosted/working_review.FeatureServer/addToDefinition",
            urls,
        )
        self.assertIn(
            "https://jm-gis.innola-solutions.com/server/rest/services/Hosted/working_review/FeatureServer/admin/addToDefinition",
            urls,
        )

    def test_post_form_treats_arcgis_status_error_as_failure(self):
        class Response:
            def __enter__(self):
                return self

            def __exit__(self, exc_type, exc, trace):
                return False

            def read(self):
                return json.dumps(
                    {
                        "status": "error",
                        "messages": ["Could not find resource or operation 'addToDefinition' on the system."],
                        "code": 404,
                    }
                ).encode("utf-8")

        original_urlopen = admin_script.urlopen
        try:
            admin_script.urlopen = lambda request, timeout: Response()
            with self.assertRaisesRegex(RuntimeError, "status=error"):
                admin_script._post_form("https://enterprise.local/admin/addToDefinition", {}, "")
        finally:
            admin_script.urlopen = original_urlopen

    def test_failed_live_provision_does_not_emit_generated_settings_for_writeback(self):
        previous = os.environ.get("ARCGIS_PORTAL_TOKEN")
        os.environ["ARCGIS_PORTAL_TOKEN"] = "token"
        original = admin_script._provision_live_layers_rest
        try:
            admin_script._provision_live_layers_rest = lambda args: (_ for _ in ()).throw(
                RuntimeError("No schema template was found")
            )
            args = admin_script.parse_args(
                [
                    "--mode",
                    "provision",
                    "--portal-url",
                    "https://enterprise.local/portal",
                    "--service-root",
                    "https://enterprise.local/server/rest",
                    "--target-service-name",
                    "working_review",
                    "--no-dry-run",
                ]
            )

            payload = admin_script.build_payload(args)
        finally:
            admin_script._provision_live_layers_rest = original
            if previous is None:
                os.environ.pop("ARCGIS_PORTAL_TOKEN", None)
            else:
                os.environ["ARCGIS_PORTAL_TOKEN"] = previous

        self.assertEqual("failed", payload["status"])
        self.assertEqual(
            {},
            payload["generated_settings"]["enterprise_working_review"]["layers"],
            "failed live provision must not expose fake layer URLs for Settings writeback",
        )

    def test_existing_empty_feature_service_is_rejected_before_writeback(self):
        original_fetch = admin_script._fetch_layer_metadata
        try:
            admin_script._fetch_layer_metadata = lambda url, token_env_var: {"layers": [], "tables": []}

            with self.assertRaisesRegex(RuntimeError, "empty hosted Feature Service"):
                admin_script._verify_service_children_or_raise(
                    "https://enterprise.local/server/rest/services/Hosted/working_review/FeatureServer",
                    "",
                )
        finally:
            admin_script._fetch_layer_metadata = original_fetch

    def test_schema_template_publish_maps_verified_children_to_runtime_layers(self):
        args = admin_script.parse_args(
            [
                "--mode",
                "provision",
                "--portal-url",
                "https://enterprise.local/portal",
                "--service-root",
                "https://enterprise.local/server/rest",
                "--target-service-name",
                "working_review",
                "--schema-template-path",
                "working_review_schema.zip",
                "--no-dry-run",
            ]
        )
        calls = []

        def fake_post(url, form, token_env_var, *, raise_on_error=True):
            calls.append((url, dict(form)))
            if url.endswith("/community/self"):
                return {"username": "admin"}
            if url.endswith("/publish"):
                self.assertIn("itemid", form)
                self.assertIn("fileType", form)
                self.assertEqual("featureService", form["outputType"])
                return {
                    "services": [
                        {
                            "serviceurl": "https://enterprise.local/server/rest/services/Hosted/working_review/FeatureServer"
                        }
                    ]
                }
            return {"success": True}

        def fake_multipart(url, form, file_field, file_path, token_env_var):
            calls.append((url, dict(form)))
            return {"success": True, "id": "template-item"}

        original_post = admin_script._post_form
        original_multipart = admin_script._post_multipart_file
        original_fetch = admin_script._fetch_layer_metadata
        original_exists = admin_script.Path.exists
        try:
            admin_script._post_form = fake_post
            admin_script._post_multipart_file = fake_multipart
            admin_script.Path.exists = lambda self: str(self).endswith("working_review_schema.zip")
            def fake_fetch(url, token_env_var):
                if url.endswith("/FeatureServer"):
                    return {
                        "spatialReference": {"wkid": 3448, "latestWkid": 3448},
                        "layers": [
                            {"id": 0, "name": "working_points"},
                            {"id": 1, "name": "working_lines"},
                            {"id": 2, "name": "working_polygons"},
                            {"id": 4, "name": "working_issues"},
                        ],
                        "tables": [{"id": 3, "name": "working_case_index"}],
                    }
                role_by_suffix = {
                    "/0": "points",
                    "/1": "lines",
                    "/2": "polygons",
                    "/3": "case_index",
                    "/4": "issues",
                }
                for suffix, role in role_by_suffix.items():
                    if url.endswith(suffix):
                        return _valid_child_metadata(role)
                return _valid_child_metadata()

            admin_script._fetch_layer_metadata = fake_fetch

            layers = admin_script._provision_live_layers_rest(args)
        finally:
            admin_script._post_form = original_post
            admin_script._post_multipart_file = original_multipart
            admin_script._fetch_layer_metadata = original_fetch
            admin_script.Path.exists = original_exists

        self.assertEqual(
            "https://enterprise.local/server/rest/services/Hosted/working_review/FeatureServer/3",
            layers["case_index"],
        )
        self.assertFalse(any(url.endswith("/addItem") for url, _ in calls))
        self.assertFalse(any(url.endswith("/publish") for url, _ in calls))

    def test_schema_template_publish_updates_existing_template_item(self):
        args = admin_script.parse_args(
            [
                "--mode",
                "provision",
                "--portal-url",
                "https://enterprise.local/portal",
                "--service-root",
                "https://enterprise.local/server/rest",
                "--target-service-name",
                "working_review",
                "--schema-template-path",
                "working_review_schema.zip",
                "--no-dry-run",
            ]
        )
        calls = []

        def fake_post(url, form, token_env_var, *, raise_on_error=True):
            calls.append((url, dict(form)))
            if url.endswith("/community/self"):
                return {"username": "admin"}
            if url.endswith("/search"):
                return {
                    "results": [
                        {
                            "id": "existing-template-item",
                            "title": "working_review schema template",
                            "type": "File Geodatabase",
                        }
                    ]
                }
            if url.endswith("/publish"):
                return {
                    "services": [
                        {
                            "serviceurl": "https://enterprise.local/server/rest/services/Hosted/working_review/FeatureServer"
                        }
                    ]
                }
            return {"success": True}

        def fake_multipart(url, form, file_field, file_path, token_env_var):
            calls.append((url, dict(form)))
            return {"success": True}

        original_post = admin_script._post_form
        original_multipart = admin_script._post_multipart_file
        original_exists = admin_script.Path.exists
        try:
            admin_script._post_form = fake_post
            admin_script._post_multipart_file = fake_multipart
            admin_script.Path.exists = lambda self: str(self).endswith("working_review_schema.zip")

            service_url = admin_script._publish_schema_template("https://enterprise.local/portal", "admin", args)
        finally:
            admin_script._post_form = original_post
            admin_script._post_multipart_file = original_multipart
            admin_script.Path.exists = original_exists

        self.assertEqual(
            "https://enterprise.local/server/rest/services/Hosted/working_review/FeatureServer",
            service_url,
        )
        self.assertTrue(any("/items/existing-template-item/update" in url for url, _ in calls))
        self.assertFalse(any(url.endswith("/addItem") for url, _ in calls))
        self.assertTrue(any(url.endswith("/publish") for url, _ in calls))

    def test_live_provision_rejects_existing_empty_service(self):
        args = admin_script.parse_args(
            [
                "--mode",
                "provision",
                "--portal-url",
                "https://enterprise.local/portal",
                "--service-root",
                "https://enterprise.local/server/rest",
                "--target-service-name",
                "working_review",
                "--no-dry-run",
            ]
        )

        original_post = admin_script._post_form
        original_fetch = admin_script._fetch_layer_metadata
        try:
            admin_script._post_form = lambda url, form, token_env_var, **kwargs: {"username": "admin"}
            admin_script._fetch_layer_metadata = lambda url, token_env_var: {
                "spatialReference": {"wkid": 3448, "latestWkid": 3448},
                "layers": [],
                "tables": [],
            }

            with self.assertRaisesRegex(RuntimeError, "empty hosted Feature Service"):
                admin_script._provision_live_layers_rest(args)
        finally:
            admin_script._post_form = original_post
            admin_script._fetch_layer_metadata = original_fetch

    def test_live_provision_rejects_existing_wrong_spatial_reference_service(self):
        args = admin_script.parse_args(
            [
                "--mode",
                "provision",
                "--portal-url",
                "https://enterprise.local/portal",
                "--service-root",
                "https://enterprise.local/server/rest",
                "--target-service-name",
                "working_review",
                "--no-dry-run",
            ]
        )

        original_post = admin_script._post_form
        original_fetch = admin_script._fetch_layer_metadata
        try:
            admin_script._post_form = lambda url, form, token_env_var, **kwargs: {"username": "admin"}
            admin_script._fetch_layer_metadata = lambda url, token_env_var: {
                "spatialReference": {"wkid": 102100, "latestWkid": 3857},
                "layers": [],
                "tables": [],
            }

            with self.assertRaisesRegex(RuntimeError, "EPSG:3448"):
                admin_script._provision_live_layers_rest(args)
        finally:
            admin_script._post_form = original_post
            admin_script._fetch_layer_metadata = original_fetch

    def test_live_provision_publishes_missing_service_from_feature_collection_schema(self):
        args = admin_script.parse_args(
            [
                "--mode",
                "provision",
                "--portal-url",
                "https://enterprise.local/portal",
                "--service-root",
                "https://enterprise.local/server/rest",
                "--target-service-name",
                "working_review",
                "--no-dry-run",
            ]
        )
        fetch_count = {"value": 0}
        post_calls = []

        def fake_post(url, form, token_env_var, *, raise_on_error=True):
            post_calls.append((url, dict(form)))
            if url.endswith("/community/self"):
                return {"username": "admin"}
            if url.endswith("/addItem"):
                schema = json.loads(form["text"])
                self.assertEqual({"wkid": 3448, "latestWkid": 3448}, schema["spatialReference"])
                self.assertEqual(5, len(schema["layers"]))
                field_names = {field["name"] for field in schema["layers"][1]["layerDefinition"]["fields"]}
                self.assertIn("bearing_txt", field_names)
                self.assertIn("distance_txt", field_names)
                self.assertIn("review_decision", field_names)
                return {"success": True, "id": "feature-collection-item"}
            if url.endswith("/publish"):
                return {
                    "services": [
                        {
                            "serviceurl": "https://enterprise.local/server/rest/services/Hosted/working_review/FeatureServer",
                            "serviceItemId": "service-item",
                            "jobId": "job-1",
                        }
                    ]
                }
            if url.endswith("/status"):
                return {"status": "completed"}
            if url.endswith("/delete"):
                return {"success": True}
            return {"success": True}

        def fake_fetch(url, token_env_var):
            fetch_count["value"] += 1
            if fetch_count["value"] == 1:
                raise RuntimeError("not found")
            if not url.endswith("/FeatureServer"):
                role_by_suffix = {
                    "/0": "points",
                    "/1": "lines",
                    "/2": "polygons",
                    "/3": "case_index",
                    "/4": "issues",
                }
                for suffix, role in role_by_suffix.items():
                    if url.endswith(suffix):
                        return _valid_child_metadata(role)
                return _valid_child_metadata()
            return {
                "spatialReference": {"wkid": 3448, "latestWkid": 3448},
                "layers": [
                    {"id": 0, "name": "working_points"},
                    {"id": 1, "name": "working_lines"},
                    {"id": 2, "name": "working_polygons"},
                    {"id": 4, "name": "working_issues"},
                ],
                "tables": [{"id": 3, "name": "working_case_index"}],
            }

        original_post = admin_script._post_form
        original_fetch = admin_script._fetch_layer_metadata
        try:
            admin_script._post_form = fake_post
            admin_script._fetch_layer_metadata = fake_fetch

            layers = admin_script._provision_live_layers_rest(args)
        finally:
            admin_script._post_form = original_post
            admin_script._fetch_layer_metadata = original_fetch

        self.assertEqual(
            "https://enterprise.local/server/rest/services/Hosted/working_review/FeatureServer/3",
            layers["case_index"],
        )
        self.assertTrue(any(url.endswith("/addItem") for url, _ in post_calls))
        self.assertTrue(any(url.endswith("/publish") for url, _ in post_calls))

    def test_feature_collection_schema_includes_compute_disposition_fields_and_domain(self):
        args = admin_script.parse_args(["--mode", "provision"])

        schema = admin_script._feature_collection_schema(args)

        for child in schema["layers"]:
            layer_definition = child["layerDefinition"]
            if layer_definition["name"] == "working_issues":
                continue

            fields = {field["name"]: field for field in layer_definition["fields"]}
            for field_name in (
                "review_decision",
                "review_decision_by",
                "review_decision_utc",
                "review_comment",
                "official_comparison_status",
                "official_reference_ids",
            ):
                self.assertIn(field_name, fields, f"{layer_definition['name']} missing {field_name}")

            self.assertGreaterEqual(fields["review_comment"]["length"], 1024)
            coded_values = {
                value["code"]
                for value in fields["review_decision"]["domain"]["codedValues"]
            }
            self.assertEqual({"pending", "approved", "rejected", "postponed"}, coded_values)

    def test_file_geodatabase_template_generator_includes_compute_disposition_fields(self):
        shared_fields = {field_name for field_name, _, _ in template_script.SHARED_FIELDS}

        self.assertIn("review_decision", shared_fields)
        self.assertIn("review_decision_by", shared_fields)
        self.assertIn("review_decision_utc", shared_fields)
        self.assertIn("review_comment", shared_fields)
        self.assertIn("official_comparison_status", shared_fields)
        self.assertIn("official_reference_ids", shared_fields)

    def test_validate_fields_reports_schema_upgrade_for_missing_disposition_fields(self):
        metadata = {
            "fields": [
                {"name": field_name}
                for field_name in (
                    "transaction_number",
                    "transaction_id",
                    "task_id",
                    "workflow_stage",
                    "review_state",
                    "case_status",
                    "created_by",
                    "created_utc",
                    "last_saved_by",
                    "last_saved_utc",
                    "run_id",
                    "is_active",
                    "edit_generation",
                )
            ]
        }
        errors = []

        admin_script._validate_fields("points", metadata, errors)

        self.assertTrue(any("schema upgrade required" in error for error in errors))
        self.assertTrue(any("review_decision" in error for error in errors))

    def test_validate_fields_allows_unrestricted_review_decision_text(self):
        metadata = {
            "fields": [
                {"name": field_name, "length": 1024 if field_name == "review_comment" else 64}
                for field_name in admin_script.SHARED_FIELDS
            ]
        }
        errors = []

        admin_script._validate_fields("points", metadata, errors)

        self.assertFalse(errors)

    def test_verified_layer_urls_rejects_old_schema_before_writeback(self):
        service_url = "https://enterprise.local/server/rest/services/Hosted/working_review/FeatureServer"
        parent_metadata = {
            "layers": [
                {"id": 0, "name": "working_points"},
                {"id": 1, "name": "working_lines"},
                {"id": 2, "name": "working_polygons"},
                {"id": 3, "name": "working_issues"},
            ],
            "tables": [{"id": 4, "name": "working_case_index"}],
        }
        old_child_metadata = {
            "fields": [
                {"name": field_name}
                for field_name in (
                    "transaction_number",
                    "transaction_id",
                    "task_id",
                    "workflow_stage",
                    "review_state",
                    "case_status",
                    "created_by",
                    "created_utc",
                    "last_saved_by",
                    "last_saved_utc",
                    "run_id",
                    "is_active",
                    "edit_generation",
                )
            ],
            "capabilities": "Query,Create,Update,Delete,Editing",
        }

        def fake_fetch(url, token_env_var):
            if url == service_url:
                return parent_metadata
            return old_child_metadata

        original_fetch = admin_script._fetch_layer_metadata
        try:
            admin_script._fetch_layer_metadata = fake_fetch
            with self.assertRaisesRegex(RuntimeError, "schema upgrade required"):
                admin_script._verified_layer_urls(service_url, "")
        finally:
            admin_script._fetch_layer_metadata = original_fetch

    def test_verified_layer_urls_rejects_wrong_child_geometry_before_writeback(self):
        service_url = "https://enterprise.local/server/rest/services/Hosted/working_review/FeatureServer"
        parent_metadata = {
            "layers": [
                {"id": 0, "name": "working_points"},
                {"id": 1, "name": "working_lines"},
                {"id": 2, "name": "working_polygons"},
            ],
            "tables": [{"id": 4, "name": "working_case_index"}],
        }

        def fake_fetch(url, token_env_var):
            if url == service_url:
                return parent_metadata
            if url.endswith("/0"):
                return _valid_child_metadata("points")
            if url.endswith("/1"):
                metadata = _valid_child_metadata("lines")
                metadata["geometryType"] = "esriGeometryPoint"
                return metadata
            if url.endswith("/2"):
                return _valid_child_metadata("polygons")
            if url.endswith("/4"):
                return _valid_child_metadata("case_index")
            return _valid_child_metadata()

        original_fetch = admin_script._fetch_layer_metadata
        try:
            admin_script._fetch_layer_metadata = fake_fetch
            with self.assertRaisesRegex(RuntimeError, "lines layer geometry must be esriGeometryPolyline"):
                admin_script._verified_layer_urls(service_url, "")
        finally:
            admin_script._fetch_layer_metadata = original_fetch

    def test_schema_template_publish_resolves_service_item_id_url(self):
        args = admin_script.parse_args(
            [
                "--mode",
                "provision",
                "--portal-url",
                "https://enterprise.local/portal",
                "--service-root",
                "https://enterprise.local/server/rest",
                "--target-service-name",
                "working_review",
            ]
        )

        def fake_post(url, form, token_env_var, *, raise_on_error=True):
            if url.endswith("/status"):
                return {"status": "completed"}
            if url.endswith("/content/items/service-item"):
                return {"url": "https://enterprise.local/server/rest/services/Hosted/working_review/FeatureServer"}
            return {}

        original_post = admin_script._post_form
        try:
            admin_script._post_form = fake_post
            service_url = admin_script._read_published_service_url(
                {"services": [{"serviceItemId": "service-item", "jobId": "job-1"}]},
                args,
                "https://enterprise.local/portal",
                "admin",
            )
        finally:
            admin_script._post_form = original_post

        self.assertEqual(
            "https://enterprise.local/server/rest/services/Hosted/working_review/FeatureServer",
            service_url,
        )

    def test_schema_template_publish_existing_service_error_resolves_target_service_url(self):
        args = admin_script.parse_args(
            [
                "--mode",
                "provision",
                "--portal-url",
                "https://enterprise.local/portal",
                "--service-root",
                "https://enterprise.local/server/rest",
                "--target-service-name",
                "working_review",
            ]
        )

        service_url = admin_script._read_published_service_url(
            {
                "services": [
                    {
                        "success": False,
                        "error": {"message": "Service name 'working_review' already exists for '0123456789ABCDEF'"},
                    }
                ]
            },
            args,
            "https://enterprise.local/portal",
            "admin",
        )

        self.assertEqual(
            "https://enterprise.local/server/rest/services/Hosted/working_review/FeatureServer",
            service_url,
        )

    def test_verified_layer_urls_are_mapped_by_child_name_not_fixed_position(self):
        original_fetch = admin_script._fetch_layer_metadata
        try:
            def fake_fetch(url, token_env_var):
                if url.endswith("/FeatureServer"):
                    return {
                        "layers": [
                            {"id": 7, "name": "working_polygons"},
                            {"id": 2, "name": "working_points"},
                            {"id": 9, "name": "working_issues"},
                            {"id": 4, "name": "working_lines"},
                        ],
                        "tables": [{"id": 12, "name": "working_case_index"}],
                    }
                role_by_suffix = {
                    "/2": "points",
                    "/4": "lines",
                    "/7": "polygons",
                    "/9": "issues",
                    "/12": "case_index",
                }
                for suffix, role in role_by_suffix.items():
                    if url.endswith(suffix):
                        return _valid_child_metadata(role)
                return _valid_child_metadata()

            admin_script._fetch_layer_metadata = fake_fetch

            layers = admin_script._verified_layer_urls("https://enterprise.local/FeatureServer", "")
        finally:
            admin_script._fetch_layer_metadata = original_fetch

        self.assertEqual("https://enterprise.local/FeatureServer/2", layers["points"])
        self.assertEqual("https://enterprise.local/FeatureServer/4", layers["lines"])
        self.assertEqual("https://enterprise.local/FeatureServer/7", layers["polygons"])
        self.assertEqual("https://enterprise.local/FeatureServer/12", layers["case_index"])
        self.assertEqual("https://enterprise.local/FeatureServer/9", layers["issues"])

    def test_cleanup_writes_scoped_audit(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            audit_json = Path(temp_dir) / "enterprise_working_admin_audit.json"
            with redirect_stdout(io.StringIO()):
                exit_code = admin_script.main(
                    [
                        "--mode",
                        "cleanup",
                        "--require-cleanup-scope",
                        "--cleanup-scope-value",
                        "100000416",
                        "--points-layer",
                        "https://enterprise.local/FeatureServer/0",
                        "--lines-layer",
                        "https://enterprise.local/FeatureServer/1",
                        "--polygons-layer",
                        "https://enterprise.local/FeatureServer/2",
                        "--case-index-layer",
                        "https://enterprise.local/FeatureServer/3",
                        "--audit-json",
                        str(audit_json),
                    ]
                )
            audit = json.loads(audit_json.read_text(encoding="utf-8"))

        self.assertEqual(0, exit_code)
        self.assertEqual("transaction_number", audit["scope_field"])
        self.assertEqual("100000416", audit["scope_value"])
        self.assertEqual("cleanup_ready", audit["status"])
        self.assertEqual(0, audit["affected_counts"]["case_index"])


if __name__ == "__main__":
    unittest.main()
