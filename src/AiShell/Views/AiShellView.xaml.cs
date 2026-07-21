using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using System.Windows.Media;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using AJTools.AiShell.ViewModels;
using AJTools.AiShell.Helpers;

namespace AJTools.AiShell.Views
{
    public partial class AiShellView : UserControl
    {
        private volatile bool _isUpdatingText = false;
        private TextMarkerService _textMarkerService;
        private DispatcherTimer _syntaxCheckTimer;

        public AiShellView()
        {
            InitializeComponent();
            
            _textMarkerService = new TextMarkerService(CodeEditor.Document);
            CodeEditor.TextArea.TextView.BackgroundRenderers.Add(_textMarkerService);
            CodeEditor.TextArea.TextView.LineTransformers.Add(_textMarkerService);
            
            var textViewConnect = _textMarkerService as ICSharpCode.AvalonEdit.Rendering.ITextViewConnect;
            textViewConnect?.AddToTextView(CodeEditor.TextArea.TextView);

            _syntaxCheckTimer = new DispatcherTimer();
            _syntaxCheckTimer.Interval = TimeSpan.FromMilliseconds(500);
            _syntaxCheckTimer.Tick += SyntaxCheckTimer_Tick;

            DataContextChanged += AiShellView_DataContextChanged;
        }

