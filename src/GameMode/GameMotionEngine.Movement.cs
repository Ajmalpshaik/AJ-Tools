#region Metadata
/*
 * Tool Name     : AJ Game Mode (motion engine - walking physics)
 * File Name     : GameMotionEngine.Movement.cs
 * Purpose       : Partial of GameMotionEngine: collision-checked movement (with wall sliding),
 *                 gravity, jumping, stairs step-up, window climb-through and the no-floor hover.
 *                 See GameMotionEngine.cs for the class metadata, fields and full changelog.
 *
 * Author        : Ajmal P.S.
 * Created Date  : 2026-07-29 (split out of GameMotionEngine.cs v1.3.1, code unchanged)
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
        /// <summary>
        /// Try to move; when blocked, slide along the free axes instead of stopping dead.
        /// Ghost mode skips all of this.
        /// </summary>
        private XYZ MoveWithCollision(XYZ position, XYZ moveVector)
        {
            if (_session.Mode == GameMoveMode.Ghost)
            {
                return position + moveVector;
            }

            if (TryDirection(position, moveVector))
            {
                return position + moveVector;
            }

            // Axis slide: keep whatever component is free (walk along the wall, not into it).
            XYZ result = position;
            var xPart = new XYZ(moveVector.X, 0, 0);
            var yPart = new XYZ(0, moveVector.Y, 0);
            var zPart = new XYZ(0, 0, moveVector.Z);

            if (Math.Abs(moveVector.X) > 1e-9 && TryDirection(result, xPart)) { result = result + xPart; }
            if (Math.Abs(moveVector.Y) > 1e-9 && TryDirection(result, yPart)) { result = result + yPart; }
            if (Math.Abs(moveVector.Z) > 1e-9 && TryDirection(result, zPart)) { result = result + zPart; }
            return result;
        }

        /// <summary>Is this step free of obstacles (body-width check at head, chest and shin height)?</summary>
        private bool TryDirection(XYZ position, XYZ moveVector)
        {
            double length = moveVector.GetLength();
            if (length < 1e-9)
            {
                return false;
            }

            XYZ direction = moveVector.Normalize();
            double reach = length + GameTuning.BodyRadiusFt;
            bool airborne = !_session.Grounded;

            // Eye, chest and shin rays, scaled to the current (possibly crouched) eye height so
            // the shin ray always stays ~300 mm above the floor - stairs are handled by the
            // gravity step-up snap instead.
            double eyeFt = _currentEyeHeightFt;
            double[] drops = { 0.0, eyeFt * 0.47, Math.Max(0.0, eyeFt - (300.0 * Constants.MM_TO_FEET)) };
            foreach (double drop in drops)
            {
                XYZ origin = new XYZ(position.X, position.Y, position.Z - drop);
                GameRayHit hit = _collision.CastNearest(origin, direction, reach);
                if (hit == null)
                {
                    continue;
                }

                // Windows stop blocking while you are in the air - jump and move to climb through.
                if (hit.IsWindow && airborne)
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        private XYZ ApplyGravity(XYZ position, GameInputState input, GameHudState hud, double dt)
        {
            if (input.JumpQueued)
            {
                input.JumpQueued = false;
                if (_session.Grounded)
                {
                    _session.VerticalVelocityFtPerSec = GameTuning.JumpSpeedFtPerSec;
                    _session.Grounded = false;
                }
            }

            GameRayHit groundHit = _collision.CastNearest(position, new XYZ(0, 0, -1), GameTuning.FloorSearchFt);

            if (_session.Grounded)
            {
                if (groundHit == null)
                {
                    // Nothing to stand on anywhere below (open shaft, or a model with no floors at
                    // all - e.g. a pure MEP file). Hover instead of falling forever.
                    hud.NoFloorHover = true;
                    _session.VerticalVelocityFtPerSec = 0;
                    return position;
                }

                double gap = groundHit.DistanceFt - _currentEyeHeightFt;
                if (Math.Abs(gap) <= GameTuning.StepUpFt)
                {
                    // Stairs / ramps / thresholds: follow the floor.
                    return new XYZ(position.X, position.Y, position.Z - gap);
                }

                if (gap > GameTuning.StepUpFt)
                {
                    // Walked off an edge - start falling.
                    _session.Grounded = false;
                    _session.VerticalVelocityFtPerSec = 0;
                    return position;
                }

                // Floor pushed up above step height (rare - e.g. respawned inside geometry): snap up.
                return new XYZ(position.X, position.Y, position.Z - gap);
            }

            // Airborne: integrate gravity.
            _session.VerticalVelocityFtPerSec -= GameTuning.GravityFtPerSec2 * dt;
            if (_session.VerticalVelocityFtPerSec < -GameTuning.MaxFallFtPerSec)
            {
                _session.VerticalVelocityFtPerSec = -GameTuning.MaxFallFtPerSec;
            }

            double dz = _session.VerticalVelocityFtPerSec * dt;

            // Head bonk on the way up (skylight windows still let you through).
            if (dz > 0)
            {
                GameRayHit ceiling = _collision.CastNearest(position, XYZ.BasisZ, dz + (300.0 * Constants.MM_TO_FEET));
                if (ceiling != null && !ceiling.IsWindow)
                {
                    _session.VerticalVelocityFtPerSec = 0;
                    dz = 0;
                }
            }

            var moved = new XYZ(position.X, position.Y, position.Z + dz);

            // Landing check.
            if (_session.VerticalVelocityFtPerSec <= 0)
            {
                GameRayHit landing = _collision.CastNearest(moved, new XYZ(0, 0, -1), _currentEyeHeightFt + (50.0 * Constants.MM_TO_FEET));
                if (landing != null && landing.DistanceFt <= _currentEyeHeightFt)
                {
                    _session.Grounded = true;
                    _session.VerticalVelocityFtPerSec = 0;
                    return new XYZ(moved.X, moved.Y, moved.Z + (_currentEyeHeightFt - landing.DistanceFt));
                }

                if (groundHit == null && moved.Z < _session.StartPosition.Z - GameTuning.FloorSearchFt)
                {
                    // Absolute safety net: fell absurdly far with no floor anywhere - respawn.
                    _session.Grounded = false;
                    _session.VerticalVelocityFtPerSec = 0;
                    return _session.StartPosition;
                }
            }

            return moved;
        }
    }
}
