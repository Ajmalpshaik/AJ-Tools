// Tool Name: Neon Defender
// Description: Launches the Neon Defender WPF mini-game for breaks.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2025-12-07
// Revit Version: 2020
// Dependencies: Autodesk.Revit.UI
using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AJTools.Commands
{
    /// <summary>
    /// Launches the Neon Defender mini-game.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdNeonDefender : IExternalCommand
    {
        /// <summary>
        /// Launches the Neon Defender mini-game.
        /// </summary>
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var game = new NeonWindow();
                game.Show();
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("AJ Tools - Neon Defender", "Could not start the game:\n\n" + ex.Message);
                return Result.Failed;
            }
        }
    }
}
