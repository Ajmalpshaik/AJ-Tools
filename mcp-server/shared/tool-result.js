// Wraps a bridge response into the MCP content/isError shape every tool returns.

export function asToolResult(result) {
  return {
    content: [{ type: "text", text: JSON.stringify(result, null, 2) }],
    isError: result?.Success === false || result?.success === false,
  };
}
