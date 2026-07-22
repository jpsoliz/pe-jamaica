import json
import tempfile
import unittest
from pathlib import Path

from contracts.contract_writer import write_contract


class ContractWriterTests(unittest.TestCase):
    def test_write_contract_creates_json_file(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            path = Path(temp_dir) / "nested" / "manifest.json"
            payload = {"schema_version": "1.0.0", "transaction_id": "TR-SMD-0000001"}

            write_contract(path, payload)

            self.assertEqual(payload, json.loads(path.read_text(encoding="utf-8")))


if __name__ == "__main__":
    unittest.main()
