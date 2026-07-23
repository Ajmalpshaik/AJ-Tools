# Claude session memory

Running log of decisions and progress across Claude Code chats. Newest entries at
the top. Keep entries short; delete sections that are no longer relevant.

## 2026-07-23 — Graphics Tools family multi-version compatibility audit (2020–2027)

- Twelfth pass (8 commands + 7 services + 7 models + window as one family), same
  branch/PR (#25). Result: **fully compatible 2020–2027, zero source changes**.
  Category overrides, the full OverrideGraphicSettings writer surface (transparency,
  background patterns, line weights/patterns, InvalidPenNumber), and
  InsulationLiningBase.GetInsulationIds verified identical in all 8 assemblies.
- Audit section added to `docs/COMPAT-AUDITS.md`.

## 2026-07-23 — Smart Connect multi-version compatibility audit (2020–2027)

- Eleventh pass, same branch/PR (#25). Result: **fully compatible 2020–2027, zero
  source changes**. Duct/Pipe/CableTray.Create and NewElbowFitting verified with
  exact signatures in all 8 assemblies; whole connector API identical throughout.
- Audit section added to `docs/COMPAT-AUDITS.md`.

## 2026-07-23 — Intelligent Tag Arranger multi-version compatibility audit (2020–2027)

- Tenth pass, same branch/PR (#25). Result: **fully compatible 2020–2027, zero
  source changes**. All leader work routes through LeaderLogicService/TagCompat
  (2023 tag boundary handled); Element.IsValidObject and
  TransactionStatus.Committed/RolledBack verified present in all 8 assemblies.
- Audit section added to `docs/COMPAT-AUDITS.md`.

## 2026-07-23 — Purge tools multi-version compatibility audit (2020–2027)

- Ninth pass (a family of 6 commands audited together), same branch/PR (#25) as
  MEP Openings. Result: **fully compatible 2020–2027, zero source changes**.
  Family-editor APIs (FamilyManager/FamilyType/FamilyParameter), view purge
  (View3D/ViewSection/Viewport, Document.Delete), Transaction.GetStatus — all
  identical in all 8 assemblies.
- Confirmed the existing 2025 LinearDimension workaround is exactly right: the
  subclass appears in the 2025 assembly; the category-based dimension collector
  is version-safe everywhere with no #if needed.
- Audit section added to `docs/COMPAT-AUDITS.md`.

## 2026-07-23 — MEP Openings multi-version compatibility audit (2020–2027)

- Eighth tool of the passes (new PR after #24 merged). Result: **fully compatible
  2020–2027**, with the audit series' first real code changes: two inline `#if REVIT`
  branches in the tool's own files (ElementId category check, mm→internal units)
  replaced with the existing ElementIdExtensions/RevitCompat helpers — identical
  behaviour, but version logic belongs in helpers per the project rule.
- Notable verified subtlety: NewFamilyInstance(XYZ, FamilySymbol, Level,
  StructuralType) moved from Creation.Document (2020–2023) to ItemFactoryBase
  (2024+) — call sites unaffected. Also confirmed DisplayUnitType exists only
  2020–2021 (removed 2022), UnitTypeId from 2021.
- Audit section added to `docs/COMPAT-AUDITS.md`.

## 2026-07-23 — Auto Dimensions multi-version compatibility audit (2020–2027)

- Seventh tool of the passes, same branch/PR (#24) as Duct Standards Manager.
  Result: **fully compatible 2020–2027, zero source changes**. Dimension creation
  (ItemFactoryBase.NewDimension), datum curves (DatumPlane.GetCurvesInView +
  DatumExtentType), Line.CreateBound, ReferenceArray — all identical in all 8
  assemblies. The tool crosses no version boundary at all.
- Audit section added to `docs/COMPAT-AUDITS.md`.

## 2026-07-23 — Duct Standards Manager multi-version compatibility audit (2020–2027)

- Sixth tool of the passes (first after PR #23 merged — branch restarted from
  master, new PR). Result: **fully compatible 2020–2027, zero source changes**.
- The tool's big boundary — the 2022 ForgeTypeId transition for shared-parameter
  creation/binding — routes entirely through RevitCompat + the guarded AjSpec/AjGroup
  alias pattern. Whole shared-parameter file workflow (DefinitionFile/Groups/
  Definitions, CategorySet, InstanceBinding, BindingMap.ForwardIterator via its
  DefinitionBindingMap base) verified identical in all 8 assemblies.
- Audit section added to `docs/COMPAT-AUDITS.md`.

## 2026-07-23 — Pipe Sizing multi-version compatibility audit (2020–2027)

- Fifth tool of the passes, same branch/PR (#23) as Ceiling Magnet. Result: **fully
  compatible 2020–2027, zero source changes**. The tool is a self-contained
  calculator — its only Revit API is the command wrapper; TransactionMode.ReadOnly
  verified present in all 8 assemblies. No unit API anywhere (does its own
  metric/imperial math), so the 2021 units boundary doesn't apply.
- Audit section added to `docs/COMPAT-AUDITS.md`.

## 2026-07-23 — Ceiling Magnet multi-version compatibility audit (2020–2027)

- Fourth tool of the passes (first after PR #22 merged — branch restarted from
  master, so this went up as a new PR). Result: **fully compatible 2020–2027**.
- Verified: Ceiling.GetCeilingGridLines is absent 2020–2024 / present 2025+ in the
  reference assemblies — CeilingGridApiCompat's REVIT2025 gate + runtime reflection
  probe (for unpatched 2025.0–2025.2) is exactly right. Everything else, including
  RevitLinkInstance.GetTotalTransform (inherited from Instance), identical in all 8.
- Only source change: corrected a stale header note in CmdCeilingMagnet.cs and
  CeilingMagnetService.cs claiming the tool still "uses UnitUtils with
  DisplayUnitType — revisit for 2021+" (it already routes through RevitCompat).
- Audit section added to `docs/COMPAT-AUDITS.md`.

## 2026-07-23 — Smart MEP Tag multi-version compatibility audit (2020–2027)

- Third tool of the passes, same branch/PR (#22). Result: **fully compatible
  2020–2027, zero source changes** — TagCompat's REVIT2023 switch point is exactly
  right (verified: old single-reference IndependentTag members exist 2020–2022 and
  are removed in 2023; multi-reference API added 2022). The TagMode-based
  IndependentTag.Create overload the tool uses exists in all 8 versions.
- LeaderLogicService's reflection fallbacks are runtime-only — compile-safe on all
  versions. All #if blocks in the services are #if DEBUG, not version branches.
- Audit section added to `docs/COMPAT-AUDITS.md`.

## 2026-07-23 — Colorize multi-version compatibility audit (2020–2027)

- Second tool of the one-tool-at-a-time passes, same session/branch/PR (#22) as the
  Filter Pro audit below. Result: **fully compatible 2020–2027, zero source changes**
  (not even header fixes — Colorize's headers are newer and contain no stale claims).
- Colorize-specific members (view-scoped FilteredElementCollector ctor, ToElementIds,
  View.SetElementOverrides, Transaction.RollBack, UIApplication.MainWindowHandle,
  OperationCanceledException) verified identical in all 8 per-version assemblies;
  everything else reuses the surface already verified for Filter Pro.
- Audit section added to `docs/COMPAT-AUDITS.md` (newest first).

## 2026-07-23 — Filter Pro multi-version compatibility audit (2020–2027)

- Ran the per-tool compatibility audit process on **Filter Pro** (first tool of the
  one-tool-at-a-time passes). Result: **fully compatible 2020–2027, zero code changes
  needed** — FilterRuleCompat / ElementIdHelper / ElementIdExtensions already cover
  every real API boundary the tool crosses.
- Verification was done against the real per-version RevitAPI/RevitAPIUI reference
  assemblies (Nice3point NuGet, all 8 versions), decoding method signatures from
  assembly metadata — not just docs. Full record: `docs/COMPAT-AUDITS.md` (new file,
  designed to accumulate one section per audited tool).
- Corrected finding vs older notes: `ElementId(int)` ctor and `ElementId.IntegerValue`
  were both already removed in the **2026** assemblies (not 2027). Helpers switch at
  2024, so nothing breaks.
- Only source changes: stale compatibility notes fixed in the headers of
  FilterCreator.cs / FilterApplier.cs / FilterProDataProvider.cs (documentation only).
- Improvement noted, NOT applied: `View.GetOrderedFilters()` (2021+) could replace
  FilterReorderer's order cache, but would make 2020 behave differently — skipped.
- Cloud sandbox cannot build (no .NET SDK, download blocked by network policy);
  real 8-configuration build still needs to run on the local machine.
- Next tools in the queue when Ajmal asks: pick any ribbon tool and repeat the same
  audit process (script approach documented in COMPAT-AUDITS.md header).

## 2026-07-23 — Claude Code plugin setup

- Added `.claude/settings.json` so every new Claude Code session on this repo
  auto-installs three plugins: `code-review@claude-plugins-official`,
  `superpowers@claude-plugins-official`, `claude-mem@thedotmack` (PR #20, merged).
- claude-mem's cloud sync (paid cmem.ai Pro service) was considered and
  **intentionally skipped**. Cross-chat continuity is handled by this memory file
  plus CLAUDE.md instead. claude-mem still works within a single chat.
- Reminder for future sessions: each cloud chat starts on a fresh machine —
  anything worth keeping must be committed and pushed.

## Open next steps

- None recorded yet.
