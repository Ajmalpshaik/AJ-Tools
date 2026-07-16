#region Metadata
/*
 * Tool Name     : AJ AI (Gemini Shell) - AutoDebugger MCP Bridge
 * File Name     : McpBridgeService.cs
 * Purpose       : Local named-pipe server that lets an external MCP (Model Context Protocol)
 *                 process run C# snippets against the live Revit document, by forwarding them
 *                 into the existing RevitExecutionService/RoslynService pipeline. This is what
 *                 the "AutoDebugger" Connect/Disconnect toggle in the AJ AI pane starts and stops.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.5.0
 *
 * Created Date  : 2026-07-07
 * Last Updated  : 2026-07-16
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : System.IO.Pipes, Newtonsoft.Json, RevitExecutionService, GeneratedCodeSafetyValidator,
 *                 AiTaskWarningBarService
 *
 * Input         : Newline-delimited JSON requests on a local named pipe (never a network socket)
 * Output        : Newline-delimited JSON responses; model changes only via RevitExecutionService
 *
 * Notes         :
 * - Named pipes are local-machine-only by construction (no port, no firewall exposure, no admin
 *   rights required to host one) - a safer default here than an HTTP loopback listener, which on
 *   Windows requires either admin rights or a one-time URL ACL reservation.
 * - Every request must include the per-session token written to the discovery file at Start() -
 *   this stops any other local process from driving Revit through the pipe unnoticed.
 * - Every request is still scanned by GeneratedCodeSafetyValidator before it reaches RoslynService,
 *   same as a human-typed script in the AJ AI pane: outright-dangerous patterns are always blocked,
 *   and destructive-but-legitimate ones (Delete/Purge/file write) are refused unless the caller
 *   explicitly sets AllowDestructive - there is no user at the keyboard to answer a confirmation
 *   popup here, so silence must mean "no".
 * - A second Start() attempt (e.g. two Revit sessions open) fails cleanly with a plain error instead
 *   of crashing.
 * - Only one chat session is ever "active" (able to run scripts) at a time - Revit itself only runs
 *   one script at a time anyway (see RevitExecutionService's re-entrancy guard), so this matches
 *   reality rather than pretending concurrency is possible. When a new chat window connects, it
 *   preempts whoever was active immediately (no waiting) - ListenLoopAsync always keeps one extra
 *   pipe instance listening so the next connect can be accepted the instant it arrives. The preempted
 *   session isn't broken: its own next call reconnects transparently (Node client already handles a
 *   dropped pipe). IdleReleaseTimeout is a secondary safety net only, for a connected-but-abandoned
 *   session with nobody else trying to connect either.
 * - Pre-warm fires a trivial, document-touching-free script through the normal execution pipeline right
 *   after Start() succeeds, on a detached task - it never blocks Start() from returning, and a failure
 *   (e.g. no document open yet) is swallowed silently since pre-warming is a pure optimization.
 * - The audit log is append-only, best-effort (a logging failure never fails the underlying request),
 *   and truncates code/output to keep each line small; it is not a security control by itself - actual
 *   safety still comes from GeneratedCodeSafetyValidator above.
 *
 * Changelog     :
 * v1.5.0 (2026-07-16) - Instant preemption: a new chat window connecting immediately takes over as
 *                       the active session instead of waiting for the previous one to time out.
 *                       Pipe instance count raised 1 -> 2 (one active + one always-listening) so the
 *                       next connect can land the moment it arrives. Supersedes the plain idle-only
 *                       release below with what Ajmal actually asked for: finish in one chat, move
 *                       straight to the next, no wait.
 * v1.4.0 (2026-07-16) - Idle-release timeout (3 min) on the held connection, so switching to a
 *                       different chat window without explicitly disconnecting the first one no
 *                       longer times out - the first session just reconnects transparently next time
 *                       it's used. Fixes: single pipe slot meant a forgotten chat window silently
 *                       blocked every other chat from reaching Revit. (Now a secondary fallback only -
 *                       see v1.5.0.)
 * v1.3.0 (2026-07-14) - Roslyn pre-warm on Start() (first real query no longer pays the one-time
 *                       JIT/assembly-load cost) and an append-only audit log of every non-ping request
 *                       (timestamp, success, truncated code/output) at
 *                       %AppData%/AJTools/autodebugger-audit.jsonl. Ideas adapted from reviewing several
 *                       external Revit MCP projects' published docs (see scripts/README.md "Where these
 *                       ideas came from" - Fourth pass); no code copied, this project's own architecture.
 * v1.2.1 (2026-07-12) - Capture Revit's UI dispatcher for the AutoDebugger activity banner.
 * v1.2.0 (2026-07-12) - Show a temporary non-blocking activity banner while an authenticated AI task runs.
 * v1.1.0 (2026-07-11) - Keep authenticated clients connected for multiple newline-delimited requests,
 *                        avoiding a named-pipe reconnect for every MCP tool call.
 * v1.0.0 (2026-07-07) - Initial release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.IO;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Newtonsoft.Json;

namespace AJTools.GeminiShell.Services
{
    public class McpBridgeRequest
    {
        public string Token { get; set; }
        public string Code { get; set; }
        public bool AllowDestructive { get; set; }
    }

    public class McpBridgeResponse
    {
        public bool Success { get; set; }
        public string Output { get; set; }
        public string Error { get; set; }
        public bool NeedsConfirmation { get; set; }
    }

    public class McpBridgeService
    {
        private const string PipeName = "AJTools.AutoDebugger";

        private readonly RevitExecutionService _executionService;
        private readonly AiTaskWarningBarService _activityBanner;
        private readonly object _pipeLock = new object();

        private CancellationTokenSource _cts;
        private NamedPipeServerStream _activePipe;   // instance currently servicing a connected chat, if any
        private NamedPipeServerStream _waitingPipe;  // instance currently listening for the next connect
        private Task _listenLoopTask;

        public bool IsRunning { get; private set; }
        public string Token { get; private set; }

        public McpBridgeService(RevitExecutionService executionService)
        {
            _executionService = executionService;
            // GeminiShellPaneProvider constructs this service on Revit's UI thread.
            _activityBanner = new AiTaskWarningBarService(Dispatcher.CurrentDispatcher);
        }

        private static string DiscoveryFilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AJTools", "autodebugger-bridge.json");

        /// <summary>Starts hosting the pipe. Returns false with a plain error if the pipe is already in use.</summary>
        public bool Start(out string errorMessage)
        {
            if (IsRunning)
            {
                errorMessage = null;
                return true;
            }

            try
            {
                var firstWaitingPipe = CreatePipe();
                lock (_pipeLock) { _waitingPipe = firstWaitingPipe; }

                Token = GenerateToken();
                WriteDiscoveryFile();

                _cts = new CancellationTokenSource();
                IsRunning = true;

                _listenLoopTask = Task.Run(() => ListenLoopAsync(firstWaitingPipe, _cts.Token));
                PreWarmRoslyn();

                errorMessage = null;
                return true;
            }
            catch (Exception ex)
            {
                IsRunning = false;
                errorMessage = "Could not start the AutoDebugger bridge: " + ex.Message;
                return false;
            }
        }

        public void Stop()
        {
            if (!IsRunning) return;

            IsRunning = false;
            _cts?.Cancel();

            lock (_pipeLock)
            {
                try { _activePipe?.Dispose(); } catch { /* unblocks a pending read */ }
                try { _waitingPipe?.Dispose(); } catch { /* unblocks a pending WaitForConnectionAsync */ }
                _activePipe = null;
                _waitingPipe = null;
            }

            DeleteDiscoveryFile();
        }

        private static NamedPipeServerStream CreatePipe()
        {
            // 2, not 1: instant preemption (below) needs one instance actively servicing the current
            // chat AND a second instance already listening for the next connect at the same time.
            return new NamedPipeServerStream(
                PipeName,
                PipeDirection.InOut,
                2,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);
        }

        private async Task ListenLoopAsync(NamedPipeServerStream firstWaitingPipe, CancellationToken token)
        {
            var waitingPipe = firstWaitingPipe;

            while (!token.IsCancellationRequested)
            {
                try
                {
                    await waitingPipe.WaitForConnectionAsync().ConfigureAwait(false);
                }
                catch
                {
                    if (token.IsCancellationRequested) break;
                    // A single bad connection should not take the whole bridge down - keep listening.
                    try { waitingPipe.Dispose(); } catch { }
                    try
                    {
                        waitingPipe = CreatePipe();
                        lock (_pipeLock) { _waitingPipe = waitingPipe; }
                    }
                    catch { break; }
                    continue;
                }

                if (token.IsCancellationRequested)
                {
                    try { waitingPipe.Dispose(); } catch { }
                    break;
                }

                // A new client just connected - it becomes the active session immediately, preempting
                // whoever held it before instead of making them wait out the idle timeout. Matches
                // Ajmal's real workflow: finish everything in one chat, then move straight to the next.
                var newActivePipe = waitingPipe;
                NamedPipeServerStream oldActivePipe;
                lock (_pipeLock)
                {
                    oldActivePipe = _activePipe;
                    _activePipe = newActivePipe;
                }
                try { oldActivePipe?.Dispose(); } catch { /* unblocks its pending read, if any */ }

                // Service this connection on its own task so the loop returns immediately to stand up
                // the next waiting instance - a third chat window can preempt just as fast as this one did.
                var handlerPipe = newActivePipe;
                _ = HandleConnectionAsync(handlerPipe).ContinueWith(_ =>
                {
                    try { handlerPipe.Dispose(); } catch { }
                    lock (_pipeLock) { if (_activePipe == handlerPipe) _activePipe = null; }
                }, TaskScheduler.Default);

                try
                {
                    waitingPipe = CreatePipe();
                    lock (_pipeLock) { _waitingPipe = waitingPipe; }
                }
                catch
                {
                    break;
                }
            }
        }

        // Secondary safety net, not the main mechanism: if a connected chat goes quiet this long with
        // nobody else trying to connect either, release it anyway (harmless self-cleanup - the Node
        // client, mcp-server/index.js, already detects a dropped pipe and reconnects transparently on
        // its next call). The main mechanism for switching chats is the instant preemption above.
        private static readonly TimeSpan IdleReleaseTimeout = TimeSpan.FromMinutes(3);

        private async Task HandleConnectionAsync(NamedPipeServerStream pipe)
        {
            try
            {
                using (var reader = new StreamReader(pipe, Encoding.UTF8, false, 4096, leaveOpen: true))
                using (var writer = new StreamWriter(pipe, new UTF8Encoding(false), 4096, leaveOpen: true) { AutoFlush = true })
                {
                    // The Node MCP server remains alive for the whole Codex session. Keep its authenticated
                    // pipe open and process one newline-delimited request at a time instead of forcing a
                    // connect/disconnect round trip for every count, ping, or script execution.
                    while (true)
                    {
                        var readTask = reader.ReadLineAsync();
                        var finished = await Task.WhenAny(readTask, Task.Delay(IdleReleaseTimeout)).ConfigureAwait(false);
                        if (finished != readTask) break; // idle too long - drop this connection, free the slot

                        string line = await readTask.ConfigureAwait(false);
                        if (line == null) break; // client disconnected normally

                        var response = await BuildResponseAsync(line).ConfigureAwait(false);
                        await writer.WriteLineAsync(JsonConvert.SerializeObject(response)).ConfigureAwait(false);
                    }
                }
            }
            catch
            {
                // Preempted mid-read (pipe disposed by a newer connection) or a bad client - either way,
                // just stop servicing this connection. Cleanup runs in the caller's continuation.
            }
        }

        private async Task<McpBridgeResponse> BuildResponseAsync(string requestLine)
        {
            McpBridgeRequest request;
            try
            {
                request = JsonConvert.DeserializeObject<McpBridgeRequest>(requestLine ?? string.Empty);
            }
            catch (Exception ex)
            {
                return new McpBridgeResponse { Success = false, Error = "Malformed request: " + ex.Message };
            }

            if (request == null || string.IsNullOrEmpty(request.Token) || !TokensMatch(request.Token, Token))
            {
                return new McpBridgeResponse { Success = false, Error = "Unauthorized: missing or wrong token." };
            }

            if (string.IsNullOrWhiteSpace(request.Code))
            {
                return new McpBridgeResponse { Success = false, Error = "No code to execute." };
            }

            var safety = GeneratedCodeSafetyValidator.Validate(request.Code);

            if (safety.IsBlocked)
            {
                string reasons = string.Join(" | ", System.Linq.Enumerable.Select(safety.Findings, f => f.Reason));
                return new McpBridgeResponse { Success = false, Error = "Blocked - not allowed from the AutoDebugger bridge: " + reasons };
            }

            if (safety.RequiresConfirmation && !request.AllowDestructive)
            {
                string reasons = string.Join(" | ", System.Linq.Enumerable.Select(safety.Findings, f => f.Reason));
                return new McpBridgeResponse
                {
                    Success = false,
                    NeedsConfirmation = true,
                    Error = "Needs confirmation: " + reasons + " Resend with allowDestructive = true if this is intentional."
                };
            }

            bool isHealthProbe = string.Equals(request.Code.Trim(), "\"pong\"", StringComparison.Ordinal);
            if (!isHealthProbe)
                _activityBanner.BeginTask();

            try
            {
                var result = await _executionService.ExecuteAsync(request.Code).ConfigureAwait(false);
                if (!isHealthProbe)
                    AppendAuditLogEntry(request.Code, result.Success, result.Output, result.ErrorMessage);
                return new McpBridgeResponse { Success = result.Success, Output = result.Output, Error = result.ErrorMessage };
            }
            finally
            {
                if (!isHealthProbe)
                    _activityBanner.EndTask();
            }
        }

        private static bool TokensMatch(string a, string b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }

        private static string GenerateToken()
        {
            var bytes = new byte[24];
            using (var rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(bytes);
            }
            return Convert.ToBase64String(bytes);
        }

        private void WriteDiscoveryFile()
        {
            string dir = Path.GetDirectoryName(DiscoveryFilePath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var info = new { pipeName = PipeName, token = Token };
            File.WriteAllText(DiscoveryFilePath, JsonConvert.SerializeObject(info));
        }

        private static void DeleteDiscoveryFile()
        {
            try
            {
                if (File.Exists(DiscoveryFilePath)) File.Delete(DiscoveryFilePath);
            }
            catch { /* best-effort cleanup */ }
        }

        /// <summary>
        /// Fires a trivial, document-touching-free script through the normal execution pipeline right
        /// after the bridge starts, so Roslyn's one-time JIT/assembly-load cost is paid here instead of
        /// on the first real query of the session. Detached on purpose - never blocks Start() from
        /// returning, and any failure (e.g. no document open yet) is swallowed since this is a pure
        /// optimization, not a required step.
        /// </summary>
        private void PreWarmRoslyn()
        {
            _ = Task.Run(async () =>
            {
                try { await _executionService.ExecuteAsync("\"warm\"").ConfigureAwait(false); }
                catch { /* best-effort - pre-warming must never affect bridge startup or later requests */ }
            });
        }

        private static string AuditLogFilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AJTools", "autodebugger-audit.jsonl");

        private const int AuditLogFieldMaxChars = 500;

        /// <summary>
        /// Appends one line to a permanent, append-only audit log of every non-ping request this bridge
        /// has executed - timestamp, success, and truncated code/output/error. Best-effort: a logging
        /// failure (disk full, permissions) never fails the underlying request that triggered it. This is
        /// a record for Ajmal's own review, not a safety control - GeneratedCodeSafetyValidator upstream
        /// is still what actually blocks/gates anything.
        /// </summary>
        private static void AppendAuditLogEntry(string code, bool success, string output, string error)
        {
            try
            {
                string dir = Path.GetDirectoryName(AuditLogFilePath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                var entry = new
                {
                    timestamp = DateTime.UtcNow.ToString("o"),
                    success,
                    code = Truncate(code),
                    output = Truncate(output),
                    error = Truncate(error)
                };

                File.AppendAllText(AuditLogFilePath, JsonConvert.SerializeObject(entry) + Environment.NewLine);
            }
            catch { /* best-effort - never let audit logging fail the actual request */ }
        }

        private static string Truncate(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= AuditLogFieldMaxChars) return value;
            return value.Substring(0, AuditLogFieldMaxChars) + "...(truncated)";
        }
    }
}
