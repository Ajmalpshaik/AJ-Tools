using System.Diagnostics;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AJTools
{
    /// <summary>
    /// Command to display information about the AJ Tools add-in.
    /// </summary>
    [Autodesk.Revit.Attributes.Transaction(
        Autodesk.Revit.Attributes.TransactionMode.ReadOnly)]
    public class CmdAbout : IExternalCommand
    {
        private const string Version = "1.1.0";

        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            TaskDialog about = new TaskDialog("About AJ Tools");
            about.MainInstruction = $"AJ Tools v{Version} for Revit 2020";
            about.MainContent =
                "A lightweight, productivity-focused add-in designed to streamline\n" +
                "day-to-day documentation and modeling workflows.\n\n" +
                "Features:\n" +
                "  • Graphics helpers (toggle links, unhide all, reset overrides)\n" +
                "  • Auto dimensions for grids and levels\n" +
                "  • Datum management tools\n" +
                "  • View range copy/paste\n" +
                "  • MEP elevation matching\n" +
                "  • Filter Pro for quick parameter-based filters\n" +
                "  • Mini-games for refreshing your mind\n\n" +
                "Have an idea or found a bug? Reach out anytime!";

            string url = "https://www.linkedin.com/in/ajmalps/";

            about.FooterText =
                "© 2025 Ajmal P.S  •  ajmalnattika@gmail.com";

            about.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Visit LinkedIn Profile");

            TaskDialogResult result = about.Show();

            if (result == TaskDialogResult.CommandLink1)
            {
                OpenLinkedInProfile(url);
            }

            return Result.Succeeded;
        }

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
