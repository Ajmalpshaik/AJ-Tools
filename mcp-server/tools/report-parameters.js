import { z } from "zod";
import { filterFields, buildElementsClause, runGenerated, cs } from "../shared/element-filter.js";

export function register(server) {
  server.tool(
    "report_parameters",
    "Table of named parameter values for matching elements, including each Element ID.",
    { ...filterFields, parameterNames: z.array(z.string()).describe("Parameter display names to report, e.g. ['Family and Type','Level','Mark']."), maxRows: z.number().optional() },
    async ({ parameterNames, maxRows, ...filter }) => {
      const cap = maxRows || 50;
      const namesArray = parameterNames.map((n) => cs(n)).join(", ");
      const script = [
        buildElementsClause(filter),
        `string[] __names = new string[] { ${namesArray} };`,
        `sb.AppendLine("Id | " + string.Join(" | ", __names));`,
        `foreach (var e in elements.Take(${cap})) {`,
        `  var __row = new List<string> { e.Id.IntegerValue.ToString() };`,
        `  foreach (var __n in __names) { var p = e.LookupParameter(__n); __row.Add(p != null && p.HasValue ? (p.AsValueString() ?? p.AsString() ?? "") : ""); }`,
        `  sb.AppendLine(string.Join(" | ", __row));`,
        `}`,
        `if (elements.Count > ${cap}) sb.AppendLine("... " + (elements.Count - ${cap}) + " more not shown.");`,
        `return sb.ToString();`,
      ].join("\n");
      return runGenerated(script);
    }
  );
}
