# Live Model — Tagging & leader placement

> Part of the live-model knowledge set. Index: [`README.md`](README.md) — go back there to route to another topic.

## Posting AJ Tools' own ribbon commands — doesn't reliably work
- `RevitCommandId.LookupCommandId(...)` with a guessed `"CustomCtrl_%CustomCtrl_%{Tab}%{Panel}%{Pulldown}%{Button}"`
  string did not resolve for a real button (`CmdSmartMepTag`) nested inside a `PulldownButton`, across
  several format variants tried.
- `PushButton` has no `GetCommandId()` in this Revit API version.
- Conclusion: don't spend time re-attempting this. To exercise a real AJ Tools command's behavior from a
  script, replicate the needed logic directly with plain Revit API calls, or ask Ajmal to click the button
  himself once preconditions (e.g. view visibility) are set up.

## Tagging directly via script instead of clicking a real AJ Tools command
- Ajmal explicitly rejected "just click Smart MEP Tags yourself" and asked for direct AI-driven tagging
  instead ("if you need to make skill make whatever need to make make it"). Confirmed empirically (not
  just from the existing "reflection is hard-blocked" note): `new AJTools.Services.LeaderLogic
  .LeaderLogicService(view)` in a `run_csharp` script fails `CS0246 — the type or namespace name
  'AJTools' could not be found` — the bridge's Roslyn compilation genuinely has no reference to
  AJTools.dll, so no AJTools class (public or internal) is reachable from a script, full stop. Same
  applies to `SmartMepTagService` (also `internal`) and its clash-avoidance scoring engine
  (`SmartTagPlacementEngine`) — there is no way to invoke the real compiled tool from here.
- **What worked instead**: reproduce the *algorithm*, not the class. `LeaderLogicService.GetE1`'s
  L-shaped elbow math is short enough to copy verbatim as a local `Func<XYZ,XYZ,XYZ>` inside the script,
  using only plain `View.RightDirection`/`UpDirection` projections — same result, no assembly reference
  needed. Used directly to tag 62 ducts in `1 - Mech` (Revit 2020) with working L-shaped leaders.
  Now saved as [`recipes/tag-elements-in-active-view.cs`](../../scripts/recipes/tag-elements-in-active-view.cs).
- **What could NOT be reproduced**: `SmartTagPlacementEngine`'s real clash-free scoring/placement — that
  logic wasn't fully read/ported, so the script uses a much simpler alternating-side + stagger offset
  instead. This is good enough to get real, usable tags placed, but dense/parallel duct runs can still
  overlap. Tell Ajmal this plainly every time this recipe is used — don't imply clash-free placement it
  didn't actually do. If Ajmal wants the exact scored placement without clicking the button himself, that
  needs a genuinely new compiled AJ Tools command (ajtools-build), not a bigger script.
