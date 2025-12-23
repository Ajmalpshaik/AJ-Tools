// Tool Name: View Selection Form
// Description: WinForms dialog for choosing plan views when applying view range copies.
// Author: Ajmal P.S.
// Version: 1.0.1
// Last Updated: 2025-12-23
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, System.Windows.Forms (Add Reference in Visual Studio)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using View = Autodesk.Revit.DB.View; // Resolve ambiguity with System.Windows.Forms.View

namespace AJTools.UI
{
    /// <summary>
    /// WinForms dialog for selecting one or more plan views to apply view range changes.
    /// </summary>
    public class ViewSelectionForm : System.Windows.Forms.Form
    {
        private readonly CheckedListBox _list;
        private readonly Button _ok;
        private readonly Button _cancel;
        private readonly Button _selectAll;
        private readonly Button _selectNone;
        private readonly Label _lblHeader;

        // This list returns the actual Revit View objects to the Command
        public List<ViewPlan> SelectedViews { get; private set; }

        /// <summary>
        /// Builds the selection dialog.
        /// </summary>
        /// <param name="allViews">List of all eligible plan views in the project.</param>
        /// <param name="currentSourceView">The view we copied FROM (to avoid pasting into itself).</param>
        public ViewSelectionForm(IEnumerable<ViewPlan> allViews, ViewPlan currentSourceView)
        {
            // 1. Form Setup
            Text = "Select Views - AJ Tools";
            Width = 400;
            Height = 550;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            TopMost = true; // Keeps it above Revit window

            // 2. Header Label
            _lblHeader = new Label
            {
                Text = "Select views to apply the copied View Range:",
                Top = 10,
                Left = 10,
                Width = 360,
                Height = 20,
                Font = new System.Drawing.Font("Segoe UI", 9, System.Drawing.FontStyle.Bold)
            };

            // 3. Checked List Box
            _list = new CheckedListBox
            {
                Top = 40,
                Left = 10,
                Width = 365,
                Height = 410, // Leaves room for buttons at bottom
                CheckOnClick = true,
                FormattingEnabled = true,
                ScrollAlwaysVisible = true
            };
            
            // Format the list to show View Names
            _list.Format += (s, e) =>
            {
                if (e.ListItem is ViewPlan vp)
                    e.Value = vp.Name;
            };

            // Populate the list (Excluding the source view to prevent redundancy)
            var sortedViews = allViews.OrderBy(v => v.Name).ToList();
            foreach (ViewPlan v in sortedViews)
            {
                // Logic: Do not show the source view in the list of targets
                if (currentSourceView != null && v.Id == currentSourceView.Id)
                    continue;

                _list.Items.Add(v);
            }

            // 4. Buttons
            int btnTop = 465;

            _selectAll = new Button { Text = "Select All", Left = 10, Top = btnTop, Width = 80 };
            _selectNone = new Button { Text = "Select None", Left = 100, Top = btnTop, Width = 80 };
            
            _ok = new Button 
            { 
                Text = "OK", 
                DialogResult = DialogResult.OK, 
                Left = 205, 
                Top = btnTop, 
                Width = 80,
                Enabled = false // Disabled until something is checked
            };

            _cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Left = 295, Top = btnTop, Width = 80 };

            // 5. Events
            _selectAll.Click += (s, e) => SetAll(true);
            _selectNone.Click += (s, e) => SetAll(false);
            
            // Enable OK button only if at least one item is checked
            _list.ItemCheck += (s, e) => 
            {
                // The item checked state updates *after* this event, so we calculate manually
                this.BeginInvoke((MethodInvoker)delegate 
                {
                    _ok.Enabled = _list.CheckedItems.Count > 0;
                });
            };

            // 6. Add Controls
            Controls.Add(_lblHeader);
            Controls.Add(_list);
            Controls.Add(_selectAll);
            Controls.Add(_selectNone);
            Controls.Add(_ok);
            Controls.Add(_cancel);

            AcceptButton = _ok;
            CancelButton = _cancel;

            SelectedViews = new List<ViewPlan>();
        }

        /// <summary>
        /// Captures checked views when the dialog closes with OK.
        /// </summary>
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (DialogResult == DialogResult.OK)
            {
                SelectedViews.Clear();
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
            {
                _list.SetItemChecked(i, state);
            }
            _ok.Enabled = state && _list.Items.Count > 0;
        }
    }
}