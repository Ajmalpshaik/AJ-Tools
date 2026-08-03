import { z } from "zod";
import { filterFields, viewField, buildElementsClause, buildViewClause, runGenerated } from "../shared/element-filter.js";

export function register(server) {
  server.tool(
    "set_transparency",
    "Set surface transparency (0-100%) on matching elements in a view.",
    { ...filterFields, ...viewField, percent: z.number().min(0).max(100).describe("0 = opaque, 100 = fully transparent") },
    async ({ targetViewId, percent, ...filter }) => {
      const clamped = Math.max(0, Math.min(100, Math.round(percent)));
      const script = [
        buildElementsClause(filter),
        buildViewClause(targetViewId),
        `if (view == null) { sb.AppendLine("Target view not found."); return sb.ToString(); }`,
        `using (var t = new Transaction(Document, "AJ AI - Set Transparency")) {`,
        `  t.Start();`,
        `  try { foreach (var e in elements) { var ogs = view.GetElementOverrides(e.Id); ogs.SetSurfaceTransparency(${clamped}); view.SetElementOverrides(e.Id, ogs); } t.Commit(); sb.AppendLine("Set ${clamped}% transparency on " + elements.Count + " element(s) in '" + view.Name + "'."); }`,
        `  catch (Exception ex) { t.RollBack(); sb.AppendLine("FAILED: " + ex.Message); }`,
        `}`,
        `return sb.ToString();`,
      ].join("\n");
      return runGenerated(script);
    }
  );
}
