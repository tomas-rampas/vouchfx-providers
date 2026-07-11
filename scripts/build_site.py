#!/usr/bin/env python3
"""Build the vouchfx-providers GitHub Pages site.

Copies the static landing page (site/) into the output directory, then renders
the repository's markdown — the provider-implementation guide, the contributor
documents and the worked-example README — into styled HTML that matches the
engine project site. The markdown files remain the single source of truth;
this generates their HTML on every run, so a CI deploy keeps the published
pages current with every push.

    python scripts/build_site.py [output_dir]   # default: _site

Requires: markdown, pygments  (pip install markdown pygments)
"""
from __future__ import annotations

import html
import json
import os
import posixpath
import re
import shutil
import sys
import urllib.request
from pathlib import Path

import markdown
from markdown.extensions.codehilite import CodeHiliteExtension
from markdown.extensions.toc import TocExtension
from pygments.formatters import HtmlFormatter

ROOT = Path(__file__).resolve().parent.parent
SITE = ROOT / "site"
OUT = Path(sys.argv[1]).resolve() if len(sys.argv) > 1 else ROOT / "_site"

# Markdown files to render, in sidebar order. (source path relative to ROOT, nav group, label)
# Provider authoring journey: seven stages (1-6 are doc pages, 7 is registry/VOUCHED_CHECKLIST)
DOCS: list[tuple[str, str, str]] = [
    # Stage 1-6: authoring journey (overview + five split guides)
    ("docs/implementing-a-provider.md", "Provider authoring", "Overview & journey map"),
    ("template/Vouchfx.Community.Hello/README.md", "Provider authoring", "Stage 1: template scaffold"),
    ("docs/provider-project-setup.md", "Provider authoring", "Stage 2: project setup"),
    ("docs/provider-contract.md", "Provider authoring", "Stage 3: the contract surfaces"),
    ("docs/provider-csx-composition.md", "Provider authoring", "Stage 4: CSX composition"),
    ("docs/provider-testing.md", "Provider authoring", "Stage 5: testing"),
    ("docs/provider-publishing.md", "Provider authoring", "Stage 6: publishing"),
    ("community/Vouchfx.Community.JsonRpc/README.md", "Provider authoring", "rpc.json-rpc · reference implementation"),

    # Consuming providers
    ("docs/consuming-a-provider.md", "Consuming providers", "Using community providers"),

    # Contributing & governance
    ("CONTRIBUTING.md", "Contributing", "Contributing & the tiers"),
    ("VOUCHED_CHECKLIST.md", "Contributing", "The Vouched checklist"),
    ("registry/README.md", "Contributing", "Community registry"),

    # Project
    ("GOVERNANCE.md", "Project", "Governance"),
    ("SECURITY.md", "Project", "Security policy"),
    ("CODE_OF_CONDUCT.md", "Project", "Code of conduct"),
    ("README.md", "Project", "Repository README"),
    ("CHANGELOG.md", "Project", "Changelog"),
]

# Any additional markdown that is link-reachable but not in the sidebar.
EXTRA: list[str] = []

# Markdown that must never be published, even when present on a maintainer's
# disk. Nothing in this repository is internal today; the mechanism is kept so
# an accidental future addition fails safe the same way the engine site does.
SKIP: set[str] = set()
SKIP_PREFIXES: tuple[str, ...] = ()


def out_path(rel: str) -> Path:
    """Mirror the repo layout under OUT, with .html extension."""
    return OUT / (rel[:-3] + ".html")


def rel_root(target: Path) -> str:
    """Relative path from a generated file back to OUT root, e.g. '../'.
    Forward slashes always, so Windows and CI builds emit identical HTML."""
    rp = os.path.relpath(OUT, target.parent).replace(os.sep, "/")
    return "" if rp == "." else rp + "/"


GITHUB_URL = f"https://github.com/{os.environ.get('GITHUB_REPOSITORY', 'tomas-rampas/vouchfx-providers')}/"
ENGINE_SITE = "https://tomas-rampas.github.io/vouchfx/"
PUBLISHED: set[str] = set()


def compute_published() -> set[str]:
    rels = {rel for rel, _group, _label in DOCS} | set(EXTRA)
    for src in ROOT.glob("docs/**/*.md"):
        rel = src.relative_to(ROOT).as_posix()
        if rel not in SKIP and not rel.startswith(SKIP_PREFIXES):
            rels.add(rel)
    return rels


