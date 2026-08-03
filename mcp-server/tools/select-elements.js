import { filterFields, buildElementsClause, runGenerated } from "../shared/element-filter.js";

export function register(server) {
  server.tool(
    "select_elements",
    "Set matching elements as the active Revit selection, visible to the user in the UI.",
    filterFields,
    async (filter) => {
      const script = [
        buildElementsClause(filter),
        `UIDocument.Selection.SetElementIds(elements.Select(e => e.Id).ToList());`,
        `sb.AppendLine("Selected " + elements.Count + " element(s) in Revit.");`,
        `return sb.ToString();`,
      ].join("\n");
      return runGenerated(script);
    }
  );
}
