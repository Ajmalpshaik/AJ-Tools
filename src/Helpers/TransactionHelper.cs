// Tool Name: Transaction Helper
// Description: Centralized transaction execution with automatic rollback on errors.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2025-12-11
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB

using System;
using Autodesk.Revit.DB;

namespace AJTools.Utils
{
    /// <summary>
    /// Provides safe transaction execution patterns with automatic error handling.
    /// </summary>
    internal static class TransactionHelper
    {
        /// <summary>
        /// Executes an action within a transaction with automatic rollback on error.
        /// </summary>
        /// <param name="doc">The document to create the transaction in.</param>
        /// <param name="transactionName">The name of the transaction.</param>
        /// <param name="action">The action to execute.</param>
        /// <returns>True if successful, false if an error occurred.</returns>
        public static bool ExecuteSafe(Document doc, string transactionName, Action action)
        {
            using (Transaction t = new Transaction(doc, transactionName))
            {
                try
                {
                    t.Start();
                    action();
                    t.Commit();
                    return true;
                }
                catch
                {
                    if (t.HasStarted() && !t.HasEnded())
                        t.RollBack();
                    return false;
                }
            }
        }

        /// <summary>
        /// Executes an action within a transaction with automatic rollback on error and returns the exception message.
        /// </summary>
        /// <param name="doc">The document to create the transaction in.</param>
        /// <param name="transactionName">The name of the transaction.</param>
        /// <param name="action">The action to execute.</param>
        /// <param name="errorMessage">The error message if execution fails.</param>
        /// <returns>True if successful, false if an error occurred.</returns>
        public static bool ExecuteSafe(Document doc, string transactionName, Action action, out string errorMessage)
        {
            errorMessage = string.Empty;

            using (Transaction t = new Transaction(doc, transactionName))
            {
                try
                {
                    t.Start();
                    action();
                    t.Commit();
                    return true;
                }
                catch (Exception ex)
                {
                    if (t.HasStarted() && !t.HasEnded())
                        t.RollBack();
                    errorMessage = ex.Message;
                    return false;
                }
            }
        }

        /// <summary>
        /// Executes a function within a transaction with automatic rollback on error.
        /// </summary>
        /// <typeparam name="T">The return type of the function.</typeparam>
        /// <param name="doc">The document to create the transaction in.</param>
        /// <param name="transactionName">The name of the transaction.</param>
        /// <param name="func">The function to execute.</param>
        /// <param name="result">The result of the function if successful.</param>
        /// <returns>True if successful, false if an error occurred.</returns>
        public static bool ExecuteSafe<T>(Document doc, string transactionName, Func<T> func, out T result)
        {
            result = default(T);

            using (Transaction t = new Transaction(doc, transactionName))
            {
                try
                {
                    t.Start();
                    result = func();
                    t.Commit();
                    return true;
                }
                catch
                {
                    if (t.HasStarted() && !t.HasEnded())
                        t.RollBack();
                    return false;
                }
            }
        }
    }
}
