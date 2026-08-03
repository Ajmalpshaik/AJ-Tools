import { z } from "zod";
import { callBridge } from "../bridge-connection.js";
import { asToolResult } from "../shared/tool-result.js";

export function register(server) {
  server.tool(
    "run_csharp",
    "Run a C# snippet against the currently open Revit document (via AJ Tools' AJ AI bridge). " +
      "Use Document/UIDocument/Application/UIApplication directly by name (same globals as the AJ AI shell). " +
      "The last expression's value (or an explicit 'return' in a script-style block) becomes the output. " +
      "Destructive operations (Delete/Purge/file writes) are refused unless allowDestructive is set to true.",
    {
      code: z.string().describe("C# script to run against the live Revit document."),
      allowDestructive: z
        .boolean()
        .optional()
        .describe("Set true to allow Delete/Purge/file-write operations. Defaults to false."),
    },
    async ({ code, allowDestructive }) => {
      try {
        const result = await callBridge(code, allowDestructive);
        return asToolResult(result);
      } catch (err) {
        return asToolResult({ success: false, error: err.message });
      }
    }
  );
}
