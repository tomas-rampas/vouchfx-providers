#!/usr/bin/env python3
"""Build the vouchfx-providers GitHub Pages site.

Copies the static landing page (site/) into the output directory, then renders
the repository's markdown — the provider-implementation guide, the contributor
documents and the worked-example README — into styled HTML that matches the
engine project site. The markdown files remain the single source of truth;
this generates their HTML on every run, so a CI deploy keeps the published
pages current with every push.

The rendering machinery is shared with the other three vouchfx sites — see
https://github.com/tomas-rampas/vouchfx/tree/main/scripts/site-tools (the
vouchfx-site-tools package, vouchfx issue #200). This file only carries what
is specific to this repository's own site: the doc set, the page/portal HTML,
and the local community_provider_count fact source.

    python scripts/build_site.py [output_dir]   # default: _site

Requires: markdown, pygments, vouchfx-site-tools
"""
from __future__ import annotations

import json
import os
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
OUT = Path(sys.argv[1]).resolve() if len(sys.argv) > 1 else ROOT / "_site"


def _bootstrap_site_tools() -> None:
    """Resolve vouchfx_site_tools in four steps: (1) an already-installed
    package — this is what CI's pip install satisfies; (2) VOUCHFX_SITE_TOOLS,
    if set, pointing at a scripts/site-tools/src checkout; (3) the maintainer's
    usual local layout, all four repos checked out side by side. Each step is
    tried independently so a wrong VOUCHFX_SITE_TOOLS still falls through to
    the sibling checkout instead of failing outright."""
    try:
        import vouchfx_site_tools  # noqa: F401

        return
    except ImportError:
        pass

    env_path = os.environ.get("VOUCHFX_SITE_TOOLS")
    if env_path:
        sys.path.insert(0, env_path)
        try:
            import vouchfx_site_tools  # noqa: F401

            return
        except ImportError:
            sys.path.pop(0)

    sibling = (ROOT / ".." / "vouchfx" / "scripts" / "site-tools" / "src").resolve()
    sys.path.insert(0, str(sibling))
    try:
        import vouchfx_site_tools  # noqa: F401

        return
    except ImportError:
        sys.path.pop(0)

    raise SystemExit(
        "vouchfx-site-tools is not installed and no local checkout was found.\n"
        "Install it with:\n"
        '  pip install "vouchfx-site-tools @ git+https://github.com/tomas-rampas/vouchfx.git@<sha>'
        '#subdirectory=scripts/site-tools"\n'
        "(substitute <sha> for the pinned commit in .github/workflows/pages.yml), "
        "or set VOUCHFX_SITE_TOOLS to a local scripts/site-tools/src checkout, "
        "or check out vouchfx as a sibling of this repository."
    )


_bootstrap_site_tools()

from vouchfx_site_tools import SiteConfig, build  # noqa: E402

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

PAGE = """<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8" />
<meta name="viewport" content="width=device-width, initial-scale=1.0" />
<title>{title} · vouchfx providers</title>
<meta name="description" content="{desc}" />
<meta name="theme-color" content="#0b0f1a" />
<link rel="canonical" href="{canonical}" />
<meta property="og:type" content="article" />
<meta property="og:site_name" content="vouchfx providers" />
<meta property="og:title" content="{title}" />
<meta property="og:description" content="{desc}" />
<meta property="og:url" content="{canonical}" />
<meta name="twitter:card" content="summary" />
<meta name="twitter:title" content="{title}" />
<meta name="twitter:description" content="{desc}" />
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
      <a href="https://vouchfx.io/">Engine docs</a>
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

PORTAL = """<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8" />
<meta name="viewport" content="width=device-width, initial-scale=1.0" />
<title>Documentation · vouchfx providers</title>
<meta name="description" content="vouchfx community provider hub documentation — step providers (plugins) extending the end-to-end integration testing framework." />
<meta name="theme-color" content="#0b0f1a" />
<link rel="canonical" href="https://providers.vouchfx.io/docs.html" />
<meta property="og:type" content="article" />
<meta property="og:site_name" content="vouchfx providers" />
<meta property="og:title" content="Documentation · vouchfx providers" />
<meta property="og:description" content="vouchfx community provider hub documentation — step providers (plugins) extending the end-to-end integration testing framework." />
<meta property="og:url" content="https://providers.vouchfx.io/docs.html" />
<meta name="twitter:card" content="summary" />
<meta name="twitter:title" content="Documentation · vouchfx providers" />
<meta name="twitter:description" content="vouchfx community provider hub documentation — step providers (plugins) extending the end-to-end integration testing framework." />
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
      <a href="https://vouchfx.io/">Engine docs</a>
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
      <a class="doc-card" href="https://vouchfx.io/" target="_blank" rel="noopener noreferrer"><span class="doc-card__k">ENGINE</span><h3>Engine project site</h3><p>The architecture blueprint, the YAML DSL specification, user guides, language reference, and the Core provider catalogue.</p></a>
      <a class="doc-card" href="https://samples.vouchfx.io/" target="_blank" rel="noopener noreferrer"><span class="doc-card__k">SAMPLES</span><h3>Samples site</h3><p>End-to-end test suites for sample applications (C#, Python, Java) demonstrating the engine and community providers. Includes the <code>ledger-jsonrpc</code> worked example.</p></a>
      <a class="doc-card" href="https://telemetry.vouchfx.io/" target="_blank" rel="noopener noreferrer"><span class="doc-card__k">TELEMETRY</span><h3>Telemetry backend</h3><p>The opt-in telemetry story — why, what is (and is not) collected, how to verify it locally, and how to self-host the backend.</p></a>
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
      <a href="https://vouchfx.io/" target="_blank" rel="noopener noreferrer">Engine docs</a>
      <a href="https://github.com/tomas-rampas/vouchfx-providers/blob/main/LICENSE" target="_blank" rel="noopener noreferrer">Licence (Apache-2.0)</a>
    </div>
  </div>
</footer>
</body>
</html>
"""


def _local_community_provider_count() -> str:
    """This repository IS the registry (registry/community-providers.json), so
    read it straight off disk rather than fetching it over the network — the
    freshest possible value, no propagation delay. Still best-effort: a
    malformed or missing file falls back the same as every other fact."""
    data = json.loads((ROOT / "registry" / "community-providers.json").read_text(encoding="utf-8"))
    if not isinstance(data, list):
        raise ValueError("registry JSON is no longer a top-level array")
    return str(len(data))


CONFIG = SiteConfig(
    root=ROOT,
    default_repo="tomas-rampas/vouchfx-providers",
    docs=DOCS,
    page_template=PAGE,
    portal_html=PORTAL,
    meta_description_prefix="vouchfx community provider hub — step providers (plugins) for the end-to-end integration testing framework",
    extra=EXTRA,
    skip=SKIP,
    skip_prefixes=SKIP_PREFIXES,
    site_url="https://providers.vouchfx.io/",
    fact_overrides={"community_provider_count": _local_community_provider_count},
)


def main() -> None:
    build(CONFIG, OUT)


if __name__ == "__main__":
    main()
