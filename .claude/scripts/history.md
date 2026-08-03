# AJ Tools — Scripts: where these ideas came from

> History behind [`README.md`](README.md) / [`architecture.md`](architecture.md). Read only for the
> story behind a decision — not needed for any normal script task.

## Where these ideas came from

Two external Revit-MCP projects were reviewed for adaptable ideas (2026-07-09), evaluated against our
actual architecture — a raw C# scripting bridge, not a fixed menu of pre-built MCP tools:

- **github.com/ProfRino/Nonica-Revit-MCP-Skill** — wraps a commercial MCP server's ~50 fixed tools with
  reference docs for frontier vs. local LLMs. Not directly portable (different architecture entirely),
  but confirmed the "explorer → arguments → invoker" discipline now documented above.
- **github.com/mcp-servers-for-revit/revit-mcp-commandset** (archived) — a compiled C# MCP command set
  with real, working Revit API code for filtering, coloring, and element operations. Closer to our
  situation, but still architecturally different (compiled event handlers registered with a JSON-RPC
  server, not raw scripts run per-request) — so **no code was copied**; every fragment below was
  independently written for this project's own conventions (mm-based INPUTS blocks, `elements`/`sb`
  composition contract, existing helper patterns). What genuinely transferred was technique:
  - Grouping elements by a parameter's *actual value* (storage-type-aware: Double/ElementId/Integer
    boolean detection/String), not a hardcoded grouping rule — now `action-color-by-group.cs`'s default.
  - `View.CanUseTemporaryVisibilityModes()` instead of a hand-maintained ViewType list — now used in
    `action-isolate-elements.cs`.
  - Explicit `Transaction.RollBack()` with a clear reported reason on failure, and `TransactionGroup`
    for multi-step sequences that must succeed or fail atomically — now the standing rule above.
  - Category resolution by plain string name, `BoundingBoxIntersectsFilter` for spatial regions, and
    `FamilyInstanceFilter` for exact-type lookups — now `filter-by-category-name.cs`,
    `filter-by-region.cs`, and the fast path in `filter-by-category-and-family.cs`.
  - Transparency overrides and section-box-and-zoom as element operations we didn't have yet — now
    `action-set-transparency.cs` and `action-section-box-and-zoom.cs`.

  What was **not** taken: their JSON-schema tool registration, `ManualResetEvent`/`IExternalEventHandler`
  threading model, and polymorphic per-element-type "full info" report builder — all solve a problem
  (running inside a persistent compiled add-in behind a JSON-RPC server) that AutoDebugger's per-request
  script model doesn't have.

**Second review pass (2026-07-09), five more repos** — `mcp-servers-for-revit/mcp-servers-for-revit`
(the successor monorepo to commandset+plugin+server), `mcp-servers-for-revit/revit-mcp` (archived
Node/TS server, 27-tool catalog), `mcp-servers-for-revit/revit-mcp-plugin` (the C# socket-bridge
add-in), `shuotao/REVIT_MCP_study` (an independent, much larger project), and `DTDucas/RevitMCPSDK`
(a JSON-RPC command SDK). Again, all architecturally different from our raw-scripting bridge, so no
code was copied — only evidence-backed capability gaps and one genuinely new process idea:

- Their tool catalogs converged independently on generic element **creation** (levels, point-placed
  family instances, rooms) and **material takeoffs** as core capabilities — confirmed against Ajmal's
  own session history (past "create levels up to N" / "add N of X on level Y" requests handled ad hoc
  each time) as real, evidenced gaps, not speculative additions. Now `creators/create-levels.cs`,
  `creators/create-point-based-element.cs`, `creators/create-room.cs`, and
  `actions/action-material-takeoff.cs`.
- `shuotao/REVIT_MCP_study` independently arrived at the same three-layer split we already use here
  (domain knowledge / AI orchestration / low-level tools — maps directly onto our
  `knowledge/` / `skills/` / `scripts/`) — good external validation, nothing to change. It also runs its
  own PowerShell QA/QC pass checking documentation sync and link validity — genuinely worth adopting as
  a process, independently written for our own file layout: see
  [`.claude/tools/verify-knowledge-consistency.ps1`](../tools/verify-knowledge-consistency.ps1).
- `revit-mcp-plugin`'s `ExternalEventManager` (Revit API calls must run on Revit's main thread, so a
  socket-triggered command has to hop onto an `ExternalEvent` to touch the document safely) confirmed
  something worth stating explicitly: **AutoDebugger's `run_csharp` already handles this for us** — every
  script in this folder can call `Transaction`/`Document.Create`/etc. directly with no manual
  external-event plumbing, because the bridge itself already runs each call in a safe context. Nothing
  to add; just a reviewed-and-ruled-out item, not a silent gap.
