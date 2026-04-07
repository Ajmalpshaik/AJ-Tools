// Tool Name: Intelligent Tag Arranger Settings Command
// Description: Configures default vertical spacing for Arrange Tags.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2026-04-07
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, Autodesk.Revit.UI, AJTools.Utils

using System.Globalization;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.Utils;
using Drawing = System.Drawing;
using WinForms = System.Windows.Forms;

namespace AJTools.Commands
{
    /// <summary>
    /// Opens settings dialog for Arrange Tags spacing (in mm).
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdIntelligentTagArrangerSettings : IExternalCommand
    {
        private const string Title = "Arrange Tags Settings";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData?.Application?.ActiveUIDocument;
            Document doc = uidoc?.Document;
            if (doc == null)
            {
                message = "No active document.";
                return Result.Failed;
            }

            double current = TagArrangeSettings.GetTagSpacingMm();
            if (!TryPromptSpacing(current, out double spacingMm))
                return Result.Cancelled;

            TagArrangeSettings.SaveTagSpacingMm(spacingMm);
            DialogHelper.ShowInfo(Title, $"Tag spacing saved as {spacingMm:0.###} mm.");
            return Result.Succeeded;
        }

        private static bool TryPromptSpacing(double currentSpacingMm, out double spacingMm)
        {
            spacingMm = 0;

            using (var form = new WinForms.Form())
            using (var title = new WinForms.Label())
            using (var inputLabel = new WinForms.Label())
            using (var input = new WinForms.TextBox())
            using (var save = new WinForms.Button())
            using (var cancel = new WinForms.Button())
            {
                form.Text = Title;
                form.FormBorderStyle = WinForms.FormBorderStyle.FixedDialog;
                form.StartPosition = WinForms.FormStartPosition.CenterScreen;
                form.ClientSize = new Drawing.Size(430, 150);
                form.MaximizeBox = false;
                form.MinimizeBox = false;

                title.Text = "Tagging Settings";
                title.Font = new Drawing.Font(form.Font, Drawing.FontStyle.Bold);
                title.AutoSize = true;
                title.Location = new Drawing.Point(12, 12);

                inputLabel.Text = "Enter default vertical spacing for tags (in mm):";
                inputLabel.AutoSize = true;
                inputLabel.Location = new Drawing.Point(12, 44);

                input.Text = currentSpacingMm.ToString("0.###", CultureInfo.CurrentCulture);
                input.Location = new Drawing.Point(12, 64);
                input.Width = 400;

                save.Text = "Save";
                save.DialogResult = WinForms.DialogResult.OK;
                save.Location = new Drawing.Point(256, 104);
                save.Width = 75;

                cancel.Text = "Cancel";
                cancel.DialogResult = WinForms.DialogResult.Cancel;
                cancel.Location = new Drawing.Point(337, 104);
                cancel.Width = 75;

                form.Controls.Add(title);
                form.Controls.Add(inputLabel);
                form.Controls.Add(input);
                form.Controls.Add(save);
                form.Controls.Add(cancel);
                form.AcceptButton = save;
                form.CancelButton = cancel;

                if (form.ShowDialog() != WinForms.DialogResult.OK)
                    return false;

                string raw = input.Text?.Trim();
                if (string.IsNullOrWhiteSpace(raw))
                {
                    DialogHelper.ShowError(Title, "Enter a spacing value.");
                    return false;
                }

                if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.CurrentCulture, out double parsed))
                {
                    DialogHelper.ShowError(Title, "Invalid input. Please enter a valid number.");
                    return false;
                }

                if (parsed <= 0)
                {
                    DialogHelper.ShowError(Title, "Spacing must be greater than zero.");
                    return false;
                }

                spacingMm = parsed;
            }

            return true;
        }
    }
}
