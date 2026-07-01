#region Metadata
/*
 * Tool Name     : AJ AI (Gemini Shell)
 * File Name     : GeneratedCodeSafetyValidator.cs
 * Purpose       : Pre-execution text scan of AI-generated C# scripts. Blocks clearly dangerous
 *                 operations (process launch, registry, network, reflection/unsafe, disk I/O)
 *                 and flags destructive-but-legitimate Revit operations (delete/purge) so the
 *                 user can confirm before running.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-07-01
 * Last Updated  : 2026-07-01
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : None (pure text/regex analysis)
 *
 * Input         : Raw C# script text about to be handed to RoslynService
 * Output        : CodeSafetyResult listing Blocked/Warning findings
 *
 * Notes         :
 * - This is a pattern-based guard, not a sandbox. It catches the obvious dangerous calls
 *   (Process.Start, registry, HttpClient/WebClient, reflection, unsafe, raw file I/O) but a
 *   determined attempt to obfuscate code (e.g. string-built reflection) can still slip through.
 *   Real isolation would require AppDomain/process-level sandboxing, which is out of scope here.
 * - Delete/Purge calls are not blocked outright (they are normal Revit operations) but are
 *   flagged so the caller can show a confirmation prompt first.
 *
 * Changelog     :
 * v1.0.0 (2026-07-01) - Initial release as part of the AJ AI safety hardening pass.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AJTools.GeminiShell.Services
{
    public enum CodeRiskLevel
    {
        Safe = 0,
        Warning = 1,
        Blocked = 2
    }

    public class CodeSafetyFinding
    {
        public CodeRiskLevel Level { get; set; }
        public string Reason { get; set; }
    }

    public class CodeSafetyResult
    {
        public CodeRiskLevel HighestLevel { get; set; } = CodeRiskLevel.Safe;
        public List<CodeSafetyFinding> Findings { get; } = new List<CodeSafetyFinding>();
        public bool IsBlocked => HighestLevel == CodeRiskLevel.Blocked;
        public bool RequiresConfirmation => HighestLevel == CodeRiskLevel.Warning;
    }

    public static class GeneratedCodeSafetyValidator
    {
        // Never allowed in an in-Revit automation script — these have no legitimate use case here.
        private static readonly (Regex Pattern, string Reason)[] BlockedPatterns =
        {
            (new Regex(@"Process\s*\.\s*Start", RegexOptions.IgnoreCase),
                "Launches an external program (Process.Start)."),
            (new Regex(@"Microsoft\s*\.\s*Win32\s*\.\s*Registry|\bRegistryKey\b", RegexOptions.IgnoreCase),
                "Reads or writes the Windows registry."),
            (new Regex(@"\b(HttpClient|WebClient|WebRequest|HttpWebRequest|Socket|TcpClient|TcpListener|FtpWebRequest|UdpClient)\b"),
                "Attempts network/internet access."),
            (new Regex(@"(?<![A-Za-z0-9_])unsafe\b"),
                "Contains 'unsafe' code."),
            (new Regex(@"Assembly\s*\.\s*(Load|LoadFrom|LoadFile)\s*\(|Reflection\s*\.\s*Emit|Activator\s*\.\s*CreateInstance\s*\("),
                "Loads assemblies or uses reflection to bypass normal API usage."),
            (new Regex(@"\bDllImport\b|Marshal\s*\.\s*"),
                "Calls unmanaged/native code."),
            (new Regex(@"Environment\s*\.\s*Exit\s*\("),
                "Attempts to terminate the Revit process."),
            (new Regex(@"(File|Directory)\s*\.\s*(Delete|Move)\s*\("),
                "Deletes or moves files/folders on disk — this cannot be undone with Ctrl+Z."),
        };

        // Legitimate but destructive or disk-touching operations — require user confirmation, not a block.
        // (e.g. exporting selected element data to a CSV file is a normal, useful request.)
        private static readonly (Regex Pattern, string Reason)[] WarningPatterns =
        {
            (new Regex(@"for(each)?\s*\([^)]*\)\s*\{[^{}]*\.\s*Delete\s*\(", RegexOptions.IgnoreCase | RegexOptions.Singleline),
                "Deletes Revit elements inside a loop (bulk delete)."),
            (new Regex(@"\.\s*Delete\s*\(", RegexOptions.IgnoreCase),
                "Deletes one or more Revit elements."),
            (new Regex(@"\bPurge\w*\s*\(", RegexOptions.IgnoreCase),
                "Purges elements/types from the model."),
            (new Regex(@"(File)\s*\.\s*(WriteAllText|WriteAllBytes|WriteAllLines|Copy|Create|AppendAllText)\s*\("),
                "Writes a file to disk (e.g. exporting a report) — check the file path looks correct."),
        };

        public static CodeSafetyResult Validate(string code)
        {
            var result = new CodeSafetyResult();
            if (string.IsNullOrWhiteSpace(code)) return result;

            foreach (var check in BlockedPatterns)
            {
                if (check.Pattern.IsMatch(code))
                {
                    result.Findings.Add(new CodeSafetyFinding { Level = CodeRiskLevel.Blocked, Reason = check.Reason });
                }
            }

            // Only surface warnings when nothing is already blocked — a blocked script won't run anyway.
            if (result.Findings.Count == 0)
            {
                foreach (var check in WarningPatterns)
                {
                    if (check.Pattern.IsMatch(code))
                    {
                        result.Findings.Add(new CodeSafetyFinding { Level = CodeRiskLevel.Warning, Reason = check.Reason });
                    }
                }
            }

            result.HighestLevel = result.Findings.Count == 0
                ? CodeRiskLevel.Safe
                : result.Findings.Max(f => f.Level);

            return result;
        }
    }
}
