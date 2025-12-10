// Tool Name: View Selection Form
// Description: WinForms dialog for choosing plan views when applying view range copies.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2025-12-10
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, System.Windows.Forms
using System.Collections.Generic;
using System.Windows.Forms;
using Autodesk.Revit.DB;

namespace AJTools.Commands
{
    /// <summary>
    /// WinForms dialog for selecting one or more plan views to apply view range changes.
    /// </summary>
    internal class ViewSelectionForm : System.Windows.Forms.Form
    {
        private readonly CheckedListBox _list;
        private readonly Button _ok;
        private readonly Button _cancel;
        private readonly Button _selectAll;
        private readonly Button _selectNone;

        public List<ViewPlan> SelectedViews { get; }

        /// <summary>
        /// Builds the selection dialog and pre-checks the active plan if present.
        /// </summary>
        public ViewSelectionForm(IList<ViewPlan> views, ViewPlan activePlan)
        {
            Text = "Select Plan Views";
            Width = 420;
            Height = 520;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
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

        /// <summary>
        /// Captures checked views when the dialog closes with OK.
        /// </summary>
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (this.DialogResult == DialogResult.OK)
            {
                foreach (object item in _list.CheckedItems)
                {
                    if (item is ViewPlan vp)
                        SelectedViews.Add(vp);
                }
            }
            base.OnFormClosing(e);
        }

        /// <summary>
        /// Selects or clears all items in the list.
        /// </summary>
        private void SetAll(bool state)
        {
            for (int i = 0; i < _list.Items.Count; i++)
                _list.SetItemChecked(i, state);
        }
    }
}