def rewrite_links(body: str, src_rel: str) -> str:
    """Rewrite relative links: published .md pages become .html; any other
    repo-relative target becomes an absolute GitHub URL (it has no page on the
    site). Absolute URLs, anchors and mailto links pass through untouched."""
    src_dir = posixpath.dirname(src_rel)

    def repl(m: re.Match) -> str:
        href = m.group(1)
        if re.match(r"[a-z]+://", href) or href.startswith("#") or href.startswith("mailto:"):
            return m.group(0)
        path, sep, frag = href.partition("#")
        target = posixpath.normpath(posixpath.join(src_dir, path))
        if path.endswith(".md") and target in PUBLISHED:
            return f'href="{path[:-3] + ".html"}{sep}{frag}"'
        kind = "tree" if (ROOT / target).is_dir() else "blob"
        return f'href="{GITHUB_URL}{kind}/main/{target}{sep}{frag}"'

    return re.sub(r'href="([^"]+)"', repl, body)


def extract_mermaid(text: str) -> tuple[str, list[str]]:
    """Pull ```mermaid fenced blocks out before markdown processing."""
    blocks: list[str] = []

    def grab(m: re.Match) -> str:
        blocks.append(m.group(1))
        return f"\n@@MERMAID{len(blocks) - 1}@@\n"

    text = re.sub(r"```mermaid\n(.*?)```", grab, text, flags=re.DOTALL)
    return text, blocks


def sidebar(active_rel: str, root: str) -> str:
    groups: dict[str, list[str]] = {}
    for rel, group, label in DOCS:
        href = root + rel[:-3] + ".html"
        cls = ' class="active"' if rel == active_rel else ""
        groups.setdefault(group, []).append(f'<a href="{href}"{cls}>{html.escape(label)}</a>')
    parts = [f'<a href="{root}docs.html">← All documentation</a>']
    for group, links in groups.items():
        parts.append(f"<h4>{html.escape(group)}</h4>")
        parts.extend(links)
    return "\n".join(parts)


# ---------------------------------------------------------------------------
# Fact injection — self-healing volatile facts
#
# A handful of numbers on the rendered site (the latest engine release, the
# published SDK/community-provider versions, the community registry size)
# change on a cadence this repository doesn't control. Rather than let them
# silently drift out of date in hand-written HTML, any page can carry a
# {{fact:KEY}} token that fetch_facts() resolves at build time. Each source
# is independently best-effort: a network hiccup or API shape change falls
# back to the last known-good value in site/facts-fallback.json rather than
# failing the build — a stale fact is a much smaller problem than a broken
# Pages deploy. community_provider_count is the one exception: the registry
# lives IN THIS REPOSITORY (registry/community-providers.json), so it is
# read straight off disk rather than fetched over the network — always the
# freshest possible value, no propagation delay, but still best-effort (a
# malformed or missing file falls back the same as the others).
# ---------------------------------------------------------------------------

FACT_TOKEN = re.compile(r"\{\{fact:([A-Za-z0-9_]+)\}\}")
FACTS: dict[str, str] = {}


def _fetch_json(url: str, headers: dict[str, str] | None = None):
    req = urllib.request.Request(url, headers=headers or {"User-Agent": "vouchfx-providers-build-site"})
    with urllib.request.urlopen(req, timeout=5) as resp:  # nosec B310 - fixed https URLs only
        return json.loads(resp.read().decode("utf-8"))


