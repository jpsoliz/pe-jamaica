import unittest

from adapters import extraction_adapter, output_adapter, preflight_adapter, validation_adapter


class AdapterPlaceholderTests(unittest.TestCase):
    def test_adapters_are_explicit_placeholders(self):
        for adapter in (preflight_adapter, extraction_adapter, output_adapter):
            with self.assertRaises(NotImplementedError):
                adapter.run("input.json", "output.json")

    def test_validation_adapter_requires_cli_entrypoint(self):
        with self.assertRaises(NotImplementedError):
            validation_adapter.run("input.json", "output.json")


if __name__ == "__main__":
    unittest.main()
