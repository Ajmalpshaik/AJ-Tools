# Live Model — Revisions

> Part of the live-model knowledge set. Index: [`README.md`](README.md) — go back there to route to another topic.

## Revision sequence auto-purges unused revisions — attach before you trust it'll stay
- **Proof case**: created 8 project-level Revisions (`Revision.Create`) for sheet text-note dates, then
  in a *separate* later transaction called `ViewSheet.SetAdditionalRevisionIds(...)` to attach them to
  matching sheets. A fresh read-back right after showed the project's two PRE-EXISTING revisions (one
  dated `2020/12/26`, one entirely blank — neither ever attached to any sheet or revision cloud) had
  vanished from both `Revision.GetAllRevisionIds(doc)` and a raw `FilteredElementCollector(doc)
  .OfClass(typeof(Revision))` — genuinely gone as elements, not just filtered out of a summary list.
  `Revision.SequenceNumber` also silently renumbered from 1..10 down to 1..8 once the two orphans were
  swept, so a Seq number captured before this kind of operation cannot be trusted afterward either —
  match revisions by a property you set yourself (Description/IssuedTo/IssuedBy/RevisionDate), not Seq.
- **Root cause (best evidence, not confirmed by Autodesk docs)**: any Revision never referenced by a
  cloud AND never added via `SetAdditionalRevisionIds` on any sheet appears to be an "orphan" that Revit
  purges the next time something forces the revision sequence to recompute (observed trigger: calling
  `SetAdditionalRevisionIds` on a sheet). It did NOT happen right after `Revision.Create` alone — the
  fresh read-back immediately after creating the 8 revisions still showed all 10 (8 new + 2 old orphans)
  intact. It was the *next* transaction, the one that touched sheet-revision association, that triggered
  the purge project-wide — not scoped to just the sheet being edited.
  In this case Ajmal confirmed the two orphans were already stale/unwanted (his own prior cleanup), so no
  recovery was needed — but treat this as a real, silent, project-wide side effect every time, not a
  one-off. **Before creating a Revision meant to persist without being placed on a sheet immediately**,
  either attach it to at least one sheet in the same transaction, or warn Ajmal it may not survive the
  next sheet-revision-touching operation.
- **Verification technique**: never trust `Revision.GetAllRevisionIds(doc)` or a Seq number read
  *inside* the same transaction that also called `SetAdditionalRevisionIds` — mid-transaction reads of
  `SequenceNumber` were inconsistent (the same date showed different Seq numbers depending which sheet's
  loop iteration had run so far). Always re-query fresh (a separate `run_csharp` call, after commit) and
  match by a Revision's own set data (date/description/issuedTo/issuedBy), never by Seq captured earlier.

