#!/usr/bin/env bash
# .claude/hooks/session-start.sh: SessionStart bootstrap for Claude Code on the web.
#
# Registered in .claude/settings.json. It runs only in a Claude Code on the web
# container ($CLAUDE_CODE_REMOTE=true); a local machine keeps whatever SDK its owner
# installed and is never touched.
#
# What it guarantees, idempotently:
#   1. A .NET 10 SDK that satisfies global.json (10.0.100, rollForward latestFeature).
#      The web sandbox's egress proxy blocks builds.dotnet.microsoft.com (where
#      dotnet-install.sh downloads from), and Microsoft's Ubuntu 22.04 (jammy) apt feed,
#      which this hook used for .NET 8, publishes no dotnet-sdk-10.0. Ubuntu 24.04's own
#      archive (noble-updates/noble-security) carries dotnet-sdk-10.0 in the 10.0.1xx
#      band, which satisfies global.json, so that is the source used. It installs under
#      /usr/lib/dotnet.
#      A container in which the .NET 8 version of this hook ran carries Microsoft-feed
#      .NET packages (SDK, runtimes, dotnet-host, hostfxr, targeting packs), the
#      microsoft-prod apt list and its keyring, and the hook-specific apt preference
#      /etc/apt/preferences.d/dotnet-microsoft. Left in place they make a mixed-vendor
#      install: Ubuntu's dotnet-host-10.0 conflicts with Microsoft's dotnet-host, so apt
#      removes the muxer and cross-grades hostfxr, stranding the old SDK and runtimes
#      in /usr/share/dotnet with no host. So, whenever it runs as root, the hook checks
#      for that earlier hook's footprint: the dotnet-microsoft preference (only the .NET 8
#      version of this hook writes it) or a non-Ubuntu dotnet-host (the package that
#      conflicts). The microsoft-prod list and keyring are NOT evidence on their own,
#      because Microsoft's own setup writes the same two file names. When the
#      preference was present, the hook deletes it together with the list and keyring,
#      then purges every installed dotnet*/aspnetcore*/netstandard* package whose
#      version is not an Ubuntu build. The purge also runs when only a non-Ubuntu
#      dotnet-host is found. Otherwise the list and keyring are left alone, and non-Ubuntu
#      .NET packages, which someone may have installed on purpose, are not purged (installing the
#      SDK can still move any of its own dependencies to the Ubuntu build). The purge
#      is simulated first: if the simulation fails, or prints any action other than a
#      purge of exactly those packages, the hook stops and purges nothing. A fresh
#      container skips that step. Also whenever it runs as root, it (re)writes an apt
#      preference that pins those packages to Ubuntu's <codename>-updates and
#      <codename>-security suites only, replacing any wider pin an earlier version wrote;
#      a missing or malformed VERSION_CODENAME stops the hook before the pin is written.
#      The install runs with --no-remove, so it fails rather than removing any package.
#   2. /usr/bin/dotnet, and DOTNET_ROOT/PATH in /etc/profile.d/dotnet.sh and in
#      $CLAUDE_ENV_FILE, so login shells and the session's own tool calls agree.
#
#   3. The published `vouchfx` global tool at the version Directory.Build.props pins as
#      VouchfxSdkVersion (the engine release this hub builds its providers against), for
#      `vouchfx list`/`validate` against the same engine. It is installed only into an
#      EMPTY slot and never replaces a different installed version: when several fleet
#      repos share one session, vouchfx-mcp's hook owns the global tool, because its
#      parity tests gate on an exact version and would otherwise silently skip.
#      It owns it by replacing any other version it finds, so the order the hooks
#      run in does not matter: measured both ways, the slot ends at vouchfx-mcp's
#      pin whether this hook ran first (vouchfx-mcp's then replaced what it installed)
#      or second (it left vouchfx-mcp's install alone).
#      The provider assemblies still target net8.0 (the provider contract target); that
#      is a build target, not a runtime requirement of this machine.
#
# Synchronous by design: the session starts only once the SDK is present, so nothing
# races a half-installed toolchain. On a container that already has the SDK (the web
# environment caches container state after this hook completes) it finishes in well
# under a second. Progress goes to stderr; the single summary line on stdout is what
# the session sees, so it also reports how many packages a purge removed.
#
# Trust model. This file comes from the checked-out branch, and the SessionStart hook
# runs it at session start (as root in a web container) before anyone has read the
# diff. So .claude/ is treated like .github/workflows/: it is code-owned in
# .github/CODEOWNERS, and web sessions should be opened only on branches you trust.
# global.json belongs beside .claude/ in that trust model: the .NET 10 host honours
# "sdk.paths" there and would load an SDK from a repository-relative path, i.e. run
# branch content as root. So, before the first dotnet call that has the repository as
# its working directory, the hook refuses to continue unless global.json holds nothing
# but an "sdk" object with the keys "version", "rollForward" and "allowPrerelease"
# (global_json_safe, below; it fails closed on anything it cannot classify). Every other
# dotnet call (tool list/install, --version) runs from a scratch directory, not the repo.
# The alternative is to move this bootstrap into the Claude Code environment's own
# setup script, which no branch controls, and delete this file and its registration:
# remove the "hooks" block from .claude/settings.json (it holds nothing else today).
set -euo pipefail

