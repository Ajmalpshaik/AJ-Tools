# AJ AI Bridge — MCP tools index

One file per tool. Read this table, open the one file you need — don't read the whole folder. Every
tool is registered from `../index.js`; nothing here runs on its own.

## Original tools (flexible, always available)
| Tool | File | Job |
|---|---|---|
| `run_csharp` | [`run-csharp.js`](run-csharp.js) | Run any C# against the live document — the fallback for anything the native tools below don't cover |
| `ping` | [`ping.js`](ping.js) | Check the bridge is connected |
| `model_summary` | [`model-summary.js`](model-summary.js) | Fast count/breakdown for a fixed set of common MEP categories |

## Native tools (typed, schema-validated — added 2026-07-22)
Each generates the same proven C# pattern as the matching `../../scripts/` fragment, via the shared
generator in [`../shared/element-filter.js`](../shared/element-filter.js).

| Tool | File | Job |
|---|---|---|
| `list_elements` | [`list-elements.js`](list-elements.js) | Real items (Id + Category + Family/Type), not just a count |
| `count_elements` | [`count-elements.js`](count-elements.js) | Bare count, any category |
| `hide_elements` | [`hide-elements.js`](hide-elements.js) | Temp or permanent hide |
| `unhide_elements` | [`unhide-elements.js`](unhide-elements.js) | Reverse a permanent hide |
| `isolate_elements` | [`isolate-elements.js`](isolate-elements.js) | Temporary isolate |
| `reset_isolation` | [`reset-isolation.js`](reset-isolation.js) | Clear temporary hide/isolate |
| `set_color` | [`set-color.js`](set-color.js) | RGB line + solid fill override |
| `reset_graphic_overrides` | [`reset-graphic-overrides.js`](reset-graphic-overrides.js) | Clear overrides |
| `set_transparency` | [`set-transparency.js`](set-transparency.js) | 0-100% surface transparency |
| `select_elements` | [`select-elements.js`](select-elements.js) | Set the active Revit selection |
| `set_parameter_value` | [`set-parameter-value.js`](set-parameter-value.js) | Bulk-set one parameter |
| `report_parameters` | [`report-parameters.js`](report-parameters.js) | Parameter table with Element IDs |
| `move_elements` | [`move-elements.js`](move-elements.js) | Translate by an mm offset |
| `delete_elements` | [`delete-elements.js`](delete-elements.js) | Permanent delete — schema requires `confirm: true` |

## Adding a new native tool
1. Copy the shape of an existing simple one (`hide-elements.js` for an action, `count-elements.js` for
   a query).
2. Import what you need from `../shared/element-filter.js` (`filterFields`, `viewField`,
   `buildElementsClause`, `buildViewClause`, `runGenerated`, `cs`).
3. Export a `register(server)` function that calls `server.tool(...)`.
4. Wire it into `../index.js` (import + call).
5. Add a row to this table.
6. `node --check` the new file, then the whole folder, before considering it done.
