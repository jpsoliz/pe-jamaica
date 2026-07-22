"""Schema loading placeholder for processing adapters."""

from pathlib import Path


def schema_path(schema_name):
    return Path(__file__).resolve().parents[2] / "Contracts" / "schemas" / schema_name