if [ "${CLAUDE_CODE_REMOTE:-}" != "true" ]; then
  exit 0
fi

DOTNET_DIR=/usr/lib/dotnet
DOTNET_PREFS=/etc/apt/preferences.d/dotnet-ubuntu
# Written by the .NET 8 version of this hook: Microsoft's jammy feed, its signing key, and
# the preference that pinned dotnet packages to it. Only the preference is specific to this
# hook; the list and keyring use the names Microsoft's own setup writes too.
LEGACY_MS_LIST=/etc/apt/sources.list.d/microsoft-prod.list
LEGACY_MS_KEYRING=/usr/share/keyrings/microsoft-prod.gpg
LEGACY_MS_PREFS=/etc/apt/preferences.d/dotnet-microsoft
# Set by remove_microsoft_feed to the number of packages it purged, for the summary line.
PURGED_COUNT=0

log() { printf '[session-start] %s\n' "$*" >&2; }
REPO_DIR="${CLAUDE_PROJECT_DIR:-$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)}"

VOUCHFX_TOOL="${DOTNET_CLI_HOME:-$HOME}/.dotnet/tools/vouchfx"

# A NuGet config that holds nuget.org and nothing else, for the one install below.
# `dotnet tool install` has no --source on this SDK, and --add-source only ADDS nuget.org
# beside every feed already configured (machine, user, and any nuget.config above the
# working directory), any of which could serve a same-version `vouchfx`. Measured on the
# .NET 8 SDK: a fake 1.0.0-rc.5 packed into a folder feed that a local nuget.config named was
# installed instead of nuget.org's under --add-source, and nuget.org's was installed under
# --configfile with this file. The version check after the install proves only the commit
# a package claims, so it cannot stand in for choosing the source.
nuget_org_only_config() {
  local dir
  dir="$(mktemp -d)" || return 1
  printf '%s\n' '<?xml version="1.0" encoding="utf-8"?>' '<configuration>' '  <packageSources>' \
    '    <clear />' '    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />' \
    '  </packageSources>' '</configuration>' >"$dir/nuget.config"
  printf '%s' "$dir"
}

