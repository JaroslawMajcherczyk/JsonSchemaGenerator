namespace JsonSchemaGenerator.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Reactive;
    using System.Threading.Tasks;
    using Avalonia.Controls;
    using Avalonia.Media;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Newtonsoft.Json.Schema;
    using ReactiveUI;

    public class MainWindowViewModel : ViewModelBase
    {
        private Window _parentWindow;

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
            set
            {
                this.RaiseAndSetIfChanged(ref _validationJsonPath, value);
                this.RaisePropertyChanged(nameof(ValidationJsonPath)); // Aktualizujemy ReactiveUI
            }
        }

        private string _validationSchemaPath;

        public string ValidationSchemaPath
        {
            get => _validationSchemaPath;
            set
            {
                this.RaiseAndSetIfChanged(ref _validationSchemaPath, value);
                this.RaisePropertyChanged(nameof(ValidationSchemaPath)); // Aktualizujemy ReactiveUI
            }
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

        public string JsonPath { get; set; }

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
            SaveSchemaCommand = ReactiveCommand.CreateFromTask(SaveSchema, this.WhenAnyValue(x => x.JsonPath, jsonPath => !string.IsNullOrEmpty(jsonPath)));

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
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Title = "Select JSON file",
                Filters = new() { new FileDialogFilter { Name = "JSON Files", Extensions = { "json" } } }
            };

            var result = await openFileDialog.ShowAsync(_parentWindow);
            if (result?.FirstOrDefault() is string selectedPath)
            {
                JsonPath = selectedPath;
                this.RaisePropertyChanged(nameof(JsonPath));
                ValidationResult = "JSON file selected:  " + JsonPath;
                JsonContent = File.ReadAllText(JsonPath);
            }
        }

        private async Task SaveSchema()
        {
            if (!string.IsNullOrEmpty(JsonPath))
            {
                SaveFileDialog saveFileDialog = new SaveFileDialog
                {
                    Title = "Save JSON Schema",
                    Filters = new() { new FileDialogFilter { Name = "JSON Schema Files", Extensions = { "json" } } },
                    DefaultExtension = "json"
                };

                string filePath = await saveFileDialog.ShowAsync(_parentWindow);
                if (!string.IsNullOrEmpty(filePath))
                {
                    // Pobieramy katalog i nazwę pliku bez rozszerzenia
                    string directory = Path.GetDirectoryName(filePath);
                    string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);

                    // Tworzymy nową nazwę pliku z dopiskiem .Schema.json
                    string newFilePath = Path.Combine(directory, fileNameWithoutExtension + ".Schema.json");

                    string schema = GenerateJsonSchema(JsonPath);

                    if (!string.IsNullOrEmpty(schema) && schema != "{}")
                    {
                        File.WriteAllText(newFilePath, schema);
                        SchemaPath = newFilePath;
                        ValidationResult = "JSON Schema saved to: " + newFilePath;

                        // << DODAJ TO >>
                        SchemaContent = schema;
                    }
                    else
                    {
                        ValidationResult = "Error: Failed to generate JSON Schema.";
                    }
                }
            }
        }

        private string GenerateJsonSchema(string jsonPath)
        {
            string fileContent = File.ReadAllText(jsonPath);
            JObject jsonObject = JObject.Parse(fileContent);

            var schema = new JSchema { Type = JSchemaType.Object };

            foreach (var property in jsonObject.Properties())
            {
                schema.Properties.Add(property.Name, GenerateSchemaForToken(property.Value, property.Name));
            }

            return schema.ToString((SchemaVersion)Formatting.Indented);
        }

        private JSchema GenerateSchemaForToken(JToken token, string propertyName = "")
        {
            if (token.Type == JTokenType.Object)
            {
                var schema = new JSchema { Type = JSchemaType.Object };

                foreach (var childProperty in token.Children<JProperty>())
                {
                    schema.Properties.Add(childProperty.Name, GenerateSchemaForToken(childProperty.Value, childProperty.Name));
                }

                return schema;
            }
            else if (token.Type == JTokenType.Array)
            {
                var schema = new JSchema { Type = JSchemaType.Array };

                var uniqueTypes = token.Children().Select(GetJSchemaType).Distinct().ToList();

                if (uniqueTypes.Count == 1)
                {
                    schema.Items.Add(new JSchema { Type = uniqueTypes.First() });
                }
                else
                {
                    foreach (var type in uniqueTypes)
                    {
                        schema.AnyOf.Add(new JSchema { Type = type });
                    }
                }

                return schema;
            }
            else if (token.Type == JTokenType.String)
            {
                var schema = new JSchema { Type = JSchemaType.String };
                string detectedPattern = GetPatternForProperty(propertyName, token.ToString());
                if (!string.IsNullOrEmpty(detectedPattern))
                {
                    schema.Pattern = detectedPattern;
                }

                return schema;
            }
            else
            {
                return new JSchema { Type = GetJSchemaType(token) };
            }
        }

        private JSchemaType GetJSchemaType(JToken token)
        {
            return token.Type switch
            {
                JTokenType.String => JSchemaType.String,
                JTokenType.Integer => JSchemaType.Integer,
                JTokenType.Float => JSchemaType.Number,
                JTokenType.Boolean => JSchemaType.Boolean,
                JTokenType.Object => JSchemaType.Object,
                JTokenType.Array => JSchemaType.Array,
                _ => JSchemaType.String // Default to string
            };
        }

        private string GetPatternForProperty(string propertyName, string value)
        {
            Dictionary<string, string> knownPatterns = new()
            {
                { "code", "^[A-Z]{3}-\\d{3}$" },  // Matches "ABC-123" format
                { "phone", "^\\+\\d{1,3}-\\d{9,15}$" },  // Matches "+48-123456789"
                { "email", "^[\\w.-]+@[a-zA-Z_-]+?\\.[a-zA-Z]{2,6}$" },  // Matches emails
                { "postalCode", "^\\d{2}-\\d{3}$" },  // Matches "00-123" format
                { "date", "^\\d{4}-\\d{2}-\\d{2}$" }  // Matches "YYYY-MM-DD" format
            };

            if (knownPatterns.TryGetValue(propertyName.ToLower(), out string pattern))
            {
                return pattern;
            }

            // ** Attempt automatic pattern inference from the value **
            if (value.All(char.IsDigit) && value.Length == 4) return "^\\d{4}$"; // Matches "1234"
            if (value.All(char.IsDigit) && value.Length == 6) return "^\\d{6}$"; // Matches "123456"

            return string.Empty; // No pattern detected
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

        // Validation JSON with JSON Schema

        private async Task<string> SelectFile(string title)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Title = title,
                Filters = new() { new FileDialogFilter { Name = "JSON Files", Extensions = { "json" } } }
            };

            var result = await openFileDialog.ShowAsync(_parentWindow);
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

            JObject jsonObject = JObject.Parse(jsonContent);
            JSchema schema = JSchema.Parse(schemaContent);

            ApplyStrictValidation(schema);

            IList<string> errors;
            bool isValid = jsonObject.IsValid(schema, out errors);

            ComparisonResult = isValid
                ? "Files match."
                : $"Files DO NOT match!\nErrors:\n{string.Join("\n", errors)}";

            Debug.WriteLine(ComparisonResult);

            // 🔄 Aktualizacja widoków
            JsonContent = jsonContent;
            SchemaContent = schemaContent;

            var schemaLines = schemaContent.Split('\n');
            var jsonLines = jsonContent.Split('\n');

            List<int> jsonErrorLines = new();
            List<int> schemaErrorLines = new();

            // 🔹 Etap 1: Linie błędów z JSON
            foreach (var error in errors)
            {
                var jsonLineMatch = System.Text.RegularExpressions.Regex.Match(error, @"line (\d+)");
                if (jsonLineMatch.Success && int.TryParse(jsonLineMatch.Groups[1].Value, out int jsonLine))
                {
                    if (!jsonErrorLines.Contains(jsonLine))
                        jsonErrorLines.Add(jsonLine);
                }

                var schemaPosMatch = System.Text.RegularExpressions.Regex.Match(error, @"position (\d+)");
                if (schemaPosMatch.Success && int.TryParse(schemaPosMatch.Groups[1].Value, out int pos))
                {
                    int charCount = 0;
                    for (int i = 0; i < schemaLines.Length; i++)
                    {
                        charCount += schemaLines[i].Length + 1;
                        if (charCount >= pos)
                        {
                            if (!schemaErrorLines.Contains(i + 1))
                                schemaErrorLines.Add(i + 1);
                            break;
                        }
                    }
                }
            }

            // 🔹 Etap 2: dynamiczne słowa kluczowe z Path '...'
            var keywords = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            foreach (var error in errors)
            {
                var pathMatch = System.Text.RegularExpressions.Regex.Match(error, @"Path '([^']+)'");
                if (pathMatch.Success)
                {
                    var rawParts = pathMatch.Groups[1].Value.Split('.');

                    foreach (var raw in rawParts)
                    {
                        // Usuwanie indeksów z tablic: tags[1] -> tags
                        var clean = System.Text.RegularExpressions.Regex.Replace(raw, @"\[\d+\]", "");
                        if (!string.IsNullOrWhiteSpace(clean))
                            keywords.Add(clean);
                    }
                }
            }

            // 🔹 Etap 3: znajdź linie schematu zawierające słowo kluczowe + cały blok klamrowy
            if (keywords.Any())
            {
                for (int i = 0; i < schemaLines.Length; i++)
                {
                    foreach (var key in keywords)
                    {
                        if (schemaLines[i].IndexOf($"\"{key}\"", StringComparison.InvariantCultureIgnoreCase) >= 0)
                        {
                            if (!schemaErrorLines.Contains(i + 1))
                                schemaErrorLines.Add(i + 1);

                            // 🔁 Szukaj bloku { ... } zbalansowanego
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

                            break; // nie przeszukuj dalej tej samej linii
                        }
                    }
                }
            }

            // 🔚 Przekazanie linii błędów do widoku
            JsonErrorLines = jsonErrorLines;
            SchemaErrorLines = schemaErrorLines;
        }


        private void ApplyStrictValidation(JSchema schema)
        {
            schema.AllowAdditionalProperties = false;

            foreach (var property in schema.Properties.Values)
            {
                if (property.Type == JSchemaType.Object)
                {
                    ApplyStrictValidation(property); // Rekurencyjne sprawdzenie zagnieżdżonych obiektów
                }
            }
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