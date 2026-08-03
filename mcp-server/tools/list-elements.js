import { z } from "zod";
import { filterFields, buildElementsClause, runGenerated } from "../shared/element-filter.js";

export function register(server) {
  server.tool(
    "list_elements",
    "List the actual matching elements (Element Id + Category + Family/Type) for a category/filter, " +
      "or for a given list of Element Ids. Use this instead of count_elements when the user wants the " +
      "real items (e.g. 'show me the 300x300 VCDs'), not just a number — the Element IDs are what let a " +
      "follow-up request act on this exact set.",
    { ...filterFields, maxRows: z.number().optional().describe("Cap the number of rows returned. Defaults to 50.") },
    async ({ maxRows, ...filter }) => {
      const cap = maxRows || 50;
      const script = [
        buildElementsClause(filter),
        `foreach (var e in elements.Take(${cap})) sb.AppendLine("Id " + e.Id.IntegerValue + ": '" + e.Name + "' — " + (e.Category?.Name ?? "(no category)"));`,
        `if (elements.Count > ${cap}) sb.AppendLine("... " + (elements.Count - ${cap}) + " more not shown.");`,
        `return sb.ToString();`,
      ].join("\n");
      return runGenerated(script);
    }
  );
}
