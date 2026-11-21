#region Metadata
/*
 * Tool Name     : AJ Game Mode (motion engine - third-person view)
 * File Name     : GameMotionEngine.ThirdPerson.cs
 * Purpose       : Partial of GameMotionEngine: the third-person camera (behind and right of the
 *                 player, sliding in when a wall is behind) and the character's pose for this frame
 *                 (where it stands, which way it faces, how fast it moves - which drives its stand /
 *                 walk / run animation). The character itself is drawn by GameCharacterServer. See GameMotionEngine.cs for the
 *                 class metadata, fields and full changelog.
 *
 * Author        : Ajmal P.S.
 * Created Date  : 2026-11-21
 * License       : All Rights Reserved   |   Repo: AJ-Tools
 */
#endregion

using System;
using Autodesk.Revit.DB;
using AJTools.Utils;

namespace AJTools.Services.GameMode
{
    internal sealed partial class GameMotionEngine
    {
        // Limb swing size (radians): walking, running, and how fast it eases in / out.
        private const double WalkSwingRad = 0.45;   // ~26 degrees
        private const double RunSwingRad = 0.75;    // ~43 degrees
        private const double SwingEasePerSec = 8.0;

        /// <summary>
        /// Where aim rays start: the camera in third person (so shots land on the crosshair), the
        /// player's eye in first person.
        /// </summary>
        private XYZ AimOrigin(XYZ position)
        {
            return (_session.Input.ThirdPerson && _thirdPersonEye != null) ? _thirdPersonEye : position;
        }

        /// <summary>
        /// Returns the camera eye for this frame. First person: the player's eye, character hidden.
        /// Third person: a point behind and right of the player, pulled in front of any wall in
        /// between, and the character's pose updated for the next redraw.
        /// </summary>
        private XYZ UpdateThirdPerson(View3D gameView, GameInputState input, XYZ position, XYZ forward, XYZ right, double dt)
        {
            GameCharacterPose pose = GameCharacterServer.Pose;

            if (!input.ThirdPerson || !GameCharacterServer.EnsureRegistered())
            {
                pose.Visible = false;
                _thirdPersonEye = null;
                _characterLastFeet = null;
                return position;
            }

            // --- Character: feet on the floor under the eye, facing the look direction ---
            XYZ feet = new XYZ(position.X, position.Y, position.Z - _currentEyeHeightFt);

            double walkedFt = 0.0;
            if (_characterLastFeet != null)
            {
                double dx = feet.X - _characterLastFeet.X;
                double dy = feet.Y - _characterLastFeet.Y;
                walkedFt = Math.Sqrt((dx * dx) + (dy * dy));

                // A teleport / bookmark jump / respawn is not a step - no wild leg spin.
                if (walkedFt > GameTuning.StrideFt * 2.0)
                {
                    walkedFt = 0.0;
                }
            }
            _characterLastFeet = feet;

            double speedFtPerSec = dt > 1e-6 ? walkedFt / dt : 0.0;
            bool running = speedFtPerSec > GameTuning.WalkSpeedFtPerSec * 1.6;
            double targetSwing = 0.0;
            if (speedFtPerSec > 0.2)
            {
                targetSwing = running ? RunSwingRad : WalkSwingRad;
            }

            _swingAmplitudeRad += (targetSwing - _swingAmplitudeRad) * Math.Min(1.0, dt * SwingEasePerSec);
            _walkPhaseRad = (_walkPhaseRad + (walkedFt / GameTuning.StrideFt * Math.PI)) % (Math.PI * 2.0);

            double cos = Math.Cos(_walkPhaseRad);
            pose.Feet = feet;
            pose.YawRad = input.YawDeg * Math.PI / 180.0;
            pose.SwingRad = _swingAmplitudeRad * Math.Sin(_walkPhaseRad);
            // Each knee bends while its leg swings through under the body, straight while it carries.
            pose.KneeLeftRad = _swingAmplitudeRad * 1.2 * (0.5 + (0.5 * cos));
            pose.KneeRightRad = _swingAmplitudeRad * 1.2 * (0.5 - (0.5 * cos));
            pose.ViewId = ElementIdHelper.GetIntegerValue(gameView.Id);
            pose.Visible = true;

            // The real character (if its file loaded): stand / walk / run to match this frame.
            GameCharacterServer.Advance(dt, speedFtPerSec, running);

            // --- Camera: behind + right of the player's head, never through a wall ---
            XYZ pivot = new XYZ(feet.X, feet.Y, feet.Z + GameTuning.ThirdPersonPivotFt);
            XYZ wanted = pivot - (forward * GameTuning.ThirdPersonBackFt) + (right * GameTuning.ThirdPersonShoulderFt);

            XYZ offset = wanted - pivot;
            double length = offset.GetLength();
            if (length < 1e-6)
            {
                _thirdPersonEye = pivot;
                return pivot;
            }

            XYZ direction = offset / length;
            double allowed = length;
            GameRayHit blocker = _collision.CastNearest(pivot, direction, length);
            if (blocker != null)
            {
                allowed = Math.Max(GameTuning.ThirdPersonMinBackFt, blocker.DistanceFt - GameTuning.ThirdPersonWallGapFt);
                allowed = Math.Min(allowed, length);
            }

            _thirdPersonEye = pivot + (direction * allowed);
            return _thirdPersonEye;
        }
    }
}