# Installs the vouchfx global tool at exactly $1 (a NuGet version, no leading "v"), but
# only into an EMPTY slot. When any vouchfx is already registered it is left untouched,
# whatever its version and even when its shim cannot answer --version: that slot belongs
# to whichever repo installed it (see the header), and removing it here could take away
# the exact version another repo's tests gate on. The package comes from nuget.org alone
# (nuget_org_only_config, above).
# dotnet is called by absolute path: a non-root run never links /usr/bin/dotnet, and
# the PATH this hook writes only reaches later processes, not this one.
install_cli() {
  local want="$1" listing have
  # A listing that fails says nothing about the slot, and it cannot be treated as empty:
  # `dotnet tool install` over a registered tool UPDATES it (measured on the .NET 8 SDK,
  # 1.0.0-rc.4 to 1.0.0-rc.5, exit 0), so installing blind could replace the version
  # another repo installed. Decline instead, and say so.
  if ! listing="$("$DOTNET_DIR/dotnet" tool list -g 2>/dev/null)"; then
    log "WARNING: could not list the global tools, so vouchfx ${want} was not installed (this hook never installs into a slot it cannot inspect)."
    return 0
  fi
  have="$(awk 'tolower($1)=="vouchfx" {print $2}' <<<"$listing")"
  if [ -n "$have" ]; then
    log "vouchfx ${have} is already registered; left as is (this hook never replaces it)."
    return 0
  fi
  log "Installing vouchfx ${want}."
  local cfg status=0
  cfg="$(nuget_org_only_config)"
  "$DOTNET_DIR/dotnet" tool install -g vouchfx --version "$want" --allow-roll-forward --configfile "$cfg/nuget.config" >/dev/null || status=$?
  rm -rf "$cfg"
  return "$status"
}

# The installed CLI's informational version ("<version>+<commit-sha>"), or empty. The
# published CLI at the pin targets net8.0 and this machine has only the .NET 10 runtime.
# install_cli passes --allow-roll-forward, which writes rollForward: Major into the tool's
# runtimeconfig, so a tool this hook installs answers a plain probe, and so does every
# later use in the session. cli_version is that plain probe; nothing is exported.
cli_version() { "$VOUCHFX_TOOL" --version 2>/dev/null | head -n1 | tr -d '\r' || true; }
# The same probe with roll-forward granted for this one call. It succeeds for a slot
# another repository registered without --allow-roll-forward; the caller then reports
# that the CLI runs only with DOTNET_ROLL_FORWARD=Major, because later plain uses fail.
cli_version_rolled() { DOTNET_ROLL_FORWARD=Major "$VOUCHFX_TOOL" --version 2>/dev/null | head -n1 | tr -d '\r' || true; }

# The system-wide steps (apt, /usr/bin/dotnet, /etc/profile.d) run only as root, which
# is what a Claude Code on the web container is. This hook NEVER escalates: there is no
# sudo, because a checked-in hook that elevated itself would hand any branch a privileged
# path. Not root and no SDK is a loud refusal; not root with an SDK present skips only
# the system-wide writes.
is_root() { [ "$(id -u)" -eq 0 ]; }

# True when $1 (a global.json path) is absent, or consists of nothing but an "sdk" object
# whose keys are among "version", "rollForward" and "allowPrerelease" with plain values.
# Anything else, "paths" and "errorMessage" included, and anything this cannot classify
# (comments, a BOM, other top-level keys, malformed JSON, an unreadable file), is refused.
# Needs only bash and tr: python3 and jq are not guaranteed in a web container.
global_json_safe() {
  local file="$1" flat kv re
  [ -e "$file" ] || return 0
  flat="$(tr -d ' \t\r\n' <"$file")" || return 1
  kv='("version":"[0-9A-Za-z.+-]+"|"rollForward":"[A-Za-z]+"|"allowPrerelease":(true|false))'
  re="^\\{\"sdk\":\\{${kv}(,${kv})*\\}\\}\$"
  [[ "$flat" =~ $re ]]
}

# True when an SDK under $DOTNET_DIR resolves this repository's global.json. The dotnet
# host applies the pinned version and its rollForward policy itself, and exits non-zero
# (145) when no installed SDK satisfies them, so this check cannot drift from global.json
# the way a hard-coded version band could.
sdk_ok() { (cd "$REPO_DIR" && "$DOTNET_DIR/dotnet" --version) >/dev/null 2>&1; }

