# Live Model — Tracing real MEP connectivity

> Part of the live-model knowledge set. Index: [`README.md`](README.md) — go back there to route to another topic.

## Tracing real MEP connectivity (when tags/naming can't be trusted)
Sometimes two pieces of equipment are supposed to be connected by pipe/duct, but **Revit's own connector
graph doesn't show it** — every `Connector.IsConnected` along the run is `false`, even though the pipes
are properly modeled and physically touching end to end. In that case a connector-graph walk
(`Connector.AllRefs`) finds nothing, and the only way to find the real destination is a **geometric**
trace — matching connector *positions* directly, ignoring the `IsConnected` flag entirely.

**This is a general method, parameterized by pipe/system type — never hardcode it to one system.**
Ajmal will name a different pipe type almost every time he asks ("refrigerant", "CDP", "water supply", —
it changes per request, so the system-name filter is always an input, not a fixed constant). Check
`glossary.md` for the mapping from Ajmal's word to the actual Revit system-type name(s) to filter on (e.g.
"refrigerant" → any system name/type containing `DXS`, which covers `DXS-SL`, `DXS-LL`, `DXS-C`, `DXS-S`
variants — don't assume it's only the Suction+Liquid pair, the real project has more DXS sub-types than
that).

**Preferred approach — bulk clustering, not one path at a time (Ajmal-confirmed method, 2026-07-08):**
rather than tracing a single named starting unit outward, process the *whole* filtered pipe/fitting set at
once and let the circuits fall out of the geometry themselves:

1. Collect every pipe + fitting element whose system name/type matches the requested filter.
2. Group them into physical circuits in bulk:
   - **Fast path** — check whether Revit's own `MEPSystem.Name`/`Id` grouping is already a real physical
     grouping: sample a few elements in the same system and confirm `Connector.IsConnected` is `true`
     between them (only the very last hop out to equipment tends to be `false`). If so, each distinct
     system name+number already *is* one physical circuit — no manual walk needed, just group by it.
   - **Fallback** — if `IsConnected` isn't reliably `true` within a system, build the groups yourself:
     for every connector, find any other element's connector whose position matches within a small
     tolerance (~50mm worked well) and merge the two elements into the same set; keep merging until no
     more matches are found. Each finished set is one physical circuit.
3. For each circuit, find its open/terminal connectors — the ones with `IsConnected = false` (or, in the
   fallback, wherever no matching neighbor was found).
4. For each open end, identify what's actually there: match against known equipment connector positions
   (for equipment with a `ConnectorManager`) or, if that equipment has no connectors at all (common for
   outdoor/condensing units in this project — check first, don't assume), match to the nearest equipment
   by bounding-box distance instead.
5. Now every circuit has its two real endpoints identified. Color each circuit with its own distinct color
   (see color-coding below) so the grouping is visible at a glance.

**Why bulk clustering over one-path-at-a-time:** Ajmal doesn't have to name a specific starting unit — he
just names the pipe/system type he cares about, and every circuit of that type gets traced and reported in
one pass. Faster when there are many circuits, and matches how Ajmal actually thinks about the problem
(see his 2026-07-08 description: gather all pipes of the type → walk each pipe's two ends → merge whatever
touches → repeat until every set is complete → whatever equipment sits at a set's ends is what's really
connected).

The original hop-by-hop, single-path version of this (step 2's fallback, walked manually one connector at
a time rather than as a bulk merge) is the same underlying technique — cap the hop count (60 was plenty
for a ~24-hop real chain) to avoid an infinite loop if the model has a genuine geometric coincidence that
isn't a real path.

**Don't assume tag/name conventions reflect actual connectivity** — always verify by tracing before
reporting a connection as fact. This project's CRAC units looked like they should pair by matching code
(`CAC001A` ↔ `ACU001A*`) but the real wiring is cross-connected A↔B (see `glossary.md`) — the naming
convention was actively misleading here, not just incomplete.

**Color-coding a traced run** — once you have the full path (all pipe/fitting IDs from source to
destination), apply an `OverrideGraphicSettings` per-element via `view.SetElementOverrides(id, ogs)` — set
both line color (`SetProjectionLineColor`/`SetCutLineColor`) AND, if you want the pipe to genuinely read as
that color in shaded/realistic views, a solid surface fill (find the `<Solid fill>` `FillPatternElement`,
then `SetSurfaceForegroundPatternColor`/`Id`/`Visible` and the matching `Cut...` triple). Ajmal explicitly
wants both — line-color-only was called out as insufficient.

