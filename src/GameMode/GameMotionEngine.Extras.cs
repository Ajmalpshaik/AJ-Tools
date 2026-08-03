#region Metadata
/*
 * Tool Name     : AJ Game Mode (motion engine - extras)
 * File Name     : GameMotionEngine.Extras.cs
 * Purpose       : Partial of GameMotionEngine: teleport (T), saved positions (B / 1..9), the
 *                 CLEANER weapon's temporary hide (U restores), the SNAG MARKER's red paint +
 *                 punch list, the SELECTOR's live Revit selection, the J full graphics reset and
 *                 tour mode (O). See GameMotionEngine.cs for the class metadata, fields and full
 *                 changelog.
 *
 * Author        : Ajmal P.S.
 * Created Date  : 2026-07-29 (split out of GameMotionEngine.cs v1.3.1, code unchanged)
 * License       : All Rights Reserved   |   Repo: AJ-Tools
 */
#endregion

using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.Utils;

namespace AJTools.Services.GameMode
{
    internal sealed partial class GameMotionEngine
    {
        /// <summary>
        /// The extras Ajmal picked ("add all"): teleport, save/jump positions, cleaner-weapon
        /// temporary hide, unhide-all, snag marking, selector shots and the J graphics reset.
        /// </summary>
        private XYZ HandleExtras(UIDocument uidoc, View3D gameView, GameInputState input, GameHudState hud, XYZ position, XYZ forward)
        {
            // T - teleport to the point under the crosshair.
            if (input.TeleportQueued)
            {
                input.TeleportQueued = false;
                GameRayHit target = _collision.CastNearest(position, forward, GameTuning.AimRangeFt);
                if (target != null)
                {
                    position = target.Point - (forward * (GameTuning.BodyRadiusFt * 2.0));
                    _session.VerticalVelocityFtPerSec = 0;
                    _session.Grounded = false;
                    hud.Toast("Teleported");
                }
                else
                {
                    hud.Toast("Aim at something first, then press T");
                }
            }

            // B - save the current position into the next slot (unlimited; the tour visits all).
            if (input.SaveBookmarkQueued)
            {
                input.SaveBookmarkQueued = false;
                int slot = _session.NextBookmarkSlot;
                _session.Bookmarks[slot] = new[] { position.X, position.Y, position.Z, input.YawDeg, input.PitchDeg };
                _session.NextBookmarkSlot = slot + 1;
                _session.BookmarksVersion++;
                hud.Toast(slot <= 9
                    ? "Saved position " + slot + " - press " + slot + " to come back"
                    : "Saved position " + slot + " - the tour (O) visits it");
            }

            // 1..9 - jump to a saved position (restores the saved look direction too).
            if (input.JumpBookmarkSlot > 0)
            {
                int slot = input.JumpBookmarkSlot;
                input.JumpBookmarkSlot = 0;
                double[] saved;
                if (_session.Bookmarks.TryGetValue(slot, out saved))
                {
                    position = new XYZ(saved[0], saved[1], saved[2]);
                    hud.LookOverrideYawDeg = saved[3];
                    hud.LookOverridePitchDeg = saved[4];
                    hud.LookOverridePending = true;
                    _session.VerticalVelocityFtPerSec = 0;
                    _session.Grounded = false;
                    hud.Toast("Position " + slot);
                }
                else
                {
                    hud.Toast("No saved position " + slot + " yet - press B to save one");
                }
            }

            // Cleaner weapon shot: temporarily hide the element hit (U restores).
            if (input.CleanShotQueued)
            {
                input.CleanShotQueued = false;
                GameRayHit victim = _collision.CastNearest(position, forward, GameTuning.AimRangeFt);
                if (victim == null || victim.ResolvedElement == null)
                {
                    hud.Toast("No element hit");
                }
                else if (victim.FromLink)
                {
                    hud.Toast("That element is in a linked model - it cannot be hidden from here");
                }
                else if (TryTemporaryHide(uidoc.Document, gameView, victim.ResolvedElement.Id))
                {
                    hud.Toast("Hidden: " + GameCollisionService.Describe(victim, false) + "   (U brings everything back)");
                    _collision.Refresh();
                }
                else
                {
                    hud.Toast("Revit refused to hide that element");
                }
            }

            // U - bring back everything the cleaner hid.
            if (input.UnhideAllQueued)
            {
                input.UnhideAllQueued = false;
                if (TryRestoreHidden(uidoc.Document, gameView))
                {
                    hud.Toast("All temporarily hidden elements are back");
                    _collision.Refresh();
                }
                else
                {
                    hud.Toast("Nothing to restore");
                }
            }

            // Snag-marker weapon: paint the element red and add it to the punch list.
            if (input.SnagShotQueued)
            {
                input.SnagShotQueued = false;
                GameRayHit victim = _collision.CastNearest(position, forward, GameTuning.AimRangeFt);
                if (victim == null || victim.ResolvedElement == null)
                {
                    hud.Toast("No element hit");
                }
                else if (victim.FromLink)
                {
                    hud.Toast("Linked element - it cannot be marked (host elements only)");
                }
                else if (TrySnagMark(uidoc.Document, gameView, victim.ResolvedElement.Id))
                {
                    string what = GameCollisionService.Describe(victim, false);
                    _snagReportLines.Add(string.Format(
                        "{0}. {1}   |   hit at X {2:N0}  Y {3:N0}  Z {4:N0}  mm",
                        _snagReportLines.Count + 1,
                        what,
                        victim.Point.X / Constants.MM_TO_FEET,
                        victim.Point.Y / Constants.MM_TO_FEET,
                        victim.Point.Z / Constants.MM_TO_FEET));
                    hud.SnagCount = _snagReportLines.Count;
                    hud.Toast("SNAG " + _snagReportLines.Count + ":  " + what);
                }
                else
                {
                    hud.Toast("Revit refused to mark that element");
                }
            }

            // Selector weapon: shot adds the element to the live Revit selection (shot again
            // removes it). The selection survives the game - exit and edit straight away.
            if (input.SelectShotQueued)
            {
                input.SelectShotQueued = false;
                GameRayHit victim = _collision.CastNearest(position, forward, GameTuning.AimRangeFt);
                if (victim == null || victim.ResolvedElement == null)
                {
                    hud.Toast("No element hit");
                }
                else if (victim.FromLink)
                {
                    hud.Toast("Linked element - it cannot be selected from here");
                }
                else
                {
                    try
                    {
                        var ids = new HashSet<ElementId>(uidoc.Selection.GetElementIds());
                        ElementId id = victim.ResolvedElement.Id;
                        string what = GameCollisionService.Describe(victim, false);
                        if (ids.Contains(id))
                        {
                            ids.Remove(id);
                            uidoc.Selection.SetElementIds(new List<ElementId>(ids));
                            hud.Toast("Unselected: " + what + "   (" + ids.Count + " selected)");
                        }
                        else
                        {
                            ids.Add(id);
                            uidoc.Selection.SetElementIds(new List<ElementId>(ids));
                            hud.Toast("Selected: " + what + "   (" + ids.Count + " selected - stays after exit)");
                        }
                    }
                    catch (Exception)
                    {
                        hud.Toast("Revit refused the selection");
                    }
                }
            }

            // J - reset ALL element colors in the game view (same approach as the existing
            // "Reset Element Graphics in View" tool), so even marks from earlier game sessions go.
            if (input.ResetGraphicsQueued)
            {
                input.ResetGraphicsQueued = false;
                ResetAllViewGraphics(uidoc.Document, gameView, hud);
            }

            return position;
        }