def fetch_facts() -> dict[str, str]:
    fallback = json.loads((SITE / "facts-fallback.json").read_text(encoding="utf-8"))
    facts = dict(fallback)
    live: list[str] = []

    try:
        gh_headers = {"User-Agent": "vouchfx-providers-build-site", "Accept": "application/vnd.github+json"}
        token = os.environ.get("GITHUB_TOKEN")
        if token:
            gh_headers["Authorization"] = f"Bearer {token}"
        releases = _fetch_json("https://api.github.com/repos/tomas-rampas/vouchfx/releases", gh_headers)
        # Deliberately keeps pre-releases (the whole alpha series IS the release
        # line today); only drafts are skipped. Do not add a prerelease filter.
        facts["engine_release"] = next(r["tag_name"] for r in releases if not r.get("draft"))
        live.append("engine_release")
    except Exception:
        pass

    try:
        data = _fetch_json("https://api.nuget.org/v3-flatcontainer/vouchfx.sdk/index.json")
        facts["sdk_version"] = data["versions"][-1]
        live.append("sdk_version")
    except Exception:
        pass

    try:
        data = _fetch_json("https://api.nuget.org/v3-flatcontainer/vouchfx.community.jsonrpc/index.json")
        facts["community_jsonrpc_version"] = data["versions"][-1]
        live.append("community_jsonrpc_version")
    except Exception:
        pass

    try:
        data = json.loads((ROOT / "registry" / "community-providers.json").read_text(encoding="utf-8"))
        if not isinstance(data, list):
            raise ValueError("registry JSON is no longer a top-level array")
        facts["community_provider_count"] = str(len(data))
        live.append("community_provider_count")
    except Exception:
        pass

    fallback_used = sorted(set(facts) - set(live))
    print(f"facts: live={sorted(live) or ['-']} fallback={fallback_used or ['-']}")
    return facts


def apply_facts(text: str) -> str:
    """Substitute {{fact:KEY}} tokens. Called on site/ HTML right after it is
    copied, and on every rendered page's HTML before it is written."""
    return FACT_TOKEN.sub(lambda m: html.escape(FACTS[m.group(1)]) if m.group(1) in FACTS else m.group(0), text)


PAGE = """<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8" />
<meta name="viewport" content="width=device-width, initial-scale=1.0" />
<title>{title} · vouchfx providers</title>
<meta name="description" content="{desc}" />
<meta name="theme-color" content="#0b0f1a" />
<link rel="icon" href="{root}favicon.svg" type="image/svg+xml" />
<link rel="stylesheet" href="{root}styles.css" />
<link rel="stylesheet" href="{root}docs.css" />
<link rel="stylesheet" href="{root}pygments.css" />
</head>
<body>
<header class="nav">
  <div class="nav__inner">
    <a class="brand" href="{root}index.html" aria-label="vouchfx providers home">
      <span class="brand__mark" aria-hidden="true"></span>
      <span class="brand__name">vouchfx providers</span>
    </a>
    <nav class="nav__links" aria-label="Primary">
      <a href="{root}index.html">Home</a>
      <a href="{root}docs.html">Docs</a>
      <a href="{root}docs/implementing-a-provider.html">The guide</a>
      <a href="https://tomas-rampas.github.io/vouchfx/">Engine docs</a>
    </nav>
    <a class="btn btn--ghost nav__gh" href="https://github.com/tomas-rampas/vouchfx-providers" target="_blank" rel="noopener noreferrer">GitHub</a>
  </div>
</header>
<div class="doc-shell">
  <aside class="doc-side">{sidebar}</aside>
  <main class="doc-main">
    <div class="doc-breadcrumb"><a href="{root}docs.html">Documentation</a> / {crumb}</div>
    <article class="prose">{body}</article>
  </main>
  <nav class="doc-toc"><h4>On this page</h4>{toc}</nav>
</div>
{mermaid_script}
</body>
</html>
"""


def render_markdown(rel: str, label: str) -> None:
    src = ROOT / rel
    text = src.read_text(encoding="utf-8")
    text, mermaid = extract_mermaid(text)

    md = markdown.Markdown(
        extensions=[
            "extra",
            "sane_lists",
            "admonition",
            TocExtension(permalink=True, permalink_class="headerlink", permalink_title="", baselevel=2),
            CodeHiliteExtension(css_class="codehilite", guess_lang=False),
        ]
    )
    body = md.convert(text)
    body = rewrite_links(body, rel)

    # Re-insert mermaid blocks as divs.
    for i, block in enumerate(mermaid):
        body = body.replace(f"<p>@@MERMAID{i}@@</p>", f'<div class="mermaid">{html.escape(block)}</div>')
        body = body.replace(f"@@MERMAID{i}@@", f'<div class="mermaid">{html.escape(block)}</div>')

    toc = getattr(md, "toc", "") or ""
    has_mermaid = bool(mermaid)
    mermaid_script = (
        '<script type="module">import mermaid from "https://cdn.jsdelivr.net/npm/mermaid@11/dist/mermaid.esm.min.mjs";'
        'mermaid.initialize({startOnLoad:true,theme:"dark"});</script>'
        if has_mermaid
        else ""
    )

    dst = out_path(rel)
    dst.parent.mkdir(parents=True, exist_ok=True)
    root = rel_root(dst)
    desc = f"vouchfx provider hub documentation — {label}"
    page = PAGE.format(
        title=html.escape(label),
        desc=html.escape(desc),
        root=root,
        sidebar=sidebar(rel, root),
        crumb=html.escape(label),
        body=body,
        toc=toc,
        mermaid_script=mermaid_script,
    )
    dst.write_text(apply_facts(page), encoding="utf-8")
    print(f"  rendered {rel} -> {dst.relative_to(OUT)}")


