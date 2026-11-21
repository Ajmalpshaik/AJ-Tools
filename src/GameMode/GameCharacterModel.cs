#region Metadata
/*
 * Tool Name     : AJ Game Mode (third-person character - model file)
 * File Name     : GameCharacterModel.cs
 * Purpose       : Reads the third-person character from Resources\GameCharacter.gltf (a standard
 *                 glTF 2.0 file with its data embedded): the skinned mesh, its skeleton, its flat
 *                 material colours and its animation clips (Idle / Walk / Run). Pure data - no Revit
 *                 API, no drawing. GameCharacterAnimator poses it, GameCharacterServer draws it.
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
 * Dependencies  : Newtonsoft.Json (already shipped with AJ Tools)
 *
 * Input         : Resources\GameCharacter.gltf beside the deployed DLL.
 * Output        : A GameCharacterModel, or null with a plain reason (the game then falls back to the
 *                 code-built blocky character).
 *
 * Notes         :
 * - Only what Game Mode needs is read: ONE skinned mesh (triangles), POSITION / NORMAL / JOINTS_0 /
 *   WEIGHTS_0, the material base colour (no textures - Revit's DirectContext3D in 2020 has no
 *   texture vertex format), one skin, node rest poses, and translation / rotation animation tracks
 *   (LINEAR or STEP). Anything else in the file is ignored.
 * - The data stays in the file's own axes and units (Y up, character facing +Z, +X = its left).
 *   The server converts to Revit (Z up, feet) when it draws.
 * - The shipped file was trimmed from a free CC0 character (public domain, no credit required):
 *   one mesh, its skin, six colours and three clips, ~470 KB.
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
using System.IO;
using Newtonsoft.Json.Linq;

namespace AJTools.Services.GameMode
{
    /// <summary>The character as read from the glTF file (file axes and units).</summary>
    internal sealed class GameCharacterModel
    {
        internal const string FileName = "GameCharacter.gltf";

        internal sealed class Node
        {
            public string Name;
            public int Parent = -1;
            public int[] Children = new int[0];
            public double[] Translation = { 0, 0, 0 };
            public double[] Rotation = { 0, 0, 0, 1 };   // quaternion x, y, z, w
            public double[] Scale = { 1, 1, 1 };
        }

        /// <summary>One triangle list with one colour.</summary>
        internal sealed class Part
        {
            public int Material;
            public float[] Positions;   // 3 per vertex
            public float[] Normals;     // 3 per vertex
            public int[] Joints;        // 4 per vertex - index into Skin joints
            public float[] Weights;     // 4 per vertex
            public int[] Indices;       // 3 per triangle
            public int VertexCount { get { return Positions.Length / 3; } }
        }

        internal sealed class Material
        {
            public string Name;
            public double[] BaseColorLinear = { 1, 1, 1 };
        }

        internal const int PathTranslation = 0;
        internal const int PathRotation = 1;

        internal sealed class Track
        {
            public int Node;
            public int Path;            // PathTranslation or PathRotation
            public bool Step;           // STEP interpolation (else LINEAR)
            public bool CubicSpline;    // values stored as in-tangent / value / out-tangent triples
            public float[] Times;
            public float[] Values;      // 3 or 4 per key (x3 for cubic spline)
        }

        internal sealed class Clip
        {
            public string Name;
            public double Duration;
            public List<Track> Tracks = new List<Track>();
        }

        public Node[] Nodes;
        public int[] RootNodes;
        public Part[] Parts;
        public Material[] Materials;
        public int[] SkinJoints;            // node index per joint
        public double[][] InverseBind;      // 12 per joint, row-major 3x4
        public Dictionary<string, Clip> Clips = new Dictionary<string, Clip>(StringComparer.OrdinalIgnoreCase);

        private GameCharacterModel()
        {
        }

        /// <summary>Loads the character from the Resources folder beside the given DLL folder.</summary>
        internal static GameCharacterModel TryLoad(string assemblyDir, out string problem)
        {
            problem = null;
            try
            {
                if (string.IsNullOrEmpty(assemblyDir))
                {
                    problem = "add-in folder unknown";
                    return null;
                }

                string path = Path.Combine(assemblyDir, "Resources", FileName);
                if (!File.Exists(path))
                {
                    problem = "Resources\\" + FileName + " not found";
                    return null;
                }

                var model = Parse(JObject.Parse(File.ReadAllText(path)), out problem);
                return model;
            }
            catch (Exception ex)
            {
                problem = "could not read " + FileName + ": " + ex.Message;
                return null;
            }
        }

        private static GameCharacterModel Parse(JObject root, out string problem)
        {
            problem = null;
            var buffers = ReadBuffers(root);
            var reader = new AccessorReader(root, buffers);
            var model = new GameCharacterModel();

            // --- Nodes ---
            var nodesJson = (JArray)root["nodes"];
            model.Nodes = new Node[nodesJson.Count];
            for (int i = 0; i < nodesJson.Count; i++)
            {
                var n = (JObject)nodesJson[i];
                var node = new Node { Name = (string)n["name"] ?? ("node" + i) };
                node.Translation = Doubles(n["translation"], node.Translation);
                node.Rotation = Doubles(n["rotation"], node.Rotation);
                node.Scale = Doubles(n["scale"], node.Scale);
                if (n["children"] is JArray kids)
                {
                    node.Children = kids.ToIntArray();
                }
                model.Nodes[i] = node;
            }
            for (int i = 0; i < model.Nodes.Length; i++)
            {
                foreach (int c in model.Nodes[i].Children)
                {
                    model.Nodes[c].Parent = i;
                }
            }

            int sceneIndex = root["scene"] != null ? (int)root["scene"] : 0;
            var sceneNodes = root["scenes"]?[sceneIndex]?["nodes"] as JArray;
            model.RootNodes = sceneNodes != null
                ? sceneNodes.ToIntArray()
                : RootsOf(model.Nodes);

            // --- The skinned mesh node ---
            int meshIndex = -1;
            int skinIndex = -1;
            foreach (JObject n in nodesJson)
            {
                if (n["mesh"] != null && n["skin"] != null)
                {
                    meshIndex = (int)n["mesh"];
                    skinIndex = (int)n["skin"];
                    break;
                }
            }
            if (meshIndex < 0)
            {
                problem = "no skinned mesh in " + FileName;
                return null;
            }

            // --- Skin ---
            var skin = (JObject)root["skins"][skinIndex];
            model.SkinJoints = ((JArray)skin["joints"]).ToIntArray();
            float[] ibm = skin["inverseBindMatrices"] != null
                ? reader.Floats((int)skin["inverseBindMatrices"], 16)
                : null;
            model.InverseBind = new double[model.SkinJoints.Length][];
            for (int j = 0; j < model.SkinJoints.Length; j++)
            {
                // glTF matrices are column-major; keep the top 3 rows as row-major 3x4.
                var m = new double[12];
                if (ibm != null)
                {
                    int o = j * 16;
                    for (int r = 0; r < 3; r++)
                    {
                        for (int c = 0; c < 4; c++)
                        {
                            m[(r * 4) + c] = ibm[o + (c * 4) + r];
                        }
                    }
                }
                else
                {
                    m[0] = m[5] = m[10] = 1.0;
                }
                model.InverseBind[j] = m;
            }

            // --- Materials ---
            var matsJson = root["materials"] as JArray ?? new JArray();
            model.Materials = new Material[Math.Max(1, matsJson.Count)];
            for (int i = 0; i < model.Materials.Length; i++)
            {
                var mat = new Material { Name = "Default" };
                if (i < matsJson.Count)
                {
                    mat.Name = (string)matsJson[i]["name"] ?? ("Material" + i);
                    double[] factor = Doubles(matsJson[i]["pbrMetallicRoughness"]?["baseColorFactor"], null);
                    if (factor != null && factor.Length >= 3)
                    {
                        mat.BaseColorLinear = new[] { factor[0], factor[1], factor[2] };
                    }
                }
                model.Materials[i] = mat;
            }

            // --- Mesh parts ---
            var parts = new List<Part>();
            foreach (JObject prim in (JArray)root["meshes"][meshIndex]["primitives"])
            {
                int mode = prim["mode"] != null ? (int)prim["mode"] : 4;
                var attrs = (JObject)prim["attributes"];
                if (mode != 4 || attrs["POSITION"] == null || attrs["JOINTS_0"] == null || attrs["WEIGHTS_0"] == null)
                {
                    continue; // only skinned triangle lists are drawn
                }

                var part = new Part
                {
                    Material = prim["material"] != null ? (int)prim["material"] : 0,
                    Positions = reader.Floats((int)attrs["POSITION"], 3),
                    Joints = reader.Ints((int)attrs["JOINTS_0"], 4),
                    Weights = reader.Floats((int)attrs["WEIGHTS_0"], 4)
                };
                part.Normals = attrs["NORMAL"] != null
                    ? reader.Floats((int)attrs["NORMAL"], 3)
                    : new float[part.Positions.Length];
                part.Indices = prim["indices"] != null
                    ? reader.Ints((int)prim["indices"], 1)
                    : Sequence(part.VertexCount);
                if (part.Material >= model.Materials.Length)
                {
                    part.Material = 0;
                }
                parts.Add(part);
            }
            if (parts.Count == 0)
            {
                problem = "the mesh in " + FileName + " has no skinned triangles";
                return null;
            }
            model.Parts = parts.ToArray();

            // --- Animation clips ---
            if (root["animations"] is JArray anims)
            {
                foreach (JObject anim in anims)
                {
                    var clip = new Clip { Name = (string)anim["name"] ?? "Clip" };
                    var samplers = (JArray)anim["samplers"];
                    foreach (JObject ch in (JArray)anim["channels"])
                    {
                        string targetPath = (string)ch["target"]?["path"];
                        if (ch["target"]?["node"] == null
                            || (targetPath != "translation" && targetPath != "rotation"))
                        {
                            continue;
                        }

                        var sampler = (JObject)samplers[(int)ch["sampler"]];
                        string interp = (string)sampler["interpolation"] ?? "LINEAR";
                        int width = targetPath == "rotation" ? 4 : 3;
                        var track = new Track
                        {
                            Node = (int)ch["target"]["node"],
                            Path = targetPath == "rotation" ? PathRotation : PathTranslation,
                            Step = interp == "STEP",
                            CubicSpline = interp == "CUBICSPLINE",
                            Times = reader.Floats((int)sampler["input"], 1),
                            Values = reader.Floats((int)sampler["output"], width)
                        };
                        if (track.Times.Length == 0)
                        {
                            continue;
                        }
                        clip.Duration = Math.Max(clip.Duration, track.Times[track.Times.Length - 1]);
                        clip.Tracks.Add(track);
                    }
                    if (clip.Tracks.Count > 0)
                    {
                        model.Clips[clip.Name] = clip;
                    }
                }
            }

            return model;
        }

        // ---------------- Helpers ----------------

        private static int[] RootsOf(Node[] nodes)
        {
            var roots = new List<int>();
            for (int i = 0; i < nodes.Length; i++)
            {
                if (nodes[i].Parent < 0)
                {
                    roots.Add(i);
                }
            }
            return roots.ToArray();
        }

        private static int[] Sequence(int count)
        {
            var s = new int[count];
            for (int i = 0; i < count; i++)
            {
                s[i] = i;
            }
            return s;
        }

        private static double[] Doubles(JToken token, double[] fallback)
        {
            if (!(token is JArray arr))
            {
                return fallback;
            }
            var d = new double[arr.Count];
            for (int i = 0; i < arr.Count; i++)
            {
                d[i] = (double)arr[i];
            }
            return d;
        }

        private static List<byte[]> ReadBuffers(JObject root)
        {
            var list = new List<byte[]>();
            foreach (JObject b in (JArray)root["buffers"])
            {
                string uri = (string)b["uri"];
                int comma = uri != null ? uri.IndexOf(',') : -1;
                if (uri == null || !uri.StartsWith("data:", StringComparison.OrdinalIgnoreCase) || comma < 0)
                {
                    throw new InvalidDataException("only embedded (data:) buffers are supported");
                }
                list.Add(Convert.FromBase64String(uri.Substring(comma + 1)));
            }
            return list;
        }

        /// <summary>Reads glTF accessors into flat float / int arrays.</summary>
        private sealed class AccessorReader
        {
            private readonly JArray _accessors;
            private readonly JArray _views;
            private readonly List<byte[]> _buffers;

            public AccessorReader(JObject root, List<byte[]> buffers)
            {
                _accessors = (JArray)root["accessors"];
                _views = (JArray)root["bufferViews"];
                _buffers = buffers;
            }

            public float[] Floats(int accessor, int width)
            {
                var a = (JObject)_accessors[accessor];
                int count = (int)a["count"];
                var result = new float[count * width];
                Walk(a, width, (k, c, bytes, offset, type) =>
                {
                    result[(k * width) + c] = ReadNormalisedFloat(bytes, offset, type, a["normalized"] != null && (bool)a["normalized"]);
                });
                return result;
            }

            public int[] Ints(int accessor, int width)
            {
                var a = (JObject)_accessors[accessor];
                int count = (int)a["count"];
                var result = new int[count * width];
                Walk(a, width, (k, c, bytes, offset, type) =>
                {
                    result[(k * width) + c] = ReadInt(bytes, offset, type);
                });
                return result;
            }

            private void Walk(JObject a, int width, Action<int, int, byte[], int, int> read)
            {
                int type = (int)a["componentType"];
                int size = ComponentSize(type);
                int count = (int)a["count"];
                if (a["bufferView"] == null)
                {
                    return; // all zeros
                }
                var view = (JObject)_views[(int)a["bufferView"]];
                byte[] bytes = _buffers[(int)view["buffer"]];
                int stride = view["byteStride"] != null ? (int)view["byteStride"] : size * width;
                int start = (view["byteOffset"] != null ? (int)view["byteOffset"] : 0)
                            + (a["byteOffset"] != null ? (int)a["byteOffset"] : 0);
                for (int k = 0; k < count; k++)
                {
                    int o = start + (k * stride);
                    for (int c = 0; c < width; c++)
                    {
                        read(k, c, bytes, o + (c * size), type);
                    }
                }
            }

            private static int ComponentSize(int type)
            {
                switch (type)
                {
                    case 5120: case 5121: return 1;   // byte / unsigned byte
                    case 5122: case 5123: return 2;   // short / unsigned short
                    case 5125: case 5126: return 4;   // unsigned int / float
                    default: throw new InvalidDataException("unknown component type " + type);
                }
            }

            private static int ReadInt(byte[] b, int o, int type)
            {
                switch (type)
                {
                    case 5120: return (sbyte)b[o];
                    case 5121: return b[o];
                    case 5122: return BitConverter.ToInt16(b, o);
                    case 5123: return BitConverter.ToUInt16(b, o);
                    case 5125: return (int)BitConverter.ToUInt32(b, o);
                    default: return (int)BitConverter.ToSingle(b, o);
                }
            }

            private static float ReadNormalisedFloat(byte[] b, int o, int type, bool normalized)
            {
                switch (type)
                {
                    case 5126: return BitConverter.ToSingle(b, o);
                    case 5121: return normalized ? b[o] / 255f : b[o];
                    case 5123: return normalized ? BitConverter.ToUInt16(b, o) / 65535f : BitConverter.ToUInt16(b, o);
                    case 5120: return normalized ? Math.Max((sbyte)b[o] / 127f, -1f) : (sbyte)b[o];
                    case 5122: return normalized ? Math.Max(BitConverter.ToInt16(b, o) / 32767f, -1f) : BitConverter.ToInt16(b, o);
                    default: return ReadInt(b, o, type);
                }
            }
        }
    }

    internal static class GameCharacterJsonExtensions
    {
        /// <summary>Maps a JSON array to an int array (keeps the parser free of LINQ allocations).</summary>
        internal static int[] ToIntArray(this JArray arr)
        {
            var result = new int[arr.Count];
            for (int i = 0; i < arr.Count; i++)
            {
                result[i] = (int)arr[i];
            }
            return result;
        }
    }
}
