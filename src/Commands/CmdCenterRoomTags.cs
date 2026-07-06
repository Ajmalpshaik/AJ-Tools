#region Metadata
/*
 * Tool Name     : Center Room Tags
 * File Name     : CmdCenterRoomTags.cs
 * Purpose       : Centers every room tag visible in the active view on its tagged room.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-07-06
 * Last Updated  : 2026-07-06
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.Services.RoomTags (RoomTagCenteringService)
 *
 * Input         : Active View - room tags visible in the view.
 * Output        : Room tag heads moved to the center of their tagged rooms.
 *
 * Notes         :
 * - Handles local room tags and loaded linked-room tags.
 * - Skips orphaned, pinned, unloaded-link, or unreadable room tags with a summary.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.Services.RoomTags;

namespace AJTools.Commands
{
    /// <summary>
    /// Centers every room tag in the active view on its tagged room.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdCenterRoomTags : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                return RoomTagCenteringService.Execute(commandData, ref message);
            }
            catch (System.Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