PORTAL = """<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8" />
<meta name="viewport" content="width=device-width, initial-scale=1.0" />
<title>Documentation · vouchfx providers</title>
<meta name="description" content="vouchfx provider hub documentation — the provider-implementation guide, the worked example, the tier rubric and the community registry." />
<meta name="theme-color" content="#0b0f1a" />
<link rel="icon" href="favicon.svg" type="image/svg+xml" />
<link rel="stylesheet" href="styles.css" />
<link rel="stylesheet" href="docs.css" />
</head>
<body>
<header class="nav">
  <div class="nav__inner">
    <a class="brand" href="index.html" aria-label="vouchfx providers home">
      <span class="brand__mark" aria-hidden="true"></span>
      <span class="brand__name">vouchfx providers</span>
    </a>
    <nav class="nav__links" aria-label="Primary">
      <a href="index.html">Home</a>
      <a href="docs/implementing-a-provider.html">The guide</a>
      <a href="https://tomas-rampas.github.io/vouchfx/">Engine docs</a>
    </nav>
    <a class="btn btn--ghost nav__gh" href="https://github.com/tomas-rampas/vouchfx-providers" target="_blank" rel="noopener noreferrer">GitHub</a>
  </div>
</header>
<div class="container portal">
  <div class="portal__head">
    <p class="eyebrow">Documentation</p>
    <h1 class="section__title">Everything you need to ship a step provider.</h1>
    <p class="section__lede">These pages are rendered straight from the repository's markdown on every push,
      so they never drift from the code they describe.</p>
  </div>

  <section class="portal__group">
    <h2>Provider authoring journey</h2>
    <p>Six stages from template to published provider, with the reference implementation woven through. Stage 7 (registry, Vouched badge) continues under Contributing.</p>
    <div class="doc-cards">
      <a class="doc-card" href="docs/implementing-a-provider.html">
        <span class="doc-card__k">START</span><h3>Overview & journey map</h3>
        <p>Entry point: what a provider is, the two tiers, what you can build self-contained, and the seven stages.</p>
      </a>
      <a class="doc-card" href="template/Vouchfx.Community.Hello/README.html">
        <span class="doc-card__k">STAGE 1</span><h3>Template scaffold</h3>
        <p>Copy the hello.console template to bootstrap your provider project.</p>
      </a>
      <a class="doc-card" href="docs/provider-project-setup.html">
        <span class="doc-card__k">STAGE 2</span><h3>Project setup</h3>
        <p>The .csproj structure, namespace hygiene, and your step model.</p>
      </a>
      <a class="doc-card" href="docs/provider-contract.html">
        <span class="doc-card__k">STAGE 3</span><h3>The contract surfaces</h3>
        <p>The four mandatory interfaces and three of the optional extension interfaces.</p>
      </a>
      <a class="doc-card" href="docs/provider-csx-composition.html">
        <span class="doc-card__k">STAGE 4</span><h3>CSX composition</h3>
        <p>Roslyn composition rules, verdicts, secrets and capture.</p>
      </a>
      <a class="doc-card" href="docs/provider-testing.html">
        <span class="doc-card__k">STAGE 5</span><h3>Testing</h3>
        <p>Conformance tests, the custom harness pattern, and Docker integration.</p>
      </a>
      <a class="doc-card" href="docs/provider-publishing.html">
        <span class="doc-card__k">STAGE 6</span><h3>Publishing</h3>
        <p>Community submission paths (external and hub-hosted) and the Vouched badge.</p>
      </a>
      <a class="doc-card" href="community/Vouchfx.Community.JsonRpc/README.html">
        <span class="doc-card__k">REFERENCE</span><h3>rpc.json-rpc</h3>
        <p>The first community provider and canonical worked example: JSON-RPC 2.0 over HTTP with the full contract exercised.</p>
      </a>
    </div>
  </section>

  <section class="portal__group">
    <h2>Consuming providers</h2>
    <p>How to add published community providers to your test application.</p>
    <div class="doc-cards">
      <a class="doc-card" href="docs/consuming-a-provider.html">
        <span class="doc-card__k">GUIDE</span><h3>Using community providers</h3>
        <p>Compile-time discovery, NuGet pinning, and source builds. The ledger-jsonrpc sample demonstrates both paths.</p>
      </a>
      <a class="doc-card" href="registry/README.html">
        <span class="doc-card__k">REGISTRY</span><h3>Community registry</h3>
        <p>Browse published providers; understand the hub's hosting options; add your own listing.</p>
      </a>
    </div>
  </section>

  <section class="portal__group">
    <h2>Contributing</h2>
    <p>The submission path, the Vouched badge, and the community registry.</p>
    <div class="doc-cards">
      <a class="doc-card" href="CONTRIBUTING.html">
        <span class="doc-card__k">HOW</span><h3>Contributing &amp; the tiers</h3>
        <p>The Community submission path, the conformance harness, and the repository conventions.</p>
      </a>
      <a class="doc-card" href="VOUCHED_CHECKLIST.html">
        <span class="doc-card__k">BADGE</span><h3>The Vouched checklist</h3>
        <p>The published rubric for the maintainer-awarded Vouched badge — recognition recorded in the registry against this criteria.</p>
      </a>
      <a class="doc-card" href="registry/README.html">
        <span class="doc-card__k">INDEX</span><h3>Community registry</h3>
        <p>The schema-validated index of community providers and how to add a listing.</p>
      </a>
    </div>
  </section>

  <section class="portal__group">
    <h2>Project</h2>
    <p>How the hub is run.</p>
    <div class="doc-cards">
      <a class="doc-card" href="GOVERNANCE.html"><span class="doc-card__k">GOV</span><h3>Governance</h3><p>How the Vouched badge is awarded and revoked, and how disputes are resolved.</p></a>
      <a class="doc-card" href="SECURITY.html"><span class="doc-card__k">SEC</span><h3>Security policy</h3><p>How to report a vulnerability in a hosted provider, the registry or the template.</p></a>
      <a class="doc-card" href="CODE_OF_CONDUCT.html"><span class="doc-card__k">CoC</span><h3>Code of conduct</h3><p>The standards this community holds itself to.</p></a>
      <a class="doc-card" href="README.html"><span class="doc-card__k">README</span><h3>Repository README</h3><p>What the hub is, the repository layout, and the local build.</p></a>
    </div>
  </section>

  <section class="portal__group">
    <h2>The ecosystem</h2>
    <p>The engine, samples repository, telemetry backend and this provider hub.</p>
    <p class="note">Live: engine {{fact:engine_release}} · <code>Vouchfx.Sdk</code> {{fact:sdk_version}} on NuGet ·
      registry lists {{fact:community_provider_count}} community provider(s), latest <code>Vouchfx.Community.JsonRpc</code>
      {{fact:community_jsonrpc_version}}.</p>
    <div class="doc-cards">
      <a class="doc-card" href="https://tomas-rampas.github.io/vouchfx/" target="_blank" rel="noopener noreferrer"><span class="doc-card__k">ENGINE</span><h3>Engine project site</h3><p>The architecture blueprint, the YAML DSL specification, user guides, language reference, and the Core provider catalogue.</p></a>
      <a class="doc-card" href="https://tomas-rampas.github.io/vouchfx-samples/" target="_blank" rel="noopener noreferrer"><span class="doc-card__k">SAMPLES</span><h3>Samples site</h3><p>End-to-end test suites for sample applications (C#, Python, Java) demonstrating the engine and community providers. Includes the <code>ledger-jsonrpc</code> worked example.</p></a>
      <a class="doc-card" href="https://tomas-rampas.github.io/vouchfx-telemetry-backend/" target="_blank" rel="noopener noreferrer"><span class="doc-card__k">TELEMETRY</span><h3>Telemetry backend</h3><p>The opt-in telemetry story — why, what is (and is not) collected, how to verify it locally, and how to self-host the backend.</p></a>
      <a class="doc-card" href="https://github.com/tomas-rampas/vouchfx" target="_blank" rel="noopener noreferrer"><span class="doc-card__k">REPO</span><h3>Engine repository</h3><p>The engine, the Provider SDK sources, and the in-repo Example.Steps.Echo / Hello templates.</p></a>
    </div>
  </section>
</div>

<footer class="footer">
  <div class="container footer__inner">
    <div class="footer__brand">
      <span class="brand__mark" aria-hidden="true"></span>
      <div><strong>vouchfx providers</strong><p>The community hub for vouchfx step providers — Core and Community tiers with optional Vouched badge, all Apache-2.0.</p></div>
    </div>
    <div class="footer__links">
      <a href="index.html">Home</a>
      <a href="https://github.com/tomas-rampas/vouchfx-providers" target="_blank" rel="noopener noreferrer">Repository</a>
      <a href="https://tomas-rampas.github.io/vouchfx/" target="_blank" rel="noopener noreferrer">Engine docs</a>
      <a href="https://github.com/tomas-rampas/vouchfx-providers/blob/main/LICENSE" target="_blank" rel="noopener noreferrer">Licence (Apache-2.0)</a>
    </div>
  </div>
</footer>
</body>
</html>
"""


