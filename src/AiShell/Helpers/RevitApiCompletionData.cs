#region Metadata
/*
 * Tool Name     : C#
 * File Name     : RevitApiCompletionData.cs
 * Purpose       : Ctrl+Space autocomplete for the C# Code Editor - a curated, static list of the
 *                 Revit API types/members/snippets a modeler is most likely to need, shown in an
 *                 AvalonEdit CompletionWindow. This is RevitPythonShell's "autocompletion (press
 *                 CTRL+SPACE after a period)" feature, scaled down on purpose: full semantic
 *                 IntelliSense needs a live Roslyn Workspace/CompletionService wired to the editor's
 *                 text on every keystroke - real plumbing that cannot be verified without a local
 *                 Revit + Visual Studio build/test loop. A fixed list needs no workspace at all, so
 *                 it carries none of that risk while still answering "what do I type" for the
 *                 globals, types, and patterns used constantly in this tool's own generated scripts.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-07-21
 * Last Updated  : 2026-07-21
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in (AvalonEdit CodeCompletion)
 *
 * Dependencies  : AvalonEdit (ICSharpCode.AvalonEdit.CodeCompletion/Document)
 *
 * Input         : None (static list) - AiShellView wires up the Ctrl+Space trigger and insertion.
 * Output        : ICompletionData entries for AvalonEdit's CompletionWindow.
 *
 * Notes         :
 * - Deliberately not context-sensitive (does not know if the caret is after a "." or what type
 *   precedes it) - AvalonEdit's CompletionList already filters this static set live as the user
 *   keeps typing after the window opens, which covers the common case well enough without a
 *   semantic model.
 * - Snippet entries insert multi-line text (e.g. a Transaction block); simple entries insert just
 *   the type/member name.
 *
 * Changelog     :
 * v1.0.0 (2026-07-21) - Initial release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Collections.Generic;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;

namespace AJTools.AiShell.Helpers
{
    /// <summary>One entry in the Ctrl+Space completion list - a name/snippet plus a short
    /// description, inserted verbatim at the caret (replacing whatever partial word triggered the
    /// filter) when picked.</summary>
    public class RevitApiCompletionData : ICompletionData
    {
        private readonly string _insertText;

        public RevitApiCompletionData(string displayText, string description, string insertText = null)
        {
            Text = displayText;
            Description = description;
            _insertText = insertText ?? displayText;
        }

        public ImageSource Image => null;
        public string Text { get; }
        public object Content => Text;
        public object Description { get; }
        public double Priority => 0;

        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
        {
            textArea.Document.Replace(completionSegment, _insertText);
        }

        /// <summary>The curated list shown on Ctrl+Space - Revit script globals, the types/members
        /// used constantly in this tool's own AI-generated scripts, and a couple of full snippets
        /// for the boilerplate that's easy to get slightly wrong by hand (a Transaction block, a
        /// category-filtered collector).</summary>
        public static IEnumerable<ICompletionData> GetDefaultList()
        {
            return new List<ICompletionData>
            {
                // Script globals (RevitScriptGlobals) - always available, never "Document.Document".
                new RevitApiCompletionData("Document", "The active Revit document (Document)."),
                new RevitApiCompletionData("UIDocument", "The active UI document (UIDocument) - selection, active view."),
                new RevitApiCompletionData("Application", "The Revit Application (Autodesk.Revit.ApplicationServices)."),
                new RevitApiCompletionData("UIApplication", "The Revit UIApplication."),
                new RevitApiCompletionData("ReportProgress", "Action<int,string> - call to update the progress bar, e.g. ReportProgress(50, \"Halfway...\")."),

                // Common snippets - the boilerplate most likely to have a small typo by hand.
                new RevitApiCompletionData(
                    "Transaction block",
                    "A Transaction wrapping a model change.",
                    "using (Transaction t = new Transaction(Document, \"Name\"))\n{\n    t.Start();\n\n    t.Commit();\n}"),
                new RevitApiCompletionData(
                    "FilteredElementCollector (category)",
                    "Collect all instances of one category in the document.",
                    "new FilteredElementCollector(Document).OfCategory(BuiltInCategory.OST_Walls).WhereElementIsNotElementType().ToElements()"),
                new RevitApiCompletionData(
                    "FilteredElementCollector (class)",
                    "Collect all elements of one .NET type in the document.",
                    "new FilteredElementCollector(Document).OfClass(typeof(Wall)).ToElements()"),
                new RevitApiCompletionData(
                    "Selected elements",
                    "The currently selected element ids in the active view.",
                    "UIDocument.Selection.GetElementIds()"),

                // Frequently used types/namespaces.
                new RevitApiCompletionData("FilteredElementCollector", "Queries elements in a document."),
                new RevitApiCompletionData("ElementId", "Identifies a Revit element."),
                new RevitApiCompletionData("BuiltInCategory", "Enum of built-in Revit categories, e.g. BuiltInCategory.OST_Walls."),
                new RevitApiCompletionData("BuiltInParameter", "Enum of built-in Revit parameters."),
                new RevitApiCompletionData("Parameter", "A single element or type parameter."),
                new RevitApiCompletionData("Transaction", "Wraps one undoable change to the model."),
                new RevitApiCompletionData("TransactionGroup", "Groups several Transactions into one undo step."),
                new RevitApiCompletionData("XYZ", "A 3D point/vector."),
                new RevitApiCompletionData("Line", "A bounded straight Curve."),
                new RevitApiCompletionData("Curve", "Base class for lines, arcs, and other curves."),
                new RevitApiCompletionData("Level", "A Revit level (datum)."),
                new RevitApiCompletionData("View", "Base class for all Revit views."),
                new RevitApiCompletionData("ViewPlan", "A plan view."),
                new RevitApiCompletionData("Wall", "A wall instance."),
                new RevitApiCompletionData("WallType", "A wall type."),
                new RevitApiCompletionData("Floor", "A floor instance."),
                new RevitApiCompletionData("Ceiling", "A ceiling instance."),
                new RevitApiCompletionData("Duct", "A duct instance (Autodesk.Revit.DB.Mechanical)."),
                new RevitApiCompletionData("Pipe", "A pipe instance (Autodesk.Revit.DB.Plumbing)."),
                new RevitApiCompletionData("CableTray", "A cable tray instance (Autodesk.Revit.DB.Electrical)."),
                new RevitApiCompletionData("FamilyInstance", "A placed instance of a family."),
                new RevitApiCompletionData("FamilySymbol", "A family type (loadable family)."),
                new RevitApiCompletionData("Room", "A room element (Autodesk.Revit.DB.Architecture)."),
                new RevitApiCompletionData("ElementCategoryFilter", "Filters elements by category."),
                new RevitApiCompletionData("ElementClassFilter", "Filters elements by .NET type."),
                new RevitApiCompletionData("LogicalAndFilter", "Combines two element filters with AND."),
                new RevitApiCompletionData("LogicalOrFilter", "Combines two element filters with OR."),
                new RevitApiCompletionData("TaskDialog", "Shows a Revit-style message dialog (avoid for routine success messages - return a summary string instead)."),

                // Common members.
                new RevitApiCompletionData("GetOrderedParameters()", "All parameters on an element/type, in Revit's own display order."),
                new RevitApiCompletionData("LookupParameter(\"\")", "Find a parameter on an element by its display name."),
                new RevitApiCompletionData("get_Parameter(BuiltInParameter.)", "Find a parameter on an element by built-in parameter id."),
                new RevitApiCompletionData("IsValidObject", "True if an element still exists and hasn't been deleted."),
            };
        }
    }
}
