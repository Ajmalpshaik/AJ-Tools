// ============================================================
// SCRIPT (context) — context-project-units.cs
// PURPOSE: Report every unit spec that's actually valid for this document's discipline (Length, Area,
//          HVAC Airflow, Piping Flow, etc.) and what display unit each is currently set to — e.g. is
//          this project in mm or m, CFM or L/s. Read-only, zero input, safe to call anytime.
// GOTCHA: this project's live Revit is 2020 — uses the old `UnitType`/`DisplayUnitType` API, not the
//         2021+ `ForgeTypeId`/`UnitTypeId` API. See ../knowledge/live-model/core.md's unit-conversion notes.
// ============================================================

var sb = new System.Text.StringBuilder();
Units units = Document.GetUnits();

int reported = 0;
foreach (UnitType ut in Enum.GetValues(typeof(UnitType)))
{
    FormatOptions fo;
    try { fo = units.GetFormatOptions(ut); }
    catch { continue; } // not a valid spec for this document — skip, don't report

    string specName;
    try { specName = LabelUtils.GetLabelFor(ut); } catch { specName = ut.ToString(); }

    string unitName, symbol;
    try { unitName = LabelUtils.GetLabelFor(fo.DisplayUnits); } catch { unitName = fo.DisplayUnits.ToString(); }
    try { symbol = (fo.UnitSymbol != UnitSymbolType.UST_NONE) ? LabelUtils.GetLabelFor(fo.UnitSymbol) : ""; } catch { symbol = ""; }

    sb.AppendLine($"{specName} -> {unitName}" + (string.IsNullOrEmpty(symbol) ? "" : $" ({symbol})"));
    reported++;
}

sb.AppendLine($"---\n{reported} unit spec(s) reported (only specs valid for this document's discipline are included).");
return sb.ToString();