        private bool TrySnagMark(Document doc, View3D view, ElementId elementId)
        {
            try
            {
                if (_solidFillPatternId == null)
                {
                    foreach (Element element in new FilteredElementCollector(doc).OfClass(typeof(FillPatternElement)))
                    {
                        var pattern = element as FillPatternElement;
                        if (pattern != null && pattern.GetFillPattern().IsSolidFill)
                        {
                            _solidFillPatternId = pattern.Id;
                            break;
                        }
                    }
                }

                var red = new Autodesk.Revit.DB.Color(255, 40, 40);
                var overrides = new OverrideGraphicSettings();
                overrides.SetProjectionLineColor(red);
                if (_solidFillPatternId != null)
                {
                    overrides.SetSurfaceForegroundPatternId(_solidFillPatternId);
                    overrides.SetSurfaceForegroundPatternColor(red);
                }

                using (var t = new Transaction(doc, "AJ Tools - Snag Mark"))
                {
                    t.Start();
                    view.SetElementOverrides(elementId, overrides);
                    t.Commit();
                }
                _snagIds.Add(elementId);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Resets the graphic override of EVERY element in the game view (mirrors the existing
        /// Reset Element Graphics tool: default OverrideGraphicSettings per element, failures
        /// skipped). Also empties the snag punch list.
        /// </summary>
        private void ResetAllViewGraphics(Document doc, View3D view, GameHudState hud)
        {
            try
            {
                var reset = new OverrideGraphicSettings();
                int applied = 0;
                using (var t = new Transaction(doc, "AJ Tools - Game Reset Graphics in View"))
                {
                    t.Start();
                    foreach (Element element in new FilteredElementCollector(doc, view.Id).WhereElementIsNotElementType())
                    {
                        try
                        {
                            view.SetElementOverrides(element.Id, reset);
                            applied++;
                        }
                        catch (Exception)
                        {
                            // Some elements refuse overrides - same skip rule as the existing tool.
                        }
                    }
                    t.Commit();
                }

                _snagIds.Clear();
                _snagReportLines.Clear();
                hud.SnagCount = 0;
                hud.Toast("All element colors in the game view reset (" + applied.ToString("N0") + " elements)");
            }
            catch (Exception)
            {
                hud.Toast("Could not reset the element colors");
            }
        }

        /// <summary>
        /// Tour mode (O): fly smoothly through the saved positions in slot order, adopting each
        /// saved look direction on arrival. Any movement key stops it.
        /// </summary>
        private XYZ HandleTour(GameInputState input, GameHudState hud, XYZ position, double dt)
        {
            if (input.TourToggleQueued)
            {
                input.TourToggleQueued = false;
                if (_tourActive)
                {
                    EndTour(hud, "Tour ended");
                }
                else
                {
                    // EVERY saved position, in saved order - however many there are (Ajmal's ask).
                    _tourSlots = new List<int>(_session.Bookmarks.Keys);
                    _tourSlots.Sort();

                    if (_tourSlots.Count < 1)
                    {
                        hud.Toast("Save positions with B first - then O plays the tour");
                    }
                    else
                    {
                        _tourActive = true;
                        _tourIndex = 0;
                        _tourDwellLeft = 0;
                        hud.TourRunning = true;
                        hud.Toast("Tour started - sit back (any move key stops it)");
                    }
                }
            }

            if (!_tourActive)
            {
                return position;
            }

            if (input.MoveForward || input.MoveBack || input.StrafeLeft || input.StrafeRight || input.JumpQueued)
            {
                input.JumpQueued = false;
                EndTour(hud, "Tour stopped");
                return position;
            }

            double[] target = _session.Bookmarks[_tourSlots[_tourIndex]];
            var targetPoint = new XYZ(target[0], target[1], target[2]);

            if (_tourDwellLeft > 0)
            {
                _tourDwellLeft -= dt;
                hud.LookOverrideYawDeg = target[3];
                hud.LookOverridePitchDeg = target[4];
                hud.LookOverridePending = true;
                if (_tourDwellLeft <= 0)
                {
                    _tourIndex++;
                    if (_tourIndex >= _tourSlots.Count)
                    {
                        EndTour(hud, "Tour finished");
                    }
                }
                return position;
            }

            XYZ delta = targetPoint - position;
            double distance = delta.GetLength();
            double step = GameTuning.WalkSpeedFtPerSec * 2.0 * dt;
            if (distance <= step || distance < 1e-6)
            {
                _tourDwellLeft = 1.6;
                return targetPoint;
            }

            XYZ direction = delta.Normalize();
            double yaw = Math.Atan2(direction.Y, direction.X) * 180.0 / Math.PI;
            double pitch = Math.Asin(Math.Max(-1.0, Math.Min(1.0, direction.Z))) * 180.0 / Math.PI;
            hud.LookOverrideYawDeg = yaw;
            hud.LookOverridePitchDeg = Math.Max(-60.0, Math.Min(60.0, pitch));
            hud.LookOverridePending = true;

            return position + (direction * step);
        }

        private void EndTour(GameHudState hud, string message)
        {
            _tourActive = false;
            hud.TourRunning = false;
            if (_tourSlots != null)
            {
                _tourSlots.Clear();
            }
            hud.Toast(message);
        }

        private static bool TryTemporaryHide(Document doc, View3D view, ElementId elementId)
        {
            var ids = new List<ElementId> { elementId };
            try
            {
                view.HideElementsTemporary(ids);
                return true;
            }
            catch (Autodesk.Revit.Exceptions.ModificationOutsideTransactionException)
            {
                try
                {
                    using (var t = new Transaction(doc, "AJ Tools - Game Hide"))
                    {
                        t.Start();
                        view.HideElementsTemporary(ids);
                        t.Commit();
                    }
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool TryRestoreHidden(Document doc, View3D view)
        {
            try
            {
                view.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate);
                return true;
            }
            catch (Autodesk.Revit.Exceptions.ModificationOutsideTransactionException)
            {
                try
                {
                    using (var t = new Transaction(doc, "AJ Tools - Game Unhide"))
                    {
                        t.Start();
                        view.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate);
                        t.Commit();
                    }
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

    }
}
