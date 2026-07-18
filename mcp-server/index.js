// AJ Tools AJ AI Bridge MCP server.
//
// Bridges an MCP client (an AI agent) to a live Revit session. AJ Tools hosts a local named-pipe
// server (McpBridgeService, inside the AiShell module) that this script talks to whenever the
// "Connect AJ AI Bridge" toggle in the AJ AI pane is switched on. The MCP process keeps one
// authenticated pipe connection open between requests, then recreates it when Revit reconnects.
//
// Tools exposed:
//   - run_csharp: run a C# snippet against the open Revit document and return the result.
//   - ping:       quick check that Revit is open and the bridge is listening.
//   - model_summary: fast read-only count, with an optional parameter breakdown.

import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { z } from "zod";
import net from "node:net";
import fs from "node:fs";
import path from "node:path";

const DISCOVERY_FILE = path.join(process.env.APPDATA || "", "AJTools", "ajai-bridge.json");
// RevitExecutionService soft-cancels a loop-based script at 60s, then gives it a further 20s grace
// period to actually unwind before its own hard backstop gives up (see that file's HardWaitTimeout,
// 80s total). This must stay comfortably above that 80s, or a script that's still legitimately
// unwinding gets reported here as "timed out" even though Revit would have finished it normally a
// few seconds later.
const RESPONSE_TIMEOUT_MS = 90_000;
const CONNECT_TIMEOUT_MS = 10_000;

let cachedDiscovery;
let activeConnection;

function readDiscoveryInfo() {
  if (!fs.existsSync(DISCOVERY_FILE)) {
    cachedDiscovery = undefined;
    throw new Error(
      "AJ AI Bridge is not connected. In Revit, open the AJ AI pane and click \"Connect AJ AI Bridge\", then try again."
    );
  }

  const stat = fs.statSync(DISCOVERY_FILE);
  if (
    cachedDiscovery &&
    cachedDiscovery.mtimeMs === stat.mtimeMs &&
    cachedDiscovery.size === stat.size
  ) {
    return cachedDiscovery.info;
  }

  const raw = fs.readFileSync(DISCOVERY_FILE, "utf8");
  const info = JSON.parse(raw);
  if (!info.pipeName || !info.token) {
    throw new Error("AJ AI bridge connection file is malformed. Reconnect from the AJ AI pane in Revit.");
  }

  cachedDiscovery = { mtimeMs: stat.mtimeMs, size: stat.size, info };
  return info;
}

function connectionKey(info) {
  return `${info.pipeName}\u0000${info.token}`;
}

function detachConnection(connection, error) {
  if (!connection || connection.closed) return;

  connection.closed = true;
  if (activeConnection === connection) activeConnection = undefined;

  if (connection.pending) {
    const pending = connection.pending;
    connection.pending = undefined;
    clearTimeout(pending.timer);
    pending.reject(error);
  }
}

function closeConnection(connection, reason) {
  if (!connection || connection.closed) return;

  detachConnection(connection, new Error(reason));
  if (!connection.socket.destroyed) connection.socket.destroy();
}

