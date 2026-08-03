import { filterFields, viewField, buildElementsClause, buildViewClause, runGenerated } from "../shared/element-filter.js";

export function register(server) {
  server.tool(
    "reset_graphic_overrides",
    "Clear graphic overrides (color, fill pattern) on matching elements in a view.",
    { ...filterFields, ...viewField },
    async ({ targetViewId, ...filter }) => {
      const script = [
        buildElementsClause(filter),
        buildViewClause(targetViewId),
        `if (view == null) { sb.AppendLine("Target view not found."); return sb.ToString(); }`,
        `using (var t = new Transaction(Document, "AJ AI - Reset Graphic Overrides")) {`,
        `  t.Start();`,
        `  try { var blank = new OverrideGraphicSettings(); foreach (var e in elements) view.SetElementOverrides(e.Id, blank); t.Commit(); sb.AppendLine("Reset overrides on " + elements.Count + " element(s) in '" + view.Name + "'."); }`,
        `  catch (Exception ex) { t.RollBack(); sb.AppendLine("FAILED: " + ex.Message); }`,
        `}`,
        `return sb.ToString();`,
      ].join("\n");
      return runGenerated(script);
    }
  );
}
