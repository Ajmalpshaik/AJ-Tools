# Smart MEP Tags — settable size/length filters and a per-category leader toggle

**Date:** 2026-08-17
**Suite version:** 1.49.7
**Requested by:** Ajmal, who then delegated every open decision ("you can decide the things do the best thing for good").

## Problem

1. Smart MEP Tags has **no** minimum-size or minimum-length setting. Three values are hardcoded in
   `SmartMepTagService.cs`: `MinCurveLength` 1000mm, `MinDuctWidth` 100mm, `MinPipeDiameter` 0mm
   (so pipes are not size-filtered at all). Create Tags exposes its minimum length; Smart MEP Tags
   does not.
2. Every tag gets a leader. For accessories a leader is noise — the tag should just sit beside the
   element.
3. `SmartTagSettingsTracker` keeps state in a **static in-memory field**, so anything set here would
   be lost when Revit closes.

## Decisions taken

Confirmed with Ajmal:
- **Leader toggle is per category**, as a new column in the grid that already carries Tag?/Priority.
- **Round pipes and ducts**: the diameter counts as **both** width and height.
- **A duct must clear BOTH minimums** — 400×50 against 100×100 is skipped.

Taken under delegation:
- **Defaults: size filter ON, width 100mm, height 0mm.** Height 0 means "no height minimum". This
  keeps ducts behaving exactly as they do today (100mm width, no height check). The one behaviour
  change is that pipes under 100mm are now skipped, where before no pipe was ever size-filtered —
  visible in the skip tally, and switched off by setting width to 0 or unticking the filter.
  Rejected alternatives: 100×100 (silently drops shallow ducts too), filter OFF (silently starts
  tagging small ducts that are skipped today).
- **Length and size filters apply to MEP curves only** (ducts, pipes, cable trays). Accessories and
  mechanical equipment are unaffected — they have no meaningful length, and this matches the existing
  code, which only length-checks an `MEPCurve`.
- **New settings persist to `%APPDATA%\AJTools\TagClash.config`**, the file that already holds the
  shared vertical-run setting. Categories and priorities stay per-document and in memory as now — not
  changed, to avoid altering existing behaviour.

## Design

### Settings
`SmartTagSettingsState` gains: `MinLengthMm`, `FilterBySize`, `MinWidthMm`, `MinHeightMm`, and
`CategoryUseLeader` (per-category, defaulting to true for all six).

`TagClashSettings` gains read/write for the five new values, reusing `AppDataConfigStore` and the
existing read-modify-write-and-verify save so a failed write reports rather than being lost silently.

### Filter
`SmartMepTagService`'s three hardcoded constants are replaced by the resolved settings. The size check
runs only when `FilterBySize` is on, only for `MEPCurve`, and skips when **either** dimension is below
its minimum. A minimum of 0 means "no minimum" on that axis, matching how `MinPipeDiameter` already
behaved. Round sections report their diameter as both width and height. Every skip goes through the
existing `SkipTally`, so the reasons stay visible.

### No-leader placement
`FindBestTagPosition` already runs two passes, `leaderPasses = { true, false }`, but returns early
whenever the leader pass scores at all — so today the no-leader pass is only ever a fallback. When the
candidate's category has Leader unticked, the leader pass is skipped and only the no-leader pass runs,
with the close-in `baseOff` it already uses. No new placement maths.

Direction order for a no-leader tag becomes **Right, Left, Top, Bottom** (sideways first). This alone
delivers what was asked: the tag is placed to the side; if that side clashes, the existing scoring
picks the other side; if both clash, it carries on into Top/Bottom and the normal clash handling. The
scoring loop is untouched.

### Window
Converted to a `TabControl`:
- **What to tag** — the existing grid plus the new Leader? column, and the skip-vertical-runs tick.
- **Advanced** — minimum length, the "Also filter by size" tick, and the two size boxes, which grey
  out when it is off.

Validation message and the Save/Cancel/Reset buttons stay **outside** the TabControl, per the house
rule that an error on one tab must not be hidden by the other. `TabMotionHelper.AttachTabTransitions`
is called, as every other tabbed window does. Reset restores all five new values to their defaults
alongside the existing ones.

## Out of scope

Create Tags and Stack Tags keep their own minimum-length setting; this pass does not merge the two
tools' settings. The three settings windows on other panels still missing a Reset button (MEP Opening,
Revision Cloud, Flow Direction) are unrelated to this change.

## Testing

Compile-only. Both `Release` (2020) and `Release R25` (2025) must build with zero errors and zero
warnings. Nothing here can be verified without Revit — Ajmal tests placement, the no-leader accessory
behaviour, and whether the pipe size default suits his models.
