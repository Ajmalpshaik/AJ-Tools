using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AJTools
{
    internal class CopiedViewRange
    {
        public PlanViewRange Range { get; set; }
        public string SourceName { get; set; }
    }

    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class CmdCopyViewRange : IExternalCommand
    {
        private static CopiedViewRange _cachedRange;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            if (uiDoc == null)
            {
                TaskDialog.Show("Copy View Range", "Open a project view before running this command.");
                return Result.Failed;
            }

            Document doc = uiDoc.Document;
            ViewPlan activePlan = doc.ActiveView as ViewPlan;
            if (activePlan == null || activePlan.IsTemplate)
            {
                TaskDialog.Show("Copy View Range", "Active view must be a non-template plan view.");
                return Result.Cancelled;
            }

            TaskDialogResult action = PromptForAction();
            if (action == TaskDialogResult.CommandLink1)
            {
                return CopyFromActiveView(activePlan);
            }

            if (action == TaskDialogResult.CommandLink2)
            {
                return PasteToSelectedViews(doc, activePlan);
            }

            return Result.Cancelled;
        }

        private static TaskDialogResult PromptForAction()
        {
            TaskDialog dialog = new TaskDialog("Copy View Range")
            {
                MainInstruction = _cachedRange == null ? "Copy view range from the active view" : "Choose what to do",
                MainContent = _cachedRange == null
                    ? "Copy the active plan view's range so you can paste it to other plan views."
                    : "Use the cached view range or copy a fresh one.",
                CommonButtons = TaskDialogCommonButtons.Cancel,
                AllowCancellation = true
            };

            dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Copy from active view");
            if (_cachedRange != null)
                dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "Paste to other plan views");

            return dialog.Show();
        }

        private static Result CopyFromActiveView(ViewPlan sourceView)
        {
            try
            {
                PlanViewRange range = sourceView.GetViewRange();
                _cachedRange = new CopiedViewRange
                {
                    Range = range,
                    SourceName = sourceView.Name
                };

                TaskDialog.Show("Copy View Range", $"Copied view range from '{sourceView.Name}'.");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Copy View Range", "Could not copy view range:\n" + ex.Message);
                return Result.Failed;
            }
        }

        private static Result PasteToSelectedViews(Document doc, ViewPlan activePlan)
        {
            if (_cachedRange == null)
            {
                TaskDialog.Show("Copy View Range", "Nothing stored yet. Copy a view range first.");
                return Result.Cancelled;
            }

            List<ViewPlan> targets = GetTargetViews(doc, activePlan);
            if (targets.Count == 0)
            {
                TaskDialog.Show("Copy View Range", "No plan views selected.");
                return Result.Cancelled;
            }

            int updated = 0;
            int skipped = 0;

            using (Transaction t = new Transaction(doc, "Paste View Range"))
            {
                t.Start();
                foreach (ViewPlan target in targets)
                {
                    if (target.IsTemplate || !CanEditViewRange(target))
                    {
                        skipped++;
                        continue;
                    }

                    try
                    {
                        // Apply the cached plan view range directly, matching the Python script logic.
                        target.SetViewRange(_cachedRange.Range);
                        updated++;
                    }
                    catch
                    {
                        skipped++;
                    }
                }
                t.Commit();
            }

            string msg = $"Applied view range to {updated} view(s) from '{_cachedRange.SourceName}'.";
            if (skipped > 0)
                msg += $"\nSkipped {skipped} view(s) (template or read-only view range).";
            TaskDialog.Show("Copy View Range", msg);
            return updated > 0 ? Result.Succeeded : Result.Cancelled;
        }

        private static List<ViewPlan> GetTargetViews(Document doc, ViewPlan activePlan)
        {
            List<ViewPlan> plans = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewPlan))
                .Cast<ViewPlan>()
                .Where(v => !v.IsTemplate && v.ViewType != ViewType.AreaPlan)
                .OrderBy(v => v.Name)
                .ToList();

            using (var form = new ViewSelectionForm(plans, activePlan))
            {
                DialogResult result = form.ShowDialog();
                if (result != DialogResult.OK)
                    return new List<ViewPlan>();
                return form.SelectedViews;
            }
        }

        private static bool CanEditViewRange(ViewPlan view)
        {
            if (view == null) return false;
            if (view.IsTemplate) return false;
            try
            {
                // Try to access the view range; if this throws, it's not editable.
                var vr = view.GetViewRange();
                return vr != null;
            }
            catch
            {
                return false;
            }
        }
    }

    internal class ViewSelectionForm : System.Windows.Forms.Form
    {
        private readonly CheckedListBox _list;
        private readonly Button _ok;
        private readonly Button _cancel;
        private readonly Button _selectAll;
        private readonly Button _selectNone;

        public List<ViewPlan> SelectedViews { get; }

        public ViewSelectionForm(IList<ViewPlan> views, ViewPlan activePlan)
        {
            Text = "Select Plan Views";
            Width = 420;
            Height = 520;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            _list = new CheckedListBox
            {
                Dock = DockStyle.Top,
                Height = 400,
                CheckOnClick = true,
                FormattingEnabled = true
            };
            _list.Format += (s, e) =>
            {
                if (e.ListItem is ViewPlan vp)
                    e.Value = vp.Name;
            };

            foreach (ViewPlan v in views)
            {
                int idx = _list.Items.Add(v);
                if (activePlan != null && v.Id == activePlan.Id)
                    _list.SetItemChecked(idx, true);
            }

            _ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Anchor = AnchorStyles.Bottom | AnchorStyles.Right, Left = 220, Top = 430, Width = 80 };
            _cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Anchor = AnchorStyles.Bottom | AnchorStyles.Right, Left = 310, Top = 430, Width = 80 };
            _selectAll = new Button { Text = "Select All", Anchor = AnchorStyles.Bottom | AnchorStyles.Left, Left = 10, Top = 430, Width = 90 };
            _selectNone = new Button { Text = "Select None", Anchor = AnchorStyles.Bottom | AnchorStyles.Left, Left = 110, Top = 430, Width = 90 };

            _selectAll.Click += (s, e) => SetAll(true);
            _selectNone.Click += (s, e) => SetAll(false);

            Controls.Add(_list);
            Controls.Add(_ok);
            Controls.Add(_cancel);
            Controls.Add(_selectAll);
            Controls.Add(_selectNone);

            AcceptButton = _ok;
            CancelButton = _cancel;

            SelectedViews = new List<ViewPlan>();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (this.DialogResult == System.Windows.Forms.DialogResult.OK)
            {
                foreach (object item in _list.CheckedItems)
                {
                    if (item is ViewPlan vp)
                        SelectedViews.Add(vp);
                }
            }
            base.OnFormClosing(e);
        }

        private void SetAll(bool state)
        {
            for (int i = 0; i < _list.Items.Count; i++)
                _list.SetItemChecked(i, state);
        }
    }
}
