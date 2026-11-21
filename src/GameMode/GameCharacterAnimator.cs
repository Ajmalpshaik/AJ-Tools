#region Metadata
/*
 * Tool Name     : AJ Game Mode (third-person character - animation)
 * File Name     : GameCharacterAnimator.cs
 * Purpose       : Plays the character's Idle / Walk / Run clips and bends its mesh with its bones
 *                 every frame: stands breathing when the player stops, walks with W, runs with
 *                 Shift, with a short cross-fade between them so the change never snaps. The legs
 *                 speed up with the player's speed, up to a cap. Pure maths - no Revit API; the
 *                 result is the posed mesh in the file's own axes and units.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-11-21
 * Last Updated  : 2026-11-21
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : GameCharacterModel
 *
 * Input         : Frame time, the player's ground speed (ft/s), whether the player is running.
 * Output        : Posed vertex positions and normals per mesh part (file axes, file units), plus the
 *                 scale and floor offset that make the character the wanted height on the floor.
 *
 * Notes         :
 * - Standard linear-blend skinning: each vertex = sum of (weight x joint matrix x rest vertex),
 *   joint matrix = bone's world pose x the file's inverse bind matrix. Proven against the shipped
 *   file in a separate check before this code was written (standing, feet on the floor, face front
 *   in Idle, Walk and Run).
 * - The clips walk on the spot (no forward drift), so nothing has to be stripped out.
 * - Natural clip speed is measured from how far the feet travel front-to-back in the clip; the
 *   playback rate is player speed / natural speed, capped at 2.2x (walk) / 2.4x (run) so the legs
 *   never blur. Game speeds are arcade-fast (walk 3500 mm/s needs ~3.6x the walk clip, sprint ~6x
 *   the run clip), so in practice both sit on the cap and the feet slide a little - a deliberate
 *   trade for legs that still read as walking / running.
 * - ~4,600 vertices: posing + skinning costs well under a millisecond, done once per game frame
 *   (not per redraw - Revit may redraw several times per frame).
 *
 * Changelog     :
 * v1.0.0 (2026-11-21) - Initial release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Collections.Generic;

namespace AJTools.Services.GameMode
{
    /// <summary>Poses and skins a GameCharacterModel frame by frame.</summary>
    internal sealed class GameCharacterAnimator
    {
        private const double FadeSec = 0.2;               // cross-fade between clips
        private const double MovingFtPerSec = 0.6;        // below this the character stands (~180 mm/s)
        private const double MinRate = 0.5;
        private const double MaxWalkRate = 2.2;
        private const double MaxRunRate = 2.4;
        private const double DefaultWalkMmPerSec = 1000.0;  // used only if the clip speed cannot be measured
        private const double DefaultRunMmPerSec = 1700.0;

        private readonly GameCharacterModel _model;
        private readonly GameCharacterModel.Clip _idle;
        private readonly GameCharacterModel.Clip _walk;
        private readonly GameCharacterModel.Clip _run;
        private readonly double _walkNaturalFtPerSec;
        private readonly double _runNaturalFtPerSec;
        private readonly int[] _order;                    // nodes, parents before children

        // Working pose buffers (per node).
        private readonly double[] _t;
        private readonly double[] _r;
        private readonly double[] _tB;
        private readonly double[] _rB;
        private readonly double[][] _world;
        private readonly double[][] _joint;

        // Clip state.
        private GameCharacterModel.Clip _current;
        private double _currentTime;
        private GameCharacterModel.Clip _previous;
        private double _previousTime;
        private double _previousRate;
        private double _fade = 1.0;                       // 1 = only the current clip

        /// <summary>Posed positions per part (3 floats per vertex, file axes and units).</summary>
        public readonly float[][] Positions;

        /// <summary>Posed normals per part (3 floats per vertex, unit length).</summary>
        public readonly float[][] Normals;

        /// <summary>File units to Revit feet (makes the character the wanted height).</summary>
        public readonly double UnitsToFeet;

        /// <summary>The file-units height of the soles in the standing pose (subtracted so feet touch the floor).</summary>
        public readonly double FloorOffsetUnits;

        /// <summary>True once at least one pose has been computed.</summary>
        public bool HasPose { get; private set; }

        /// <summary>The character being animated (parts, materials).</summary>
        public GameCharacterModel Model { get { return _model; } }

        internal GameCharacterAnimator(GameCharacterModel model, double wantedHeightFt)
        {
            _model = model;
            int n = model.Nodes.Length;
            _t = new double[n * 3];
            _r = new double[n * 4];
            _tB = new double[n * 3];
            _rB = new double[n * 4];
            _world = new double[n][];
            for (int i = 0; i < n; i++)
            {
                _world[i] = new double[12];
            }
            _joint = new double[model.SkinJoints.Length][];
            for (int j = 0; j < _joint.Length; j++)
            {
                _joint[j] = new double[12];
            }
            _order = BuildOrder(model);

            Positions = new float[model.Parts.Length][];
            Normals = new float[model.Parts.Length][];
            for (int p = 0; p < model.Parts.Length; p++)
            {
                Positions[p] = new float[model.Parts[p].Positions.Length];
                Normals[p] = new float[model.Parts[p].Normals.Length];
            }

            _idle = Find("Idle");
            _walk = Find("Walk") ?? _idle;
            _run = Find("Run") ?? _walk;
            _current = _idle ?? _walk ?? _run;

            // Size: pose the standing clip once and measure it.
            ComputePose();
            Skin();
            double minY, maxY;
            MeasureHeight(out minY, out maxY);
            double heightUnits = Math.Max(1e-6, maxY - minY);
            UnitsToFeet = wantedHeightFt / heightUnits;
            FloorOffsetUnits = minY;

            double mmToFt = 1.0 / 304.8;
            _walkNaturalFtPerSec = NaturalSpeedFtPerSec(_walk, DefaultWalkMmPerSec * mmToFt);
            _runNaturalFtPerSec = NaturalSpeedFtPerSec(_run, DefaultRunMmPerSec * mmToFt);
            HasPose = true;
        }

        /// <summary>Moves the animation on by one game frame and re-poses the mesh.</summary>
        internal void Advance(double dt, double speedFtPerSec, bool running)
        {
            if (dt <= 0.0 || double.IsNaN(dt))
            {
                dt = 0.0;
            }
            dt = Math.Min(dt, 0.25); // a long stall must not fast-forward the legs

            GameCharacterModel.Clip target;
            double rate;
            if (speedFtPerSec < MovingFtPerSec || _walk == null)
            {
                target = _idle ?? _current;
                rate = 1.0;
            }
            else if (running && _run != null)
            {
                target = _run;
                rate = Clamp(speedFtPerSec / _runNaturalFtPerSec, MinRate, MaxRunRate);
            }
            else
            {
                target = _walk;
                rate = Clamp(speedFtPerSec / _walkNaturalFtPerSec, MinRate, MaxWalkRate);
            }

            if (target != null && target != _current)
            {
                // Walk <-> Run keep the step phase so the legs carry on; from Idle start a fresh step.
                double phase = (_current != null && _current != _idle && target != _idle && _current.Duration > 0)
                    ? _currentTime / _current.Duration
                    : 0.0;
                _previous = _current;
                _previousTime = _currentTime;
                _previousRate = _current == _idle ? 1.0 : rate;
                _current = target;
                _currentTime = phase * target.Duration;
                _fade = 0.0;
            }

            _currentTime = Wrap(_currentTime + (dt * rate), _current);
            if (_previous != null)
            {
                _previousTime = Wrap(_previousTime + (dt * _previousRate), _previous);
                _fade = Math.Min(1.0, _fade + (dt / FadeSec));
                if (_fade >= 1.0)
                {
                    _previous = null;
                }
            }

            ComputePose();
            Skin();
            HasPose = true;
        }

        // ---------------- Posing ----------------

        private void ComputePose()
        {
            LoadRest(_t, _r);
            if (_current != null)
            {
                Sample(_current, _currentTime, _t, _r);
            }

            if (_previous != null && _fade < 1.0)
            {
                LoadRest(_tB, _rB);
                Sample(_previous, _previousTime, _tB, _rB);
                BlendInto(_tB, _rB, _t, _r, _fade);
            }

            // World matrices, parents first.
            var nodes = _model.Nodes;
            var local = new double[12];
            foreach (int i in _order)
            {
                ToMatrix(_t, _r, nodes[i].Scale, i, local);
                if (nodes[i].Parent >= 0)
                {
                    Multiply(_world[nodes[i].Parent], local, _world[i]);
                }
                else
                {
                    Array.Copy(local, _world[i], 12);
                }
            }

            for (int j = 0; j < _joint.Length; j++)
            {
                Multiply(_world[_model.SkinJoints[j]], _model.InverseBind[j], _joint[j]);
            }
        }

        private void Skin()
        {
            var m = new double[12];
            for (int p = 0; p < _model.Parts.Length; p++)
            {
                var part = _model.Parts[p];
                float[] src = part.Positions;
                float[] srcN = part.Normals;
                float[] dst = Positions[p];
                float[] dstN = Normals[p];
                int count = part.VertexCount;
                for (int v = 0; v < count; v++)
                {
                    Array.Clear(m, 0, 12);
                    double total = 0.0;
                    for (int k = 0; k < 4; k++)
                    {
                        double w = part.Weights[(v * 4) + k];
                        if (w <= 0.0)
                        {
                            continue;
                        }
                        int ji = part.Joints[(v * 4) + k];
                        if (ji < 0 || ji >= _joint.Length)
                        {
                            continue;
                        }
                        double[] jm = _joint[ji];
                        for (int e = 0; e < 12; e++)
                        {
                            m[e] += w * jm[e];
                        }
                        total += w;
                    }
                    if (total <= 0.0)
                    {
                        m[0] = m[5] = m[10] = 1.0; // unweighted vertex stays where it is
                    }
                    else if (Math.Abs(total - 1.0) > 1e-4)
                    {
                        for (int e = 0; e < 12; e++)
                        {
                            m[e] /= total;
                        }
                    }

                    int o = v * 3;
                    double x = src[o], y = src[o + 1], z = src[o + 2];
                    dst[o] = (float)((m[0] * x) + (m[1] * y) + (m[2] * z) + m[3]);
                    dst[o + 1] = (float)((m[4] * x) + (m[5] * y) + (m[6] * z) + m[7]);
                    dst[o + 2] = (float)((m[8] * x) + (m[9] * y) + (m[10] * z) + m[11]);

                    double nx = srcN[o], ny = srcN[o + 1], nz = srcN[o + 2];
                    double ox = (m[0] * nx) + (m[1] * ny) + (m[2] * nz);
                    double oy = (m[4] * nx) + (m[5] * ny) + (m[6] * nz);
                    double oz = (m[8] * nx) + (m[9] * ny) + (m[10] * nz);
                    double len = Math.Sqrt((ox * ox) + (oy * oy) + (oz * oz));
                    if (len > 1e-9)
                    {
                        ox /= len; oy /= len; oz /= len;
                    }
                    else
                    {
                        ox = 0; oy = 1; oz = 0;
                    }
                    dstN[o] = (float)ox;
                    dstN[o + 1] = (float)oy;
                    dstN[o + 2] = (float)oz;
                }
            }
        }

        private void LoadRest(double[] t, double[] r)
        {
            var nodes = _model.Nodes;
            for (int i = 0; i < nodes.Length; i++)
            {
                t[i * 3] = nodes[i].Translation[0];
                t[(i * 3) + 1] = nodes[i].Translation[1];
                t[(i * 3) + 2] = nodes[i].Translation[2];
                r[i * 4] = nodes[i].Rotation[0];
                r[(i * 4) + 1] = nodes[i].Rotation[1];
                r[(i * 4) + 2] = nodes[i].Rotation[2];
                r[(i * 4) + 3] = nodes[i].Rotation[3];
            }
        }

        /// <summary>Writes the clip's value at time t into the node pose arrays.</summary>
        private static void Sample(GameCharacterModel.Clip clip, double time, double[] t, double[] r)
        {
            foreach (var track in clip.Tracks)
            {
                int width = track.Path == GameCharacterModel.PathRotation ? 4 : 3;
                float[] times = track.Times;
                int last = times.Length - 1;

                int k0, k1;
                double u;
                if (time <= times[0] || last == 0)
                {
                    k0 = k1 = 0;
                    u = 0.0;
                }
                else if (time >= times[last])
                {
                    k0 = k1 = last;
                    u = 0.0;
                }
                else
                {
                    int lo = 0, hi = last;
                    while (hi - lo > 1)
                    {
                        int mid = (lo + hi) / 2;
                        if (times[mid] <= time) { lo = mid; } else { hi = mid; }
                    }
                    k0 = lo;
                    k1 = hi;
                    double span = times[k1] - times[k0];
                    u = span > 1e-9 ? (time - times[k0]) / span : 0.0;
                    if (track.Step)
                    {
                        u = 0.0;
                    }
                }

                // Cubic spline keys hold (in-tangent, value, out-tangent): use the value, linearly.
                int stride = track.CubicSpline ? width * 3 : width;
                int shift = track.CubicSpline ? width : 0;
                int a = (k0 * stride) + shift;
                int b = (k1 * stride) + shift;
                float[] v = track.Values;
                if (b + width > v.Length)
                {
                    continue;
                }

                if (width == 3)
                {
                    int o = track.Node * 3;
                    t[o] = v[a] + ((v[b] - v[a]) * u);
                    t[o + 1] = v[a + 1] + ((v[b + 1] - v[a + 1]) * u);
                    t[o + 2] = v[a + 2] + ((v[b + 2] - v[a + 2]) * u);
                }
                else
                {
                    Nlerp(v[a], v[a + 1], v[a + 2], v[a + 3], v[b], v[b + 1], v[b + 2], v[b + 3], u, r, track.Node * 4);
                }
            }
        }

        /// <summary>to = from x (1 - w) + to x w, per node (translations lerp, rotations nlerp).</summary>
        private static void BlendInto(double[] tFrom, double[] rFrom, double[] tTo, double[] rTo, double w)
        {
            for (int i = 0; i < tTo.Length; i++)
            {
                tTo[i] = tFrom[i] + ((tTo[i] - tFrom[i]) * w);
            }
            for (int q = 0; q < rTo.Length; q += 4)
            {
                Nlerp(rFrom[q], rFrom[q + 1], rFrom[q + 2], rFrom[q + 3],
                      rTo[q], rTo[q + 1], rTo[q + 2], rTo[q + 3], w, rTo, q);
            }
        }

        private static void Nlerp(double ax, double ay, double az, double aw,
                                  double bx, double by, double bz, double bw,
                                  double u, double[] outQ, int o)
        {
            if ((ax * bx) + (ay * by) + (az * bz) + (aw * bw) < 0.0)
            {
                bx = -bx; by = -by; bz = -bz; bw = -bw; // shortest way round
            }
            double x = ax + ((bx - ax) * u);
            double y = ay + ((by - ay) * u);
            double z = az + ((bz - az) * u);
            double w = aw + ((bw - aw) * u);
            double len = Math.Sqrt((x * x) + (y * y) + (z * z) + (w * w));
            if (len < 1e-12)
            {
                x = 0; y = 0; z = 0; w = 1; len = 1;
            }
            outQ[o] = x / len;
            outQ[o + 1] = y / len;
            outQ[o + 2] = z / len;
            outQ[o + 3] = w / len;
        }

        // ---------------- Matrices (row-major 3x4) ----------------

        private static void ToMatrix(double[] t, double[] r, double[] s, int node, double[] m)
        {
            double x = r[node * 4], y = r[(node * 4) + 1], z = r[(node * 4) + 2], w = r[(node * 4) + 3];
            double sx = s[0], sy = s[1], sz = s[2];
            m[0] = (1 - (2 * ((y * y) + (z * z)))) * sx;
            m[1] = 2 * ((x * y) - (z * w)) * sy;
            m[2] = 2 * ((x * z) + (y * w)) * sz;
            m[3] = t[node * 3];
            m[4] = 2 * ((x * y) + (z * w)) * sx;
            m[5] = (1 - (2 * ((x * x) + (z * z)))) * sy;
            m[6] = 2 * ((y * z) - (x * w)) * sz;
            m[7] = t[(node * 3) + 1];
            m[8] = 2 * ((x * z) - (y * w)) * sx;
            m[9] = 2 * ((y * z) + (x * w)) * sy;
            m[10] = (1 - (2 * ((x * x) + (y * y)))) * sz;
            m[11] = t[(node * 3) + 2];
        }

        private static void Multiply(double[] a, double[] b, double[] o)
        {
            for (int row = 0; row < 3; row++)
            {
                int ra = row * 4;
                double a0 = a[ra], a1 = a[ra + 1], a2 = a[ra + 2], a3 = a[ra + 3];
                o[ra] = (a0 * b[0]) + (a1 * b[4]) + (a2 * b[8]);
                o[ra + 1] = (a0 * b[1]) + (a1 * b[5]) + (a2 * b[9]);
                o[ra + 2] = (a0 * b[2]) + (a1 * b[6]) + (a2 * b[10]);
                o[ra + 3] = (a0 * b[3]) + (a1 * b[7]) + (a2 * b[11]) + a3;
            }
        }

        // ---------------- Setup helpers ----------------

        private GameCharacterModel.Clip Find(string name)
        {
            GameCharacterModel.Clip clip;
            return _model.Clips.TryGetValue(name, out clip) && clip.Duration > 0 ? clip : null;
        }

        private static int[] BuildOrder(GameCharacterModel model)
        {
            var order = new List<int>(model.Nodes.Length);
            var seen = new bool[model.Nodes.Length];
            var stack = new Stack<int>();
            for (int i = 0; i < model.Nodes.Length; i++)
            {
                if (model.Nodes[i].Parent < 0)
                {
                    stack.Push(i);
                }
            }
            while (stack.Count > 0)
            {
                int i = stack.Pop();
                if (seen[i])
                {
                    continue;
                }
                seen[i] = true;
                order.Add(i);
                foreach (int c in model.Nodes[i].Children)
                {
                    stack.Push(c);
                }
            }
            return order.ToArray();
        }

        private void MeasureHeight(out double minY, out double maxY)
        {
            minY = double.MaxValue;
            maxY = double.MinValue;
            foreach (float[] part in Positions)
            {
                for (int o = 1; o < part.Length; o += 3)
                {
                    minY = Math.Min(minY, part[o]);
                    maxY = Math.Max(maxY, part[o]);
                }
            }
            if (minY > maxY)
            {
                minY = 0.0;
                maxY = 1.0;
            }
        }

        /// <summary>
        /// How fast the clip "walks": one cycle carries the body two foot-swings forward, a swing
        /// being how far a foot travels front-to-back (file +Z is the facing direction).
        /// </summary>
        private double NaturalSpeedFtPerSec(GameCharacterModel.Clip clip, double fallbackFtPerSec)
        {
            if (clip == null || clip.Duration <= 0)
            {
                return fallbackFtPerSec;
            }

            double swing = 0.0;
            foreach (var track in clip.Tracks)
            {
                string name = _model.Nodes[track.Node].Name ?? string.Empty;
                if (track.Path != GameCharacterModel.PathTranslation
                    || name.IndexOf("foot", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }
                int stride = track.CubicSpline ? 9 : 3;
                int shift = track.CubicSpline ? 3 : 0;
                double lo = double.MaxValue, hi = double.MinValue;
                for (int k = shift + 2; k < track.Values.Length; k += stride)
                {
                    lo = Math.Min(lo, track.Values[k]);
                    hi = Math.Max(hi, track.Values[k]);
                }
                if (hi > lo)
                {
                    swing = Math.Max(swing, hi - lo);
                }
            }

            double speed = swing * 2.0 * UnitsToFeet / clip.Duration;
            return speed > 0.05 ? speed : fallbackFtPerSec;
        }

        private static double Wrap(double time, GameCharacterModel.Clip clip)
        {
            if (clip == null || clip.Duration <= 0)
            {
                return 0.0;
            }
            time %= clip.Duration;
            return time < 0 ? time + clip.Duration : time;
        }

        private static double Clamp(double v, double lo, double hi)
        {
            return v < lo ? lo : (v > hi ? hi : v);
        }
    }
}
