// Tool Name: About
// Description: Displays information about the AJ Tools add-in.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2025-12-10
// Revit Version: 2020
// Dependencies: Autodesk.Revit.UI
using System.Diagnostics;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AJTools.Commands
{
    /// <summary>
    /// Shows About information for AJ Tools with contact link.
    /// </summary>
    [Autodesk.Revit.Attributes.Transaction(
        Autodesk.Revit.Attributes.TransactionMode.ReadOnly)]
    public class CmdAbout : IExternalCommand
    {
        /// <summary>
        /// Displays the About dialog and optionally opens the LinkedIn profile.
        /// </summary>
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            TaskDialog about = new TaskDialog("About AJ Tools");
            about.MainInstruction = "AJ Tools - Revit 2020 Companion";
            about.MainContent =
                "A lightweight toolkit focused on day-to-day documentation productivity.\n\n" +
                "Highlights:\n" +
                "  - Graphics helpers (toggle links, unhide all, reset overrides)\n" +
                "  - Documentation helpers (auto dimension grids/levels, datum reset)\n" +
                "  - Designed specifically around Ajmal P.S workflows\n\n" +
                "Have an idea or found a bug? Reach out anytime.";

            string url = "https://www.linkedin.com/in/ajmalps/";

            about.FooterText =
                "Developed by Ajmal P.S  -  ajmalnattika@gmail.com  -  LinkedIn Profile";

            // Provide an explicit command link so the user can open the profile if they choose.
            about.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Open LinkedIn profile");

            TaskDialogResult result = about.Show();

            if (result == TaskDialogResult.CommandLink1)
            {
                OpenLinkedInProfile(url);
            }

            return Result.Succeeded;
        }

        /// <summary>
        /// Attempts to launch the provided URL in the system browser.
        /// </summary>
        private static void OpenLinkedInProfile(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch
            {
                TaskDialog.Show("Link Error", "Unable to open the link:\n" + url);
            }
        }
    }
}
