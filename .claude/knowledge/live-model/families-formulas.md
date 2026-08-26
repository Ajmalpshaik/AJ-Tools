# Live Model — Family formulas (what the formula language will and will not accept)

> Part of the live-model knowledge set. Index: [`README.md`](README.md) — go back there to route to another topic.

Revit's formula language, learned by isolating failures one clause at a time. Almost every rejection
comes back as the same useless `"Formula setting failed."` or `"It is an invalid formula string."`, so
this file exists to save re-deriving which part it objected to.

### Conditional formula switch — one Yes/No toggle choosing between a formula and a typed value (2026-08-26)

A parameter that holds a formula is permanently read-only (`FamilyParameter.IsDeterminedByFormula` comes
back `true`; greyed out in Family Types), so a single parameter can never be "formula sometimes, typed
sometimes". The working pattern is three parameters, with only the last one touching geometry:

| Parameter | Type | Role |
|---|---|---|
| `Use Formula` | Yes/No | the toggle |
| `Width Manual` | Length | what the user types when the toggle is off |
| `Width` | Length | holds `if(Use Formula, Length / 2, Width Manual)` — the one that gets the `Dimension.FamilyLabel` |

Confirmed live (Revit 2020, Generic Model family) by flipping the toggle and reading `Width` back each
time: Yes -> 600mm from `Length / 2`, No -> 300mm from `Width Manual`, Yes again -> 600mm.

- **A Yes/No parameter is used bare inside `if()`** — `if(Use Formula, A, B)`, no `= 1` comparison
  needed, and (consistent with the spaced-name finding above) no quotes around the spaced name.
- **Both branches of the `if` must return the same unit type** — length on both sides. A bare number
  against a length fails with the same unhelpful generic "It is an invalid formula string."
- **Scope rule**: a formula on a *Type* parameter cannot read an *Instance* parameter; an *Instance*
  parameter's formula can read Type parameters. So if `Width` is Type, `Use Formula` and `Width Manual`
  must be Type too — otherwise make all three Instance. Mixing the wrong way is a silent design trap,
  not an obvious error.
