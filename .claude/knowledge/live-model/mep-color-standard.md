# Live Model — MEP Color Data Standard sync

> Part of the live-model knowledge set. Index: [`README.md`](README.md) — go back there to route to another topic.

## Syncing an external MEP Color Data Standard (Excel) into Duct/Pipe System Types, Materials, and View Filters
Full recipe from rolling out `D:\Ajmal\BIM Resources\NEW\Modeling\03_Standards\MEP Color Data Standard.xlsx`
(one row per system: Discipline_Code, Service_Code, Type, System_Name, System Classification,
Main_System_Code, Sub_System_Code, System_Code, Abbreviation, System_Flow_Type, Element Type, TrueColor
RGB, HEX, Color Name, Description) across a live model (2026-07-15/16). Read the Excel with
openpyxl (see the `xlsx` skill) — `data_only=True` for a static reference sheet like this one, no
recalc needed since it has no formulas.

**Match rule — always by Abbreviation, never by matching whole names.** The model's *existing* Duct/Pipe
System Type names (before renaming) don't match the Excel's `Type` column format at all (e.g. model had
`AC_Return Air Duct (RAD)`, Excel's Type column says `HVAC_AC_Return Air Duct System_RAD`) — the only
reliable join key is the abbreviation, extracted from the model name's trailing `(XXX)` parenthetical
(regex `\(([^)]+)\)\s*$`) for pre-rename names, or the trailing `_XXX` suffix once already renamed to the
standard. **Don't create new System Types for Excel rows that don't exist in the model** — only edit
what's already there; ask before creating (confirmed 2026-07-16, several rows — OPD, PUD, LPG, steam,
several fire types — had no corresponding model type and were correctly left uncreated).

**Both `MechanicalSystemType` (ducts) and `PipingSystemType` (pipes) already carry a matching set of
custom project parameters** mirroring the Excel columns almost exactly: `Discipline_Code`, `Service_Code`,
`System_Name`, `Main_System_Code`, `Sub_System_Code`, `System_Code`, `Abbreviation`, `System_Flow_Type` —
all plain string parameters, `LookupParameter("Name")` finds each one directly, no `BuiltInParameter`
needed. Don't assume these are pre-filled correctly just because the duct ones happen to be — in this
project the duct types already had 6 of 7 correct (only `System_Name` was blank), but the pipe types had
ALL of them blank except `Main_System_Code` (which held the wrong value — Service_Code's value had leaked
into it). Read every column fresh per system type before assuming any of them are already right.

**Renaming the Type itself**: `mechanicalSystemType.Name = newName` / `pipingSystemType.Name = newName`
just works — no special API needed, same as any other named element.

**Description**: the plain Type Name AND its assigned Material both expose a native `Description` field
via `BuiltInParameter.ALL_MODEL_DESCRIPTION` (`get_Parameter`, not `LookupParameter` — it's built-in, not
custom). Convention settled on for this project: Description = the same text as `System_Name` (e.g.
"Return Air Duct System"), not custom prose — matches how the rest of this sync reuses existing column
text rather than inventing new content.

**Material class gotcha (2020 API)**: `Material` has NO `Keywords` property or parameter at all — confirmed
by reflecting on `typeof(Material).GetProperties(...)` and by `LookupParameter("Keywords")` returning
`null`. The Identity Data "Keywords" field visible in the Revit UI is not scriptable in this Revit
version. `Class` IS scriptable — it's `Material.MaterialClass` (a plain string property, not a Parameter).

**Bug found via cross-reference, not from names/tags (Modeler mindset case)**: one duct system type
(Kitchen Exhaust, KED) had its `Material` parameter pointing at the SAME `ElementId` as a *different*
system type's material (Return Air, RAD) — both types' `Material` parameter resolved to material
`HVAC_AC_Return Air Duct System_RAD`. Renaming that shared material to match either type would have
mislabeled it for the other. Caught by comparing every type's resolved Material *ElementId* side-by-side,
not by trusting each type's own name/label. Held that one back and asked Ajmal rather than guessing which
system "really" owned it — he created a proper dedicated material himself and confirmed. **General rule**:
before batch-renaming N types' assigned materials, first check for `ElementId` collisions across the
whole set — a rename that looks safe type-by-type can still clobber a second type's material.

