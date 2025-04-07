using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace JsonSchemaGenerator.Controls
{
    public partial class CodeViewer : UserControl
    {
        public static readonly StyledProperty<string> CodeTextProperty =
            AvaloniaProperty.Register<CodeViewer, string>(
                nameof(CodeText), defaultValue: string.Empty);

        public string CodeText
        {
            get => GetValue(CodeTextProperty);
            set => SetValue(CodeTextProperty, value);
        }

        public CodeViewer()
        {
            InitializeComponent();
            this.GetObservable(CodeTextProperty).Subscribe(UpdateLineNumbers);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void UpdateLineNumbers(string text)
        {
            var lines = text?.Split('\n') ?? Array.Empty<string>();
            var lineNumbers = string.Join(Environment.NewLine, Enumerable.Range(1, lines.Length).Select(i => i.ToString()));
            this.FindControl<TextBlock>("LineNumbers").Text = lineNumbers;
        }
    }
}
