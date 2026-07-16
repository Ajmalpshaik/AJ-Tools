using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using AJTools.Models.Purge;

using AJTools.Utils;
namespace AJTools.Services.Purge
{
    internal sealed class FamilyParameterDeleteService
    {
        private readonly Document _doc;
        private readonly FamilyManager _familyManager;

        public FamilyParameterDeleteService(Document doc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _familyManager = _doc.FamilyManager;
        }

        public FamilyParameterPurgeDeleteResult DeleteSelected(IList<FamilyParameterPurgeItem> selectedItems)
        {
            var result = new FamilyParameterPurgeDeleteResult();
            if (selectedItems == null || selectedItems.Count == 0)
            {
                return result;
            }

            if (_doc.IsReadOnly)
            {
                foreach (FamilyParameterPurgeItem item in selectedItems)
                {
                    result.AttemptedCount++;
                    result.AddFailure(item.ParameterName, "Document is read-only.");
                }

                return result;
            }

            using (var group = new TransactionGroup(_doc, "Purge Unused Family Parameters"))
            {
                group.Start();
                bool hasCommit = false;

                foreach (FamilyParameterPurgeItem item in selectedItems)
                {
                    result.AttemptedCount++;

                    if (item == null || !item.IsDeletable)
                    {
                        result.AddFailure(
                            item != null ? item.ParameterName : "Unknown",
                            "Parameter is not deletable.");
                        continue;
                    }

                    using (var transaction = new Transaction(_doc, "Delete Family Parameter"))
                    {
                        transaction.Start();
                        try
                        {
                            FamilyParameter parameter = FindCurrentParameter(item);
                            if (parameter == null)
                            {
                                result.AddFailure(item.ParameterName, "Parameter no longer exists.");
                                transaction.RollBack();
                                continue;
                            }

                            _familyManager.RemoveParameter(parameter);
                            transaction.Commit();
                            hasCommit = true;
                            result.DeletedCount++;
                        }
                        catch (Exception ex)
                        {
                            if (transaction.GetStatus() == TransactionStatus.Started)
                            {
                                transaction.RollBack();
                            }

                            result.AddFailure(item.ParameterName, ex.Message);
                        }
                    }
                }

                if (hasCommit)
                {
                    group.Assimilate();
                }
                else
                {
                    group.RollBack();
                }
            }

            return result;
        }

        private FamilyParameter FindCurrentParameter(FamilyParameterPurgeItem item)
        {
            IList<FamilyParameter> parameters = _familyManager.GetParameters();
            FamilyParameter byId = parameters.FirstOrDefault(p =>
                p != null &&
                p.Id != null &&
                p.Id != ElementId.InvalidElementId &&
                p.Id.IntValue() == item.ParameterIdValue);

            if (byId != null)
            {
                return byId;
            }

            return parameters.FirstOrDefault(p =>
            {
                if (p == null || p.Definition == null)
                {
                    return false;
                }

                if (!string.Equals(
                        p.Definition.Name ?? string.Empty,
                        item.ParameterName ?? string.Empty,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (p.IsInstance != item.IsInstance)
                {
                    return false;
                }

                if (p.IsShared != item.IsShared)
                {
                    return false;
                }

                if (!p.IsShared)
                {
                    return true;
                }

                Guid guid;
                try
                {
                    guid = p.GUID;
                }
                catch
                {
                    guid = Guid.Empty;
                }

                return guid != Guid.Empty && guid == item.SharedGuid;
            });
        }
    }
}
