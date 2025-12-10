// Tool Name: Cyber Snake
// Description: Launches the Cyber Snake mini-game (WinForms) for breaks.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2025-12-07
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, Autodesk.Revit.UI
using System;
using System.Windows.Forms;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AJTools.Commands
{
    /// <summary>
    /// Launches the Cyber Snake mini-game.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdSnakeGame : IExternalCommand
    {
        /// <summary>
        /// Starts the Cyber Snake mini-game window.
        /// </summary>
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                SnakeForm form = new SnakeForm();
                form.Show();
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Cyber Snake", "Could not start the game:\n\n" + ex.Message);
                return Result.Failed;
            }
        }
    }
}
