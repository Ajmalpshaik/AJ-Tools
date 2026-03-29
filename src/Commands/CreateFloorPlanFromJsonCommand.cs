using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.Models.FloorPlanImport;
using AJTools.Utils;
using Newtonsoft.Json;

namespace AJTools.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class CreateFloorPlanFromJsonCommand : IExternalCommand
    {
        private const string DialogTitle = "Create Floor Plan From JSON";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uiDoc = commandData.Application.ActiveUIDocument;
            if (uiDoc == null)
            {
                message = "No active document.";
                return Result.Failed;
            }

            Document doc = uiDoc.Document;
            string jsonFilePath = FloorPlanImportHelper.SelectJsonFilePath();
            if (string.IsNullOrWhiteSpace(jsonFilePath))
            {
                return Result.Cancelled;
            }

            FloorPlanJsonData jsonData;
            try
            {
                string jsonContent = File.ReadAllText(jsonFilePath);
                jsonData = JsonConvert.DeserializeObject<FloorPlanJsonData>(jsonContent);
            }
            catch (JsonException ex)
            {
                TaskDialog.Show(DialogTitle, "Invalid JSON file.\n\n" + ex.Message);
                return Result.Failed;
            }
            catch (Exception ex)
            {
                TaskDialog.Show(DialogTitle, "Failed to read the selected file.\n\n" + ex.Message);
                return Result.Failed;
            }

            string validationError = ValidateJsonData(jsonData);
            if (!string.IsNullOrWhiteSpace(validationError))
            {
                TaskDialog.Show(DialogTitle, validationError);
                return Result.Failed;
            }

            DisplayUnitType sourceUnitType;
            if (!FloorPlanImportHelper.TryGetLengthUnit(jsonData.Project.Units, out sourceUnitType))
            {
                TaskDialog.Show(DialogTitle, "Unsupported units: " + jsonData.Project.Units);
                return Result.Failed;
            }

            Dictionary<string, Level> levelsByName = GetLevelsByName(doc);
            string defaultLevelName = jsonData.Project.DefaultLevel.Trim();
            Level defaultLevel;
            if (!levelsByName.TryGetValue(defaultLevelName, out defaultLevel))
            {
                TaskDialog.Show(DialogTitle, "Default level not found: " + defaultLevelName);
                return Result.Failed;
            }

            Dictionary<string, WallType> wallTypesByName = GetWallTypesByName(doc);
            if (wallTypesByName.Count == 0)
            {
                TaskDialog.Show(DialogTitle, "No wall types found in the current Revit document.");
                return Result.Failed;
            }

            int createdWalls = 0;
            int skippedWalls = 0;
            List<string> skipReasons = new List<string>();

            using (Transaction tx = new Transaction(doc, "Create Floor Plan From JSON"))
            {
                try
                {
                    tx.Start();

                    foreach (WallJsonData wallData in jsonData.Walls)
                    {
                        string skipReason;
                        if (TryCreateWall(
                            doc,
                            wallData,
                            levelsByName,
                            wallTypesByName,
                            defaultLevel,
                            sourceUnitType,
                            out skipReason))
                        {
                            createdWalls++;
                        }
                        else
                        {
                            skippedWalls++;
                            if (skipReasons.Count < 5 && !string.IsNullOrWhiteSpace(skipReason))
                            {
                                skipReasons.Add(skipReason);
                            }
                        }
                    }

                    tx.Commit();
                }
                catch (Exception ex)
                {
                    if (tx.HasStarted() && !tx.HasEnded())
                    {
                        tx.RollBack();
                    }

                    TaskDialog.Show(DialogTitle, "Wall creation failed.\n\n" + ex.Message);
                    return Result.Failed;
                }
            }

            string summary = string.Format(
                "Floor plan import completed.\nWalls created: {0}\nWalls skipped: {1}",
                createdWalls,
                skippedWalls);

            if (skipReasons.Count > 0)
            {
                summary += "\n\nSkip details:\n";
                for (int i = 0; i < skipReasons.Count; i++)
                {
                    summary += (i + 1) + ". " + skipReasons[i] + "\n";
                }
            }

            TaskDialog.Show(DialogTitle, summary.TrimEnd('\n'));

            return Result.Succeeded;
        }

        private static string ValidateJsonData(FloorPlanJsonData data)
        {
            if (data == null)
            {
                return "Selected JSON file is empty or does not match the expected format.";
            }

            if (data.Project == null)
            {
                return "JSON is missing the 'project' object.";
            }

            if (string.IsNullOrWhiteSpace(data.Project.Name))
            {
                return "JSON is missing 'project.name'.";
            }

            if (string.IsNullOrWhiteSpace(data.Project.Units))
            {
                return "JSON is missing 'project.units'.";
            }

            DisplayUnitType ignoredUnitType;
            if (!FloorPlanImportHelper.TryGetLengthUnit(data.Project.Units, out ignoredUnitType))
            {
                return "Unsupported project units. Allowed values: mm, cm, m, ft, in.";
            }

            if (string.IsNullOrWhiteSpace(data.Project.DefaultLevel))
            {
                return "JSON is missing 'project.defaultLevel'.";
            }

            if (data.Walls == null || data.Walls.Count == 0)
            {
                return "JSON is missing the 'walls' array, or it is empty.";
            }

            return null;
        }

        private static Dictionary<string, Level> GetLevelsByName(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .GroupBy(level => level.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        }

        private static Dictionary<string, WallType> GetWallTypesByName(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(WallType))
                .Cast<WallType>()
                .GroupBy(wallType => wallType.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        }

        private static bool TryCreateWall(
            Document doc,
            WallJsonData wallData,
            IReadOnlyDictionary<string, Level> levelsByName,
            IReadOnlyDictionary<string, WallType> wallTypesByName,
            Level defaultLevel,
            DisplayUnitType sourceUnitType,
            out string skipReason)
        {
            skipReason = "Invalid wall data.";

            if (wallData == null || wallData.Start == null || wallData.End == null)
            {
                return false;
            }

            string wallId = string.IsNullOrWhiteSpace(wallData.Id) ? "<no id>" : wallData.Id.Trim();

            if (string.IsNullOrWhiteSpace(wallData.WallType))
            {
                skipReason = "Wall " + wallId + ": missing wallType.";
                return false;
            }

            string wallTypeName = wallData.WallType.Trim();
            WallType wallType;
            if (!wallTypesByName.TryGetValue(wallTypeName, out wallType))
            {
                skipReason = "Wall " + wallId + ": wall type not found -> " + wallTypeName;
                return false;
            }

            string levelName = string.IsNullOrWhiteSpace(wallData.Level)
                ? defaultLevel.Name
                : wallData.Level.Trim();

            Level level;
            if (!levelsByName.TryGetValue(levelName, out level))
            {
                skipReason = "Wall " + wallId + ": level not found -> " + levelName;
                return false;
            }

            if (!FloorPlanImportHelper.IsFiniteNumber(wallData.Height) || wallData.Height <= 0)
            {
                skipReason = "Wall " + wallId + ": height must be greater than zero.";
                return false;
            }

            if (!FloorPlanImportHelper.IsFiniteNumber(wallData.Start.X) ||
                !FloorPlanImportHelper.IsFiniteNumber(wallData.Start.Y) ||
                !FloorPlanImportHelper.IsFiniteNumber(wallData.End.X) ||
                !FloorPlanImportHelper.IsFiniteNumber(wallData.End.Y))
            {
                skipReason = "Wall " + wallId + ": start/end coordinates are invalid.";
                return false;
            }

            WallLocationLine wallLocationLine;
            if (!FloorPlanImportHelper.TryParseWallLocationLine(wallData.LocationLine, out wallLocationLine))
            {
                skipReason = "Wall " + wallId + ": invalid locationLine value.";
                return false;
            }

            double heightFeet = FloorPlanImportHelper.ToInternalLength(wallData.Height, sourceUnitType);
            double baseOffsetFeet = FloorPlanImportHelper.ToInternalLength(wallData.BaseOffset ?? 0.0, sourceUnitType);
            double topOffsetFeet = FloorPlanImportHelper.ToInternalLength(wallData.TopOffset ?? 0.0, sourceUnitType);

            if (!FloorPlanImportHelper.IsFiniteNumber(heightFeet) ||
                !FloorPlanImportHelper.IsFiniteNumber(baseOffsetFeet) ||
                !FloorPlanImportHelper.IsFiniteNumber(topOffsetFeet))
            {
                skipReason = "Wall " + wallId + ": numeric conversion failed.";
                return false;
            }

            XYZ start = new XYZ(
                FloorPlanImportHelper.ToInternalLength(wallData.Start.X, sourceUnitType),
                FloorPlanImportHelper.ToInternalLength(wallData.Start.Y, sourceUnitType),
                level.Elevation);

            XYZ end = new XYZ(
                FloorPlanImportHelper.ToInternalLength(wallData.End.X, sourceUnitType),
                FloorPlanImportHelper.ToInternalLength(wallData.End.Y, sourceUnitType),
                level.Elevation);

            if (start.DistanceTo(end) < 1e-6)
            {
                skipReason = "Wall " + wallId + ": start and end points are identical.";
                return false;
            }

            bool structural = wallData.Structural ?? false;
            bool roomBounding = wallData.RoomBounding ?? true;

            try
            {
                Line wallLine = Line.CreateBound(start, end);
                Wall wall = Wall.Create(doc, wallLine, wallType.Id, level.Id, heightFeet, baseOffsetFeet, false, structural);

                SetIntegerParameter(wall, BuiltInParameter.WALL_KEY_REF_PARAM, (int)wallLocationLine);
                SetIntegerParameter(wall, BuiltInParameter.WALL_ATTR_ROOM_BOUNDING, roomBounding ? 1 : 0);

                if (Math.Abs(topOffsetFeet) > 1e-9)
                {
                    SetDoubleParameter(wall, BuiltInParameter.WALL_TOP_OFFSET, topOffsetFeet);
                }

                if (!string.IsNullOrWhiteSpace(wallData.Comments))
                {
                    SetStringParameter(wall, BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS, wallData.Comments.Trim());
                }

                return true;
            }
            catch (Exception ex)
            {
                skipReason = "Wall " + wallId + ": " + ex.Message;
                return false;
            }
        }

        private static void SetIntegerParameter(Element element, BuiltInParameter builtInParameter, int value)
        {
            Parameter parameter = element.get_Parameter(builtInParameter);
            if (parameter == null || parameter.IsReadOnly || parameter.StorageType != StorageType.Integer)
            {
                return;
            }

            parameter.Set(value);
        }

        private static void SetDoubleParameter(Element element, BuiltInParameter builtInParameter, double value)
        {
            Parameter parameter = element.get_Parameter(builtInParameter);
            if (parameter == null || parameter.IsReadOnly || parameter.StorageType != StorageType.Double)
            {
                return;
            }

            parameter.Set(value);
        }

        private static void SetStringParameter(Element element, BuiltInParameter builtInParameter, string value)
        {
            Parameter parameter = element.get_Parameter(builtInParameter);
            if (parameter == null || parameter.IsReadOnly || parameter.StorageType != StorageType.String)
            {
                return;
            }

            parameter.Set(value);
        }
    }
}
