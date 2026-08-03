#region Metadata
/*
 * Tool Name     : AJ Game Mode (motion engine - core frame loop)
 * File Name     : GameMotionEngine.cs
 * Purpose       : The per-frame heart of Game Mode. Runs as an ExternalEvent handler (valid Revit
 *                 API context): validates the session, applies the look direction, moves the
 *                 perspective camera, and feeds the HUD status. The feature logic lives in the
 *                 sibling partial files so each stays small and easy to edit (Ajmal's request):
 *                   GameMotionEngine.Movement.cs - walking physics, collision, gravity, stairs
 *                   GameMotionEngine.Extras.cs   - teleport, saved positions, cleaner/snag/selector
 *                                                  shots, reset graphics, tour mode
 *
 * Author        : Ajmal P.S.
 * Version       : 1.7.1
 *
 * Created Date  : 2026-07-28
 * Last Updated  : 2026-07-29
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API + UI API, GameSession, GameCollisionService
 *
 * Input         : GameSession (input snapshot from the HUD window).
 * Output        : Camera orientation on the game View3D + HUD feedback state. NO model changes and
 *                 NO transactions - View3D.SetOrientation works without a transaction (verified
 *                 live on Revit 2020, 2026-07-28: writes apply, persist, and create no undo entry,
 *                 same as Revit's own orbit/walk navigation).
 *
 * Notes         :
 * - Every frame is wrapped in try/catch: a bad frame stops the game cleanly, never crashes Revit.
 * - The game ends automatically if the user switches away from the game view, closes it, or
 *   changes document.
 * - Collision rays only see what is VISIBLE in the game view, so Revit VG/filters/hide all work
 *   as world-building controls (hide a category and you can walk through it).
 * - The collision intersector is rebuilt every few seconds so mid-game VG changes are picked up.
 * - ALL instance fields of the class live in THIS file (partial-class house style).
 *
 * Changelog     :
 * v1.7.1 (2026-07-29) - Aim/display sync (suite 1.38.2): UpdateViewRectangle now calls
 *                       UIView.ZoomToFit on the first frame, on any window-rectangle change and on
 *                       the HUD's ResyncViewQueued request, so a 2D-zoomed/panned perspective view
 *                       can never drift the crosshair away from the real aim. (Entry added in the
 *                       1.38.4 audit - the code shipped with 1.38.2 but this changelog was never
 *                       stamped.)
 * v1.7.0 (2026-07-29) - SELECTOR weapon support (Extras): the shot toggles the element in the live
 *                       Revit selection via uidoc.Selection.SetElementIds - no transaction, no undo
 *                       entries, and the selection survives the game for immediate editing.
 * v1.6.0 (2026-07-29) - Measuring removed per Ajmal (Measure partial deleted; projection/camera
 *                       calibration code removed with it). J is now a FULL "reset element graphics
 *                       in view": every element's override in the game view is reset (existing
 *                       tool's approach - default settings per element, failures skipped), so snag
 *                       marks from earlier sessions clear too.
 * v1.5.0 (2026-07-29) - Round 11 ("add all" #2 + rubber-band measure): screen projection of 3D
 *                       points (GetZoomCorners calibration with assumed-55-degree fallback) feeding
 *                       the HUD's green anchored dimension line - live A->aim while holding, locked
 *                       A->B staying glued to the faces after release; snag-marker weapon (red
 *                       override + punch-list, J clears, report written to Documents\AJ Game Snags
 *                       on exit); tour mode (O) flying smoothly through saved positions; compass +
 *                       level line; crouch (C in walk = 1000 mm eye); live speed factor.
 * v1.4.1 (2026-07-29) - Follow-me section box removed entirely per Ajmal ("X no need") - fields,
 *                       toggle, maintenance and ApplySectionBox all deleted; the game is back to
 *                       zero undo entries in every mode.
 * v1.4.0 (2026-07-29) - Workshop-XR-style teleport support: while T is held the aim ray runs every
 *                       frame (any weapon) and feeds TeleportAimValid + distance for the HUD's arc
 *                       preview; the jump itself still happens via TeleportQueued on release.
 *                       BookmarksVersion bumps on every save for the left-side positions list.
 * v1.3.1 (2026-07-29) - Restructure only, zero behaviour change (Ajmal: one GameMode folder, one
 *                       file per feature): moved to src/GameMode and split into 4 partial files -
 *                       core loop here; Movement / Measure / Extras in their own files.
 * v1.3.0 (2026-07-29) - "Add all" round (HandleExtras): T teleports to the aimed point; B saves the
 *                       position into rotating slots 1..9, number keys jump back (restoring the
 *                       saved look direction via the HUD look-override); the CLEANER weapon
 *                       temporarily hides the element hit (host elements only - linked ones are
 *                       refused with a toast; U restores all; try-without-transaction first,
 *                       transaction fallback); X toggles a follow-me section box (10x10x7 m,
 *                       re-centred every 2.5 m walked - each re-centre is one committed
 *                       transaction, honestly noted in the toast); live clear-height (floor to
 *                       obstruction above, mm) fed to the status card each frame; weapon gates
 *                       moved from the old LaserModeOn bool to WeaponSelect (0/1/2).
 * v1.2.1 (2026-07-29) - Measuring now also fills the X and Y axis deltas (mm) for the HUD's new
 *                       axis breakdown line (Z was already there as Vertical).
 * v1.2.0 (2026-07-28) - Added UpdateMeasurement: BIM-360-style hold-and-release face-to-face
 *                       measuring in laser mode. Point A is the exact 3D laser hit on the first
 *                       face; live Total/Horizontal/Vertical distances (mm) update while holding;
 *                       releasing locks the result on the HUD until the next press. Pure reads -
 *                       no model change, works on linked elements too.
 * v1.1.0 (2026-07-28) - Weapon rework per Ajmal's play feedback: the engine no longer handles shots
 *                       (gun fire is pure HUD fun) - instead, while the laser weapon is selected
 *                       (right-click in the HUD) the aim ray runs EVERY frame and feeds both the
 *                       distance and a live element-identity line (LaserTargetInfo).
 * v1.0.0 (2026-07-28) - Initial release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.Utils;

namespace AJTools.Services.GameMode
{
    /// <summary>Per-frame physics + camera driver for Game Mode.</summary>
    internal sealed partial class GameMotionEngine : IExternalEventHandler
    {
        private readonly GameSession _session;
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private double _lastFrameSeconds = -1.0;
        private double _lastIntersectorRefreshSeconds;
        private GameCollisionService _collision;

        // Crouch-aware eye height for this frame (C held while walking = 1000 mm).
        private double _currentEyeHeightFt = GameTuning.EyeHeightFt;

        // Aim/display sync: Revit can 2D-zoom/pan a perspective view like a photo, which moves the
        // picture's centre away from the camera axis - shots then land beside the crosshair
        // (Ajmal's report). ZoomToFit re-centres it; done on start, on resume and on every resize.
        private bool _viewSyncDone;
        private int _prevRectLeft;
        private int _prevRectTop;
        private int _prevRectRight;
        private int _prevRectBottom;

        // Snag marker: everything shot with the snag weapon this session.
        private readonly List<string> _snagReportLines = new List<string>();
        private readonly List<ElementId> _snagIds = new List<ElementId>();
        private ElementId _solidFillPatternId;

        // Levels cache for the compass / level line.
        private List<KeyValuePair<double, string>> _levels;

        // Tour mode (O): auto-walk through the saved positions.
        private bool _tourActive;
        private List<int> _tourSlots;
        private int _tourIndex;
        private double _tourDwellLeft;

        public GameMotionEngine(GameSession session)
        {
            _session = session;
        }

        public string GetName()
        {
            return "AJ Tools - Game Mode";
        }

        public void Execute(UIApplication app)
        {
            if (_session == null || !_session.Running)
            {
                return;
            }

            var frameTimer = Stopwatch.StartNew();
            try
            {
                UIDocument uidoc = app != null ? app.ActiveUIDocument : null;
                if (uidoc == null || uidoc.Document == null)
                {
                    Stop("The document was closed.");
                    return;
                }

                if (!string.Equals(uidoc.Document.Title, _session.DocumentTitle, StringComparison.Ordinal))
                {
                    Stop("You switched to another project - game ended.");
                    return;
                }

                var gameView = uidoc.Document.GetElement(_session.GameViewId) as View3D;
                if (gameView == null)
                {
                    Stop("The game view no longer exists.");
                    return;
                }

                View activeView = uidoc.ActiveView;
                if (activeView == null ||
                    ElementIdHelper.GetIntegerValue(activeView.Id) != ElementIdHelper.GetIntegerValue(_session.GameViewId))
                {
                    Stop("You switched to another view - game ended.");
                    return;
                }

                if (_collision == null)
                {
                    _collision = new GameCollisionService(uidoc.Document, gameView);
                    _lastIntersectorRefreshSeconds = _clock.Elapsed.TotalSeconds;
                }

                double now = _clock.Elapsed.TotalSeconds;
                double dt = _lastFrameSeconds < 0 ? 0.016 : now - _lastFrameSeconds;
                _lastFrameSeconds = now;
                if (dt < 0.005) { dt = 0.005; }
                if (dt > 0.25) { dt = 0.25; }

                // Pick up mid-game visibility/graphics changes every few seconds.
                if (now - _lastIntersectorRefreshSeconds > 4.0)
                {
                    _collision.Refresh();
                    _lastIntersectorRefreshSeconds = now;
                }

                GameInputState input = _session.Input;
                GameHudState hud = _session.Hud;

                HandleModeToggles(input);

                // --- Look vectors ---
                double yawRad = input.YawDeg * Math.PI / 180.0;
                double pitchDeg = Math.Max(-GameTuning.PitchClampDeg, Math.Min(GameTuning.PitchClampDeg, input.PitchDeg));
                double pitchRad = pitchDeg * Math.PI / 180.0;

                var forward = new XYZ(
                    Math.Cos(pitchRad) * Math.Cos(yawRad),
                    Math.Cos(pitchRad) * Math.Sin(yawRad),
                    Math.Sin(pitchRad));
                var horizontalForward = new XYZ(Math.Cos(yawRad), Math.Sin(yawRad), 0);
                var right = new XYZ(Math.Sin(yawRad), -Math.Cos(yawRad), 0);
                XYZ up = right.CrossProduct(forward);

                XYZ position = _session.Position;

                // Crouch: holding C while walking lowers the eye to 1000 mm.
                bool walkMode = _session.Mode == GameMoveMode.Walk;
                _currentEyeHeightFt = (walkMode && input.DescendKey)
                    ? GameTuning.CrouchEyeHeightFt
                    : GameTuning.EyeHeightFt;

                // Teleport, bookmarks, cleaner/snag shots, unhide.
                position = HandleExtras(uidoc, gameView, input, hud, position, forward);

                // Tour mode drives the position by itself while active.
                position = HandleTour(input, hud, position, dt);
                bool touring = _tourActive;
                if (touring)
                {
                    _session.Grounded = false;
                    _session.VerticalVelocityFtPerSec = 0;
                }

                // --- Desired movement ---
                double forwardAxis = (input.MoveForward ? 1 : 0) - (input.MoveBack ? 1 : 0);
                double strafeAxis = (input.StrafeRight ? 1 : 0) - (input.StrafeLeft ? 1 : 0);
                double verticalAxis = (input.AscendKey ? 1 : 0) - (input.DescendKey ? 1 : 0);

                bool flying = _session.Mode != GameMoveMode.Walk;
                double speed = (flying ? GameTuning.FlySpeedFtPerSec : GameTuning.WalkSpeedFtPerSec)
                               * (input.Sprint ? GameTuning.SprintFactor : 1.0)
                               * Math.Max(0.2, Math.Min(4.0, input.SpeedFactor));

                XYZ wish;
                if (flying)
                {
                    wish = (forward * forwardAxis) + (right * strafeAxis) + (XYZ.BasisZ * verticalAxis);
                }
                else
                {
                    wish = (horizontalForward * forwardAxis) + (right * strafeAxis);
                }

                if (!touring && wish.GetLength() > 1e-9)
                {
                    XYZ moveVector = wish.Normalize() * (speed * dt);
                    position = MoveWithCollision(position, moveVector);
                }

                // --- Interaction probe: what am I looking at, close by? (doors/windows hints) ---
                hud.DoorHint = false;
                hud.WindowHint = false;
                if (_session.Mode != GameMoveMode.Ghost)
                {
                    GameRayHit lookHit = _collision.CastNearest(position, forward, GameTuning.DoorReachFt);
                    if (lookHit != null && lookHit.IsDoor)
                    {
                        hud.DoorHint = true;
                        if (input.DoorPassQueued)
                        {
                            XYZ through = horizontalForward.GetLength() > 1e-9 ? horizontalForward : forward;
                            position = position + (through.Normalize() * (lookHit.DistanceFt + GameTuning.DoorPassExtraFt));
                        }
                    }
                    else if (lookHit != null && lookHit.IsWindow && _session.Mode == GameMoveMode.Walk && _session.Grounded)
                    {
                        hud.WindowHint = true;
                    }
                }
                input.DoorPassQueued = false;

                // --- Gravity / jumping (Walk mode only) ---
                hud.NoFloorHover = false;
                if (!touring && _session.Mode == GameMoveMode.Walk)
                {
                    position = ApplyGravity(position, input, hud, dt);
                }
                else
                {
                    _session.VerticalVelocityFtPerSec = 0;
                    _session.Grounded = false;
                    input.JumpQueued = false;
                }

                if (input.RespawnQueued)
                {
                    input.RespawnQueued = false;
                    position = _session.StartPosition;
                    _session.VerticalVelocityFtPerSec = 0;
                    _session.Grounded = false;
                }

                _session.Position = position;

                // --- Apply the camera. NO transaction - verified live: no undo entries, persists. ---
                try
                {
                    gameView.SetOrientation(new ViewOrientation3D(position, up, forward));
                }
                catch (Exception ex)
                {
                    Stop("Revit refused the camera move (" + ex.Message + ").");
                    return;
                }
                uidoc.RefreshActiveView();

                // --- Laser: live rangefinder + element identity (gun shots are HUD-side fun only) ---
                // The same aim ray also feeds the Workshop-XR-style teleport arc while T is held.
                bool laserSelected = input.WeaponSelect == 1;
                GameRayHit aimHit = null;
                if (laserSelected || input.TeleportAimHold)
                {
                    aimHit = _collision.CastNearest(position, forward, GameTuning.AimRangeFt);
                }
                hud.LaserHasHit = laserSelected && aimHit != null;
                hud.LaserDistanceMm = aimHit != null ? aimHit.DistanceFt / Constants.MM_TO_FEET : 0;
                hud.LaserTargetInfo = (laserSelected && aimHit != null) ? GameCollisionService.Describe(aimHit, false) : "";
                hud.TeleportAimValid = input.TeleportAimHold && aimHit != null;
                hud.TeleportAimDistanceMm = hud.TeleportAimValid ? aimHit.DistanceFt / Constants.MM_TO_FEET : 0;

                // --- Live clear height (floor to whatever is above), in mm ---
                hud.ClearHeightMm = 0;
                GameRayHit floorProbe = _collision.CastNearest(position, new XYZ(0, 0, -1), GameTuning.FloorSearchFt);
                if (floorProbe != null)
                {
                    GameRayHit ceilingProbe = _collision.CastNearest(position, XYZ.BasisZ, GameTuning.FloorSearchFt);
                    if (ceilingProbe != null)
                    {
                        hud.ClearHeightMm = (floorProbe.DistanceFt + ceilingProbe.DistanceFt) / Constants.MM_TO_FEET;
                    }
                }

                // --- HUD status + overlay rectangle ---
                hud.CompassText = BuildCompassText(input.YawDeg, position, uidoc.Document);
                UpdateHudStatus(hud, position, input.Sprint);
                UpdateViewRectangle(uidoc, hud, input);
            }
            catch (Exception ex)
            {
                Stop("Game error: " + ex.Message);
            }
            finally
            {
                frameTimer.Stop();
                _session.Hud.LastFrameMs = frameTimer.Elapsed.TotalMilliseconds;
            }
        }

        private void HandleModeToggles(GameInputState input)
        {
            if (input.FlyToggleQueued)
            {
                input.FlyToggleQueued = false;
                _session.Mode = _session.Mode == GameMoveMode.Walk ? GameMoveMode.Fly : GameMoveMode.Walk;
            }

            if (input.GhostToggleQueued)
            {
                input.GhostToggleQueued = false;
                _session.Mode = _session.Mode == GameMoveMode.Ghost ? GameMoveMode.Walk : GameMoveMode.Ghost;
            }
        }

        private void UpdateHudStatus(GameHudState hud, XYZ position, bool sprinting)
        {
            string mode;
            switch (_session.Mode)
            {
                case GameMoveMode.Fly:
                    mode = "FLY";
                    break;
                case GameMoveMode.Ghost:
                    mode = "GHOST (through walls)";
                    break;
                default:
                    mode = _session.Grounded
                        ? "WALK - on floor"
                        : (hud.NoFloorHover ? "WALK - hovering (no floor found)" : "WALK - in air");
                    break;
            }

            if (sprinting)
            {
                mode += "  |  sprint";
            }

            hud.ModeLine = mode;
            hud.PositionLine = string.Format(
                "X {0:N0}   Y {1:N0}   Z {2:N0}  mm",
                position.X / Constants.MM_TO_FEET,
                position.Y / Constants.MM_TO_FEET,
                position.Z / Constants.MM_TO_FEET);
        }

        private void UpdateViewRectangle(UIDocument uidoc, GameHudState hud, GameInputState input)
        {
            hud.RectValid = false;
            foreach (UIView uiView in uidoc.GetOpenUIViews())
            {
                if (ElementIdHelper.GetIntegerValue(uiView.ViewId) != ElementIdHelper.GetIntegerValue(_session.GameViewId))
                {
                    continue;
                }

                var rect = uiView.GetWindowRectangle();
                hud.RectLeft = rect.Left;
                hud.RectTop = rect.Top;
                hud.RectRight = rect.Right;
                hud.RectBottom = rect.Bottom;
                hud.RectValid = true;

                // Keep the rendered picture centred on the camera axis so the crosshair and the
                // real aim can never drift apart (first frame, resume, or any window resize).
                bool rectChanged = rect.Left != _prevRectLeft || rect.Top != _prevRectTop
                                   || rect.Right != _prevRectRight || rect.Bottom != _prevRectBottom;
                if (!_viewSyncDone || rectChanged || input.ResyncViewQueued)
                {
                    try
                    {
                        uiView.ZoomToFit();
                    }
                    catch (Exception)
                    {
                        // A refused fit must never kill the frame; alignment retries on the next change.
                    }
                    _viewSyncDone = true;
                    input.ResyncViewQueued = false;
                }
                _prevRectLeft = rect.Left;
                _prevRectTop = rect.Top;
                _prevRectRight = rect.Right;
                _prevRectBottom = rect.Bottom;
                return;
            }

            Stop("The game view window was closed.");
        }

        /// <summary>"Facing N | Level 03" for the status card.</summary>
        private string BuildCompassText(double yawDeg, XYZ position, Document doc)
        {
            if (_levels == null)
            {
                _levels = new List<KeyValuePair<double, string>>();
                foreach (Element element in new FilteredElementCollector(doc).OfClass(typeof(Level)))
                {
                    var level = element as Level;
                    if (level != null)
                    {
                        _levels.Add(new KeyValuePair<double, string>(level.Elevation, level.Name));
                    }
                }
                _levels.Sort((a, b) => a.Key.CompareTo(b.Key));
            }

            // Yaw is measured from +X (east), turning towards +Y (project north).
            double angle = ((yawDeg % 360.0) + 360.0) % 360.0;
            string[] directions = { "E", "NE", "N", "NW", "W", "SW", "S", "SE" };
            string facing = directions[(int)Math.Round(angle / 45.0) % 8];

            string levelName = "";
            double feetZ = position.Z - _currentEyeHeightFt;
            for (int i = 0; i < _levels.Count; i++)
            {
                if (_levels[i].Key <= feetZ + 0.5)
                {
                    levelName = _levels[i].Value;
                }
            }

            return string.IsNullOrEmpty(levelName)
                ? "Facing " + facing
                : "Facing " + facing + "   |   " + levelName;
        }

        private void Stop(string reason)
        {
            // Ending with snag marks pending: write the punch-list report (Documents\AJ Game Snags).
            if (_snagReportLines.Count > 0)
            {
                try
                {
                    string folder = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "AJ Game Snags");
                    System.IO.Directory.CreateDirectory(folder);
                    string file = System.IO.Path.Combine(
                        folder, "Snags_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt");

                    var report = new System.Text.StringBuilder();
                    report.AppendLine("AJ Game Mode - Snag report");
                    report.AppendLine("Project: " + _session.DocumentTitle);
                    report.AppendLine("Date: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
                    report.AppendLine(new string('-', 70));
                    foreach (string line in _snagReportLines)
                    {
                        report.AppendLine(line);
                    }
                    System.IO.File.WriteAllText(file, report.ToString());

                    reason = (string.IsNullOrEmpty(reason) ? "" : reason + "   ")
                             + "Snag report saved: Documents\\AJ Game Snags\\" + System.IO.Path.GetFileName(file);
                    _snagReportLines.Clear();
                }
                catch (Exception)
                {
                    // The game must always end cleanly, report or not.
                }
            }

            _session.Running = false;
            _session.Hud.Alive = false;
            _session.Hud.StopReason = reason;
        }
    }
}
