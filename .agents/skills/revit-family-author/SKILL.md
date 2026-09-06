---
name: revit-family-author
description: Build, extend or audit a Revit FAMILY (.rfa) through the AJ AI Bridge — parameters, formulas, reference planes, geometry, connectors, subcategories, materials. Use whenever Ajmal is working inside the Family Editor: "make an air terminal family", "add a parameter to this family", "check the family", "full audit", "make it auto-size from the airflow", "the neck is not aligned", "add the subcategory", or any request that names a family part (face, neck, collar, casing, spigot, clearance). Do NOT use for the compiled AJ Tools C# plugin (that is ajtools-build / ajtools-debug), and do NOT use for editing elements in a project model (that is the ordinary scripts/ filter+action route).
---

# Authoring a Revit family through the bridge

Ajmal is a BIM modeller, not a developer. Reply in plain language, numbers in **mm**, and follow
`reply-style.md`. Every claim about the family must come from a **fresh read-back**, never from what a
previous turn returned — he edits and undoes things in Revit between messages.

## 1. Route before you write any C#

Read the ONE knowledge file that matches the request. Do not read the set.

| The request is about… | Open |
|---|---|
| Category, OmniClass, reference plane conventions, subcategories, materials, naming, metadata | [`../../knowledge/live-model/families-conventions.md`](../../knowledge/live-model/families-conventions.md) |
| A formula was rejected, or you need the formula language's rules | [`../../knowledge/live-model/families-formulas.md`](../../knowledge/live-model/families-formulas.md) |
| Creating geometry — extrusions, blends, work planes, alignments, connectors, symbolic lines | [`../../knowledge/live-model/families-geometry.md`](../../knowledge/live-model/families-geometry.md) |
| Parameters, scope, dimensions, labelled vs locked | [`../../knowledge/live-model/families-parameters.md`](../../knowledge/live-model/families-parameters.md) |
| Bridge basics, units, saving | [`../../knowledge/live-model/core.md`](../../knowledge/live-model/core.md) |

Reusable code lives in [`../../scripts/README.md`](../../scripts/README.md) — in particular
`recipes/audit-family.cs` (read-only full audit) and
`recipes/create-parametric-box-family-with-duct-connector.cs`.

## 2. The order that works

Building a family out of order causes rework. This sequence was arrived at the hard way:

1. **Category and OmniClass first** — switching category adds/removes built-in parameters, so do it
   before anything else. Read the OmniClass code out of Revit's own `OmniClassTaxonomy.txt`, never
   from memory; check the 4th column names the category you actually want.
2. **A family type must exist** before any `SetFormula` call, or it throws "There is no valid family type."
3. **Shared parameters** — load from `Shared_Parameters.txt`. Any genuinely new one must be **created in
   that file**, never only inside the family.
4. **Formulas**, then decide instance/type scope. A Type parameter's formula cannot read an Instance one.
5. **Reference planes** — named, correct `Is Reference`, subcategorised, `Defines Origin` on the datums.
6. **Dimensions** — an EQ chain to keep it centred plus a labelled dimension to drive the size.
7. **Geometry** — each solid's sketch plane built from a **reference plane**, then aligned.
8. **Connectors** last, since rebuilding a solid destroys any connector hosted on its face.

## 3. Traps that have actually cost time here

- **Never build a sketch plane with `Plane.CreateByNormalAndOrigin`.** Use
  `SketchPlane.Create(doc, referencePlane.Id)`. A free plane leaves Work Plane reading
  "<not associated>", the Work Plane parameter is **read-only**, and the only fix is deleting and
  rebuilding the geometry.
- **`FAMILY_ELEM_SUBCATEGORY` takes the CATEGORY id, not the GraphicsStyle id.** The wrong one still
  resolves through `GetElement()` but shows **blank** in Revit's UI. **Verify with `AsValueString()`** —
  if it is empty, the value is wrong however sensible the id looks.
- **Associating a material parameter is only half the job** — the family parameter must also be given a
  value on every type, or the solid silently falls back to Object Styles.
- **"Formula setting failed" on EVERY formula** including trivial ones = a scope mismatch, not syntax.
- **Verify chained formulas in a separate call.** Reading a second-level dependent in the same call that
  set its parent returns a stale value.
- **A rejection is not proof of impossibility.** Commit, then retry in a fresh call before concluding.

## 4. Before calling anything finished

- Run `recipes/audit-family.cs` and read it — it catches leftover test values, blank subcategories,
  unassociated work planes and empty material parameters.
- **Resize test**: drive the family through at least three sizes plus one extreme, and check the
  geometry, not just that the numbers changed. `Document.GetWarnings()` must be 0.
- **Restore any value you changed while testing.** Stress-test leftovers have twice been mistaken for
  real defaults.
- **Save**: `Document.Save()` is blocked from the bridge. Post Revit's own command instead, and only
  after checking `UIApplication.ActiveUIDocument` is the family you mean:
  `UIApplication.PostCommand(RevitCommandId.LookupPostableCommandId(PostableCommand.Save))`.
  Verify from the file's timestamp on disk, not by asking Revit.
- **Capture** anything new into the one knowledge file it belongs to, and harvest reusable C# into
  `scripts/`.

## 5. Confirm before

Renaming or deleting anything he made, writing to `Shared_Parameters.txt` (back it up first — his whole
library depends on it), removing a feature, and any geometry decision that is a genuine design fork.
On first-of-its-kind geometry, surface the fork and wait rather than picking a default: the louvre core
and the airflow arrows were both built to a reasoned default and both rejected on looks.
