#region Metadata
/*
 * Tool Name     : AJ Game Mode
 * File Name     : CmdGameMode.cs
 * Purpose       : Ribbon entry for Game Mode - a first-person, video-game style walkthrough of the
 *                 live model. Creates (or reuses) the "AJ Game View" perspective 3D view, places the
 *                 player, and opens the game HUD overlay (WASD walk, jump, fly, ghost, doors with E,
 *                 windows by jumping, laser distance pointer, and an identify-shooter). Clicking the
 *                 ribbon button while a game is running stops it (the button is a toggle).
 *
 * Author        : Ajmal P.S.
 * Version       : 1.1.0
 *
 * Created Date  : 2026-07-28
 * Last Updated  : 2026-07-29
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API + UI API, AJTools.Services.GameMode, AJTools.UI.GameMode,
 *                 AJTools.Utils (ValidationHelper, DialogHelper, Constants)
 *
 * Input         : Active project document. Works best with a 3D/plan view open (start point comes
 *                 from where you are currently looking).
 * Output        : One perspective view named "AJ Game View" (created once, then reused - the only
 *                 model change this tool ever makes). Camera moves during play create NO undo
 *                 entries and NO model changes.
 *
 * Notes         :
 * - The whole tool lives in the src/GameMode folder (+ the Game* images in Resources and one
 *   ribbon entry) - delete those and it is fully removed.
 * - Visibility/Graphics, filters, section boxes etc. of "AJ Game View" are fully respected: what
 *   is hidden in that view does not exist for collision either.
 * - Family documents are refused (needs a project model).
 *
 * Changelog     :
 * v1.1.0 (2026-07-28) - New game views are now created with Crop View OFF and the crop region
 *                       boundary OFF (Ajmal was turning both off manually on every fresh model).
 *                       Existing "AJ Game View" views are reused untouched, keeping whatever
 *                       settings he has given them.
 * v1.0.0 (2026-07-28) - Initial release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.Services.GameMode;
using AJTools.UI.GameMode;
using AJTools.Utils;

namespace AJTools.Commands.GameMode
{
    /// <summary>
    /// Starts (or stops) the Game Mode walkthrough in the "AJ Game View" perspective view.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdGameMode : IExternalCommand
    {
        private const string GameViewName = "AJ Game View";
        private const string ToolTitle = "AJ Game Mode";

        /// <summary>The currently open game HUD, if a game is running (one at a time).</summary>
        internal static GameHudWindow ActiveHud;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp != null ? uiapp.ActiveUIDocument : null;
            if (!ValidationHelper.ValidateUIDocumentAndView(uidoc, out message))
            {
                DialogHelper.ShowError(ToolTitle, message);
                return Result.Cancelled;
            }

            // Toggle behaviour: a second click on the ribbon button ends the running game.
            if (ActiveHud != null)
            {
                try
                {
                    ActiveHud.StopGame("Stopped from the ribbon.");
                }
                catch (Exception)
                {
                    ActiveHud = null;
                }
                return Result.Succeeded;
            }

            Document doc = uidoc.Document;
            if (doc.IsFamilyDocument)
            {
                DialogHelper.ShowError(ToolTitle, "Game Mode needs a project model - open a project, not a family.");
                return Result.Cancelled;
            }

            try
            {
                // Where the player starts - read from the CURRENT view before switching away.
                XYZ startPosition;
                double startYawDeg;
                double startPitchDeg;
                bool startFromExistingCamera;

                View3D gameView = FindExistingGameView(doc);
                if (gameView != null)
                {
                    // Resume from wherever the game camera last was.
                    ViewOrientation3D orientation = gameView.GetOrientation();
                    startPosition = orientation.EyePosition;
                    ForwardToAngles(orientation.ForwardDirection, out startYawDeg, out startPitchDeg);
                    startFromExistingCamera = true;
                }
                else
                {
                    ComputeStartFromCurrentView(uidoc, out startPosition, out startYawDeg, out startPitchDeg);
                    gameView = CreateGameView(doc);
                    startFromExistingCamera = false;
                }

                if (gameView == null)
                {
                    DialogHelper.ShowError(ToolTitle, "Could not create the game view. Please try again.");
                    return Result.Failed;
                }

                // Switch to the game view, then aim the starting camera (no transaction needed -
                // camera orientation is navigation, not a model change).
                try
                {
                    uidoc.ActiveView = gameView;
                }
                catch (Exception ex)
                {
                    DialogHelper.ShowError(ToolTitle, "Could not open the game view: " + ex.Message);
                    return Result.Failed;
                }

                if (!startFromExistingCamera)
                {
                    ApplyCamera(gameView, startPosition, startYawDeg, startPitchDeg);
                }

                var session = new GameSession(gameView.Id, doc.Title, startPosition, startYawDeg);
                session.Input.PitchDeg = startPitchDeg;

                var engine = new GameMotionEngine(session);
                ExternalEvent frameEvent = ExternalEvent.Create(engine);

                var hud = new GameHudWindow(session, frameEvent);
                var interop = new System.Windows.Interop.WindowInteropHelper(hud);
                interop.Owner = uiapp.MainWindowHandle;

                // Give the overlay its first position immediately (the engine keeps it updated).
                FillInitialRectangle(uidoc, session);

                ActiveHud = hud;
                hud.Show();
                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                DialogHelper.ShowError(ToolTitle, "Game Mode could not start: " + ex.Message);
                return Result.Failed;
            }
        }

        private static View3D FindExistingGameView(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(View3D))
                .Cast<View3D>()
                .FirstOrDefault(v => !v.IsTemplate
                                     && v.IsPerspective
                                     && string.Equals(v.Name, GameViewName, StringComparison.OrdinalIgnoreCase));
        }

        private static View3D CreateGameView(Document doc)
        {
            ViewFamilyType viewFamilyType = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .FirstOrDefault(t => t.ViewFamily == ViewFamily.ThreeDimensional);
            if (viewFamilyType == null)
            {
                return null;
            }

            using (var transaction = new Transaction(doc, "AJ Tools - Game Mode View"))
            {
                transaction.Start();

                View3D view = View3D.CreatePerspective(doc, viewFamilyType.Id);

                try
                {
                    view.Name = GameViewName;
                }
                catch (Exception)
                {
                    // A non-perspective view already owns the name - keep Revit's default name.
                }

                try
                {
                    // Shaded looks game-like out of the box; Ajmal can change it any time -
                    // whatever he sets on this view afterwards is respected and kept.
                    view.DisplayStyle = DisplayStyle.Shading;
                }
                catch (Exception)
                {
                    // Display style is cosmetic only.
                }

                try
                {
                    // Far clipping would cut the world off in the distance - switch it off.
                    Parameter farClip = view.get_Parameter(BuiltInParameter.VIEWER_BOUND_ACTIVE_FAR);
                    if (farClip != null && !farClip.IsReadOnly)
                    {
                        farClip.Set(0);
                    }
                }
                catch (Exception)
                {
                    // Far clip default is acceptable if this fails.
                }

                try
                {
                    // Ajmal's request (2026-07-28): every freshly created game view must come with
                    // Crop View and the crop region boundary already OFF - he was switching both
                    // off by hand on each new model.
                    view.CropBoxActive = false;
                    view.CropBoxVisible = false;
                }
                catch (Exception)
                {
                    // If a model refuses, the view still works - crop just stays as Revit made it.
                }

                transaction.Commit();
                return view;
            }
        }

        private static void ComputeStartFromCurrentView(
            UIDocument uidoc,
            out XYZ startPosition,
            out double startYawDeg,
            out double startPitchDeg)
        {
            View activeView = uidoc.ActiveView;
            double eyeHeightFt = GameTuning.EyeHeightFt;

            // Default: model origin at eye height, looking east.
            startPosition = new XYZ(0, 0, eyeHeightFt);
            startYawDeg = 0;
            startPitchDeg = 0;

            var perspective = activeView as View3D;
            if (perspective != null && perspective.IsPerspective)
            {
                ViewOrientation3D orientation = perspective.GetOrientation();
                startPosition = orientation.EyePosition;
                ForwardToAngles(orientation.ForwardDirection, out startYawDeg, out startPitchDeg);
                return;
            }

            // Where is the user currently looking on screen? Centre of the visible area.
            XYZ zoomCenter = null;
            foreach (UIView uiView in uidoc.GetOpenUIViews())
            {
                if (ElementIdHelper.GetIntegerValue(uiView.ViewId) != ElementIdHelper.GetIntegerValue(activeView.Id))
                {
                    continue;
                }

                try
                {
                    var corners = uiView.GetZoomCorners();
                    if (corners != null && corners.Count >= 2)
                    {
                        zoomCenter = (corners[0] + corners[1]) / 2.0;
                    }
                }
                catch (Exception)
                {
                    zoomCenter = null;
                }
                break;
            }

            var plan = activeView as ViewPlan;
            if (plan != null)
            {
                double levelZ = plan.GenLevel != null ? plan.GenLevel.Elevation : 0;
                double x = zoomCenter != null ? zoomCenter.X : 0;
                double y = zoomCenter != null ? zoomCenter.Y : 0;
                startPosition = new XYZ(x, y, levelZ + eyeHeightFt);

                // Face the plan's "up" direction (usually north) so the first look matches the plan.
                XYZ upDir = plan.UpDirection;
                startYawDeg = Math.Atan2(upDir.Y, upDir.X) * 180.0 / Math.PI;
                startPitchDeg = 0;
                return;
            }

            if (zoomCenter != null)
            {
                startPosition = new XYZ(zoomCenter.X, zoomCenter.Y, zoomCenter.Z + eyeHeightFt);
                XYZ forward = activeView.ViewDirection != null ? activeView.ViewDirection.Negate() : XYZ.BasisX;
                ForwardToAngles(forward, out startYawDeg, out startPitchDeg);
                startPitchDeg = Math.Max(-45, Math.Min(45, startPitchDeg));
            }
        }

        private static void ForwardToAngles(XYZ forward, out double yawDeg, out double pitchDeg)
        {
            yawDeg = 0;
            pitchDeg = 0;
            if (forward == null || forward.GetLength() < 1e-9)
            {
                return;
            }

            XYZ f = forward.Normalize();
            double z = Math.Max(-1.0, Math.Min(1.0, f.Z));
            pitchDeg = Math.Asin(z) * 180.0 / Math.PI;
            pitchDeg = Math.Max(-GameTuning.PitchClampDeg, Math.Min(GameTuning.PitchClampDeg, pitchDeg));
            if (Math.Abs(f.X) > 1e-9 || Math.Abs(f.Y) > 1e-9)
            {
                yawDeg = Math.Atan2(f.Y, f.X) * 180.0 / Math.PI;
            }
        }

        private static void ApplyCamera(View3D view, XYZ eye, double yawDeg, double pitchDeg)
        {
            double yawRad = yawDeg * Math.PI / 180.0;
            double pitchRad = pitchDeg * Math.PI / 180.0;
            var forward = new XYZ(
                Math.Cos(pitchRad) * Math.Cos(yawRad),
                Math.Cos(pitchRad) * Math.Sin(yawRad),
                Math.Sin(pitchRad));
            var right = new XYZ(Math.Sin(yawRad), -Math.Cos(yawRad), 0);
            XYZ up = right.CrossProduct(forward);

            try
            {
                view.SetOrientation(new ViewOrientation3D(eye, up, forward));
            }
            catch (Exception)
            {
                // The engine re-applies the camera on the first frame anyway.
            }
        }

        private static void FillInitialRectangle(UIDocument uidoc, GameSession session)
        {
            foreach (UIView uiView in uidoc.GetOpenUIViews())
            {
                if (ElementIdHelper.GetIntegerValue(uiView.ViewId) != ElementIdHelper.GetIntegerValue(session.GameViewId))
                {
                    continue;
                }

                try
                {
                    var rect = uiView.GetWindowRectangle();
                    session.Hud.RectLeft = rect.Left;
                    session.Hud.RectTop = rect.Top;
                    session.Hud.RectRight = rect.Right;
                    session.Hud.RectBottom = rect.Bottom;
                    session.Hud.RectValid = true;
                }
                catch (Exception)
                {
                    session.Hud.RectValid = false;
                }
                return;
            }
        }
    }
}
