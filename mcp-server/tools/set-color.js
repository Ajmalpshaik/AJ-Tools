import { z } from "zod";
import { filterFields, viewField, buildElementsClause, buildViewClause, runGenerated } from "../shared/element-filter.js";

export function register(server) {
  server.tool(
    "set_color",
    "Apply one RGB color (line + solid surface fill) to matching elements in a view.",
    { ...filterFields, ...viewField, r: z.number().min(0).max(255).describe("Red 0-255"), g: z.number().min(0).max(255).describe("Green 0-255"), b: z.number().min(0).max(255).describe("Blue 0-255") },
    async ({ targetViewId, r, g, b, ...filter }) => {
      const script = [
        buildElementsClause(filter),
        buildViewClause(targetViewId),
        `if (view == null) { sb.AppendLine("Target view not found."); return sb.ToString(); }`,
        `var __fill = new FilteredElementCollector(Document).OfClass(typeof(FillPatternElement)).Cast<FillPatternElement>().FirstOrDefault(f => f.GetFillPattern().IsSolidFill);`,
        `using (var t = new Transaction(Document, "AJ AI - Set Color")) {`,
        `  t.Start();`,
        `  try {`,
        `    var color = new Autodesk.Revit.DB.Color((byte)${Math.round(r)}, (byte)${Math.round(g)}, (byte)${Math.round(b)});`,
        `    foreach (var e in elements) {`,
        `      var ogs = view.GetElementOverrides(e.Id);`,
        `      ogs.SetProjectionLineColor(color); ogs.SetCutLineColor(color);`,
        `      if (__fill != null) { ogs.SetSurfaceForegroundPatternColor(color); ogs.SetSurfaceForegroundPatternId(__fill.Id); ogs.SetSurfaceForegroundPatternVisible(true); ogs.SetCutForegroundPatternColor(color); ogs.SetCutForegroundPatternId(__fill.Id); ogs.SetCutForegroundPatternVisible(true); }`,
        `      view.SetElementOverrides(e.Id, ogs);`,
        `    }`,
        `    t.Commit();`,
        `    sb.AppendLine("Colored " + elements.Count + " element(s) RGB(${Math.round(r)},${Math.round(g)},${Math.round(b)}) in '" + view.Name + "'.");`,
        `  } catch (Exception ex) { t.RollBack(); sb.AppendLine("FAILED: " + ex.Message); }`,
        `}`,
        `return sb.ToString();`,
      ].join("\n");
      return runGenerated(script);
    }
  );
}
