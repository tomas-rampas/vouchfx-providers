#!/usr/bin/env bash
# scripts/check-nupkg-target.sh <nupkg>
#
# Artefact-level check that a packed provider targets net8.0 and nothing else. The
# provider assembly target is the frozen v1 provider contract (the engine's
# `vouchfx provider install` accepts only net8.0 assets), so this reads the finished
# package rather than trusting any MSBuild evaluation that produced it:
#   - every `lib/` entry in the archive is under `lib/net8.0/`, and at least one exists;
#   - every `<group targetFramework="...">` in the nuspec is `net8.0`.
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
    echo "::error::$nupkg — '$entry' is not under lib/net8.0/. Provider assemblies must target net8.0 (the v1 provider contract target; the engine's provider install accepts only net8.0 assets)."
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
nuspec_content="$(unzip -p "$nupkg" "$nuspec_path")"
group_count=0
while IFS= read -r group_tfm; do
  [[ -n "$group_tfm" ]] || continue
  group_count=$((group_count + 1))
  if [[ "$group_tfm" != "net8.0" ]]; then
    echo "::error::$nupkg — nuspec <group targetFramework=\"$group_tfm\"> is not net8.0."
    failed=1
  fi
done < <(grep -oP '<group\b[^>]*\btargetFramework="\K[^"]*' <<<"$nuspec_content" || true)

echo "$nupkg: $lib_count lib/ entr(ies), $group_count nuspec group(s) checked"
exit "$failed"