# Installed .NET packages that are not Ubuntu archive builds, one name per line. Every
# Ubuntu build of these source packages carries "ubuntu" in its version
# (10.0.115-0ubuntu1~24.04.1); Microsoft's feed versions do not (8.0.427-1, 9.0.4-1).
foreign_dotnet_packages() {
  dpkg-query -W -f='${Status}\t${Package}\t${Version}\n' 'dotnet*' 'aspnetcore*' 'netstandard*' 2>/dev/null |
    awk -F'\t' '$1 ~ / installed$/ && $3 !~ /ubuntu/ { print $2 }' || true
}

# True when the first argument, without any :arch suffix, names one of the remaining
# arguments, also compared without their :arch suffix. Exact string comparison only.
in_package_set() {
  local want="${1%%:*}" pkg
  shift
  for pkg in "$@"; do
    [ "${pkg%%:*}" = "$want" ] && return 0
  done
  return 1
}

remove_microsoft_feed() {
  local -a foreign refused
  local file sim line verb name legacy_footprint=false
  # The hook-specific dotnet-microsoft preference is the evidence that the .NET 8 version
  # of this hook ran here; the list and keyring alone are not (Microsoft's own setup
  # writes the same names). Only when it is present are the list, keyring and preference
  # removed, preference last, so no apt operation below can see the Microsoft feed or
  # its priority-1001 pin. Its presence is recorded before it is deleted: it gates the
  # purge below.
  if [ -e "$LEGACY_MS_PREFS" ]; then
    legacy_footprint=true
    for file in "$LEGACY_MS_LIST" "$LEGACY_MS_KEYRING" "$LEGACY_MS_PREFS"; do
      if [ -e "$file" ]; then
        log "Removing ${file}."
        rm -f "$file"
      fi
    done
  fi

  mapfile -t foreign < <(foreign_dotnet_packages)
  if [ "${#foreign[@]}" -eq 0 ]; then
    return 0
  fi
  # Purge only when the earlier hook's footprint is here: its dotnet-microsoft preference
  # was present at the start of this run, or a non-Ubuntu dotnet-host is installed (the package
  # that conflicts with Ubuntu's dotnet-host-10.0). Without either, these packages came
  # from somewhere this hook did not touch, and they are left in place.
  if [ "$legacy_footprint" != true ] && ! in_package_set dotnet-host "${foreign[@]}"; then
    log "Leaving ${#foreign[@]} non-Ubuntu .NET package(s) in place: the dotnet-microsoft preference was absent and no non-Ubuntu dotnet-host is installed."
    return 0
  fi
  # Purge exactly these, and nothing else. The simulation runs first; if it fails the
  # hook stops and purges nothing. An action line is one that starts with Inst, Remv,
  # Purg or Conf followed by a space or the end of the line. Every action line must be
  # a Purg of a package in the set; any other action, or a Purg of any other package,
  # refuses the purge rather than carrying it out. Lines that do not start with one of
  # those four verbs (headings, "Need to get ...") are not actions.
  sim="$(apt-get purge -s "${foreign[@]}")" || {
    log "ERROR: the simulated purge of ${foreign[*]} failed; nothing was purged."
    exit 1
  }
  refused=()
  while IFS= read -r line; do
    [[ "$line" =~ ^(Inst|Remv|Purg|Conf)( |$) ]] || continue
    read -r verb name _ <<<"$line"
    if [ "$verb" != "Purg" ] || [ -z "${name:-}" ] || ! in_package_set "$name" "${foreign[@]}"; then
      refused+=("$line")
    fi
  done <<<"$sim"
  if [ "${#refused[@]}" -gt 0 ]; then
    log "ERROR: purging the Microsoft-feed .NET packages (${foreign[*]}) would also do: ${refused[*]}. Nothing was purged; remove them yourself."
    exit 1
  fi
  log "Purging Microsoft-feed .NET packages: ${foreign[*]}."
  apt-get purge -y -qq "${foreign[@]}" >/dev/null
  PURGED_COUNT="${#foreign[@]}"
}

