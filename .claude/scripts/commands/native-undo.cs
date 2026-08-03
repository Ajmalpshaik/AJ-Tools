// ============================================================
// SCRIPT: native-undo.cs
// PURPOSE: Revert the last transaction using Revit's own native Undo command — never hand-write a
//          delete/fix script for a flagged "mistake". Guaranteed to fully reverse exactly what the
//          last transaction did, including renamed/modified elements a manual cleanup script would miss.
// SOURCE:  ../knowledge/live-model/undo.md § Undoing a mistake
// STATUS:  living document — refine in place, don't fork a v2 file. This one rarely needs to change.
// ============================================================
// Not flagged as destructive by the AJ AI Bridge's allowDestructive gate — it's a normal Revit
// UI command (Undo), not a Delete/Purge/file write.

var uiapp = new UIApplication(Application);
var undoId = RevitCommandId.LookupPostableCommandId(PostableCommand.Undo);
uiapp.PostCommand(undoId);

return "Posted native Undo command. Verify with a fresh element count/query afterward — don't assume the pre-mistake state without checking.";
