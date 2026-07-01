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
            admin_script._fetch_layer_metadata = lambda url, token_env_var: {
                "spatialReference": {"wkid": 3448, "latestWkid": 3448},
                "layers": [
                    {"id": 0, "name": "working_points"},
                    {"id": 1, "name": "working_lines"},
                    {"id": 2, "name": "working_polygons"},
                    {"id": 4, "name": "working_issues"},
                ],
                "tables": [{"id": 3, "name": "working_case_index"}],
            }

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
            admin_script._fetch_layer_metadata = lambda url, token_env_var: {
                "layers": [
                    {"id": 7, "name": "working_polygons"},
                    {"id": 2, "name": "working_points"},
                    {"id": 9, "name": "working_issues"},
                    {"id": 4, "name": "working_lines"},
                ],
                "tables": [{"id": 12, "name": "working_case_index"}],
            }

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