**Verifying a Material's Graphics tab (Shading/Surface Pattern/Cut Pattern colors) programmatically**:
`Material.Color` (shading), `.SurfaceForegroundPatternColor`/`Id`, `.SurfaceBackgroundPatternColor`/`Id`,
`.CutForegroundPatternColor`/`Id`, `.CutBackgroundPatternColor`/`Id` — all plain properties, `.Red/.Green/.Blue`
on the `Color` struct. No `OverrideGraphicSettings` needed here (that's for a *view's* per-element or
per-filter override, not the material asset itself).

**View Filters (`ParameterFilterElement`) can be organized into folders using `/` inside the filter's own
`Name`** — e.g. `MEP_Duct_System Type/AC_Return Air Duct (RAD)` groups under a "MEP_Duct_System Type"
folder in the Filters dialog. When renaming these to match a new standard, split on the *last* `/`,
keep the prefix (folder) untouched, and only replace the suffix (the actual old type name) — same
abbreviation-matching technique as above.

**Per-view filter graphic overrides need BOTH projection AND cut set explicitly — Revit does not
default one from the other.** Found live (2026-07-16): 26 filters already applied to a floor plan view
had a fully correct Projection Line Color + Surface Pattern (Foreground) Color, but `CutLineColor` and
`CutForegroundPatternColor` were both simply unset (`IsValid == false`) on every single one — only the
"looking down on it" portion of any duct/pipe would show the standard color; anything actually sliced by
the view's cut plane would fall back to default/black. Always check and set both:
```csharp
var ogs = view.GetFilterOverrides(filterId); // or `new OverrideGraphicSettings()` for a filter not yet in this view
ogs.SetProjectionLineColor(color);
ogs.SetCutLineColor(color);
ogs.SetSurfaceForegroundPatternColor(color); ogs.SetSurfaceForegroundPatternId(solidFillId); ogs.SetSurfaceForegroundPatternVisible(true);
ogs.SetCutForegroundPatternColor(color);     ogs.SetCutForegroundPatternId(solidFillId);     ogs.SetCutForegroundPatternVisible(true);
view.SetFilterOverrides(filterId, ogs);
```
**Adding an existing filter (with its rule already defined elsewhere) to a new view**: `view.AddFilter(filterId)`
then `view.SetFilterVisibility(filterId, true)` then `SetFilterOverrides` as above — the filter's own
element-matching rule is shared/reused automatically, only the per-view override + visibility needs setting.

**Verifying enum-based classification values against the real installed API, not memory**: for systems
that don't exist in the model yet (so there's nothing to read their real classification from), get the
authoritative list first — `Enum.GetNames(typeof(Autodesk.Revit.DB.MEPSystemClassification))` — rather
than guessing names. Revit 2020's real list: `UndefinedSystemClassification, SupplyAir, ReturnAir,
ExhaustAir, OtherAir, DataCircuit, PowerCircuit, SupplyHydronic, ReturnHydronic, Telephone, Security,
FireAlarm, NurseCall, Controls, Communication, CondensateDrain, Sanitary, Vent, Storm, DomesticHotWater,
DomesticColdWater, Recirculation, OtherPipe, FireProtectWet, FireProtectDry, FireProtectPreaction,
FireProtectOther, SwitchTopology, Fitting, Global, PowerBalanced, PowerUnBalanced, CableTrayConduit`. Note
the **UI display string is not just the enum name with spaces** — confirmed several genuinely differ
(`SupplyHydronic` displays as "Hydronic Supply", word order flipped; `OtherPipe` displays as just
"Other", drops a word) — read the display string from an existing real type's own `"System Classification"`
parameter (`AsString()`, it's a plain String-storage parameter) rather than hand-formatting the enum name.
There is **no dedicated classification for fuel gas or steam** in this enum — this project's own
precedent (Refrigerant Liquid/Vapor, which also has nothing dedicated) is to fall back to `Other`; steam
supply/return was confirmed via Autodesk's own published docs to conventionally use Hydronic
Supply/Return. Several fire-suppression types (Foam, Clean Agent, Water Mist, Deluge) have no dedicated
value either and are genuine judgment calls — Autodesk's docs confirm Water Mist/Clean Agent are meant to
be "Other"; Deluge has no authoritative single answer (candidates: Dry, Pre-Action, or Other) since it
behaves like both (empty until activated like Dry, but detection-triggered like Pre-Action).

