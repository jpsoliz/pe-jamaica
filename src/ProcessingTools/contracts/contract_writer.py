"""Contract writer placeholder for processing adapters."""

import json
from pathlib import Path


def write_contract(path, payload):
    destination = Path(path)
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_text(json.dumps(payload, indent=2), encoding="utf-8")
