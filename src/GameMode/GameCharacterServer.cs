#region Metadata
/*
 * Tool Name     : AJ Game Mode (third-person character)
 * File Name     : GameCharacterServer.cs
 * Purpose       : Draws the third-person player character INSIDE the game view, so walls, ducts and
 *                 slabs hide it properly when it walks behind them. The character is a real skinned
 *                 figure (a site worker in a hard hat) read from Resources\GameCharacter.gltf and
 *                 animated by GameCharacterAnimator (stands, walks with W, runs with Shift). If that
 *                 file is missing or cannot be read, the old code-built blocky mannequin is drawn
 *                 instead, so third person always works. Nothing is added to the model: the
 *                 character is screen graphics only, gone when the game ends.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.1.0
 *
 * Created Date  : 2026-11-21
 * Last Updated  : 2026-11-21
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API (DirectContext3D, ExternalService), AJTools.Utils,
 *                 GameCharacterModel, GameCharacterAnimator
 *
 * Input         : GameCharacterPose + Advance() - both written every frame by GameMotionEngine
 *                 (third-person view).
 * Output        : Shaded triangles drawn by Revit in the game view only. NO model change, NO
 *                 transaction, NO undo entry.
 *
 * Notes         :
 * - Revit's own "draw your own 3D graphics" service (DirectContext3D). Every member used here was
 *   checked in the Revit 2020, 2025 and 2027 API reference files (2026-11-21): identical across all
 *   three. Not yet seen running in Revit - Ajmal tests it live.
 * - One server instance for the whole Revit session, registered the first time a game starts
 *   (inside the frame ExternalEvent, a valid API context) and never removed: it simply draws
 *   nothing unless a game is running in third person, in the game view. Removing a server needs an
 *   API context, which the HUD's own stop paths do not have - staying registered and idle is the
 *   safe choice.
 * - Buffers are built fresh on every draw. The character is ~17 boxes (~400 vertices), so this is
 *   cheap, and it avoids keeping GPU buffers across redraws (Revit can invalidate them).
 * - All body sizes are stated in mm and converted to feet once via Constants.MM_TO_FEET.
 *
 * - The real character: posed once per game frame by Advance() (called by the motion engine), then
 *   each redraw only turns the posed mesh into Revit coordinates - file Y-up / facing +Z / +X left
 *   becomes Revit Z-up / facing the game yaw - scaled to CharacterHeightMm, feet on the floor. One
 *   buffer per colour (6), ~4,600 vertices, far inside the 16-bit index limit per buffer.
 * - Colours come from the file's flat material colours (converted from linear to screen colour),
 *   except the file's black "Skin" and white "Face" (eyes), which are swapped for a normal skin tone
 *   and dark eyes.
 *
 * Changelog     :
 * v1.0.0 (2026-11-21) - Initial release: code-built blocky mannequin, walk/run limb swing, visor
 *                       stripe on the face so the facing direction reads at a glance.
 * v1.1.0 (2026-11-21) - Real skinned character from Resources\GameCharacter.gltf with Idle / Walk /
 *                       Run clips; the blocky mannequin stays as the fallback.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.DirectContext3D;
using Autodesk.Revit.DB.ExternalService;
using AJTools.Utils;

namespace AJTools.Services.GameMode
{
    /// <summary>Where the character stands this frame and how its limbs are swung.</summary>
    internal sealed class GameCharacterPose
    {
        /// <summary>True only while a game runs in third person.</summary>
        public bool Visible;

        /// <summary>The game view (integer id) - the character is drawn there and nowhere else.</summary>
        public int ViewId;

        /// <summary>Point between the feet, on the floor, in Revit internal feet.</summary>
        public XYZ Feet = XYZ.Zero;

        /// <summary>Facing direction, radians from +X towards +Y (same as the game's yaw).</summary>
        public double YawRad;

        /// <summary>Leg swing (radians, + = left leg forward). Arms swing the opposite way.</summary>
        public double SwingRad;

        /// <summary>Knee bend for each leg (radians, 0 = straight).</summary>
        public double KneeLeftRad;
        public double KneeRightRad;
    }

    /// <summary>Draws the third-person mannequin in the game view (Revit-native 3D graphics).</summary>
    internal sealed class GameCharacterServer : IDirectContext3DServer
    {
        private static readonly Guid ServerGuid = new Guid("6C1E3A52-9B7D-4F08-A3E1-2D5B7C9F4A61");
        private static GameCharacterServer _instance;
        private static bool _registered;

        /// <summary>The pose the next redraw uses (engine writes, server reads - same UI thread).</summary>
        internal static readonly GameCharacterPose Pose = new GameCharacterPose();

        /// <summary>Height of the real character, top of the hard hat, in mm.</summary>
        private const double CharacterHeightMm = 1850.0;

        // The real character (null = not loaded / could not load -> blocky fallback).
        private static GameCharacterAnimator _animator;
        private static bool _loadTried;

        /// <summary>Why the real character is not used (null while it loads fine). For diagnosis.</summary>
        internal static string CharacterProblem { get; private set; }

        // Colour swaps for the shipped file (by material name): its skin is black and its eyes white.
        private static readonly Dictionary<string, Color> ColorOverrides =
            new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase)
            {
                { "Skin", new Color(224, 182, 142) },
                { "Face", new Color(38, 30, 26) }
            };

        // Colour groups: one vertex buffer + one effect each.
        private const int GroupSkin = 0;
        private const int GroupShirt = 1;
        private const int GroupTrousers = 2;
        private const int GroupDark = 3;
        private const int GroupCount = 4;

        private static readonly Color[] GroupColors =
        {
            new Color(224, 182, 142), // skin
            new Color(36, 112, 204),  // shirt - blue
            new Color(58, 62, 72),    // trousers - dark grey
            new Color(24, 24, 28)     // shoes + visor - near black
        };

        private GameCharacterServer()
        {
        }

        /// <summary>
        /// Registers the server with Revit once per Revit session. MUST be called from a valid API
        /// context (the frame ExternalEvent). Never throws - a refusal only means no character.
        /// </summary>
        internal static bool EnsureRegistered()
        {
            if (_registered)
            {
                return true;
            }

            try
            {
                var service = ExternalServiceRegistry.GetService(
                    ExternalServices.BuiltInExternalServices.DirectContext3DService) as MultiServerService;
                if (service == null)
                {
                    return false;
                }

                if (_instance == null)
                {
                    _instance = new GameCharacterServer();
                }

                if (!service.IsRegisteredServerId(ServerGuid))
                {
                    service.AddServer(_instance);
                }

                var active = new List<Guid>(service.GetActiveServerIds());
                if (!active.Contains(ServerGuid))
                {
                    active.Add(ServerGuid);
                    service.SetActiveServers(active);
                }

                _registered = true;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Moves the character's animation on by one game frame. Called by the motion engine every
        /// third-person frame, after it has written Pose. Loads the character file the first time.
        /// Never throws - any failure just means the blocky fallback is drawn.
        /// </summary>
        internal static void Advance(double dt, double speedFtPerSec, bool running)
        {
            try
            {
                if (!_loadTried)
                {
                    _loadTried = true;
                    string dir = System.IO.Path.GetDirectoryName(typeof(GameCharacterServer).Assembly.Location);
                    string problem;
                    GameCharacterModel model = GameCharacterModel.TryLoad(dir, out problem);
                    if (model != null)
                    {
                        _animator = new GameCharacterAnimator(model, CharacterHeightMm * Constants.MM_TO_FEET);
                    }
                    CharacterProblem = problem;
                }

                if (_animator != null)
                {
                    _animator.Advance(dt, speedFtPerSec, running);
                }
            }
            catch (Exception ex)
            {
                _animator = null;
                CharacterProblem = "character animation failed: " + ex.Message;
            }
        }

        // ---------------- IExternalServer ----------------

        public Guid GetServerId() { return ServerGuid; }

        public ExternalServiceId GetServiceId()
        {
            return ExternalServices.BuiltInExternalServices.DirectContext3DService;
        }

        public string GetName() { return "AJ Tools - Game Mode character"; }

        public string GetVendorId() { return "AJTools"; }

        public string GetDescription() { return "Draws the third-person player in AJ Game Mode."; }

        // ---------------- IDirectContext3DServer ----------------

        public string GetApplicationId() { return string.Empty; }

        public string GetSourceId() { return string.Empty; }

        public bool UsesHandles() { return false; }

        public bool UseInTransparentPass(View dBView) { return false; }

        public bool CanExecute(View dBView)
        {
            return Pose.Visible
                   && dBView != null
                   && ElementIdHelper.GetIntegerValue(dBView.Id) == Pose.ViewId;
        }

        public Outline GetBoundingBox(View dBView)
        {
            // Loose box around the body - Revit only needs it for culling / fit.
            double reach = 1200.0 * Constants.MM_TO_FEET;
            double height = 2000.0 * Constants.MM_TO_FEET;
            XYZ f = Pose.Feet ?? XYZ.Zero;
            return new Outline(
                new XYZ(f.X - reach, f.Y - reach, f.Z - (100.0 * Constants.MM_TO_FEET)),
                new XYZ(f.X + reach, f.Y + reach, f.Z + height));
        }

        public void RenderScene(View dBView, DisplayStyle displayStyle)
        {
            try
            {
                if (!Pose.Visible || !DrawContext.IsAvailable())
                {
                    return;
                }

                GameCharacterAnimator animator = _animator;
                if (animator != null && animator.HasPose)
                {
                    DrawCharacter(animator);
                    return;
                }

                var groups = new List<Box>[GroupCount];
                for (int i = 0; i < GroupCount; i++)
                {
                    groups[i] = new List<Box>();
                }
                BuildBody(groups);

                for (int g = 0; g < GroupCount; g++)
                {
                    if (groups[g].Count > 0)
                    {
                        FlushGroup(groups[g], GroupColors[g]);
                    }
                }
            }
            catch (Exception)
            {
                // A failed draw only loses the character for one frame - never Revit's redraw.
            }
        }

        // ---------------- Real character ----------------

        /// <summary>
        /// Draws the posed character: file axes (Y up, facing +Z, +X = its left) become the game's
        /// frame (Z up, facing the yaw, +Y = its left), scaled to height with the soles on the floor.
        /// </summary>
        private static void DrawCharacter(GameCharacterAnimator animator)
        {
            GameCharacterModel model = animator.Model;
            float[][] positions = animator.Positions;
            float[][] normals = animator.Normals;

            XYZ feet = Pose.Feet ?? XYZ.Zero;
            double cos = Math.Cos(Pose.YawRad);
            double sin = Math.Sin(Pose.YawRad);
            double scale = animator.UnitsToFeet;
            double floor = animator.FloorOffsetUnits;

            for (int p = 0; p < positions.Length; p++)
            {
                GameCharacterModel.Part part = model.Parts[p];
                float[] pos = positions[p];
                float[] nor = normals[p];
                int vertexCount = part.VertexCount;
                int triangleCount = part.Indices.Length / 3;
                if (vertexCount == 0 || triangleCount == 0 || vertexCount > 65535)
                {
                    continue;
                }

                Color color = PartColor(model.Materials[part.Material]);
                var format = new VertexFormat(VertexFormatBits.PositionNormal);
                EffectInstance effect = MakeEffect(color);

                int vertexFloats = vertexCount * VertexPositionNormal.GetSizeInFloats();
                var vertexBuffer = new VertexBuffer(vertexFloats);
                vertexBuffer.Map(vertexFloats);
                VertexStreamPositionNormal vertices = vertexBuffer.GetVertexStreamPositionNormal();
                for (int v = 0; v < vertexCount; v++)
                {
                    int o = v * 3;
                    // File -> character frame: forward = file Z, left = file X, up = file Y.
                    double fx = pos[o + 2] * scale;
                    double fy = pos[o] * scale;
                    double fz = (pos[o + 1] - floor) * scale;
                    double nx = nor[o + 2];
                    double ny = nor[o];
                    double nz = nor[o + 1];
                    var point = new XYZ(
                        feet.X + (fx * cos) - (fy * sin),
                        feet.Y + (fx * sin) + (fy * cos),
                        feet.Z + fz);
                    var normal = new XYZ((nx * cos) - (ny * sin), (nx * sin) + (ny * cos), nz);
                    vertices.AddVertex(new VertexPositionNormal(point, normal));
                }
                vertexBuffer.Unmap();

                int indexShorts = triangleCount * IndexTriangle.GetSizeInShortInts();
                var indexBuffer = new IndexBuffer(indexShorts);
                indexBuffer.Map(indexShorts);
                IndexStreamTriangle triangles = indexBuffer.GetIndexStreamTriangle();
                int[] idx = part.Indices;
                for (int t = 0; t < triangleCount; t++)
                {
                    triangles.AddTriangle(new IndexTriangle(idx[t * 3], idx[(t * 3) + 1], idx[(t * 3) + 2]));
                }
                indexBuffer.Unmap();

                DrawContext.FlushBuffer(
                    vertexBuffer, vertexCount,
                    indexBuffer, indexShorts,
                    format, effect,
                    PrimitiveType.TriangleList, 0, triangleCount);
            }
        }

        /// <summary>The colour for one material: a swap from ColorOverrides, else its file colour.</summary>
        private static Color PartColor(GameCharacterModel.Material material)
        {
            Color swap;
            if (material.Name != null && ColorOverrides.TryGetValue(material.Name, out swap))
            {
                return swap;
            }
            double[] c = material.BaseColorLinear;
            return new Color(ToScreen(c[0]), ToScreen(c[1]), ToScreen(c[2]));
        }

        /// <summary>glTF colours are linear light; Revit wants screen (sRGB) values 0-255.</summary>
        private static byte ToScreen(double linear)
        {
            linear = Math.Max(0.0, Math.Min(1.0, linear));
            double s = linear <= 0.0031308 ? linear * 12.92 : (1.055 * Math.Pow(linear, 1.0 / 2.4)) - 0.055;
            return (byte)Math.Round(Math.Max(0.0, Math.Min(1.0, s)) * 255.0);
        }

        private static EffectInstance MakeEffect(Color color)
        {
            var effect = new EffectInstance(VertexFormatBits.PositionNormal);
            effect.SetColor(color);
            effect.SetDiffuseColor(color);
            effect.SetAmbientColor(Scale(color, 0.55));
            effect.SetEmissiveColor(Scale(color, 0.15));
            effect.SetSpecularColor(new Color(30, 30, 30));
            return effect;
        }

        // ---------------- Blocky fallback body ----------------

        /// <summary>One box: a frame (rotation + origin) plus centre and half sizes in that frame, feet.</summary>
        private struct Box
        {
            public Transform Frame;
            public XYZ Center;
            public XYZ Half;
        }

        private static double Mm(double mm) { return mm * Constants.MM_TO_FEET; }

        private static XYZ P(double xMm, double yMm, double zMm) { return new XYZ(Mm(xMm), Mm(yMm), Mm(zMm)); }

        /// <summary>
        /// The mannequin, in its own frame: X = facing, Y = left, Z = up, origin on the floor between
        /// the feet. Total height 1810 mm. Limbs hang from their joint, so a rotation about the
        /// joint's Y axis swings them (positive angle = the hanging end moves backward).
        /// </summary>
        private static void BuildBody(List<Box>[] groups)
        {
            Transform body = Transform.CreateTranslation(Pose.Feet ?? XYZ.Zero)
                .Multiply(Transform.CreateRotation(XYZ.BasisZ, Pose.YawRad));

            // Trunk
            Add(groups[GroupTrousers], body, P(0, 0, 960), P(100, 170, 80));   // pelvis 880..1040
            Add(groups[GroupShirt], body, P(0, 0, 1290), P(110, 190, 250));     // torso 1040..1540
            Add(groups[GroupSkin], body, P(0, 0, 1575), P(50, 50, 35));         // neck
            Add(groups[GroupSkin], body, P(0, 0, 1690), P(110, 100, 120));      // head 1570..1810
            Add(groups[GroupDark], body, P(108, 0, 1715), P(12, 80, 25));       // visor (shows facing)

            double swing = Pose.SwingRad;

            // Legs: forward swing = negative rotation about Y.
            AddLeg(groups, body, 100.0, -swing, Pose.KneeLeftRad);
            AddLeg(groups, body, -100.0, swing, Pose.KneeRightRad);

            // Arms swing opposite to the legs, a bit less, with a small constant elbow bend.
            double armSwing = swing * 0.8;
            AddArm(groups, body, 250.0, armSwing);
            AddArm(groups, body, -250.0, -armSwing);
        }

        private static void AddLeg(List<Box>[] groups, Transform body, double sideMm, double hipRad, double kneeRad)
        {
            Transform thigh = body
                .Multiply(Transform.CreateTranslation(P(0, sideMm, 960)))
                .Multiply(Transform.CreateRotation(XYZ.BasisY, hipRad));
            Add(groups[GroupTrousers], thigh, P(0, 0, -225), P(70, 70, 225));

            Transform shin = thigh
                .Multiply(Transform.CreateTranslation(P(0, 0, -450)))
                .Multiply(Transform.CreateRotation(XYZ.BasisY, kneeRad));
            Add(groups[GroupTrousers], shin, P(0, 0, -215), P(60, 60, 215));

            Transform ankle = shin.Multiply(Transform.CreateTranslation(P(0, 0, -430)));
            Add(groups[GroupDark], ankle, P(50, 0, -40), P(120, 60, 40));
        }

        private static void AddArm(List<Box>[] groups, Transform body, double sideMm, double shoulderRad)
        {
            Transform upper = body
                .Multiply(Transform.CreateTranslation(P(0, sideMm, 1480)))
                .Multiply(Transform.CreateRotation(XYZ.BasisY, shoulderRad));
            Add(groups[GroupShirt], upper, P(0, 0, -150), P(55, 55, 150));

            // Elbow bends the forearm forward (negative rotation), more as the arm swings back.
            double elbowRad = -(0.25 + Math.Max(0.0, shoulderRad) * 0.6);
            Transform fore = upper
                .Multiply(Transform.CreateTranslation(P(0, 0, -300)))
                .Multiply(Transform.CreateRotation(XYZ.BasisY, elbowRad));
            Add(groups[GroupShirt], fore, P(0, 0, -140), P(50, 50, 140));

            Transform wrist = fore.Multiply(Transform.CreateTranslation(P(0, 0, -280)));
            Add(groups[GroupSkin], wrist, P(0, 0, -50), P(45, 40, 50));
        }

        private static void Add(List<Box> list, Transform frame, XYZ center, XYZ half)
        {
            list.Add(new Box { Frame = frame, Center = center, Half = half });
        }

        // ---------------- Drawing ----------------

        // Each face: outward normal + its 4 corners as (+/-) sign triples, counter-clockwise seen
        // from outside.
        private static readonly int[][] FaceCorners =
        {
            new[] {  1, -1, -1,   1,  1, -1,   1,  1,  1,   1, -1,  1 }, // +X
            new[] { -1,  1, -1,  -1, -1, -1,  -1, -1,  1,  -1,  1,  1 }, // -X
            new[] {  1,  1, -1,  -1,  1, -1,  -1,  1,  1,   1,  1,  1 }, // +Y
            new[] { -1, -1, -1,   1, -1, -1,   1, -1,  1,  -1, -1,  1 }, // -Y
            new[] { -1, -1,  1,   1, -1,  1,   1,  1,  1,  -1,  1,  1 }, // +Z
            new[] { -1,  1, -1,   1,  1, -1,   1, -1, -1,  -1, -1, -1 }  // -Z
        };

        private static readonly XYZ[] FaceNormals =
        {
            XYZ.BasisX, -XYZ.BasisX, XYZ.BasisY, -XYZ.BasisY, XYZ.BasisZ, -XYZ.BasisZ
        };

        private static void FlushGroup(List<Box> boxes, Color color)
        {
            int vertexCount = boxes.Count * 24;
            int triangleCount = boxes.Count * 12;

            var format = new VertexFormat(VertexFormatBits.PositionNormal);
            var effect = new EffectInstance(VertexFormatBits.PositionNormal);
            effect.SetColor(color);
            effect.SetDiffuseColor(color);
            effect.SetAmbientColor(Scale(color, 0.55));
            effect.SetEmissiveColor(Scale(color, 0.15));
            effect.SetSpecularColor(new Color(30, 30, 30));

            int vertexFloats = vertexCount * VertexPositionNormal.GetSizeInFloats();
            var vertexBuffer = new VertexBuffer(vertexFloats);
            vertexBuffer.Map(vertexFloats);
            VertexStreamPositionNormal vertices = vertexBuffer.GetVertexStreamPositionNormal();

            int indexShorts = triangleCount * IndexTriangle.GetSizeInShortInts();
            var indexBuffer = new IndexBuffer(indexShorts);
            indexBuffer.Map(indexShorts);
            IndexStreamTriangle triangles = indexBuffer.GetIndexStreamTriangle();

            int baseIndex = 0;
            foreach (Box box in boxes)
            {
                for (int face = 0; face < 6; face++)
                {
                    int[] c = FaceCorners[face];
                    XYZ normal = box.Frame.OfVector(FaceNormals[face]);
                    for (int corner = 0; corner < 4; corner++)
                    {
                        var local = new XYZ(
                            box.Center.X + (c[corner * 3] * box.Half.X),
                            box.Center.Y + (c[(corner * 3) + 1] * box.Half.Y),
                            box.Center.Z + (c[(corner * 3) + 2] * box.Half.Z));
                        vertices.AddVertex(new VertexPositionNormal(box.Frame.OfPoint(local), normal));
                    }

                    triangles.AddTriangle(new IndexTriangle(baseIndex, baseIndex + 1, baseIndex + 2));
                    triangles.AddTriangle(new IndexTriangle(baseIndex, baseIndex + 2, baseIndex + 3));
                    baseIndex += 4;
                }
            }

            vertexBuffer.Unmap();
            indexBuffer.Unmap();

            DrawContext.FlushBuffer(
                vertexBuffer, vertexCount,
                indexBuffer, indexShorts,
                format, effect,
                PrimitiveType.TriangleList, 0, triangleCount);
        }

        private static Color Scale(Color c, double factor)
        {
            return new Color(
                (byte)Math.Min(255, c.Red * factor),
                (byte)Math.Min(255, c.Green * factor),
                (byte)Math.Min(255, c.Blue * factor));
        }
    }
}