        private void AiShellView_DataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is AiShellViewModel oldVm)
            {
                oldVm.PropertyChanged -= Vm_PropertyChanged;
                oldVm.CodeGenerated -= Vm_CodeGenerated;
            }
            if (e.NewValue is AiShellViewModel newVm)
            {
                newVm.PropertyChanged += Vm_PropertyChanged;
                newVm.CodeGenerated += Vm_CodeGenerated;
                CodeEditor.Text = newVm.CodeEditorContent ?? string.Empty;
            }
        }

        private void Vm_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AiShellViewModel.CodeEditorContent))
            {
                // Ensure UI updates happen on the UI thread
                if (!Dispatcher.CheckAccess())
                {
                    Dispatcher.Invoke(() => Vm_PropertyChanged(sender, e));
                    return;
                }

                if (DataContext is AiShellViewModel vm)
                {
                    if (CodeEditor.Text != vm.CodeEditorContent)
                    {
                        _isUpdatingText = true;
                        CodeEditor.Text = vm.CodeEditorContent ?? string.Empty;
                        _isUpdatingText = false;
                    }
                }
            }
            else if (e.PropertyName == nameof(AiShellViewModel.ReplTranscript))
            {
                if (!Dispatcher.CheckAccess())
                {
                    Dispatcher.Invoke(() => Vm_PropertyChanged(sender, e));
                    return;
                }

                ReplTranscriptBox.ScrollToEnd();
            }
        }

        /// <summary>Enter runs the current console line; Up/Down recall previous lines - the same
        /// keys a real interactive shell (RevitPythonShell's console, a terminal REPL) uses.</summary>
        private void ReplInputBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (!(DataContext is AiShellViewModel vm)) return;

            if (e.Key == System.Windows.Input.Key.Enter)
            {
                e.Handled = true;
                if (vm.ReplRunCommand.CanExecute(null)) vm.ReplRunCommand.Execute(null);
            }
            else if (e.Key == System.Windows.Input.Key.Up)
            {
                e.Handled = true;
                vm.ReplInput = vm.RecallPreviousReplCommand();
                ReplInputBox.CaretIndex = ReplInputBox.Text.Length;
            }
            else if (e.Key == System.Windows.Input.Key.Down)
            {
                e.Handled = true;
                vm.ReplInput = vm.RecallNextReplCommand();
                ReplInputBox.CaretIndex = ReplInputBox.Text.Length;
            }
        }

        /// <summary>Opens the Settings popup (its own Window, not touched inline in this pane
        /// anymore, per Ajmal's request). Settings never call the Revit API - pure local config -
        /// so a plain ShowDialog() here needs no ExternalEvent.</summary>
        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow { DataContext = DataContext };

            IntPtr revitWindowHandle = Process.GetCurrentProcess().MainWindowHandle;
            if (revitWindowHandle != IntPtr.Zero)
            {
                new WindowInteropHelper(settingsWindow).Owner = revitWindowHandle;
            }

            settingsWindow.ShowDialog();
        }

        private void CodeEditor_TextChanged(object sender, System.EventArgs e)
        {
            if (!_isUpdatingText && DataContext is AiShellViewModel vm)
            {
                vm.CodeEditorContent = CodeEditor.Text;
            }
            
            _syntaxCheckTimer.Stop();
            _syntaxCheckTimer.Start();
        }

        private async void SyntaxCheckTimer_Tick(object sender, EventArgs e)
        {
            _syntaxCheckTimer.Stop();

            string code = CodeEditor.Text;
            if (string.IsNullOrWhiteSpace(code))
            {
                // Only error markers - a "changed line" highlight from the last generate should
                // survive a syntax re-check, not get wiped every 500ms while the user keeps typing.
                _textMarkerService.RemoveAll(m => Equals(m.Tag, ErrorMarkerTag));
                return;
            }

            try
            {
                await Task.Run(() =>
                {
                    var options = new CSharpParseOptions(kind: SourceCodeKind.Script);
                    var tree = CSharpSyntaxTree.ParseText(code, options);
                    var diagnostics = tree.GetDiagnostics();

                    Dispatcher.Invoke(() =>
                    {
                        _textMarkerService.RemoveAll(m => Equals(m.Tag, ErrorMarkerTag));
                        foreach (var diag in diagnostics)
                        {
                            if (diag.Severity == DiagnosticSeverity.Error)
                            {
                                int start = diag.Location.SourceSpan.Start;
                                int length = diag.Location.SourceSpan.Length;

                                // Ensure marker is visible even if length is 0 (e.g. expected semicolon)
                                if (length == 0)
                                {
                                    start = Math.Max(0, start - 1);
                                    length = 2;
                                }

                                var marker = _textMarkerService.Create(start, length);
                                marker.MarkerTypes = TextMarkerTypes.SquigglyUnderline;
                                marker.MarkerColor = Colors.Red;
                                marker.ToolTip = diag.GetMessage();
                                marker.Tag = ErrorMarkerTag;
                            }
                        }
                    });
                });
            }
            catch
            {
                // Ignore compilation exceptions
            }
        }

        private const string ErrorMarkerTag = "error";
        private const string ChangedLineMarkerTag = "changed";

        /// <summary>Highlights which lines actually changed after a GenerateCodeAsync edit, so a
        /// small incremental change (e.g. "change the color to green") is visibly obvious instead of
        /// looking like the whole script was silently rewritten. Only fires from a real generate -
        /// Format/RunFromHistory/auto-fix don't raise CodeGenerated, so they never trigger this.</summary>
        private void Vm_CodeGenerated(string previousCode, string newCode)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => Vm_CodeGenerated(previousCode, newCode));
                return;
            }

            _textMarkerService.RemoveAll(m => Equals(m.Tag, ChangedLineMarkerTag));

            // Nothing to compare against (first generation of the session) - highlighting "100% new"
            // is not a useful signal, so skip it entirely rather than lighting up the whole script.
            if (string.IsNullOrEmpty(previousCode) || string.IsNullOrEmpty(newCode))
            {
                return;
            }

            string[] oldLines = previousCode.Replace("\r\n", "\n").Split('\n');
            string[] newLines = newCode.Replace("\r\n", "\n").Split('\n');
            var changedLineIndices = ComputeChangedNewLineIndices(oldLines, newLines);
            if (changedLineIndices.Count == 0 || changedLineIndices.Count == newLines.Length)
            {
                // Nothing changed, or everything changed (effectively a fresh rewrite) - neither
                // case benefits from a line-by-line highlight.
                return;
            }

            var document = CodeEditor.Document;
            foreach (int lineIndex in changedLineIndices)
            {
                int lineNumber = lineIndex + 1; // AvalonEdit lines are 1-based
                if (lineNumber < 1 || lineNumber > document.LineCount) continue;

                var docLine = document.GetLineByNumber(lineNumber);
                if (docLine.Length == 0) continue; // don't mark blank lines

                var marker = _textMarkerService.Create(docLine.Offset, docLine.Length);
                marker.BackgroundColor = Color.FromArgb(0x35, 0x00, 0xC8, 0xFF); // translucent Neon Blue
                marker.Tag = ChangedLineMarkerTag;
                marker.ToolTip = "Changed by the last generate request";
            }
        }

        /// <summary>Classic LCS-based line diff: returns the indices (into newLines) of lines that
        /// are additions/changes relative to oldLines - i.e. NOT part of the longest common
        /// subsequence shared by both. Correctly handles insertions/deletions shifting line numbers,
        /// unlike a naive index-by-index comparison.</summary>
        private static List<int> ComputeChangedNewLineIndices(string[] oldLines, string[] newLines)
        {
            int n = oldLines.Length;
            int m = newLines.Length;
            var dp = new int[n + 1, m + 1];
            for (int i = n - 1; i >= 0; i--)
            {
                for (int j = m - 1; j >= 0; j--)
                {
                    dp[i, j] = oldLines[i] == newLines[j]
                        ? dp[i + 1, j + 1] + 1
                        : Math.Max(dp[i + 1, j], dp[i, j + 1]);
                }
            }

            var changed = new List<int>();
            int a = 0, b = 0;
            while (a < n && b < m)
            {
                if (oldLines[a] == newLines[b])
                {
                    a++;
                    b++;
                }
                else if (dp[a + 1, b] >= dp[a, b + 1])
                {
                    a++; // line present only in old (removed)
                }
                else
                {
                    changed.Add(b); // line present only in new (added/changed)
                    b++;
                }
            }
            while (b < m)
            {
                changed.Add(b);
                b++;
            }
            return changed;
        }
    }
}
