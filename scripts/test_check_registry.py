"""Regression tests for the released CLI's registry compatibility contract."""

import contextlib
import io
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

from check_registry import check_registry, main


class RegistryContractTests(unittest.TestCase):
    def test_duplicate_step_kind_with_different_metadata_is_refused(self):
        entries = [
            {"stepKindId": "rpc.example", "nuget": "Example.One"},
            {"stepKindId": "rpc.example", "nuget": "Example.Two"},
        ]
        self.assertEqual(check_registry(entries), ["Entry 2: duplicate stepKindId."])

    def test_multiple_step_kinds_may_share_one_package(self):
        self.assertEqual(check_registry([
            {"stepKindId": "rpc.one", "nuget": "Example.Shared"},
            {"stepKindId": "rpc.two", "nuget": "Example.Shared"},
        ]), [])

    def test_portable_package_id_boundaries(self):
        for package_id in ("A", "a" * 100, "0", "a_b-c.d", "COM0", "COM10", "LPT0", "LPT10", "Console", "Example.CON", "CON-safe", "LPT1_safe"):
            with self.subTest(package_id=package_id):
                self.assertEqual(check_registry([
                    {"stepKindId": "rpc.example", "nuget": package_id},
                ]), [])

    def test_invalid_package_ids_are_refused(self):
        for package_id in ("", "a" * 101, ".abc", "_abc", "-abc", "Example.", "a/b", "a\\b", "a:b", "a b", "a\n", "a\r\n", "é", "aé", None, 42):
            with self.subTest(package_id=package_id):
                errors = check_registry([{"stepKindId": "rpc.example", "nuget": package_id}])
                self.assertEqual(errors, ["Entry 1: nuget ID is not portable under engine v1.2.0."])

    def test_reserved_first_segments_are_refused_case_insensitively(self):
        reserved = ["CON", "PRN", "AUX", "NUL"] + [
            f"{prefix}{number}" for prefix in ("COM", "LPT") for number in range(1, 10)
        ]
        for name in reserved:
            for package_id in (name, name.lower(), name.lower() + ".Provider"):
                with self.subTest(package_id=package_id):
                    self.assertTrue(check_registry([
                        {"stepKindId": "rpc.example", "nuget": package_id},
                    ]), "Reserved device names must be refused.")

    def test_only_source_only_hub_entries_may_omit_nuget(self):
        self.assertEqual(check_registry([{"stepKindId": "rpc.example", "hosting": "hub"}]), [])
        for hosting in (None, "external"):
            with self.subTest(hosting=hosting):
                self.assertTrue(check_registry([
                    {"stepKindId": "rpc.example", "hosting": hosting},
                ]), "External entries must supply a package ID.")

    def test_malformed_shapes_fail_closed(self):
        for entries in ({}, None, [None], [{}], [{"stepKindId": []}], [{"stepKindId": " "}]):
            with self.subTest(entries=entries):
                self.assertTrue(check_registry(entries), "Malformed registry entries must fail.")

    def test_cli_accepts_checked_in_registry_by_default(self):
        with patch("sys.argv", ["check_registry.py"]), contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(main(), 0)

    def test_cli_exit_status_for_invalid_contract_and_malformed_json(self):
        with tempfile.TemporaryDirectory() as directory:
            registry = Path(directory) / "registry.json"
            for content in (json.dumps([
                {"stepKindId": "rpc.example", "nuget": "NUL.Package"},
            ]), "{", "\ufffd"):
                registry.write_text(content, encoding="utf-8")
                output = io.StringIO()
                with patch("sys.argv", ["check_registry.py", str(registry)]), contextlib.redirect_stderr(output):
                    self.assertEqual(main(), 1)
                self.assertTrue(str(registry) not in output.getvalue(), "Diagnostics must omit host paths.")


if __name__ == "__main__":
    unittest.main()