# The Ubuntu codename from /etc/os-release; stops the hook when there is none, or when
# it is anything but one plain lower-case word (it is written into the apt preference).
# Callers run it in a command substitution, so its exit only ends that subshell: each
# caller must stop on its failure explicitly.
os_codename() {
  local codename
  codename="$(awk -F= '$1 == "VERSION_CODENAME" { gsub(/"/, "", $2); print $2 }' /etc/os-release 2>/dev/null || true)"
  if [ -z "$codename" ]; then
    log "ERROR: /etc/os-release names no VERSION_CODENAME, so the Ubuntu suites to pin are unknown."
    exit 1
  fi
  if ! [[ "$codename" =~ ^[a-z][a-z0-9]*$ ]]; then
    log "ERROR: VERSION_CODENAME in /etc/os-release is not a single lower-case word, so it is not written into the apt preference."
    exit 1
  fi
  printf '%s\n' "$codename"
}

# Pins dotnet*, aspnetcore* and netstandard* to Ubuntu's <codename>-updates and
# <codename>-security suites. Written whenever the hook runs as root, not only before an
# install, so a container where an earlier version of this hook wrote a wider pin gets
# this one. Rewriting it is idempotent.
write_dotnet_pin() {
  local codename
  codename="$(os_codename)" || exit 1

  # Above 1000 a pin also permits downgrades, so it names only the two suites that publish
  # dotnet-sdk-10.0, not every Ubuntu suite (backports and proposed stay at their defaults).
  printf 'Package: dotnet* aspnetcore* netstandard*\nPin: release o=Ubuntu,a=%s\nPin-Priority: 1001\n\n' \
    "${codename}-updates" "${codename}-security" >"$DOTNET_PREFS"
}

# Reached only as root, after the main block below has written the pin, so it does not
# write the pin again.
install_sdk() {
  export DEBIAN_FRONTEND=noninteractive
  local codename
  codename="$(os_codename)" || exit 1

  # dotnet-sdk-10.0 is published in <codename>-updates and -security, so the image's own
  # Ubuntu lists need a refresh; the package archive is reachable through the proxy.
  log "Installing dotnet-sdk-10.0 from Ubuntu's ${codename}-updates/${codename}-security archive."
  apt-get update -qq
  apt-get install -y -qq --no-remove dotnet-sdk-10.0 >/dev/null
}

write_profile() {
  local profile=/etc/profile.d/dotnet.sh want marker body
  # The marker is the block's first line and names the dotnet root, so a block an earlier
  # version wrote for another root does not count as present and a new one is appended.
  marker="# Written by .claude/hooks/session-start.sh (Claude Code on the web); DOTNET_ROOT=${DOTNET_DIR}."
  body='export DOTNET_ROOT=/usr/lib/dotnet
case ":$PATH:" in *":/usr/lib/dotnet:"*) ;; *) PATH="/usr/lib/dotnet:$PATH" ;; esac
case ":$PATH:" in *":${DOTNET_CLI_HOME:-$HOME}/.dotnet/tools:"*) ;; *) PATH="$PATH:${DOTNET_CLI_HOME:-$HOME}/.dotnet/tools" ;; esac
export PATH
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1'
  want="${marker}"$'\n'"${body}"
  if is_root && [ "$(cat "$profile" 2>/dev/null)" != "$want" ]; then
    printf '%s\n' "$want" >"$profile"
  fi
  # Once per file: a session can fire SessionStart more than once (resume, clear, compact),
  # and the marker line says this block, for this root, is already there.
  if [ -n "${CLAUDE_ENV_FILE:-}" ] && ! grep -qxF "$marker" "$CLAUDE_ENV_FILE" 2>/dev/null; then
    printf '%s\n' "$want" >>"$CLAUDE_ENV_FILE"
  fi
}

