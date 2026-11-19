"""Regression tests for scripts/check-nupkg-target.sh, the package-level target gate."""

from pathlib import Path
import subprocess
import tempfile
import unittest
import zipfile

SCRIPT = Path(__file__).resolve().parent / "check-nupkg-target.sh"

NUSPEC_NAME = "Example.Provider.nuspec"


def nuspec(*groups):
    body = "".join(groups)
    return (
        '<?xml version="1.0" encoding="utf-8"?>\n'
        '<package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">\n'
        "  <metadata><id>Example.Provider</id><dependencies>"
        f"{body}</dependencies></metadata>\n</package>\n"
    )


GROUP_NET8 = '<group targetFramework="net8.0"><dependency id="Vouchfx.Sdk" version="1.2.0" /></group>'
LIB_NET8 = {"lib/net8.0/Example.Provider.dll": b"MZ"}


class CheckNupkgTargetTests(unittest.TestCase):
    def setUp(self):
        tmp = tempfile.TemporaryDirectory()
        self.addCleanup(tmp.cleanup)
        self.dir = Path(tmp.name)

    def package(self, entries, name="fixture.nupkg"):
        path = self.dir / name
        with zipfile.ZipFile(path, "w") as archive:
            for entry, content in entries.items():
                archive.writestr(entry, content)
        return path

    def run_script(self, path):
        return subprocess.run(
            ["bash", str(SCRIPT), str(path)],
            capture_output=True,
            text=True,
            check=False,
        ).returncode

    def assert_exit(self, expected, entries, nuspec_text=None):
        files = dict(entries)
        if nuspec_text is not False:
            files[NUSPEC_NAME] = nuspec_text if nuspec_text is not None else nuspec(GROUP_NET8)
        self.assertEqual(self.run_script(self.package(files)), expected)

    def test_net8_only_passes(self):
        self.assert_exit(0, LIB_NET8)

    def test_nuspec_without_groups_passes(self):
        self.assert_exit(0, LIB_NET8, nuspec())

    def test_other_folders_are_out_of_scope(self):
        entries = dict(LIB_NET8)
        entries.update({
            "analyzers/dotnet/cs/Example.Analyzers.dll": b"MZ",
            "contentFiles/any/any/data.json": b"{}",
            "build/Example.Provider.targets": b"<Project />",
            "lib/net8.0/de/Example.Provider.resources.dll": b"MZ",
        })
        self.assert_exit(0, entries)

    def test_lib_net10_fails(self):
        self.assert_exit(1, {"lib/net10.0/Example.Provider.dll": b"MZ"})

    def test_lib_net8_alongside_net10_fails(self):
        entries = dict(LIB_NET8)
        entries["lib/net10.0/Example.Provider.dll"] = b"MZ"
        self.assert_exit(1, entries)

    def test_lib_netstandard_only_fails(self):
        self.assert_exit(1, {"lib/netstandard2.0/Example.Provider.dll": b"MZ"})

    def test_no_lib_entry_fails(self):
        self.assert_exit(1, {"readme.md": b"x"})

    def test_lib_file_without_folder_fails(self):
        self.assert_exit(1, {"lib/Example.Provider.dll": b"MZ"})

    def test_wrong_case_folder_fails(self):
        self.assert_exit(1, {"lib/NET8.0/Example.Provider.dll": b"MZ"})

    def test_nuspec_group_net10_fails(self):
        self.assert_exit(1, LIB_NET8, nuspec('<group targetFramework="net10.0" />'))

    def test_nuspec_group_single_quotes_fails(self):
        self.assert_exit(1, LIB_NET8, nuspec("<group targetFramework='net10.0' />"))

    def test_nuspec_group_split_across_lines_fails(self):
        self.assert_exit(1, LIB_NET8, nuspec('<group\n    targetFramework="net10.0" />'))

    def test_one_bad_group_among_good_ones_fails(self):
        self.assert_exit(1, LIB_NET8, nuspec(GROUP_NET8, '<group targetFramework="net10.0" />'))

    def test_not_a_zip_fails(self):
        path = self.dir / "not-a-zip.nupkg"
        path.write_bytes(b"this is not a zip archive")
        self.assertNotEqual(self.run_script(path), 0)

    def test_missing_file_fails(self):
        self.assertNotEqual(self.run_script(self.dir / "absent.nupkg"), 0)

    def test_missing_nuspec_fails(self):
        self.assert_exit(1, LIB_NET8, False)


if __name__ == "__main__":
    unittest.main()
