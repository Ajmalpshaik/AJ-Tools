#region Metadata
/*
 * Tool Name     : AJ AI Voice (Revit side)
 * File Name     : AiVoiceService.cs
 * Purpose       : Speaks the RESULT of each AJ AI Bridge request out loud, in a different voice from
 *                 the one the AI assistant itself uses, so Ajmal can follow what the model is doing
 *                 without watching the screen.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-08-11
 * Last Updated  : 2026-08-11
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Newtonsoft.Json, Windows PowerShell (fallback speech only)
 *
 * Input         : Success flag and output text of a completed bridge request.
 * Output        : One short spoken line. No model, view or sheet changes of any kind.
 *
 * Notes         :
 * - TWO VOICES, TWO JOBS, and deliberately not the same sentence twice. The assistant side (the AJ
 *   AI Brain's tools/voice/narrate-hook.mjs) speaks the INTENT before the work - "Counting air
 *   terminals" - in a male voice. This file speaks the RESULT that came back out of Revit - "Forty
 *   two" - in a female one. Having both narrate the same action just produces the same sentence
 *   twice, half a second apart, in stereo.
 *
 * - IT DOES NOT OWN A SPEAKER. It appends a line to a queue folder that the Brain's drainer.py
 *   process reads and speaks strictly in order. That shared queue is the entire reason the two
 *   voices never talk over each other, and it is why the queue lives in LocalApplicationData rather
 *   than inside either project: it is the one folder both sides can find without either knowing
 *   where the other is installed.
 *
 * - WHEN NOTHING IS LISTENING it still speaks, through Windows' own built-in voice via PowerShell.
 *   That path needs no Python, no add-in dependency and no internet, so driving the bridge from some
 *   other AI client - with the Brain nowhere in the picture - still talks. It is only reached when
 *   no drainer is running, so in normal use the better voice is the one you hear.
 *
 * - NOTHING HERE IS ALLOWED TO AFFECT A REVIT JOB. Every call is fire-and-forget on a background
 *   thread and every failure is swallowed. A model edit must never fail, stall or roll back because
 *   the machine could not say a word about it.
 *
 * Changelog     :
 * v1.0.0 (2026-08-11) - Initial release, per Ajmal's request for a JARVIS-style voice reporting what
 *                       the AI is doing. Pairs with the AJ AI Brain's tools/voice/ layer.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using Newtonsoft.Json;

namespace AJTools.AiShell.Services
{
    /// <summary>Speaks a one-line spoken confirmation of what a bridge request returned.</summary>
    internal static class AiVoiceService
    {
        /// <summary>Matches the "revit" profile in the Brain's tools/voice/voice-config.json.</summary>
        private const string VoiceProfile = "revit";

        /// <summary>Used only when no drainer is running. Present on every Windows install.</summary>
        private const string FallbackVoice = "Microsoft Zira Desktop";

        /// <summary>Longest result that is worth reading out verbatim. Past this it becomes "Done".</summary>
        private const int MaxSpokenResultLength = 60;

        private static readonly object CounterLock = new object();
        private static int _counter;

        /// <summary>
        /// The folder both the add-in and the AJ AI Brain agree on without either knowing where the
        /// other lives. LocalApplicationData rather than the Roaming folder the rest of AJ Tools uses:
        /// this holds a work queue and a generated-audio cache, which are machine-local and
        /// regenerable, and have no business being synced to a roaming profile.
        /// </summary>
        private static string RuntimeDirectory
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "AJTools",
                    "voice");
            }
        }

        private static string QueueDirectory
        {
            get { return Path.Combine(RuntimeDirectory, "queue"); }
        }

        /// <summary>Speaks what a finished bridge request actually returned. Never throws, never blocks.</summary>
        public static void SpeakResult(bool success, string output)
        {
            string line = ComposeLine(success, output);
            if (string.IsNullOrEmpty(line))
            {
                return;
            }

            // Off the Revit thread entirely. Writing a small file is fast, but "fast" is not a
            // guarantee worth betting a user's model edit on.
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    if (!TryEnqueue(line))
                    {
                        SpeakWithWindowsVoice(line);
                    }
                }
                catch
                {
                    /* Speech is a convenience. It never gets to surface an error at Ajmal. */
                }
            });
        }

        /// <summary>
        /// Turns a raw bridge result into something worth hearing.
        ///
        /// The whole point of this voice is to say the ANSWER - "Four" when Ajmal asks how many
        /// doors. The first version only read out a result that was a bare number, on the assumption
        /// that a count comes back as one. It does not: a real count returns
        ///
        ///     Matched 4 element(s) in 'Doors'.
        ///     Count: 4
        ///
        /// - words, punctuation and a line break. So every single answer fell through to "Done", and
        /// Ajmal heard nothing but "done, done, done" (reported 2026-08-11). The number is right
        /// there and is what he actually wants; it just has to be dug out of the sentence.
        /// </summary>
        private static string ComposeLine(bool success, string output)
        {
            if (!success)
            {
                return "That failed.";
            }

            string text = (output ?? string.Empty).Trim();
            if (text.Length == 0)
            {
                return "Done.";
            }

            string spokenNumber = ExtractCount(text);
            if (spokenNumber != null)
            {
                return spokenNumber + ".";
            }

            // Not a count. A short one-liner is worth hearing verbatim; a long report is meant to be
            // read on screen, not sat through, so it collapses to a single word.
            string firstLine = text.Split('\n')[0].Trim();
            bool isShortAndPlain =
                firstLine.Length > 0 &&
                firstLine.Length <= MaxSpokenResultLength &&
                firstLine.IndexOf('{') < 0 &&
                firstLine.IndexOf('[') < 0;

            return isShortAndPlain ? firstLine.TrimEnd('.') + "." : "Done.";
        }

        /// <summary>
        /// Digs the number out of a bridge result, or returns null when there is not one to say.
        /// Handles a bare number, the "Count: 4" line the count tools emit, and a leading
        /// "Matched 4 element(s)..." sentence - in that order of trust.
        /// </summary>
        private static string ExtractCount(string text)
        {
            long number;
            if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
            {
                return number.ToString(CultureInfo.InvariantCulture);
            }

            // "Count: 4" is the explicit, machine-meant line - trust it ahead of any prose.
            var countLine = System.Text.RegularExpressions.Regex.Match(
                text, @"(?im)^\s*count\s*[:=]\s*(-?\d+)\s*$");
            if (countLine.Success)
            {
                return countLine.Groups[1].Value;
            }

            var matched = System.Text.RegularExpressions.Regex.Match(
                text, @"(?i)\bmatched\s+(-?\d+)\b");
            if (matched.Success)
            {
                return matched.Groups[1].Value;
            }

            return null;
        }

        /// <summary>
        /// Drops one line into the shared queue. Returns false when nothing is there to speak it, so
        /// the caller can fall back rather than let the line vanish into a folder nobody reads.
        /// </summary>
        private static bool TryEnqueue(string line)
        {
            if (!IsSpeakerRunning())
            {
                return false;
            }

            Directory.CreateDirectory(QueueDirectory);

            // Filename ordering IS time ordering - the drainer sorts by name and speaks in that
            // order, which is what keeps this voice and the assistant's voice in sequence rather
            // than on top of each other. Same millisecond-stamp scheme as the Brain's say.mjs.
            long milliseconds = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
            int sequence;
            lock (CounterLock)
            {
                sequence = _counter++;
            }

            string unique = string.Format(
                CultureInfo.InvariantCulture,
                "{0:D13}-{1:D4}-{2}",
                milliseconds,
                sequence & 0xFFF,
                Guid.NewGuid().ToString("N").Substring(0, 6));

            string payload = JsonConvert.SerializeObject(new { text = line, profile = VoiceProfile });

            // Write beside the destination and rename in, so the drainer can never read a half-written
            // line. Same folder on purpose: a rename across volumes is not atomic and fails outright
            // on Windows.
            string temporaryPath = Path.Combine(QueueDirectory, unique + ".tmp");
            string finalPath = Path.Combine(QueueDirectory, unique + ".json");
            File.WriteAllText(temporaryPath, payload, new UTF8Encoding(false));
            File.Move(temporaryPath, finalPath);
            return true;
        }

        /// <summary>
        /// True when a drainer process is alive and will actually speak what we queue. The lock file
        /// holds its process id; a lock left behind by a killed process must not be believed, or every
        /// line after a crash would be queued into silence.
        /// </summary>
        private static bool IsSpeakerRunning()
        {
            try
            {
                string lockPath = Path.Combine(QueueDirectory, ".drainer.lock");
                if (!File.Exists(lockPath))
                {
                    return false;
                }

                string contents = File.ReadAllText(lockPath).Trim();
                int processId;
                if (!int.TryParse(contents, NumberStyles.Integer, CultureInfo.InvariantCulture, out processId))
                {
                    return false;
                }

                using (Process.GetProcessById(processId))
                {
                    return true;
                }
            }
            catch
            {
                // GetProcessById throws when the id is gone - which is the answer, not an error.
                return false;
            }
        }

        /// <summary>
        /// The always-available path: Windows' own speech engine, driven through PowerShell so this
        /// needs no NuGet package and behaves identically on .NET Framework and .NET 8+. Slower to
        /// start than the queue, and it cannot coordinate with the assistant's voice - which is why
        /// it is only ever used when there is no queue to coordinate with.
        /// </summary>
        private static void SpeakWithWindowsVoice(string line)
        {
            try
            {
                string safeLine = line.Replace("'", "''");
                string script =
                    "Add-Type -AssemblyName System.Speech; " +
                    "$s = New-Object System.Speech.Synthesis.SpeechSynthesizer; " +
                    "try { $s.SelectVoice('" + FallbackVoice + "') } catch { }; " +
                    "$s.Rate = 2; $s.Speak('" + safeLine + "'); $s.Dispose()";

                var startInfo = new ProcessStartInfo("powershell", "-NoProfile -NonInteractive -Command \"" + script + "\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true // Never flash a console window over the Revit model.
                };

                using (Process.Start(startInfo))
                {
                    // Deliberately not waited on: this is fire-and-forget narration.
                }
            }
            catch
            {
                /* No voice available. Silence is the correct outcome, not an error dialog. */
            }
        }
    }
}
