#region Metadata
/*
 * Tool Name     : MEP Openings
 * File Name     : MepOpeningShape.cs
 * Purpose       : Defines supported requested opening shapes for MEP openings.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-07-03
 * Last Updated  : 2026-07-03
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : None
 *
 * Input         : User setting per supported MEP element type.
 * Output        : Requested direct opening shape.
 *
 * Notes         :
 * - Pipe and conduit may request Circle or Rectangle.
 * - Duct and cable tray are normalized to Rectangle.
 * - Direct wall openings are rectangular in the Revit API; circular wall requests use a bounding rectangle.
 *
 * Changelog     :
 * v1.0.0 (2026-07-03) - Initial release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

namespace AJTools.Models.MepOpenings
{
    public enum MepOpeningShape
    {
        Circle = 0,
        Rectangle = 1
    }
}
