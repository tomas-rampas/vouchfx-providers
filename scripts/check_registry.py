"""Check the registry contract consumed by released engine CLIs (stdlib only)."""

import argparse
import json
from pathlib import Path
import re
import sys


# ProviderStore.ValidateId / IsReservedName at engine v1.2.0:
# https://github.com/tomas-rampas/vouchfx/blob/0af21de5bd0a4e91ca002e03e6c256bc4e8ca4e8/src/Cli/Vouchfx.Cli/Providers/ProviderStore.cs
# fullmatch supplies the strict \A...\z anchors, including refusal of a final LF.
PACKAGE_ID = re.compile(r"[A-Za-z0-9][A-Za-z0-9_.-]{0,99}")
RESERVED_NAMES = {"CON", "PRN", "AUX", "NUL"} | {
    f"{prefix}{number}" for prefix in ("COM", "LPT") for number in range(1, 10)
}
DEFAULT_REGISTRY = Path(__file__).resolve().parents[1] / "registry/community-providers.json"


def check_registry(entries):
    """Return diagnostics without echoing registry values or host paths.

    JSON Schema validation remains a separate CI gate. Hub-hosted, source-only
    entries may omit nuget; whenever present it must satisfy the CLI's rule.
    """
    if not isinstance(entries, list):
        return ["Registry must be a JSON array."]

    errors = []
    seen = set()
    for index, entry in enumerate(entries, 1):
        if not isinstance(entry, dict):
            errors.append(f"Entry {index}: expected a provider object.")
            continue
        step_kind = entry.get("stepKindId")
        if not isinstance(step_kind, str) or not step_kind.strip():
            errors.append(f"Entry {index}: stepKindId must be a non-empty string.")
        elif step_kind in seen:
            errors.append(f"Entry {index}: duplicate stepKindId.")
        else:
            # ProviderRegistry.Resolve uses StringComparison.Ordinal for aliases.
            seen.add(step_kind)

        if "nuget" not in entry:
            if entry.get("hosting") != "hub":
                errors.append(f"Entry {index}: external providers require a nuget ID.")
            continue
        package_id = entry["nuget"]
        if (
            not isinstance(package_id, str)
            or PACKAGE_ID.fullmatch(package_id) is None
            or package_id.endswith(".")
            or package_id.split(".")[0].upper() in RESERVED_NAMES
        ):
            errors.append(f"Entry {index}: nuget ID is not portable under engine v1.2.0.")
    return errors


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("registry", nargs="?", type=Path, default=DEFAULT_REGISTRY)
    args = parser.parse_args()
    try:
        entries = json.loads(args.registry.read_text(encoding="utf-8"))
    except (OSError, ValueError):
        print("Registry could not be read as UTF-8 JSON.", file=sys.stderr)
        return 1
    errors = check_registry(entries)
    for error in errors:
        print(error, file=sys.stderr)
    if errors:
        return 1
    print(f"Registry contract passed for {len(entries)} entries.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
