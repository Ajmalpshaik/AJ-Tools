# Live Model Notes — index (start here, then open ONE topic)

AJ AI Bridge/live-model knowledge, split by task shape. **Read this file, pick the row that matches the
request, open that one file — don't read the whole set.** Each topic file links back here.

`core.md` is the only file worth reading alongside another: it holds the bridge rules and the
feet↔mm conversion every script needs.

## Route by what the request is asking for

| If the request is about… | Open |
|---|---|
| Running anything through the bridge at all; units (mm↔feet); Revit version differences; reading a raw category ID | [`core.md`](core.md) **← read this for any live-model task** |
| Isolating/hiding elements in a view; creating a section view | [`views.md`](views.md) |
| "What actually connects to what" — tracing pipe/duct/equipment when names, tags or `IsConnected` can't be trusted | [`mep-trace.md`](mep-trace.md) |
| "Mistake", "undo", "go back" — reversing something | [`undo.md`](undo.md) |
| Space airflow params; how many air terminals; terminal grid layout; a terminal's Flow value | [`hvac-terminals.md`](hvac-terminals.md) |
| Placing an FCU; drawing duct between points; branch duct (riser + elbow + takeoff); facing equipment toward a target; slicing a trunk for duct sizing | [`hvac-ducts.md`](hvac-ducts.md) |
| Pushing the MEP Color Data Standard (Excel) into System Types / Materials / View Filters | [`mep-color-standard.md`](mep-color-standard.md) |
| Placing tags by script; finding the right tag family; leader elbows/side; tag overlap; view scale and clearances | [`tagging.md`](tagging.md) |
| Revisions and revision sequences | [`revisions.md`](revisions.md) |
| Building a parametric family in the Family Editor (geometry, parameters, resize test) | [`families.md`](families.md) |
| What was done on a past date | [`log.md`](log.md) |

## The two rules that override everything in these files

1. **Verify, don't trust.** Revit's own data describes intent, not reality. `IsConnected`, element names
   and tags have all been proven wrong here. Get the real answer (geometry, a second property, walking
   the model) and report what you actually found — see [`mep-trace.md`](mep-trace.md) for the proof case.
2. **Fresh reads, never recall.** Ajmal edits and undoes things in Revit between messages. Re-query before
   acting on "known" state; read back after changing anything. Every number (clearance, flow, height) is a
   per-request input — never reuse a past session's value as a default.

## Before writing new C#

Check [`../../scripts/README.md`](../../scripts/README.md) first — most requests compose from existing
fragments ("which elements" + "what to do to them") instead of a fresh bespoke script.

## Adding new knowledge

Put it in the **one** topic file it belongs to, and add a row above only if it's a genuinely new topic.
Never duplicate a fact across two files. If a topic file grows past ~300 lines, split it and update this
table — the point of this folder is that no single read is expensive.
