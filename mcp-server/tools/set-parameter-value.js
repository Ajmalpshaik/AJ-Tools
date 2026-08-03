import { z } from "zod";
import { filterFields, buildElementsClause, runGenerated, cs } from "../shared/element-filter.js";

export function register(server) {
  server.tool(
    "set_parameter_value",
    "Bulk-set one named parameter to one value across matching elements. Provide exactly one of " +
      "stringValue or numericValueMm.",
    { ...filterFields, parameterNameToSet: z.string().describe("The parameter to write."), stringValue: z.string().optional(), numericValueMm: z.number().optional() },
    async ({ parameterNameToSet, stringValue, numericValueMm, ...filter }) => {
      const script = [
        buildElementsClause(filter),
        `int __updated = 0, __skipped = 0;`,
        `using (var t = new Transaction(Document, "AJ AI - Set Parameter Value")) {`,
        `  t.Start();`,
        `  try {`,
        `    foreach (var e in elements) {`,
        `      var p = e.LookupParameter(${cs(parameterNameToSet)});`,
        `      if (p == null || p.IsReadOnly) { __skipped++; continue; }`,
        numericValueMm !== undefined && numericValueMm !== null
          ? `      if (p.StorageType == StorageType.Double) { p.Set(UnitUtils.ConvertToInternalUnits(${Number(numericValueMm)}, DisplayUnitType.DUT_MILLIMETERS)); __updated++; } else { __skipped++; }`
          : `      if (p.StorageType == StorageType.String) { p.Set(${cs(stringValue ?? "")}); __updated++; } else { __skipped++; }`,
        `    }`,
        `    t.Commit();`,
        `    sb.AppendLine("Set '" + ${cs(parameterNameToSet)} + "' on " + __updated + " element(s), skipped " + __skipped + ".");`,
        `  } catch (Exception ex) { t.RollBack(); sb.AppendLine("FAILED: " + ex.Message); }`,
        `}`,
        `return sb.ToString();`,
      ].join("\n");
      return runGenerated(script);
    }
  );
}
