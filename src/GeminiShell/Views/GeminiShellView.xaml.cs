using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Media;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using AJTools.GeminiShell.ViewModels;
using AJTools.GeminiShell.Helpers;

namespace AJTools.GeminiShell.Views
{
    public partial class GeminiShellView : UserControl
    {
        private volatile bool _isUpdatingText = false;
        private TextMarkerService _textMarkerService;
        private DispatcherTimer _syntaxCheckTimer;

        public GeminiShellView()
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

            DataContextChanged += GeminiShellView_DataContextChanged;
        }

        private void GeminiShellView_DataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is GeminiShellViewModel oldVm)
            {
                oldVm.PropertyChanged -= Vm_PropertyChanged;
            }
            if (e.NewValue is GeminiShellViewModel newVm)
            {
                newVm.PropertyChanged += Vm_PropertyChanged;
                CodeEditor.Text = newVm.CodeEditorContent ?? string.Empty;
            }
        }

        private void Vm_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(GeminiShellViewModel.CodeEditorContent))
            {
                // Ensure UI updates happen on the UI thread
                if (!Dispatcher.CheckAccess())
                {
                    Dispatcher.Invoke(() => Vm_PropertyChanged(sender, e));
                    return;
                }

                if (DataContext is GeminiShellViewModel vm)
                {
                    if (CodeEditor.Text != vm.CodeEditorContent)
                    {
                        _isUpdatingText = true;
                        CodeEditor.Text = vm.CodeEditorContent ?? string.Empty;
                        _isUpdatingText = false;
                    }
                }
            }
        }

        private void CodeEditor_TextChanged(object sender, System.EventArgs e)
        {
            if (!_isUpdatingText && DataContext is GeminiShellViewModel vm)
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
                _textMarkerService.Clear();
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
                        _textMarkerService.Clear();
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
    }
}