- `fm.NewType(...)` + `fm.CurrentType` must be set **before** the first `SetFormula` (see the "no valid
  family type" entry above) — this always bites on a brand-new family, which starts with zero types.

### Duct Size in formulas — multiplies fine, can never be divided into (2026-08-26)

Shared parameter files often store neck sizes as `HVAC_DUCT_SIZE` while face sizes are plain `LENGTH`
(this is the case in Ajmal's own `Shared_Parameters.txt`). They are NOT interchangeable, and the rule is
one-directional — established by isolating one failing formula six ways:

| Formula | Result |
|---|---|
| `Face_Width (Length) = Neck_Width * 2` | **accepted** — Duct Size multiplies into a Length |
| `Neck_Area (Area) = Neck_Width * Neck_Height` | **accepted** — Duct Size squares into an Area |
| `Face_Velocity = Actual_Supply_Air_Flow / Face_Area` where `Face_Area = Face_Width * Face_Height` (both Length) | **accepted** |
| `Neck_Velocity = Actual_Supply_Air_Flow / Neck_Area` where `Neck_Area = Neck_Width * Neck_Height` | **REJECTED** |
| `Neck_Velocity = Actual_Supply_Air_Flow / (Neck_Width * Neck_Height)` | **REJECTED** |
| `Neck_Velocity = Actual_Supply_Air_Flow / Neck_Area` where `Neck_Area` holds a **plain typed value** | **accepted** |

So: **the declared parameter type is not what Revit checks — it follows the source unit types back up
the whole formula chain.** An `AREA` parameter whose value came from Duct Size is not accepted as an
area on the bottom of a division, even though the identical parameter holding a typed-in number is.
The error is always the same useless `"Formula setting failed."`, which is why isolating variants
one at a time is the only way to find the real boundary.

**There IS a laundering workaround: route the dimension through a plain `LENGTH` parameter.** Define a
Length parameter from the Duct Size one, build the area from THAT, and the division is accepted:

```
Neck_Size     (Length) = Neck_Width + 0 mm      <- or bare "Neck_Width", both accepted
Neck_Area     (Area)   = Neck_Size * Neck_Size
Neck_Velocity          = Actual_Supply_Air_Flow / Neck_Area
```
Verified live: 450 neck, 320 l/s -> area 0.2025 m2 -> **1.580 m/s**, matching the hand calculation.
`+ 0 mm` is accepted, so the laundering parameter can read exactly the same as the nominal size — there
is no need to invent a fake physical offset just to make the maths legal.

**Correction to an earlier conclusion in this file.** An earlier attempt at exactly this hop was
recorded as proof that "the taint survives a Length hop — there is no workaround". That conclusion was
WRONG. Re-probed six ways (plain literal, bare assignment from a Duct Size parameter, `+ 0 mm`,
inside an `if` ladder, and from another Length parameter) — **all six were accepted**. The difference in
the failing case was that it *rewired an existing parameter that already held a Duct-Size-derived
formula*, inside the same transaction, with an intermediate `Regenerate()` before the dependent formula
was set; the successful probes built the chain on freshly created parameters and regenerated once at the
end. Exact mechanism unconfirmed — but the practical rule is solid: **a formula rejection on a chain you
have just rewired is not proof the formula is impossible. Commit, then retry in a fresh call before
concluding anything**, and prefer building a new chain over re-pointing an old one.

**Chained formulas read stale in the same call that sets them.** After committing a transaction that
set `Face_Width = Neck_Width + 140 mm`, reading the second-level dependent `Face_Height = Face_Width`
in that same `run_csharp` call returned its OLD value (600mm instead of 440mm), and `Face_Area`
computed off that stale number too. A separate call with its own `Regenerate()` transaction returned
every value correctly. **Verify chained formulas in a fresh call, never in the one that created them** —
the same "fresh reads, not recall" rule this project applies to model state applies to Revit's own
formula propagation.

**`FamilyManager.MakeInstance(familyParameter)` / `MakeType(...)` flip an existing parameter's
instance/type scope in place, cleanly** — this is the safe answer to the problem the ReplaceParameter
entry above describes. Verified on two shared parameters at once: both flipped, both stayed shared, no
corruption, no need for the RemoveParameter + AddParameter dance (which loses the parameter's `Id` and
would orphan any dimension label). Prefer this whenever scope needs changing. Scope matters for
formulas: a **Type** parameter's formula cannot read an **Instance** parameter, so anything computed
from a per-instance value (flow, velocity) must itself be made Instance first.

**Clean way to run a throwaway formula test without leaving residue**: do it all forward inside one
transaction — set the input value, `SetFormula`, `Document.Regenerate()`, read the result, then
`SetFormula(param, null)` to clear and restore the input — and commit. Don't rely on `RollBack()` to
undo it (see the ReplaceParameter corruption entry above for why rollback isn't trusted here). Verify
the cleanup in the same call by reading `param.Formula` (expect null) and `param.IsDeterminedByFormula`
(expect false).

**Adding shared parameters to a family via the bridge**: `Application.OpenSharedParameterFile()` returns
the `DefinitionFile` for whatever `Application.SharedParametersFilename` points at; build a
name -> `ExternalDefinition` dictionary from `defFile.Groups[].Definitions`, then
`FamilyManager.AddParameter(externalDefinition, BuiltInParameterGroup, isInstance)`. Wrap each add in
its own try/catch and commit once at the end — a single throw would otherwise roll back every add in the
call (the all-or-nothing rule above). Revit shared parameter .txt files are **UTF-16 LE**, so reading one
outside Revit needs `iconv -f UTF-16LE` first; `OmniClassTaxonomy.txt` by contrast is plain ASCII.

### Number literals in formulas — always write the unit suffix (2026-08-26)

A bare number CAN be added to a length in a Revit formula, but it is silently interpreted in the
document's current length display unit, which makes the family unsafe to reuse in a project set to
different units. Tested live against a 300mm `Neck_Width`:

| Formula | Result |
|---|---|
| `Neck_Width + 50` | accepted -> 350mm (the bare `50` was read as mm because the doc is in mm) |
| `Neck_Width + 50 mm` | accepted -> 350mm |
| `Neck_Width + 2 * 75 mm` | accepted -> 450mm |

**Always write `50 mm`, never bare `50`.** The suffix form is what Ajmal's own production families use —
`TRG_MECH_EQP_Fan Coil Unit_FCU_R0.rfa` carries `CHW_Return_Height = Unit_Height - 50 mm` and
`Drain_Offset = Unit_Width - 200 mm`. Note this does NOT rescue the Duct Size division problem above;
that is a separate unit-chain restriction, not a literal-formatting one.

### Formula operators and functions — `<=` / `>=` do NOT exist (2026-08-26)

Revit's formula language accepts `<` and `>` but **rejects `<=` and `>=`** with the generic
`"It is an invalid formula string."` This is the single most misleading failure in formula authoring,
because a long nested expression fails as a whole and every other part of it looks suspect. Isolated
one clause at a time on a real auto-sizing ladder:

| Clause | Result |
|---|---|
| `if(Required_Neck_Size < 150 mm, 150 mm, 225 mm)` | accepted |
| `if(Required_Neck_Size <= 150 mm, 150 mm, 225 mm)` | **REJECTED** |
| `if(Auto_Size, 150 mm, Neck_Width_Manual)` — Yes/No toggle mixing a Length literal with a Duct Size parameter | accepted |
| four `if`s nested | accepted |

**Workaround for a genuine `<=`**: add a small tolerance to the threshold — `< 150.1 mm` instead of
`<= 150 mm`. Without it, a value landing exactly on a boundary falls through to the next rung, which in
a size ladder means jumping a whole size.

**`sqrt()` works directly on an area and returns a length** — no unitless juggling needed.
`sqrt(Face_Area)` on a 590x590 face returned exactly 590mm, and
`sqrt(Designed_Supply_Air_Flow / Design_Neck_Velocity)` returns a usable length. But
`sqrt(Face_Area / 1 m2) * 1 m` — the "make it unitless first" trick people reach for — is **rejected**;
Revit does not accept a `m2` literal.

**Auto-sizing an air terminal from airflow — the working pattern.** Target velocity must be its own
input parameter, never the calculated one, or the formula is circular:
```
Required_Neck_Size = sqrt(Designed_Supply_Air_Flow / Design_Neck_Velocity)
Neck_Width         = if(Auto_Size,
                        if(Required_Neck_Size < 150.1 mm, 150 mm,
                        if(Required_Neck_Size < 225.1 mm, 225 mm,
                        if(Required_Neck_Size < 300.1 mm, 300 mm,
                        if(Required_Neck_Size < 450.1 mm, 450 mm, 600 mm)))),
                        Neck_Width_Manual)
```
Because airflow is an **Instance** parameter, every parameter downstream of the size has to be made
Instance too (`MakeInstance`, above) — convert the **leaves first and the source last**, so a Type
parameter never momentarily reads an Instance one mid-transaction. Verified across 50-800 l/s: every
resulting neck velocity landed at or below the 2.5 m/s target (max 2.42), and the toggle flips cleanly
between the calculated size and the manual one.

**Design velocity reference**: ASHRAE puts supply diffuser neck velocity at <= 2.5 m/s for NC-30/RC-30;
CIBSE Guide B gives 2.5 m/s for a general office at NR 35-40 and 3 m/s for noise-sensitive spaces.
2.5 m/s is the defensible default — keep it as a parameter so it can be changed per project.

### Text in formulas — no number-to-text conversion, but `if()` can RETURN text (2026-08-26)

Revit 2020 has **no way to convert a number or length into text inside a formula**. Both of the obvious
attempts fail with the generic `"It is an invalid formula string."`:

| Attempt | Result |
|---|---|
| `Neck_Width + "X" + Neck_Height` | **rejected** |
| `"" + Neck_Width` | **rejected** |
| `"fixed text"` on a Text parameter | accepted |
| `if(Neck_Width < 225.1 mm, "225X225", "450X450")` | **accepted** |
| `if(Auto_Size, "AUTO", "MANUAL")` | accepted |

So a "450X450"-style size label cannot be *built* from the dimensions — but it can be *chosen* from a
fixed list with a nested `if`, which is fine whenever the sizes come from a known ladder. Same trick
covers any derived text field (status, classification, abbreviation).

**Boolean logic is function-form only**: `and(a, b)` works, the infix `a and b` is **rejected**. Same
applies to `or(...)` and `not(...)`.

**Guard against a wrong label.** A pure `<` ladder mislabels any off-list value — a hand-typed 380mm
neck would still read "450X450". Bracket each rung instead and fall through to a `"CUSTOM"` default:
```
if(and(Neck_Width > 149.9 mm, Neck_Width < 150.1 mm), "150X150",
if(and(Neck_Width > 224.9 mm, Neck_Width < 225.1 mm), "225X225",
 ... , "CUSTOM")))
```
Verified: auto-sized values label correctly across all five sizes, a manual 300 or 600 labels correctly,
and a manual 380 reports `CUSTOM` rather than silently lying.

### "Formula setting failed" on EVERY formula = a scope mismatch, not a syntax problem (2026-08-27)

If `SetFormula` rejects **every** formula you try on a parameter — including trivially simple ones like
`Face_Width * 0.78` — stop probing the syntax. The parameter is almost certainly **Type** while something
it references is **Instance**, and a Type parameter's formula cannot read an Instance one. `MakeInstance`
on the target fixes it instantly.

The tell is that a *content* problem rejects some formulas and accepts others; a *scope* problem rejects
all of them identically. Cost an isolation probe of six variants before the penny dropped — check
`param.IsInstance` on both sides FIRST.

**Consequence for a family that has been auto-sized**: once one parameter is made Instance, everything
downstream of it must be Instance too, and any new parameter added later that reads it must be created
as Instance (`AddParameter(def, group, isInstance: true)`), not left at the default.
