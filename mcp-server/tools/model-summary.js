import { z } from "zod";
import { callBridge } from "../bridge-connection.js";
import { asToolResult } from "../shared/tool-result.js";

const MODEL_SUMMARY_TARGETS = {
  ducts: { builtInCategory: "OST_DuctCurves", label: "Duct Curves" },
  flex_ducts: { builtInCategory: "OST_FlexDuctCurves", label: "Flex Ducts" },
  air_terminals: { builtInCategory: "OST_DuctTerminal", label: "Air Terminals" },
  pipes: { builtInCategory: "OST_PipeCurves", label: "Pipes" },
  duct_fittings: { builtInCategory: "OST_DuctFitting", label: "Duct Fittings" },
  pipe_fittings: { builtInCategory: "OST_PipeFitting", label: "Pipe Fittings" },
  mechanical_equipment: { builtInCategory: "OST_MechanicalEquipment", label: "Mechanical Equipment" },
};

function buildModelSummaryScript(target, parameterName) {
  const parameterLiteral = parameterName ? JSON.stringify(parameterName) : "null";

  return `var sb = new System.Text.StringBuilder();
string requestedParameter = ${parameterLiteral};
var groups = new System.Collections.Generic.Dictionary<string, int>();
int count = 0;

foreach (Element element in new FilteredElementCollector(Document)
    .OfCategory(BuiltInCategory.${target.builtInCategory})
    .WhereElementIsNotElementType())
{
    count++;
    if (requestedParameter == null) continue;

    string value = "Unknown";
    var parameter = element.LookupParameter(requestedParameter);
    if (parameter != null)
    {
        if (parameter.StorageType == StorageType.Double)
        {
            double mm = UnitUtils.ConvertFromInternalUnits(
                parameter.AsDouble(), DisplayUnitType.DUT_MILLIMETERS);
            value = Math.Round(mm) + " mm";
        }
        else if (parameter.StorageType == StorageType.String)
        {
            value = parameter.AsString() ?? "(blank)";
        }
        else
        {
            value = parameter.AsValueString() ?? "(not set)";
        }
    }

    if (!groups.ContainsKey(value)) groups[value] = 0;
    groups[value]++;
}

sb.AppendLine("REVIT=" + Application.VersionName);
sb.AppendLine("MODEL=" + Document.Title);
sb.AppendLine("CATEGORY=${target.label}");
sb.AppendLine("COUNT=" + count);

if (requestedParameter != null)
{
    sb.AppendLine("BREAKDOWN_BY=" + requestedParameter);
    var orderedGroups = new System.Collections.Generic.List<
        System.Collections.Generic.KeyValuePair<string, int>>(groups);
    orderedGroups.Sort((left, right) =>
    {
        int quantityOrder = right.Value.CompareTo(left.Value);
        return quantityOrder != 0
            ? quantityOrder
            : string.Compare(left.Key, right.Key, StringComparison.Ordinal);
    });

    foreach (var group in orderedGroups)
        sb.AppendLine("QTY=" + group.Value + " | VALUE=" + group.Key);
}

return sb.ToString();`;
}

export function register(server) {
  server.tool(
    "model_summary",
    "Fast, read-only live-model count for a common Revit category. Optionally group the result by one " +
      "parameter, such as duct Height. Use this instead of a separate ping plus generated C# for normal " +
      "'how many' and single-dimension questions.",
    {
      category: z
        .enum(Object.keys(MODEL_SUMMARY_TARGETS))
        .describe("The Revit category to count."),
      parameter: z
        .enum(["Height", "Width", "Diameter", "Size"])
        .optional()
        .describe("Optional parameter to group by. Omit for a count only."),
    },
    async ({ category, parameter }) => {
      try {
        const result = await callBridge(
          buildModelSummaryScript(MODEL_SUMMARY_TARGETS[category], parameter),
          false
        );
        return asToolResult(result);
      } catch (err) {
        return asToolResult({ Success: false, Error: err.message });
      }
    }
  );
}
