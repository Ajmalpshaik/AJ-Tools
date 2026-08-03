# Temporary AJ AI Bridge Queries

Use this folder only for short-lived C# files sent to the live Revit bridge when a direct MCP call is
not available. These files are not AJ Tools source code and are not reusable scripts.

- One-time, small query: create it here, run it, then delete it.
- Repeated or substantial workflow: move the reusable filter/action/recipe into `../scripts/` and add it
  to that folder's README.
- Never place generated query files in the repository root or under `src/`.
