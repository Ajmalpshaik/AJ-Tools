import { z } from "zod";
import { filterFields, buildElementsClause, runGenerated } from "../shared/element-filter.js";

export function register(server) {
  server.tool(
    "delete_elements",
    "Permanently delete matching elements. HIGHEST RISK tool in this set — confirm the real count with " +
      "the user (e.g. via list_elements or count_elements) before calling this, every time.",
    { ...filterFields, confirm: z.literal(true).describe("Must be exactly true — the schema itself refuses this call without an explicit confirmation.") },
    async ({ confirm, ...filter }) => {
      const script = [
        buildElementsClause(filter),
        `int __deleted = 0, __skipped = 0;`,
        `using (var t = new Transaction(Document, "AJ AI - Delete Elements")) {`,
        `  t.Start();`,
        `  try {`,
        `    foreach (var e in elements) { try { if (!Document.GetElement(e.Id).IsValidObject) { __skipped++; continue; } Document.Delete(e.Id); __deleted++; } catch { __skipped++; } }`,
        `    t.Commit();`,
        `    sb.AppendLine("Deleted " + __deleted + " element(s), skipped " + __skipped + ".");`,
        `  } catch (Exception ex) { t.RollBack(); sb.AppendLine("FAILED: " + ex.Message); }`,
        `}`,
        `return sb.ToString();`,
      ].join("\n");
      return runGenerated(script, true);
    }
  );
}