function createConnection(info) {
  return new Promise((resolve, reject) => {
    const pipePath = `\\\\.\\pipe\\${info.pipeName}`;
    const socket = net.connect({ path: pipePath });
    const connection = {
      key: connectionKey(info),
      socket,
      buffer: "",
      pending: undefined,
      closed: false,
    };

    let connected = false;
    let connectSettled = false;

    const connectTimer = setTimeout(() => {
      if (connectSettled) return;
      connectSettled = true;
      socket.destroy();
      reject(new Error("Timed out connecting to the AJ AI bridge. Is Revit busy or disconnected?"));
    }, CONNECT_TIMEOUT_MS);

    socket.setNoDelay(true);

    socket.once("connect", () => {
      connected = true;
      connectSettled = true;
      clearTimeout(connectTimer);
      activeConnection = connection;
      resolve(connection);
    });

    socket.on("data", (chunk) => {
      connection.buffer += chunk.toString("utf8");

      while (true) {
        const newlineIndex = connection.buffer.indexOf("\n");
        if (newlineIndex === -1) return;

        const line = connection.buffer.slice(0, newlineIndex);
        connection.buffer = connection.buffer.slice(newlineIndex + 1);
        const pending = connection.pending;
        if (!pending) {
          closeConnection(connection, "Received an unexpected AJ AI bridge response.");
          return;
        }

        try {
          const response = JSON.parse(line);
          connection.pending = undefined;
          clearTimeout(pending.timer);
          // Defer by one event-loop turn so a legacy one-request server can close cleanly
          // before the next queued request decides whether to reuse this connection.
          setImmediate(() => pending.resolve(response));
        } catch (err) {
          closeConnection(connection, "Could not parse the AJ AI bridge response: " + err.message);
          return;
        }
      }
    });

    socket.on("error", (err) => {
      const error = err.code === "ENOENT"
        ? new Error("Could not reach the AJ AI bridge (pipe not found). It may have been disconnected or Revit was closed. Reconnect from the AJ AI pane.")
        : err;

      if (!connected && !connectSettled) {
        connectSettled = true;
        clearTimeout(connectTimer);
        reject(error);
      }

      detachConnection(connection, error);
    });

    socket.on("end", () => detachConnection(connection, new Error("The AJ AI bridge closed the pipe connection.")));
    socket.on("close", () => {
      if (!connected && !connectSettled) {
        connectSettled = true;
        clearTimeout(connectTimer);
        reject(new Error("The AJ AI bridge closed before the connection was established."));
      }
      detachConnection(connection, new Error("The AJ AI bridge closed the pipe connection."));
    });
  });
}

async function getConnection(info) {
  const key = connectionKey(info);
  if (
    activeConnection &&
    !activeConnection.closed &&
    !activeConnection.socket.destroyed &&
    activeConnection.socket.writable &&
    activeConnection.key === key
  ) {
    return activeConnection;
  }

  if (activeConnection) {
    closeConnection(activeConnection, "AJ AI bridge connection details changed.");
  }

  return createConnection(info);
}

function sendRequest(connection, info, code, allowDestructive) {
  if (connection.closed || connection.socket.destroyed || !connection.socket.writable) {
    return Promise.reject(new Error("The AJ AI bridge connection is no longer available."));
  }
  if (connection.pending) {
    return Promise.reject(new Error("An AJ AI bridge request is already in progress on this connection."));
  }

  return new Promise((resolve, reject) => {
    const timer = setTimeout(() => {
      if (!connection.pending) return;
      closeConnection(connection, "Timed out waiting for Revit to respond. Is Revit busy or unresponsive?");
    }, RESPONSE_TIMEOUT_MS);

    connection.pending = { resolve, reject, timer };
    try {
      connection.socket.write(JSON.stringify({ token: info.token, code, allowDestructive: !!allowDestructive }) + "\n");
    } catch (err) {
      closeConnection(connection, err.message);
    }
  });
}

async function callBridgeNow(code, allowDestructive) {
  const info = readDiscoveryInfo();
  const connection = await getConnection(info);
  return sendRequest(connection, info, code, allowDestructive);
}

// Revit runs API work on one ExternalEvent at a time. Serializing calls preserves that contract while
// the underlying named-pipe connection stays open between requests.
let bridgeCallQueue = Promise.resolve();

function callBridge(code, allowDestructive) {
  const nextCall = bridgeCallQueue.then(() => callBridgeNow(code, allowDestructive));
  bridgeCallQueue = nextCall.catch(() => undefined);
  return nextCall;
}

function asToolResult(result) {
  return {
    content: [{ type: "text", text: JSON.stringify(result, null, 2) }],
    isError: result?.Success === false || result?.success === false,
  };
}

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

const server = new McpServer({ name: "aj-tools-aj-ai", version: "1.2.0" });

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

server.tool(
  "ping",
  "Check whether Revit is open and the AJ AI bridge is connected and responding.",
  {},
  async () => {
    try {
      const result = await callBridge('"pong"', false);
      return asToolResult(result);
    } catch (err) {
      return asToolResult({ success: false, error: err.message });
    }
  }
);

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

const transport = new StdioServerTransport();
await server.connect(transport);
