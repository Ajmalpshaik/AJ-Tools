import { filterFields, viewField, buildElementsClause, buildViewClause, runGenerated } from "../shared/element-filter.js";

export function register(server) {
  server.tool(
    "isolate_elements",
    "Temporary-isolate matching elements in a view, resetting any prior isolation first.",
    { ...filterFields, ...viewField },
    async ({ targetViewId, ...filter }) => {
      const script = [
        buildElementsClause(filter),
        buildViewClause(targetViewId),
        `if (view == null) { sb.AppendLine("Target view not found."); return sb.ToString(); }`,
        `if (!view.CanUseTemporaryVisibilityModes()) { sb.AppendLine("View '" + view.Name + "' does not support temporary isolate."); return sb.ToString(); }`,
        `using (var t = new Transaction(Document, "AJ AI - Isolate Elements")) {`,
        `  t.Start();`,
        `  try {`,
        `    if (view.IsTemporaryHideIsolateActive()) view.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate);`,
        `    view.IsolateElementsTemporary(elements.Select(e => e.Id).ToList());`,
        `    t.Commit();`,
        `    sb.AppendLine("Isolated " + elements.Count + " element(s) in '" + view.Name + "'.");`,
        `  } catch (Exception ex) { t.RollBack(); sb.AppendLine("FAILED: " + ex.Message); }`,
        `}`,
        `return sb.ToString();`,
      ].join("\n");
      return runGenerated(script);
    }
  );
}
