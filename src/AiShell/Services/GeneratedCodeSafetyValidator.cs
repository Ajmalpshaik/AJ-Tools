#region Metadata
/*
 * Tool Name     : C#
 * File Name     : GeneratedCodeSafetyValidator.cs
 * Purpose       : Pre-execution text scan of AI-generated C# scripts. Blocks clearly dangerous
 *                 operations (process launch, registry, network, reflection/unsafe, disk I/O)
 *                 and flags destructive-but-legitimate Revit operations (delete/purge) so the
 *                 user can confirm before running.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.3.0
 *
 * Created Date  : 2026-07-01
 * Last Updated  : 2026-07-18
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
 *   (Process.Start, registry, HttpClient/WebClient/SmtpClient/Dns/Ping, reflection Invoke,
 *   unsafe, raw file I/O, #r/#load directives, using-static/type-alias renaming) but it is still
 *   plain text/regex matching, not an AST/semantic scan — a sufficiently determined attempt to
 *   obfuscate code (e.g. string-built reflection member names, char-code-built strings, or other
 *   indirection this file hasn't been taught to recognize yet) can still slip through undetected.
 *   Real isolation would require AppDomain/process-level sandboxing, which is out of scope here.
 *   Treat this as a speed bump against careless/accidental generated code, not a security boundary
 *   against a determined bypass.
 * - Delete/Purge calls are not blocked outright (they are normal Revit operations) but are
 *   flagged so the caller can show a confirmation prompt first.
 *
 * Changelog     :
 * v1.3.0 (2026-07-18) - Widened the process-kill block from the one specific
 *                       `Process.GetCurrentProcess().Kill()` chain to any `.Kill(` call. The old
 *                       pattern only caught a script killing Revit itself — it did not catch
 *                       `Process.GetProcessesByName("x")[0].Kill()` or similar, which could kill any
 *                       other running program on the machine (antivirus, backups, whatever else the
 *                       user has open) without tripping any check at all.
 * v1.2.0 (2026-07-18) - Closed the `using static`/type-alias bypass documented in the v1.1.0 notes:
 *                       blocked `using static X;` (which lets a script call a blocked member, e.g.
 *                       Process.Start, by its bare method name) and `using X = Y;` type aliases
 *                       (which let a script rename a blocked type, e.g. System.IO.File, to dodge the
 *                       name-based checks above). The alias pattern requires an `=` so it does not
 *                       match ordinary `using Namespace.Type;` imports. Updated the Notes to be
 *                       specific about what is now covered vs. what still isn't (string-built member
 *                       names and similar indirection remain an open gap; this is still text/regex
 *                       matching, not an AST/semantic scan).
 * v1.1.0 (2026-07-17) - Safety hardening pass: blocked #r/#load script directives (RoslynService
 *                       never disabled Roslyn's default directive resolver, so this was a full,
 *                       one-line bypass of every other check — a script could pull in an
 *                       arbitrary local or GAC-resident assembly and run unrestricted code).
 *                       Blocked reflection-based indirect member access (GetMethod/GetProperty/
 *                       GetField followed by Invoke/SetValue/GetValue), which generalized to a
 *                       bypass for most other blocked patterns. Blocked Process.Kill and
 *                       Environment.FailFast (both kill Revit outright, same as Environment.Exit).
 *                       Added SmtpClient/Dns/Ping to the network blocklist — all three ship inside
 *                       the same System.dll already referenced for HttpClient/WebClient, so they
 *                       were reachable but unchecked. Rewrote the Notes above to state plainly that
 *                       this remains a text-matching guard, not a security boundary — ordinary C#
 *                       idioms (`using static`, type aliasing) can still bypass it, and that gap is
 *                       not closed by this pass; it would need an AST/semantic-model based scan.
 * v1.0.0 (2026-07-01) - Initial release as part of the AJ AI safety hardening pass.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AJTools.AiShell.Services
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
            (new Regex(@"\.\s*Kill\s*\(|Environment\s*\.\s*FailFast\s*\("),
                "Kills a process outright — whether it's Revit itself (unsaved work would be lost) or another running program on the machine."),
            (new Regex(@"Microsoft\s*\.\s*Win32\s*\.\s*Registry|\bRegistryKey\b", RegexOptions.IgnoreCase),
                "Reads or writes the Windows registry."),
            (new Regex(@"\b(HttpClient|WebClient|WebRequest|HttpWebRequest|Socket|TcpClient|TcpListener|FtpWebRequest|UdpClient|SmtpClient|Dns|Ping)\b"),
                "Attempts network/internet access."),
            (new Regex(@"(?<![A-Za-z0-9_])unsafe\b"),
                "Contains 'unsafe' code."),
            (new Regex(@"Assembly\s*\.\s*(Load|LoadFrom|LoadFile)\s*\(|Reflection\s*\.\s*Emit|Activator\s*\.\s*CreateInstance\s*\("),
                "Loads assemblies or uses reflection to bypass normal API usage."),
            (new Regex(@"\.\s*(GetMethod|GetProperty|GetField)\s*\([^)]*\)\s*\.\s*(Invoke|SetValue|GetValue)\s*\(", RegexOptions.Singleline),
                "Uses reflection to call or read/write a member indirectly, bypassing the normal API checks above."),
            (new Regex(@"\bDllImport\b|Marshal\s*\.\s*"),
                "Calls unmanaged/native code."),
            (new Regex(@"Environment\s*\.\s*Exit\s*\("),
                "Attempts to terminate the Revit process."),
            (new Regex(@"(File|Directory)\s*\.\s*(Delete|Move)\s*\("),
                "Deletes or moves files/folders on disk — this cannot be undone with Ctrl+Z."),
            (new Regex(@"^\s*#\s*(r|load)\s*""", RegexOptions.Multiline),
                "Uses a #r/#load directive to pull in an extra assembly or file — this runs completely outside every check above, so it is never allowed."),
            (new Regex(@"^\s*using\s+static\s+", RegexOptions.Multiline),
                "Uses 'using static', which can rename a blocked call (e.g. Process.Start) to a bare method name and slip past the checks above."),
            (new Regex(@"^\s*using\s+[A-Za-z_][A-Za-z0-9_]*\s*=\s*", RegexOptions.Multiline),
                "Uses a 'using X = Y;' type alias, which can rename a blocked type (e.g. System.IO.File) to slip past the checks above."),
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
