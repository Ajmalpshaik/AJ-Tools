import { viewField, buildViewClause, runGenerated } from "../shared/element-filter.js";

export function register(server) {
  server.tool(
    "reset_isolation",
    "Clear Temporary Hide/Isolate in a view — the direct equivalent of Revit's 'Reset Temporary Hide/Isolate'.",
    viewField,
    async ({ targetViewId }) => {
      const script = [
        buildViewClause(targetViewId),
        `var sb = new System.Text.StringBuilder();`,
        `if (view == null) { sb.AppendLine("Target view not found."); return sb.ToString(); }`,
        `using (var t = new Transaction(Document, "AJ AI - Reset Isolation")) {`,
        `  t.Start();`,
        `  try { if (view.IsTemporaryHideIsolateActive()) view.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate); t.Commit(); sb.AppendLine("Reset temporary hide/isolate in '" + view.Name + "'."); }`,
        `  catch (Exception ex) { t.RollBack(); sb.AppendLine("FAILED: " + ex.Message); }`,
        `}`,
        `return sb.ToString();`,
      ].join("\n");
      return runGenerated(script);
    }
  );
}