- Explicitly **not adopted**: a generic `delete_element` action (several of these projects have one;
  this project deliberately keeps destructive operations out of reach of a script — the AutoDebugger
  bridge already blocks them by design, and `CLAUDE.md`'s "mistake"/"undo" rule routes through Revit's
  native Undo instead, on purpose); a local SQLite-style project-metadata cache (redundant with the
  knowledge-file system already serving that role — adding a second memory mechanism would fragment
  facts across two places instead of one); generic wall/floor/roof/grid/structural-framing/sheet
  creation (present in at least one of these projects, but no evidence in Ajmal's own history that he's
  asked for any of them via live scripting — building them now would be scope speculation, not a
  response to a real repeated task, contrary to house discipline).

**Third pass (2026-07-14)** — Ajmal pasted a "GROUP 1 — Zero-parameter Context Tools" list from an
external Revit MCP tool catalog (source unconfirmed — he was evaluating the idea generally, not pointing
at a specific installed server) and asked whether it was worth having. Checked first: nothing in that
list existed anywhere in this project already. Verdict: the *capability* is useful (quick, safe,
zero-risk orientation lookups), but a second MCP server/tool-per-lookup is the wrong shape for this
project — `run_csharp` can already answer every one of them, so the gap is convenience, not capability.
Closed it the same way as everything else in this folder — six new `context/` fragments (active view,
project units, warnings, worksets, model categories, used families) instead of new server-side tools.
Two items from the source list were deliberately not duplicated:
- "current selection" — already `filters/filter-by-current-selection.cs`.
- "creation tool names explorer" (14 available creation tools) — doesn't map to anything here; this
  README's own Creators table already serves that purpose for this project's architecture.

**Fourth pass (2026-07-14)** — Ajmal asked for a fresh GitHub check for anything new since the first two
passes (2026-07-09). Found the field has grown a lot: `LuDattilo/RevitCortex` (173 tools), a `LuDattilo`
fork of the monorepo (138 tools), `Demolinator/revit-mcp-server` (pyRevit-based, 48 tools, MEP/clash
focus), and `schauh11/revit-mcp-server` (WebSocket, live view/selection context, 53+ tools) were reviewed
via their published documentation (not source — same as before, different architecture, so technique
only, never code). Findings:
- **Sheets/viewports/schedules/materials are standard features everywhere** — confirms these are
  reasonable, common asks, not speculative scope. Nothing architecturally new to adopt; our filter+action
  shape already covers the equivalent ground once built.
- **Ray-casting/geometric intersection is notably ABSENT even from the most feature-rich catalog**
  (RevitCortex's 173 tools has no ray-cast or intersection tool) — so Ajmal's "snap diffuser to ceiling via
  ray-cast" idea isn't something these projects already solved either; building it would be ahead of, not
  behind, the field.
- **View crop**: at least one competitor (LuDattilo's server) doesn't have a dedicated tool for this at
  all — approximates it via `override_graphics` + `create_view_filter`. AJ Tools' own compiled View Crop
  feature is a real, dedicated tool, ahead of that implementation on this specific point.
- **Genuinely new idea worth considering (not built — touches the compiled bridge, needs Ajmal's
  sign-off first, same bar as the previously-flagged-but-not-built Roslyn pre-warming idea)**: RevitCortex
  logs every tool call to an append-only audit file (`~/.revitcortex/audit.jsonl`) and has a global
  `readOnlyMode` switch that blocks all writes regardless of what a script asks to do. Both would help
  our own "was this live-tested yet" bookkeeping, currently done by hand in this file's dated log entries.
- **Reassurance, not a gap**: at least one competitor's own docs admit "no explicit confirmation dialogs
  or rollback... users bear responsibility for backup discipline," and destructive ops aren't grouped
  into a single undo step. Our existing discipline (RollBack-on-failure per transaction, Delete/Purge/
  file-write blocked unless explicitly allowed, confirm-before-bulk) is already stricter than that.
- Not yet reviewed this pass (time-boxed to the most feature-rich ones found): `oakplank/RevitMCP`,
  `PiggyAndrew/revit_mcp`, `mcp-servers-for-revit/mcp-server-for-revit-python`, the `sam-aec` AEC Model
  Bridge. Flagged here rather than silently skipped — check these if a future pass has more budget.

