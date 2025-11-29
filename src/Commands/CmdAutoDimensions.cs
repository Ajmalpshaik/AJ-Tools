using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using DB = Autodesk.Revit.DB;

namespace AJTools
{
    [Transaction(TransactionMode.Manual)]
    public class CmdAutoDimensions : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, DB.ElementSet elements)
        {
            return AutoDimensionService.Execute(commandData, AutoDimensionMode.Combined, "Auto Dimension Grids & Levels");
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class CmdAutoDimensionsGrids : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, DB.ElementSet elements)
        {
            return AutoDimensionService.Execute(commandData, AutoDimensionMode.GridsOnly, "Auto Dimension Grids");
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class CmdAutoDimensionsLevels : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, DB.ElementSet elements)
        {
            return AutoDimensionService.Execute(commandData, AutoDimensionMode.LevelsOnly, "Auto Dimension Levels");
        }
    }
}
