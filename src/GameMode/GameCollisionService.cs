#region Metadata
/*
 * Tool Name     : AJ Game Mode (collision + aiming rays)
 * File Name     : GameCollisionService.cs
 * Purpose       : All raycasting for Game Mode - collision against whatever is VISIBLE in the game
 *                 view (so Visibility/Graphics, filters and hidden elements naturally shape the game
 *                 world), floor detection for gravity/stairs, and the laser/bullet aim ray that
 *                 identifies the element hit (resolving elements inside linked models too).
 *
 * Author        : Ajmal P.S.
 * Version       : 1.2.0
 *
 * Created Date  : 2026-07-28
 * Last Updated  : 2026-07-30
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API (ReferenceIntersector), AJTools.Utils (ElementIdHelper, Constants)
 *
 * Input         : Ray origin/direction in Revit internal feet, the game View3D.
 * Output        : Nearest hit (distance, point, door/window flags, element identity text). Read-only -
 *                 never modifies the model.
 *
 * Notes         :
 * - ReferenceIntersector on a PERSPECTIVE View3D was verified live on Revit 2020 (2026-07-28):
 *   works, same results as an orthographic view, ~0.1 ms per hitting ray on the test model.
 *   It only reports elements visible in that view - exactly what the game wants.
 * - FindReferencesInRevitLinks is ON: architecture usually lives in a linked model. Hits are
 *   resolved through RevitLinkInstance to the real linked element for category/identify checks.
 * - Door/window checks use the resolved element's category compared against OST_Doors/OST_Windows
 *   (a plain enum compare - no Enum.IsDefined, which breaks on Revit 2024+ per house conventions).
 * - RBS_SYSTEM_NAME_PARAM and RBS_SYSTEM_CLASSIFICATION_PARAM were verified present in BOTH the
 *   real installed Revit 2020 and 2027 RevitAPI.dll (UTF-8 byte scan, 2026-07-29 - bridge was
 *   unavailable) before first use here; the level lookup ladder reuses the pattern already proven
 *   in ReassignLevelService.
 *
 * Changelog     :
 * v1.2.0 (2026-07-30) - Describe() now also reports the element's System and Level (Ajmal: "Laser
 *                       also shows System and Level i need it") - System Name parameter with the
 *                       system classification as fallback, level via the LevelId/reference-level/
 *                       family-level ladder, both resolved against the element's own document so
 *                       linked elements report their own model's names. Blank for elements that
 *                       have neither (walls, floors). The richer line automatically reaches the
 *                       laser readout, the cleaner/snag/selector toasts AND the snag punch-list
 *                       report, since all of them funnel through Describe().
 * v1.1.0 (2026-07-28) - Describe() gained an includeDistance switch: the laser's live readout shows
 *                       the distance separately (big mm number), so its identity line omits it.
 * v1.0.0 (2026-07-28) - Initial release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using Autodesk.Revit.DB;
using AJTools.Utils;

namespace AJTools.Services.GameMode
{
    /// <summary>One resolved ray hit.</summary>
    internal sealed class GameRayHit
    {
        public double DistanceFt;
        public XYZ Point;
        public bool IsDoor;
        public bool IsWindow;
        public bool FromLink;

        /// <summary>The real element hit (the linked element itself when the hit came from a link).</summary>
        public Element ResolvedElement;
    }

    /// <summary>
    /// Raycasts against the game view. What is hidden in the view does not exist for the game -
    /// hide a category via VG/filters and the player walks straight through it.
    /// </summary>
    internal sealed class GameCollisionService
    {
        private readonly Document _doc;
        private readonly View3D _gameView;
        private ReferenceIntersector _intersector;

        public GameCollisionService(Document doc, View3D gameView)
        {
            _doc = doc;
            _gameView = gameView;
            Refresh();
        }

        /// <summary>
        /// Rebuild the intersector so recent visibility/graphics changes are respected.
        /// Cheap - called on resume and periodically by the engine.
        /// </summary>
        public void Refresh()
        {
            _intersector = new ReferenceIntersector(_gameView)
            {
                FindReferencesInRevitLinks = true
            };
        }

        /// <summary>Nearest visible hit along the ray, or null when nothing is within maxDistanceFt.</summary>
        public GameRayHit CastNearest(XYZ origin, XYZ direction, double maxDistanceFt)
        {
            ReferenceWithContext context;
            try
            {
                context = _intersector.FindNearest(origin, direction);
            }
            catch (Exception)
            {
                // A degenerate ray (zero direction) or a view mid-regeneration must never kill a frame.
                return null;
            }

            if (context == null)
            {
                return null;
            }

            double distance = context.Proximity;
            if (distance > maxDistanceFt)
            {
                return null;
            }

            var hit = new GameRayHit
            {
                DistanceFt = distance,
                Point = origin + (direction * distance)
            };

            Reference reference = context.GetReference();
            if (reference == null)
            {
                return hit;
            }

            Element element = _doc.GetElement(reference);
            var linkInstance = element as RevitLinkInstance;
            if (linkInstance != null)
            {
                Document linkDoc = linkInstance.GetLinkDocument();
                Element linked = linkDoc != null ? linkDoc.GetElement(reference.LinkedElementId) : null;
                if (linked != null)
                {
                    hit.ResolvedElement = linked;
                    hit.FromLink = true;
                }
            }
            else
            {
                hit.ResolvedElement = element;
            }

            Category category = hit.ResolvedElement != null ? hit.ResolvedElement.Category : null;
            if (category != null)
            {
                int categoryId = ElementIdHelper.GetIntegerValue(category.Id);
                hit.IsDoor = categoryId == (int)BuiltInCategory.OST_Doors;
                hit.IsWindow = categoryId == (int)BuiltInCategory.OST_Windows;
            }

            return hit;
        }

        /// <summary>
        /// Plain-language identity line for the HUD, e.g.
        /// "Duct - Default - Size 400x250 - Id 919700 (linked)".
        /// Always includes the Element ID (house rule: reported elements carry their ID).
        /// includeDistance adds "... mm away" - the laser readout shows distance separately.
        /// </summary>
        public static string Describe(GameRayHit hit, bool includeDistance)
        {
            if (hit == null)
            {
                return "No hit - nothing in range.";
            }

            Element element = hit.ResolvedElement;
            if (element == null)
            {
                return string.Format("Hit at {0:N0} mm - element could not be resolved.", hit.DistanceFt / Constants.MM_TO_FEET);
            }

            string categoryName = element.Category != null ? element.Category.Name : "Element";

            string name;
            var familyInstance = element as FamilyInstance;
            if (familyInstance != null && familyInstance.Symbol != null)
            {
                name = familyInstance.Symbol.FamilyName + " : " + element.Name;
            }
            else
            {
                name = element.Name;
            }

            string size = "";
            try
            {
                Parameter sizeParam = element.LookupParameter("Size");
                if (sizeParam != null && sizeParam.HasValue)
                {
                    string sizeText = sizeParam.AsString();
                    if (!string.IsNullOrWhiteSpace(sizeText))
                    {
                        size = " - Size " + sizeText;
                    }
                }
            }
            catch (Exception)
            {
                // Size is a bonus - never fail the identify because of it.
            }

            string system = GetSystemText(element);
            string level = GetLevelText(element);

            string linkNote = hit.FromLink ? " (linked)" : "";
            string distancePart = includeDistance
                ? string.Format(" - {0:N0} mm away", hit.DistanceFt / Constants.MM_TO_FEET)
                : "";

            return string.Format(
                "{0} - {1}{2}{3}{4}{5} - Id {6}{7}",
                categoryName,
                name,
                size,
                system,
                level,
                distancePart,
                ElementIdHelper.GetIntegerValue(element.Id),
                linkNote);
        }

        /// <summary>
        /// " - SAD 5" style System text (Ajmal's ask, 2026-07-29): the System Name parameter covers
        /// ducts, pipes, fittings, accessories, terminals and equipment; the classification
        /// ("Supply Air") is the fallback. Non-MEP elements have neither and return "".
        /// </summary>
        private static string GetSystemText(Element element)
        {
            try
            {
                string text = ReadStringParam(element, BuiltInParameter.RBS_SYSTEM_NAME_PARAM);
                if (string.IsNullOrWhiteSpace(text))
                {
                    text = ReadStringParam(element, BuiltInParameter.RBS_SYSTEM_CLASSIFICATION_PARAM);
                }
                return string.IsNullOrWhiteSpace(text) ? "" : " - " + text.Trim();
            }
            catch (Exception)
            {
                return "";
            }
        }

        private static string ReadStringParam(Element element, BuiltInParameter parameter)
        {
            Parameter param = element.get_Parameter(parameter);
            if (param == null || !param.HasValue)
            {
                return null;
            }

            string text = param.AsString();
            if (string.IsNullOrWhiteSpace(text))
            {
                try { text = param.AsValueString(); } catch (Exception) { text = null; }
            }
            return text;
        }

        /// <summary>
        /// " - Level 03" style Level text. Same lookup ladder ReassignLevelService already proved:
        /// Element.LevelId first, then the MEP reference level, then the family instance level
        /// parameters. Resolved against the element's OWN document, so linked elements report the
        /// linked model's level names correctly.
        /// </summary>
        private static string GetLevelText(Element element)
        {
            try
            {
                ElementId levelId = element.LevelId;
                if (levelId == null || levelId == ElementId.InvalidElementId)
                {
                    levelId = ReadLevelIdParam(element, BuiltInParameter.RBS_START_LEVEL_PARAM);
                }
                if (levelId == null || levelId == ElementId.InvalidElementId)
                {
                    levelId = ReadLevelIdParam(element, BuiltInParameter.FAMILY_LEVEL_PARAM);
                }
                if (levelId == null || levelId == ElementId.InvalidElementId)
                {
                    levelId = ReadLevelIdParam(element, BuiltInParameter.SCHEDULE_LEVEL_PARAM);
                }
                if (levelId == null || levelId == ElementId.InvalidElementId)
                {
                    return "";
                }

                Element levelElement = element.Document != null ? element.Document.GetElement(levelId) : null;
                return levelElement is Level ? " - " + levelElement.Name : "";
            }
            catch (Exception)
            {
                return "";
            }
        }

        private static ElementId ReadLevelIdParam(Element element, BuiltInParameter parameter)
        {
            Parameter param = element.get_Parameter(parameter);
            return param != null && param.HasValue ? param.AsElementId() : ElementId.InvalidElementId;
        }
    }
}
