import { filterFields, viewField, buildElementsClause, buildViewClause, runGenerated } from "../shared/element-filter.js";

export function register(server) {
  server.tool(
    "unhide_elements",
    "Reverse a PERMANENT per-element hide on matching elements. Not for temporary isolate/hide — use " +
      "reset_isolation for that.",
    { ...filterFields, ...viewField },
    async ({ targetViewId, ...filter }) => {
      const script = [
        buildElementsClause(filter),
        buildViewClause(targetViewId),
        `if (view == null) { sb.AppendLine("Target view not found."); return sb.ToString(); }`,
        `using (var t = new Transaction(Document, "AJ AI - Unhide Elements")) {`,
        `  t.Start();`,
        `  try { view.UnhideElements(elements.Select(e => e.Id).ToList()); t.Commit(); sb.AppendLine("Unhid " + elements.Count + " element(s) in '" + view.Name + "'."); }`,
        `  catch (Exception ex) { t.RollBack(); sb.AppendLine("FAILED: " + ex.Message); }`,
        `}`,
        `return sb.ToString();`,
      ].join("\n");
      return runGenerated(script);
    }
  );
}
