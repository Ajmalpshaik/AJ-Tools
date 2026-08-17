// Tool Name: Dialog Helper
// Description: Centralized dialog methods for common user interactions.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2025-12-11
// Revit Version: 2020
// Dependencies: Autodesk.Revit.UI

using Autodesk.Revit.UI;

namespace AJTools.Utils
{
    /// <summary>
    /// Provides simplified methods for common dialog patterns.
    /// </summary>
    internal static class DialogHelper
    {
        /// <summary>
        /// Shows an error message dialog.
        /// </summary>
        /// <param name="title">The dialog title.</param>
        /// <param name="message">The error message.</param>
        public static void ShowError(string title, string message)
        {
            TaskDialog.Show(title, message);
        }

        /// <summary>
        /// Shows an information message dialog.
        /// </summary>
        /// <param name="title">The dialog title.</param>
        /// <param name="message">The information message.</param>
        public static void ShowInfo(string title, string message)
        {
            TaskDialog.Show(title, message);
        }

        /// <summary>
        /// Shows a warning message with a Yes/No prompt.
        /// </summary>
        /// <param name="title">The dialog title.</param>
        /// <param name="message">The warning message.</param>
        /// <returns>True if user clicked Yes, false otherwise.</returns>
        public static bool ShowYesNo(string title, string message)
        {
            TaskDialogResult result = TaskDialog.Show(
                title,
                message,
                TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No);

            return result == TaskDialogResult.Yes;
        }

        /// <summary>
        /// Above this many elements, a long-running tool asks before it starts. Below it the run is
        /// quick enough that a prompt is just a click in the way.
        /// </summary>
        /// <remarks>
        /// Deliberately NOT exposed in any settings window: it is a "warn me before Revit goes busy"
        /// threshold, not a modelling choice. Shared so every tool agrees on what counts as a big run.
        /// Lived on TagClashSettings until v1.49.5, which is where it was first needed - it was moved
        /// here once tools outside the clash feature (Center Room Tags, L-Shape Leader) started using
        /// it, since a suite-wide threshold living on one feature's settings class reads as if it only
        /// applies to that feature.
        /// </remarks>
        internal const int LongRunConfirmThreshold = 500;

        /// <summary>
        /// Asks before a long run, and only when the job is actually big.
        /// </summary>
        /// <param name="title">The tool's own title, used as the dialog title.</param>
        /// <param name="count">How many elements are about to be worked on.</param>
        /// <param name="whatWillHappen">
        /// One sentence naming the work in the tool's own words, e.g.
        /// "About to place tags on 3,204 elements." The shared "Revit will be busy" paragraph and the
        /// "Continue?" prompt are appended here so all tools word it identically.
        /// </param>
        /// <returns>
        /// True to go ahead - either because the run is small enough not to warrant asking, or because
        /// the user said yes. False only when the user actively declined.
        /// </returns>
        /// <remarks>
        /// This exists because the tools that need it run WITHOUT a window, so there is nowhere to put
        /// a progress bar or a Cancel button short of rebuilding them as modeless windows driven by an
        /// ExternalEvent. Asking up front is the honest alternative to Revit going white with no
        /// warning. A tool that DOES own a window should grow a progress bar instead (see
        /// ProgressReporter and PurgeUnusedElementsWindow) rather than call this.
        /// </remarks>
        public static bool ConfirmLongRun(string title, int count, string whatWillHappen)
        {
            if (count <= LongRunConfirmThreshold)
                return true;

            return ShowYesNo(
                title,
                string.Format(
                    "{0}\n\n"
                    + "Revit will be busy while it works and can't be stopped part way. It is a "
                    + "single undo step, so you can undo the whole thing afterwards.\n\n"
                    + "Continue?",
                    whatWillHappen));
        }

        /// <summary>
        /// Shows a confirmation dialog with custom buttons.
        /// </summary>
        /// <param name="title">The dialog title.</param>
        /// <param name="mainInstruction">The main instruction text.</param>
        /// <param name="mainContent">The detailed content.</param>
        /// <returns>The user's selection result.</returns>
        public static TaskDialogResult ShowDialog(string title, string mainInstruction, string mainContent)
        {
            TaskDialog dialog = new TaskDialog(title)
            {
                MainInstruction = mainInstruction,
                MainContent = mainContent,
                CommonButtons = TaskDialogCommonButtons.Ok
            };

            return dialog.Show();
        }
    }
}
