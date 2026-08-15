#region Metadata
/*
 * Tool Name     : Dimensioning (shared)
 * File Name     : DimensionOwnership.cs
 * Purpose       : Marks the dimensions AJ Tools created, and recognises them again later, so the tool
 *                 can replace its own work without ever touching a dimension somebody drew by hand.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-08-15
 * Last Updated  : 2026-08-15
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API (ExtensibleStorage)
 *
 * Input         : A Dimension that has just been created, or an existing Element to test.
 * Output        : An invisible marker stored on the element; IsOwned() reads it back.
 *
 * Notes         :
 * - Ajmal's rule (2026-08-15): the tool may delete ONLY dimensions it created itself, never one drawn
 *   by hand. A geometric "looks the same" test cannot tell those apart, so ownership is recorded
 *   explicitly on the element.
 * - Extensible Storage was verified present in the real installed RevitAPI.dll for Revit 2020
 *   (Element.SetEntity/GetEntity, SchemaBuilder, Schema.Lookup). This is the FIRST use of Extensible
 *   Storage in this project - nothing else in src/ stores data on elements this way.
 * - The marker is invisible: no parameter, no comment, nothing on the sheet. It survives save/reopen.
 * - The schema GUID must never change. A new GUID would orphan every dimension already marked, and the
 *   tool would then refuse to tidy up its own earlier work.
 * - Every call is guarded. If the schema cannot be built or read for any reason, Mark() does nothing and
 *   IsOwned() returns false - which fails SAFE, because an unrecognised dimension is never deleted.
 *
 * Changelog     :
 * v1.0.0 (2026-08-15) - Initial release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;

namespace AJTools.Services.Dimensioning
{
    /// <summary>
    /// Records that AJ Tools created a dimension, so it can safely replace it later.
    /// </summary>
    internal static class DimensionOwnership
    {
        // Never change this. Marked dimensions are found by this GUID alone.
        private static readonly Guid SchemaGuid = new Guid("7B3D1C2E-9A64-4F5B-B1E7-2C6A8D4F9013");

        private const string SchemaName = "AJToolsDimensionOwnership";
        private const string VendorId = "AJT";
        private const string FieldName = "CreatedByAjTools";

        /// <summary>
        /// Stamps a dimension as created by AJ Tools. Must be called inside an open transaction.
        /// Never throws - a failed stamp only means the tool will not tidy that dimension up later.
        /// </summary>
        internal static void Mark(Element element)
        {
            if (element == null)
                return;

            try
            {
                Schema schema = EnsureSchema();
                if (schema == null)
                    return;

                Entity entity = new Entity(schema);
                entity.Set(schema.GetField(FieldName), true);
                element.SetEntity(entity);
            }
            catch
            {
                // A dimension that could not be stamped simply will not be replaced automatically.
            }
        }

        /// <summary>
        /// True only when this element carries the AJ Tools marker. Anything drawn by hand, or created
        /// before this marker existed, returns false and is therefore never deleted.
        /// </summary>
        internal static bool IsOwned(Element element)
        {
            if (element == null)
                return false;

            try
            {
                Schema schema = Schema.Lookup(SchemaGuid);
                if (schema == null)
                    return false;

                Entity entity = element.GetEntity(schema);
                if (entity == null || !entity.IsValid())
                    return false;

                return entity.Get<bool>(schema.GetField(FieldName));
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Finds the schema, creating it the first time it is needed. Returns null if it cannot be built.
        /// </summary>
        private static Schema EnsureSchema()
        {
            Schema existing = Schema.Lookup(SchemaGuid);
            if (existing != null)
                return existing;

            try
            {
                SchemaBuilder builder = new SchemaBuilder(SchemaGuid);
                builder.SetSchemaName(SchemaName);
                builder.SetVendorId(VendorId);

                // Public on both sides: another AJ Tools build must be able to read and tidy up
                // dimensions an earlier one created.
                builder.SetReadAccessLevel(AccessLevel.Public);
                builder.SetWriteAccessLevel(AccessLevel.Public);

                builder.AddSimpleField(FieldName, typeof(bool));

                return builder.Finish();
            }
            catch
            {
                return null;
            }
        }
    }
}