def build_portal() -> None:
    (OUT / "docs.html").write_text(apply_facts(PORTAL), encoding="utf-8")
    print("  built docs.html portal")


def derive_label(src: Path) -> str:
    """Best-effort page label from the first heading, else the file stem."""
    for line in src.read_text(encoding="utf-8").splitlines():
        if line.startswith("# "):
            return line[2:].strip()
    return src.stem


def main() -> None:
    # Safety: only ever build into a subdirectory of the repo, never ROOT or an
    # outside path — main() removes OUT with rmtree before rebuilding.
    if OUT == ROOT or ROOT not in OUT.parents:
        raise SystemExit(f"refusing to build into {OUT}: must be a subdirectory of {ROOT}")
    if OUT.exists():
        shutil.rmtree(OUT)
    shutil.copytree(SITE, OUT)
    print(f"copied {SITE.relative_to(ROOT)}/ -> {OUT.name}/")

    # Resolve facts, then substitute {{fact:KEY}} tokens into whatever HTML
    # site/ just copied verbatim (index.html). site/facts-fallback.json itself
    # is build tooling, not a page — it ships inside site/ so it copies above,
    # but has no business being served, so remove the copy once read.
    global FACTS
    FACTS = fetch_facts()
    for html_file in OUT.glob("*.html"):
        html_file.write_text(apply_facts(html_file.read_text(encoding="utf-8")), encoding="utf-8")
    (OUT / "facts-fallback.json").unlink(missing_ok=True)

    # Pygments stylesheet (dark) for fenced code blocks.
    (OUT / "pygments.css").write_text(
        HtmlFormatter(style="monokai").get_style_defs(".codehilite") + "\n.codehilite{background:transparent}",
        encoding="utf-8",
    )

    PUBLISHED.update(compute_published())

    rendered: set[str] = set()
    for rel, _group, label in DOCS:
        render_markdown(rel, label)
        rendered.add(rel)
    for rel in EXTRA:
        if (ROOT / rel).exists():
            render_markdown(rel, derive_label(ROOT / rel))
            rendered.add(rel)

    # Auto-render any markdown under docs/ not explicitly listed, so a newly
    # added file is published (linkable) rather than silently omitted.
    for src in sorted(ROOT.glob("docs/**/*.md")):
        rel = src.relative_to(ROOT).as_posix()
        if rel in rendered or rel in SKIP or rel.startswith(SKIP_PREFIXES):
            continue
        print(f"  (auto) {rel} not in DOCS — rendering with derived label")
        render_markdown(rel, derive_label(src))
        rendered.add(rel)

    build_portal()
    print(f"done -> {OUT}")


if __name__ == "__main__":
    main()
