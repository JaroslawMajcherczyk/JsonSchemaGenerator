using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media;
using JsonSchemaGenerator.JsonSchema;
using ReactiveUI;

namespace JsonSchemaGenerator.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        private Window _parentWindow;

        private readonly JsonParser _parser = new();
        private readonly SchemaGenerator _generator = new();
        private readonly JsonValidator _validator = new();

        private string _validationResult;
        public string ValidationResult
        {
            get => _validationResult;
            set => this.RaiseAndSetIfChanged(ref _validationResult, value);
        }

        private string _comparisonResult;
        public string ComparisonResult
        {
            get => _comparisonResult;
            set => this.RaiseAndSetIfChanged(ref _comparisonResult, value);
        }

        private IBrush _schemaBackground = Brushes.Transparent;
        public IBrush SchemaBackground
        {
            get => _schemaBackground;
            set => this.RaiseAndSetIfChanged(ref _schemaBackground, value);
        }

        private IBrush _jsonBackground = Brushes.Transparent;
        public IBrush JsonBackground
        {
            get => _jsonBackground;
            set => this.RaiseAndSetIfChanged(ref _jsonBackground, value);
        }

        private string _validationJsonPath;
        public string ValidationJsonPath
        {
            get => _validationJsonPath;
            set => this.RaiseAndSetIfChanged(ref _validationJsonPath, value);
        }

        private string _validationSchemaPath;
        public string ValidationSchemaPath
        {
            get => _validationSchemaPath;
            set => this.RaiseAndSetIfChanged(ref _validationSchemaPath, value);
        }

        private List<int> _jsonErrorLines = new();
        public List<int> JsonErrorLines
        {
            get => _jsonErrorLines;
            set => this.RaiseAndSetIfChanged(ref _jsonErrorLines, value);
        }

        private List<int> _schemaErrorLines = new();
        public List<int> SchemaErrorLines
        {
            get => _schemaErrorLines;
            set => this.RaiseAndSetIfChanged(ref _schemaErrorLines, value);
        }

        private string _jsonPath;

        public string JsonPath
        {
            get => _jsonPath;
            set => this.RaiseAndSetIfChanged(ref _jsonPath, value);
        }

        public string SchemaPath { get; set; }

        public ReactiveCommand<Unit, Unit> SelectJsonCommand { get; }
        public ReactiveCommand<Unit, Unit> SaveSchemaCommand { get; }
        public ReactiveCommand<Unit, Unit> SelectValidationJsonCommand { get; }
        public ReactiveCommand<Unit, Unit> SelectValidationSchemaCommand { get; }
        public ReactiveCommand<Unit, Unit> CompareCommand { get; }

        public MainWindowViewModel(Window parentWindow)
        {
            _parentWindow = parentWindow ?? throw new ArgumentNullException(nameof(parentWindow));

            SelectJsonCommand = ReactiveCommand.CreateFromTask(SelectJson);
            SaveSchemaCommand = ReactiveCommand.CreateFromTask(
                SaveSchema,
                this.WhenAnyValue(x => x.JsonPath, jsonPath => !string.IsNullOrEmpty(jsonPath)));

            SelectValidationJsonCommand = ReactiveCommand.CreateFromTask(SelectValidationJson);
            SelectValidationSchemaCommand = ReactiveCommand.CreateFromTask(SelectValidationSchema);
            CompareCommand = ReactiveCommand.CreateFromTask(Compare, this.WhenAnyValue(
                x => x.ValidationJsonPath,
                x => x.ValidationSchemaPath,
                (json, schema) => !string.IsNullOrEmpty(json) && !string.IsNullOrEmpty(schema)
            ));
        }

        private async Task SelectJson()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select JSON file",
                Filters = { new FileDialogFilter { Name = "JSON Files", Extensions = { "json" } } }
            };
            var result = await dialog.ShowAsync(_parentWindow);
            if (result?.FirstOrDefault() is string selectedPath)
            {
                JsonPath = selectedPath;
                this.RaisePropertyChanged(nameof(JsonPath)); // <-- To też zostaw
                ValidationResult = "JSON file selected:  " + JsonPath;
                JsonContent = File.ReadAllText(JsonPath);
            }
        }

        private async Task SaveSchema()
        {
            if (!string.IsNullOrEmpty(JsonPath))
            {
                var dialog = new SaveFileDialog
                {
                    Title = "Save JSON Schema",
                    Filters = { new FileDialogFilter { Name = "JSON Schema Files", Extensions = { "json" } } },
                    DefaultExtension = "json"
                };

                string filePath = await dialog.ShowAsync(_parentWindow);
                if (!string.IsNullOrEmpty(filePath))
                {
                    string content = File.ReadAllText(JsonPath);
                    var parsed = _parser.Parse(content);
                    var schemaObj = _generator.Generate(parsed);

                    var options = new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                    };

                    string serialized = JsonSerializer.Serialize(schemaObj, options);
                    File.WriteAllText(filePath, serialized);

                    SchemaPath = filePath;
                    ValidationResult = "JSON Schema saved to: " + filePath;
                    SchemaContent = serialized;
                }
            }
        }

        private async Task SelectValidationJson()
        {
            ValidationJsonPath = await SelectFile("Select JSON file for validation");
            ComparisonResult = !string.IsNullOrEmpty(ValidationJsonPath) ? $"Validation JSON file: {ValidationJsonPath}" : "";
            if (!string.IsNullOrEmpty(ValidationJsonPath))
                JsonContent = File.ReadAllText(ValidationJsonPath);
        }

        private async Task SelectValidationSchema()
        {
            ValidationSchemaPath = await SelectFile("Select JSON Schema file");
            ComparisonResult = !string.IsNullOrEmpty(ValidationSchemaPath) ? $"JSON Schema file: {ValidationSchemaPath}" : "";
            if (!string.IsNullOrEmpty(ValidationSchemaPath))
                SchemaContent = File.ReadAllText(ValidationSchemaPath);
        }

        private async Task<string> SelectFile(string title)
        {
            var dialog = new OpenFileDialog
            {
                Title = title,
                Filters = { new FileDialogFilter { Name = "JSON Files", Extensions = { "json" } } }
            };
            var result = await dialog.ShowAsync(_parentWindow);
            return result?.FirstOrDefault();
        }

        private async Task Compare()
        {
            if (string.IsNullOrEmpty(ValidationJsonPath) || string.IsNullOrEmpty(ValidationSchemaPath))
            {
                ComparisonResult = "Error: Select both files before comparing.";
                return;
            }

            string jsonContent = File.ReadAllText(ValidationJsonPath);
            string schemaContent = File.ReadAllText(ValidationSchemaPath);

            object parsedJson = _parser.Parse(jsonContent);
            var schema = JsonSerializer.Deserialize<CustomJsonSchema>(schemaContent);

            var (isValid, errors) = _validator.Validate(parsedJson, schema);
            ComparisonResult = isValid ? "Files match." : $"Files DO NOT match!\nErrors:\n{string.Join("\n", errors)}";

            JsonContent = jsonContent;
            SchemaContent = schemaContent;

            var schemaLines = schemaContent.Split('\n');
            var jsonLines = jsonContent.Split('\n');

            var jsonErrorLines = new List<int>();
            var schemaErrorLines = new List<int>();

            var keywords = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

            foreach (var error in errors)
            {
                var pathMatch = System.Text.RegularExpressions.Regex.Match(error, @"(?:Path|at) '([^']+)'|at (\w+)\[?\d*\]?:");
                if (pathMatch.Success)
                {
                    string path = pathMatch.Groups[1].Success ? pathMatch.Groups[1].Value : pathMatch.Groups[2].Value;
                    var parts = path.Split('.');
                    foreach (var part in parts)
                    {
                        var clean = System.Text.RegularExpressions.Regex.Replace(part, "\\[\\d+\\]", "");
                        if (!string.IsNullOrWhiteSpace(clean))
                            keywords.Add(clean);
                    }
                }
            }

            // Szukaj błędnych linii w JSON
            foreach (var key in keywords)
            {
                for (int i = 0; i < jsonLines.Length; i++)
                {
                    if (jsonLines[i].IndexOf("\"" + key + "\"", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        if (!jsonErrorLines.Contains(i + 1))
                            jsonErrorLines.Add(i + 1);
                    }
                }
            }

            // Szukaj błędnych linii w Schema
            foreach (var key in keywords)
            {
                for (int i = 0; i < schemaLines.Length; i++)
                {
                    if (schemaLines[i].IndexOf("\"" + key + "\"", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        if (!schemaErrorLines.Contains(i + 1))
                            schemaErrorLines.Add(i + 1);

                        // Rozszerz na cały blok {...} jeśli potrzeba
                        if (schemaLines[i].Contains("{"))
                        {
                            int braceBalance = schemaLines[i].Count(c => c == '{') - schemaLines[i].Count(c => c == '}');
                            int j = i + 1;
                            while (j < schemaLines.Length && braceBalance > 0)
                            {
                                braceBalance += schemaLines[j].Count(c => c == '{');
                                braceBalance -= schemaLines[j].Count(c => c == '}');

                                if (!schemaErrorLines.Contains(j + 1))
                                    schemaErrorLines.Add(j + 1);

                                j++;
                            }
                        }
                    }
                }
            }

            JsonErrorLines = jsonErrorLines;
            SchemaErrorLines = schemaErrorLines;

            JsonBackground = isValid ? Brushes.Green : Brushes.Red;
            SchemaBackground = isValid ? Brushes.Green : Brushes.Red;
        }

        private string _schemaContent;
        public string SchemaContent
        {
            get => _schemaContent;
            set => this.RaiseAndSetIfChanged(ref _schemaContent, value);
        }

        private string _jsonContent;
        public string JsonContent
        {
            get => _jsonContent;
            set => this.RaiseAndSetIfChanged(ref _jsonContent, value);
        }
    }
}