# Runs whenever root, not only before a fresh install: a container that an earlier version
# of this hook already moved to the Ubuntu SDK may still hold the stranded Microsoft-feed
# packages and the wider pin. On a clean container the purge finds nothing and changes
# nothing, and the pin is rewritten with the same content. install_sdk relies on the pin
# written here.
if is_root; then
  export DEBIAN_FRONTEND=noninteractive
  remove_microsoft_feed
  write_dotnet_pin
fi
# Before any dotnet call that has the repository as its working directory (sdk_ok is the
# first): refuse a global.json that can steer the host to code in the branch (see the
# trust model in the header).
if ! global_json_safe "$REPO_DIR/global.json"; then
  log "ERROR: ${REPO_DIR}/global.json is not a plain {\"sdk\": {version, rollForward, allowPrerelease}} object (it may carry \"paths\", \"errorMessage\" or something this hook cannot classify). The .NET host would honour it as root, so nothing was run. Review global.json."
  exit 1
fi
if ! sdk_ok; then
  if ! is_root; then
    log "ERROR: no SDK under ${DOTNET_DIR} satisfies global.json, and installing one needs root. This hook never escalates; install the SDK yourself."
    exit 1
  fi
  install_sdk
  sdk_ok || { log "ERROR: dotnet-sdk-10.0 installed, but no SDK under ${DOTNET_DIR} satisfies global.json."; exit 1; }
fi
if is_root; then
  [ "$(readlink -f /usr/bin/dotnet 2>/dev/null)" = "$DOTNET_DIR/dotnet" ] || ln -sf "$DOTNET_DIR/dotnet" /usr/bin/dotnet
else
  log "Not root: /usr/bin/dotnet and /etc/profile.d/dotnet.sh are left as they are; the session environment is still written."
fi
write_profile

export DOTNET_ROOT="$DOTNET_DIR" DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
# Every dotnet call from here on (tool list/install, the CLI probe, --version) runs from a
# scratch directory, so no file in the branch's working tree can steer the host.
WORK_DIR="$(mktemp -d)"
trap 'rm -rf "$WORK_DIR"' EXIT
cd "$WORK_DIR"
purge_note=""
if [ "$PURGED_COUNT" -gt 0 ]; then
  purge_note="; purged ${PURGED_COUNT} Microsoft-feed .NET package(s)"
fi
# ---- the engine CLI, at the SDK pin ----
summary_cli="no usable vouchfx (see stderr)"
pin_ver="$(sed -n 's:.*<VouchfxSdkVersion>\([^<]*\)</VouchfxSdkVersion>.*:\1:p' "$REPO_DIR/Directory.Build.props" | head -n1)"
if [[ "$pin_ver" =~ ^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.]+)?$ ]]; then
  roll_note=""
  actual="$(cli_version)"
  if [ -z "$actual" ]; then
    # A plain probe failed. A slot another repository registered without
    # --allow-roll-forward answers only with roll-forward granted; say so, since every
    # later plain use of vouchfx in this session fails (this hook never replaces it).
    actual="$(cli_version_rolled)"
    if [ -n "$actual" ]; then
      roll_note=" (runs only with DOTNET_ROLL_FORWARD=Major)"
    else
      install_cli "$pin_ver" || log "WARNING: could not install vouchfx ${pin_ver}."
      actual="$(cli_version)"
    fi
  fi
  case "$actual" in
    "${pin_ver}+"*) summary_cli="vouchfx ${actual} matches VouchfxSdkVersion${roll_note}" ;;
    "") ;;
    *) summary_cli="vouchfx ${actual} is installed; VouchfxSdkVersion is ${pin_ver} (left as is, see this hook's header)${roll_note}" ;;
  esac
else
  log "WARNING: no usable <VouchfxSdkVersion> in Directory.Build.props; skipping the CLI install."
fi
echo "session-start: .NET SDK $("$DOTNET_DIR/dotnet" --version) ready (global.json floor 10.0.100)${purge_note}; ${summary_cli}."
