using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

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

        public static readonly StyledProperty<IList<int>> ErrorLinesProperty =
            AvaloniaProperty.Register<CodeViewer, IList<int>>(nameof(ErrorLines));

        public IList<int> ErrorLines
        {
            get => GetValue(ErrorLinesProperty);
            set => SetValue(ErrorLinesProperty, value);
        }

        public ObservableCollection<ColoredLine> ParsedLines { get; set; } = new();
        public ObservableCollection<ColoredLine> LineNumbers { get; set; } = new();

        public CodeViewer()
        {
            InitializeComponent();
            this.GetObservable(CodeTextProperty).Subscribe(_ => UpdateLineNumbersAndColors());
            this.GetObservable(ErrorLinesProperty).Subscribe(_ => UpdateLineNumbersAndColors());
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public int LineNumber { get; set; }  // w klasie ColoredLine

        private void UpdateLineNumbersAndColors()
        {
            var lines = CodeText?.Split('\n') ?? Array.Empty<string>();

            ParsedLines.Clear();
            LineNumbers.Clear();

            for (int i = 0; i < lines.Length; i++)
            {
                bool isError = ErrorLines != null && ErrorLines.Contains(i + 1);

                var color = isError ? Brushes.IndianRed : Brushes.Green;

                ParsedLines.Add(new ColoredLine
                {
                    Text = lines[i],
                    Color = color
                });

                LineNumbers.Add(new ColoredLine
                {
                    Text = (i + 1).ToString(),
                    Color = color
                });
            }
        }
    }
}