# Live Model — Family parameters and constraints

> Part of the live-model knowledge set. Index: [`README.md`](README.md) — go back there to route to another topic.

Adding, scoping and removing family parameters, driving geometry from them, and the difference between
a dimension that is *locked* and one that is *labelled* — including one case that corrupted a document.

### Third build (2026-07-19, bifurcation duct fitting) — ReplaceParameter/RenameParameter rollback corruption

**`FamilyManager.ReplaceParameter`/`RenameParameter`, used together to change an existing parameter's
group while keeping its name, produced real data corruption from a transaction that never committed —
the "uncommitted `Transaction` rolls back cleanly" assumption relied on elsewhere in this file does NOT
reliably hold for this specific API sequence.** Calling `ReplaceParameter(currentParameter, sameName,
newGroup, isInstance)` directly always throws `"Cannot replace a family parameter with another family
parameter, use RenameParameter() instead."` whenever `parameterName` matches the parameter's own current
name (Revit treats it as colliding with itself). The two-step fix implied by that message —
`RenameParameter(p, name+"__tmp")` then `ReplaceParameter(p, name, newGroup, isInstance)` — was tried
inside one transaction, looped over ~10 parameters. It threw the same error again partway through the
loop (plausibly a same-transaction stale-read issue, the same family of bug as this file's own
`Regenerate()`-outside-transaction lesson above), and the `Transaction` was never `Commit()`ed — it
should have rolled back everything. **It did not.** Live re-query after the exception showed: one
parameter split into two garbage duplicates with wrong values (a "Branch Spacing" parameter meant to
hold 550mm came back as two separately-named parameters holding 1120mm and 0mm), a completely different
parameter silently deleted outright (`get_Parameter` returned null, no trace), and — found later by
re-checking specifically because the above raised suspicion — two more parameters' VALUES quietly
changed (a width parameter and the main body's own driving Length parameter), even though nothing in the
failed script ever called `fm.Set()` on them. Separately, 3 `Extrusion` elements turned up in the
document where exactly 1 had ever been created — no script in the session explicitly duplicated one.

**Net effect: treat any `ReplaceParameter`/`RenameParameter` sequence that throws as leaving the entire
document's parameter *and geometry* state untrusted, not just "that one call failed."** After a failure
here, don't assume "the transaction didn't commit, so nothing changed" — re-query every parameter's name,
group, AND value (not just existence), and re-count geometry elements, before continuing. Same standard
of proof this file already requires for resize tests, just triggered by a failure instead of a success.

**Working, corruption-free alternative — but only for parameters with NO existing geometry
association** (no `Dimension.FamilyLabel`, no `AssociateElementParameterToFamilyParameter` link):
`FamilyManager.RemoveParameter(param)` followed immediately by a fresh `FamilyManager.AddParameter(sameName,
newGroup, sameType, sameIsInstance)` + `fm.Set()` to restore the value, all in one transaction. Worked
cleanly across 7 parameters, no corruption. **NOT attempted, and NOT safe, for a parameter that already
drives geometry** — removing and re-adding creates a new parameter with a new `Id`, which would orphan
any dimension label or association built in earlier steps. No verified-safe technique for changing an
already-geometry-linked parameter's group was found this session. If this comes up again: test on a
throwaway/duplicate family first, and re-verify the geometry's bounding box afterward before trusting it.

### "Locked" vs "labelled" dimensions — and how to actually find parametric failures (2026-08-26)

**A dimension labelled to a family parameter already drives the geometry. It does not need to be locked
as well, and Revit will not let it be both.** `Dimension.FamilyLabel` set = parameter-driven;
`Dimension.IsLocked` = held rigid with no parameter. They are alternatives, not a belt-and-braces pair.
So "that dimension isn't locked" is NOT a diagnosis — check whether it is *labelled* first. In the FCU,
`Pipe_Stub_Length` was reported as broken because its dimension showed `locked=False`; it was in fact
labelled and worked perfectly.

**The real way to find what's broken: a parametric stress test, not a dimension audit.** Snapshot every
`GenericForm`'s bounding box, change one driving parameter, regenerate, snapshot again, and diff. Anything
that reports UNCHANGED when it should have moved — or moved when it should have stayed — is the actual
fault. Restore the parameter afterwards in the same call.

Reading the results needs care; **"unchanged" is not automatically wrong**:
- Geometry dimensioned from a plane that did not move (e.g. pipe heights measured from `Casing Bottom`
  when only `Casing Top` moves) *correctly* stays put.
- Geometry whose parameter has its own formula (`CHW_Return_Height = Unit_Height - 50 mm`) *correctly*
  moves.
- The genuine fault is geometry that ends up **physically impossible** — in the FCU, `Supply_Air_Width`
  and `Return_Air_Width` were fixed at 850mm with no link to `Unit_Length`, so dropping the unit to 800
  left the spigots protruding 25mm past the casing on both sides.

**Fix pattern**: give the dependent dimension a formula that reproduces its current value exactly at the
current size, so nothing about the existing design changes — `Supply_Air_Width = Unit_Length - 150 mm`
gives 850 at 1000 as before, and stays inside the casing at 800 and 1400. Verify by re-running the
stress test at several sizes and asserting the child stays within the parent's bounding box.

### An associated material parameter with no VALUE silently falls back to Object Styles (2026-08-27)

Associating a solid's `MATERIAL_ID_PARAM` to a family parameter is only half the job — **the family
parameter must also be given a value**. If it is left empty, the solid renders `<By Category>` and quietly
picks up the subcategory's Object Styles material instead. Everything looks correct, so the gap is
invisible until the Object Styles material is cleared, at which point the geometry loses its material
altogether.

Caught on the diffuser: all three solids were associated to `Material`, which had never been set. They
were rendering from the `Supply Air Diffuser - Body` Object Styles entry the whole time. Clearing that
entry — believing it inert, as it genuinely was on the FCU — left them with no material at all.

**Check both halves**: `GetAssociatedFamilyParameter(materialParam)` returns the driving parameter AND
`FamilyType.AsElementId(thatParameter)` returns a real material id. Set the value on **every family
type**, not just the current one.

**Related tidy-up rule**: delete model subcategories nothing sits on. Find them by collecting each
`GenericForm`'s `FAMILY_ELEM_SUBCATEGORY` -> `GraphicsStyle.GraphicsStyleCategory.Id` into a set, then
`Document.Delete` any subcategory whose id is absent. Revit also drops some unused user subcategories by
itself on save, so the list can shrink without you touching it — re-read before assuming what is there.
