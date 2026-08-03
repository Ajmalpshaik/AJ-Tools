import { z } from "zod";
import { filterFields, viewField, buildElementsClause, buildViewClause, runGenerated } from "../shared/element-filter.js";

export function register(server) {
  server.tool(
    "hide_elements",
    "Hide matching elements in a view — temporary by default (Reset Temporary Hide/Isolate clears it), " +
      "or permanent if requested.",
    { ...filterFields, ...viewField, permanent: z.boolean().optional().describe("true = permanent View.HideElements. Defaults to false (temporary).") },
    async ({ targetViewId, permanent, ...filter }) => {
      const script = [
        buildElementsClause(filter),
        buildViewClause(targetViewId),
        `if (view == null) { sb.AppendLine("Target view not found."); return sb.ToString(); }`,
        `using (var t = new Transaction(Document, "AJ AI - Hide Elements")) {`,
        `  t.Start();`,
        `  try {`,
        `    var ids = elements.Select(e => e.Id).ToList();`,
        `    if (${permanent ? "true" : "false"}) view.HideElements(ids); else view.HideElementsTemporary(ids);`,
        `    t.Commit();`,
        `    sb.AppendLine("Hid " + elements.Count + " element(s) " + (${permanent ? "true" : "false"} ? "permanently" : "temporarily") + " in '" + view.Name + "'.");`,
        `  } catch (Exception ex) { t.RollBack(); sb.AppendLine("FAILED: " + ex.Message); }`,
        `}`,
        `return sb.ToString();`,
      ].join("\n");
      return runGenerated(script);
    }
  );
}