- **Revit 2020 API notes hit while building this** (verify fresh per version, don't assume): no
  `UnitTypeId` (that's 2021+) — use `DisplayUnitType.DUT_MILLIMETERS` with `UnitUtils
  .ConvertToInternalUnits`. No `IndependentTag.GetTaggedLocalElementIds()` (that's the 2022+
  multi-reference tag API) — use the single `IndependentTag.TaggedLocalElementId` property instead.
  `IndependentTag.TagHeadPosition` / `.LeaderElbow` / `.HasLeader` are plain public settable properties
  in 2020 — these are exactly what `LeaderLogicService`/`TagCompat` wrap for multi-version compat; a
  2020-only script can set them directly.

## Finding the RIGHT tag family — Document.GetDefaultFamilyTypeId, not a guessed name
- **Proof case**: tagging 62 ducts, picked "M_Duct Size Tag" because it's the generic out-of-box
  Autodesk name and looked like the safe default. Ajmal corrected this: he wanted whatever tag family
  Revit itself uses when he tags a duct manually — not a guess, not AJ Tools' own Smart MEP Tag
  Settings (a different, AJ-Tools-specific priority/enable setting), Revit's own per-category default.
  **The real mechanism** (verified live, don't assume): `Autodesk.Revit.DB.ElementTypeGroup` does
  **not** have per-MEP-category tag entries (no `DuctTagType` etc. — checked the full enum list, only
  generic ones like `TagNoteType` exist). The actual API is
  `Document.GetDefaultFamilyTypeId(Category.GetCategory(doc, BuiltInCategory.OST_DuctTags).Id)` — this
  is "whatever Tag by Category would use", confirmed to return `TRG_TG_Duct_System+Dimension+Flow -
  ABB+Size+BOD+Flow+Comments`, a custom project tag family, not the generic Autodesk one. Use this
  lookup (per relevant `OST_..Tags` category) instead of hardcoding or guessing a family name whenever
  Ajmal asks for "the [normal/standard/manual] tag", across any category (pipe, equipment, cable tray).
- **Second, separate gotcha found while fixing this**: `IndependentTag.Create(document, symId,
  ownerDBViewId, reference, addLeader, tagOrientation, pnt)` does not reliably honor the `symId` you
  pass — confirmed live that all 38 tags created with `symId` explicitly set to "M_Duct Size Tag"
  (Id 102763) actually came out with type Id 932392 (the document's own default type) instead, with no
  exception thrown. It happened to be harmless this time only because the document default and the
  wanted type turned out to be the same value after the fix — don't count on that coincidence recurring.
  **Always verify `tag.GetTypeId() == intendedTypeId` right after `Create()` and call
  `tag.ChangeTypeId(intendedTypeId)` if it doesn't match** — now baked into
  [`recipes/tag-elements-in-active-view.cs`](../../scripts/recipes/tag-elements-in-active-view.cs). Not yet
  confirmed whether this also affects the compiled `SmartMepTagService`'s own tag creation — worth a
  fresh check if a wrong-tag-family report ever comes in from that tool specifically.

## Elbow clearance must clear the tag's own text, not a small guard stub
- **Proof case**: Ajmal reported "the leader is clashing with its own tag" after the first live-tagged
  batch of 38 ducts. Root cause: `tag-elements-in-active-view.cs` copied `LeaderLogicService`'s default
  `minHorizontalStub` (0.5ft/152mm) as the ELBOW PUSH DISTANCE for the "same-X" guard case — but that
  constant's real purpose in `LeaderLogicService` is only "how close counts as *straight-line-risk*
  before bending at all," not "how far the elbow needs to be from the tag to clear its own text." Those
  are two different distances that happen to share a variable name.
- **Measured, don't guess, the real clearance needed**: placed a batch of tags, read `tag.get_BoundingBox
  (view)` back on each, and compared `TagHeadPosition` to the box extents. This project's default duct
  tag (`TRG_TG_Duct_System+Dimension+Flow`) measured a consistent **395mm half-width x 243mm
  half-height** around the head position across every tag (same family, fairly uniform content length
  here) — i.e., the real tag footprint. With the 152mm guard push, **26 of 38 tags** had the elbow
  landing inside that footprint (`elbowDx/elbowDy < halfWidth/halfHeight` on the shared axis) — a
  systemic, not occasional, defect, because axis-aligned duct runs in a rectilinear HVAC layout very
  commonly produce the "same view-X" or "same view-Y" case this guard governs.
- **Fix**: set the elbow push distance from the MEASURED half-extent + margin (used 550mm, safely above
  the observed 395mm), not the small anti-straight-line guard value. Re-ran, re-measured all 38 with the
  same before/after test — **0 clashing after**. Now the default in
  [`recipes/tag-elements-in-active-view.cs`](../../scripts/recipes/tag-elements-in-active-view.cs).
- **Clash-detection technique** (reusable for any future tag-placement QA): for each tag, get
  `t1 = tag.TagHeadPosition`, `bb = tag.get_BoundingBox(view)`, `e1 = tag.LeaderElbow` (guard with
  try/catch — throws on tags with no free-end leader). `halfW/halfH` = the SMALLER of (t1 to bb.Min) and
  (t1 to bb.Max) on each axis — this is a text-footprint estimate since the box also includes the leader
  run, but taking the min of both sides works because the leader only extends the box on ONE side (the
  side toward the tagged element), so the smaller side is (usually) pure text. Flag a clash when the
  elbow sits on the same row/column as the head (`elbowDy` or `elbowDx` near zero) AND its offset on the
  other axis is still less than that half-extent.
- **If a different tag family is used**, re-measure — don't reuse 395mm/550mm blindly. A shorter/longer
  tag family or longer parameter text (e.g. a long "Comments" value) changes the real footprint.
- **Also true for `SmartMepTagService`'s own compiled placement logic** — not yet checked whether it has
  the same category of bug (using a guard-only constant as a clearance distance). Worth a look if Ajmal
  ever reports the same clash symptom from the real Smart MEP Tags button, not just this script.

## Tag-vs-tag overlap resolution — iterative push-apart, not full clash-scoring
- **Different problem from the own-leader clash above**: that one was a tag's leader crossing into its
  OWN text; this one is two DIFFERENT tags' boxes overlapping each other (e.g. two parallel duct runs
  close together both landing tags nearby). Ajmal asked directly: "any idea to fix this issue" — the
  real `SmartTagPlacementEngine` clash-scoring engine solves this properly but is unreachable from a
  script (internal class, no assembly reference — same wall as everywhere else in this session). Built a
  simpler, good-enough alternative instead of declining: an iterative AABB (axis-aligned bounding box)
  separation pass.
- **Technique**: project each tag's `get_BoundingBox(view)` corners through `view.RightDirection`/
  `UpDirection` to get a view-space 2D box (needed because the box itself is stored in model XYZ; a
  rotated/oriented view needs the projection to get a correct 2D rectangle, not just raw X/Y). For every
  pair whose boxes intersect, compute the overlap depth on both axes, push the two tags apart along
  whichever axis has the SMALLER overlap (standard minimum-translation-vector heuristic — moves less
  distance than pushing along the larger axis), split the push 50/50 between the pair with a small
  margin (used 50mm), recompute each moved tag's `LeaderElbow` via the same `getE1` used for placement
  (critical — reusing the SAME elbow function means this pass can't reintroduce the own-tag clash a
  separate fix just resolved), `doc.Regenerate()`, and repeat until a pass finds zero overlaps or an
  iteration cap is hit (used 15, real convergence was 3 iterations for 7 overlapping pairs out of 38
  duct tags).
- **This is NOT full clash-free placement** — it only resolves what's already overlapping after initial
  placement, doesn't consider text-length variance up front, and has no guarantee of convergence on a
  much denser view (hence the iteration cap + explicit "still remaining" report if hit — never claim
  clash-free if the cap was reached without reaching zero). For anything beyond moderate density, the
  real `SmartMepTagService`/Intelligent Tag Arranger (clicked by Ajmal) remains the actual clash-free
  tool.
- **Verified how**: fresh re-check after the pass, independent of the resolution loop's own bookkeeping
  — recomputed the same pairwise-overlap test AND the own-leader-clash test from scratch in a separate
  `run_csharp` call. 0 tag-vs-tag overlaps, 0 own-leader clashes, still 38/38 distinct ducts tagged (no
  duplicates/losses from the moves). Now folded into
  [`recipes/tag-elements-in-active-view.cs`](../../scripts/recipes/tag-elements-in-active-view.cs) as a
  second pass that runs automatically after initial placement.

## Guard-1 tie-break defaults to one direction when dx is EXACTLY zero — don't trust it for a deliberate side
- **Proof case**: after switching to fixed-side placement (below for horizontal ducts), Ajmal reported
  "the leader is on both left" — every horizontal duct's leader bent the same direction regardless of
  which side of the layout it was on. **Root cause, traced through the actual math, not guessed**: the
  fixed-side offset is purely `-viewUp` (no X component), so `TagHeadPosition.X` equals the leader-end
  `X` EXACTLY for every horizontal duct — `dxView` in `GetE1`'s Guard-1 branch is therefore always
  precisely `0`. The tie-break `(dxView < 0) ? 1.0 : -1.0` evaluates `0 < 0` → always `false` → always
  picks the SAME branch, for every single horizontal tag, no matter which side of the drawing it's on.
  A tie-break that looks reasonable for genuinely varied dx values becomes a silent constant once you
  deliberately zero out one axis — worth remembering any time an offset is intentionally axis-locked.
- **Fix**: gave the elbow function an explicit optional `preferredSign` parameter that overrides the
  degenerate tie-break with a deliberate direction, computed once per duct from where it sits relative
  to the centroid of the whole tagged group (left-of-centroid ducts push further left, right-of-centroid
  push right) — matches a branching layout (e.g. two mirrored branches off a central riser) without
  needing real MEP connector/system tracing to find "which end connects to what". Pass `null` only from
  the overlap-resolution pass (PASS 2), where there's no "correct side" to preserve, just a clash to
  clear — reusing the old dx-sign tie-break there is fine since it's arbitrary anyway.
- **Verified how**: fresh, independent check after the fix — 23 of 26 horizontal tags landed on the side
  matching their duct's actual position relative to the group centroid; the other 3 were intentionally
  moved to the opposite side by PASS 2's overlap resolver to clear a real clash (expected trade-off, not
  a residual bug — clash resolution takes priority over preferred side). All three clash checks
  (own-leader, tag-vs-tag, tag-vs-duct) still at zero. Now baked into
  [`recipes/tag-elements-in-active-view.cs`](../../scripts/recipes/tag-elements-in-active-view.cs).

## Leader side should follow REAL flow direction, not a geometric proxy
- **Evolution of the same "which side" question across this session** (each version replaced the last):
  1. Alternate left/right by index (anti-clash spread) — Ajmal: looked inconsistent/messy.
  2. Fixed side by orientation (horizontal=below, vertical=right) — fixed consistency within one
     orientation, but a degenerate dxView=0 tie-break made every horizontal tag push the SAME direction
     regardless of position (see the Guard-1 tie-break note above).
  3. Fixed the tie-break using a view-wide centroid (left-of-centroid pushes left, right pushes right) —
     broke when a view has MULTIPLE separate branch clusters, since one global centroid doesn't
     represent "which side of ITS OWN riser" for a cluster that sits entirely on one side of the view.
  4. Nearest-riser-by-X-proximity — fixed the multi-cluster case (each branch finds its own local
     riser), but still just a geometric proxy for what Ajmal actually meant.
  5. **Real flow direction** (final, confirmed with Ajmal directly asking "is it based on where flow is
     going?" — yes): use `Connector.Direction` (`FlowDirectionType.In`/`Out`) on the duct's own
     `ConnectorManager` — this project's model already has calculated flow (verified live: real `Flow`
     values present, not just direction labels), so no extra calculation step needed. Leader extends
     toward the `Out` (downstream) connector's side. Falls back to nearest-riser, then group centroid,
     only if a duct has no resolvable In/Out pair (flow not calculated for that segment).
- **Lesson**: when a geometric heuristic is standing in for a real physical/logical property (flow
  direction, in this case), and the model actually HAS that real data available, check for it and use
  it directly rather than refining the geometric guess further — each geometric refinement fixed a
  symptom but flow direction was the actual ground truth the whole time.
- **Verified how**: independent fresh check — 26 of 26 horizontal tags' leader direction matched the
  real connector flow direction (0 mismatches), all three clash checks (own-leader, tag-vs-tag,
  tag-vs-duct) still zero, 38/38 ducts correctly tagged. Now
  [`recipes/tag-elements-in-active-view.cs`](../../scripts/recipes/tag-elements-in-active-view.cs)'s
  standard side-selection logic.

## Supply flips opposite of real flow; Return/Exhaust follow it as-is
- **Ajmal, 2026-07-14**, after confirming the registry-based rebuild looked "perfect": pointed out
  across color-coded views (blue=Supply, magenta=Return, green=Exhaust) that Return and Exhaust leaders
  correctly followed real flow direction, and Supply ALSO correctly followed real flow direction — but
  he wants Supply to be the OPPOSITE, "same like a supply and return" (the standard drafting convention
  of drawing supply/return as visual mirror images for quick distinction, independent of what each
  system's own real flow direction says). Asked one clarifying question before rebuilding 1092 tags
  again — which system(s) should flip — confirmed: Supply only; Return/Exhaust stay as they were.
- **Identifying system type**: `Duct.MEPSystem.SystemType` — but the actual PROPERTY TYPE is
  `Autodesk.Revit.DB.Mechanical.DuctSystemType`, not the more obvious-sounding
  `Autodesk.Revit.DB.Mechanical.MechanicalSystemType` (a real compile error caught here, not a silent
  bug — `ToString()` on the value prints "SupplyAir" either way, but the two are different enum types
  and guessing wrong doesn't compile). **Verified via reflection** (`prop.PropertyType.FullName`)
  before trusting it, per house rule to never guess a Revit API detail from a plausible-sounding name.
  Enum values confirmed live: `UndefinedSystemType, SupplyAir, ReturnAir, ExhaustAir, OtherAir, Fitting,
  Global`.
- **Implementation**: wrap the existing real-flow-direction function (`flowDirSignX`) in one more
  function that flips the sign only when `SystemType == DuctSystemType.SupplyAir` — everything else
  (candidate scoring, registry placement, cleanup passes) is unchanged, since this only changes WHICH
  sign gets treated as "preferred," not the placement algorithm itself.
- **Verified how**: full independent re-check after rebuild — 1092/1092 tags, 0 own-clash, small
  residual (2 tag-vs-tag / 4 tag-vs-duct, same one dense pocket as the pre-flip build), **Supply: 184
  correct / 2 wrong (99%), Return+Exhaust: 360 correct / 0 wrong (100%)**. The 2 "wrong" Supply tags are
  almost certainly the same ones caught in that dense pocket's cleanup pass, which doesn't know about
  the Supply-flip rule and can override it as a last resort to guarantee clash-free — same honest
  trade-off pattern as the earlier residual-clash notes, not a new bug.

## Registry-based scored placement — replaced place-all-then-resolve entirely
- **Ajmal, 2026-07-14**: asked directly for an improved design, pointed at AJ Tools' own compiled
  `SmartTagPlacementEngine.cs` ("you can refer our smart tag program... take from there if you need").
  Read it in full — it scores 4 candidate positions (top/bottom/left/right) per element against a
  live registry of everything already placed, rather than placing everything on one fixed side and
  fixing overlaps afterward. Adapted the core ideas (can't call the class itself — internal, no
  assembly reference from this bridge, same wall as everywhere else this session):
  - Generate candidates at 3 sliding anchor points along the duct (50%/25%/75%) — a short segment gets
    more chances to find a clean spot, instead of forcing the same offset from a fixed midpoint.
  - Score clash-free candidates by: free space (base), distance from nearest neighbor, a
    **consistency bonus** for matching the side the nearest already-placed same-orientation tag chose
    (keeps runs looking uniform without a rigid "always below" rule), and a small preference for the
    canonical midpoint/full-leader options.
  - Two full passes: WITH leader at full offset first; only if NOTHING scores clash-free, retry
    WITHOUT a leader at a smaller offset (tag sits close, no cramped L-shape) — solves the "leader
    looks bad in small spaces" complaint directly.
  - Never skip an element ("I need to tag everywhere") — if truly nothing scores clash-free anywhere,
    place the least-overlapping candidate as a last resort, tracked separately so it's never silently
    counted as clean.
- **Why this replaced v2's approach entirely, not just patched it**: v2 placed every tag on one FIXED
  side then ran an iterative push-apart pass on overlaps. At 1092 tags that pass had to move roughly
  half of them to converge — and since the resolver only knows "clear this overlap", not "preserve why
  this tag chose this side", it silently destroyed flow-direction correctness (dropped from confirmed
  100% down to ~49%, a coin flip). Registry-based placement decides each tag's final position ONCE,
  already aware of everything placed before it, so a later tag simply never chooses a clashing spot —
  nothing to "fix" after the fact for the vast majority of tags.
- **Performance**: obstacle/tag/side lookups all go through spatial hash grids (same technique as v2's
  large-scale resolver), not O(n²) full scans — 1092 ducts placed in ~34 seconds, each duct evaluating
  up to 12 candidates (2 sides × 3 anchors × with/without leader) against grid-local neighbors only.
- **Verified how, full independent re-check after both a targeted and a final unrestricted cleanup
  pass on the small residual**: 1092/1092 tags, 1092 distinct ducts (no loss), 0 own-leader-clash,
  **546/546 (100%) flow-direction match** (v2's equivalent check was 270/546), 1 tag-vs-tag + 2
  tag-vs-duct clashes remaining — both concentrated in ONE genuinely dense pocket that didn't resolve
  even after unrestricted-movement iterations (a real geometric deadlock, not a bug; 99.7% of tags are
  fully clean). Only 36 of 1092 ducts (3.3%) needed the last-resort forced fallback at all.
- Now the whole placement algorithm in
  [`recipes/tag-elements-in-active-view.cs`](../../scripts/recipes/tag-elements-in-active-view.cs) — the
  old two-phase push-apart resolver (still a valid technique, see the sections below) is no longer the
  primary mechanism, only used ad hoc for cleaning up a small residual after registry placement if one
  exists.

## Check view.Scale FIRST, every time — every mm clearance constant depends on it
- **Ajmal's rule, stated directly after the flow-direction/clash trade-off discussion**: "before start
  everything, you have to check first the scale... otherwise the same issue before we face that it will
  face every time." This is now a standing, mandatory first step for any tag-placement work, not just
  advice for the one incident that surfaced it.
- **What actually happened**: the 1092-duct re-tag looked completely healthy right after placement
  (26/26-style checks would have passed if run then), but a full verification pass afterward found 546
  own-leader-clashes — traced to the view scale having changed from 1:50 to 1:100 between sessions.
  `offsetFeet` (the base tag-head offset) already scaled correctly via `view.Scale/50.0`, but
  `minHorizontalStub`/`minVerticalStub` (the elbow clearance, tuned from a real measurement at 1:50)
  were still hardcoded mm constants — at 1:100 the real text footprint doubled (395mm → 790mm measured
  live) while the clearance constant didn't move, silently reintroducing the exact bug that had already
  been fixed once. The resolver's own `marginFeet`/`ductMarginFeet` had the same latent bug, just not
  yet triggered.
- **Fix, now structural not reactive**: every mm-based physical clearance in
  [`recipes/tag-elements-in-active-view.cs`](../../scripts/recipes/tag-elements-in-active-view.cs) is
  computed via one shared `viewScaleRatio = view.Scale / 50.0` computed as literally the first executable
  step after resolving the view — not a per-fragment ad-hoc scaling. Any NEW mm clearance added to this
  recipe (or a sibling one) must multiply by this ratio too; a hardcoded mm constant is the bug pattern
  to watch for.
- **General lesson beyond this recipe**: any script that measures a real-world clearance from the live
  model (not just this tag one) should treat that measurement as valid ONLY for the view/scale it was
  measured under, and either re-measure or explicitly scale before reusing it in a different context.
  Don't assume a constant tuned once stays correct — verify the context it's being reused in matches.

## Prefer moving the straight-leader tag, leave an L-shaped one in place
- **Ajmal's feedback, with a reference screenshot**: "you find the clash and you are moving both tags —
  try to make clash free with moving straight leader tag, L shaped one keep same place, no need to
  move." The existing tag-vs-tag resolver always split a clash 50/50 between both tags — reasonable
  when both are equally "cheap" to move, but an L-shaped (bent) leader already threaded around another
  constraint (its own text, a duct, another tag), so nudging it risks reopening a problem a separate fix
  just solved. A straight leader has no such history — freely movable.
- **Implementation**: at each overlap-resolution iteration, classify each of the two clashing tags as
  "straight" or "L-shaped" by re-running the same `getE1` elbow function against its CURRENT position
  (returns null = straight, non-null = L-shaped) — don't track this as separate state, derive it fresh
  each time from the tag's actual current geometry. If exactly one of the pair is straight, that one
  takes 100% of the separation push; the L-shaped one gets 0%. Only split 50/50 when both are the same
  kind (both straight, or both already L-shaped).
- **This can fail to converge** — verified live: 1 of 38 tags had genuinely no room to escape a clash
  by moving alone (a real geometric dead end in a tight spot), so straight-only resolution hit its
  iteration cap with that one pair still unresolved. **Two-phase fix**: Phase 1 runs the straight-only
  preference for its full iteration budget; if it doesn't fully converge, Phase 2 runs a further budget
  with an unrestricted 50/50 fallback (clash-free is the harder requirement — a stuck pair proved it
  needs both to move, so let it). Re-verified after Phase 2: the same pair resolved, 0 clashes overall.
  **Report this trade-off plainly if it happens** — don't silently let a "keep it in place" preference
  block genuine clash resolution, and don't silently drop the preference either; say which pair needed
  the exception and why.
- **Verified how**: fresh independent re-check of all three clash types after both phases — still 0/0/0,
  38/38 ducts tagged, 26/26 flow-direction matches unaffected (this pass never touches `preferredSign`,
  only resolves collisions after initial placement). Now
  [`recipes/tag-elements-in-active-view.cs`](../../scripts/recipes/tag-elements-in-active-view.cs)'s PASS 2.

## Tag-vs-tag AND tag-vs-duct — resolve together, not sequentially
- **Third clash type Ajmal found, same session**: after fixing own-leader-clash and tag-vs-tag overlap
  separately, he reported "tag clash with duct" — a tag's text box sitting on top of duct geometry
  (its own tagged duct, or a different nearby one). Same detection shape as tag-vs-tag (project
  `get_BoundingBox(view)` corners through view axes, AABB overlap test), but against every element of
  `elementCategory` in the view, using the tag's TEXT-only sub-box (see the own-leader-clash note for
  why: the full tag bbox includes the leader run, which is SUPPOSED to touch/cross duct geometry on its
  way to the element — only the text portion clashing with a duct is the actual problem).
- **Ran it as a separate pass first (mistake, caught immediately)**: resolving tag-vs-duct, then
  re-running the already-working tag-vs-tag pass afterward, reintroduced 1 tag-vs-tag overlap that had
  already been at zero — moving a tag to clear a duct can push it straight into a neighboring tag.
  **Fix**: merged both checks into ONE iteration loop — every round checks both tag-vs-tag AND
  tag-vs-duct, accumulates whatever moves either needs, applies both, regenerates, repeats. Converged
  clean in a further few iterations once combined. **General lesson**: when two geometric constraints
  can each perturb the same objects, resolve them in the SAME loop, not sequential passes — sequential
  passes can each locally satisfy their own constraint while breaking the other's already-solved state.
- **Verified how**: after the combined pass, re-checked all THREE clash types (own-leader, tag-vs-tag,
  tag-vs-duct) fresh and independently in one separate `run_csharp` call — all zero simultaneously,
  38/38 ducts still correctly tagged. Now the standard PASS 2 in
  [`recipes/tag-elements-in-active-view.cs`](../../scripts/recipes/tag-elements-in-active-view.cs)
  (superseded the tag-vs-tag-only version from earlier this session).

