#!/usr/bin/env bash
# scripts/check-nupkg-target.sh <nupkg>
#
# Artefact-level check that a packed provider targets net8.0 and nothing else. The
# provider assembly target is the frozen v1 provider contract (the engine's
# `vouchfx providers install` installs only net8.0-compatible assets; the hub requires
# exactly lib/net8.0), so this reads the finished
# package rather than trusting any MSBuild evaluation that produced it:
#   - every `lib/` entry in the archive is under `lib/net8.0/`, and at least one exists;
#   - every `<group targetFramework="...">` in the nuspec is `net8.0` (either quote style,
#     and an attribute split across lines, are read).
# `runtimes/` and `ref/` are deliberately out of scope: the engine only extracts lib/*.dll
# from the group nearest to net8.0, so nothing under those folders is loaded.
# Run from conformance.yml's pack gate (validate_nupkg) and from publish-provider.yml
# straight after the pack that ships. Prints one ::error:: line per violation and exits
# non-zero if there was any.
set -euo pipefail

if [[ $# -ne 1 ]]; then
  echo "usage: $0 <nupkg>" >&2
  exit 2
fi
nupkg="$1"
failed=0

# -Z1 (zipinfo, names only) gives exact entry paths, one per line.
if ! zip_listing="$(unzip -Z1 "$nupkg")"; then
  echo "::error::$nupkg — could not list the package contents."
  exit 1
fi

lib_count=0
while IFS= read -r entry; do
  [[ "$entry" == lib/* ]] || continue
  lib_count=$((lib_count + 1))
  if [[ "$entry" != lib/net8.0/* ]]; then
    echo "::error::$nupkg — '$entry' is not under lib/net8.0/. Provider assemblies must target net8.0 (the v1 provider contract target; the engine's providers install installs only net8.0-compatible assets; the hub requires exactly lib/net8.0)."
    failed=1
  fi
done <<<"$zip_listing"
if [[ "$lib_count" -eq 0 ]]; then
  echo "::error::$nupkg — contains no lib/ entry, so it carries no provider assembly."
  failed=1
fi

# The nuspec is the one *.nuspec at the package root.
nuspec_path="$(grep -E '^[^/]+\.nuspec$' <<<"$zip_listing" | head -n1 || true)"
if [[ -z "$nuspec_path" ]]; then
  echo "::error::$nupkg — contains no root-level .nuspec."
  exit 1
fi
if ! nuspec_content="$(unzip -p "$nupkg" "$nuspec_path")"; then
  echo "::error::$nupkg — could not read '$nuspec_path'."
  exit 1
fi
# Flatten newlines so an attribute split across lines is still seen, then take every
# targetFramework attribute of a <group ...> tag, single- or double-quoted. grep exits 1 for
# "no match" (a nuspec with no groups is fine) and 2 or more for a real error, which fails.
flat_nuspec="$(tr '\n\r' '  ' <<<"$nuspec_content")"
group_status=0
group_attrs="$(grep -oE "<group[^>]*targetFramework[[:space:]]*=[[:space:]]*(\"[^\"]*\"|'[^']*')" <<<"$flat_nuspec")" || group_status=$?
if [[ "$group_status" -ge 2 ]]; then
  echo "::error::$nupkg — could not scan the nuspec for target framework groups (grep exit $group_status)."
  exit 1
fi
group_count=0
while IFS= read -r group_attr; do
  [[ -n "$group_attr" ]] || continue
  group_count=$((group_count + 1))
  group_tfm="${group_attr#*targetFramework}"
  group_tfm="${group_tfm#*[\"\']}"
  group_tfm="${group_tfm%[\"\']*}"
  if [[ "$group_tfm" != "net8.0" ]]; then
    echo "::error::$nupkg — nuspec <group targetFramework=\"$group_tfm\"> is not net8.0."
    failed=1
  fi
done <<<"$group_attrs"

echo "$nupkg: $lib_count lib/ entr(ies), $group_count nuspec group(s) checked"
exit "$failed"
