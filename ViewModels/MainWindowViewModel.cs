using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;
using Newtonsoft.Json.Schema.Generation;
using ReactiveUI;
using System.Reactive;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace JsonSchemaGenerator.ViewModels
{
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
                JsonPath = selectedPath;  // Aktualizacja wartości, aby wywołać zmianę stanu
                this.RaisePropertyChanged(nameof(JsonPath)); // Wymuszenie powiadomienia o zmianie
                ValidationResult = "JSON file selected:  " + JsonPath;
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

                    if (!string.IsNullOrEmpty(schema) && schema != "{}") // Unikamy pustego schematu
                    {
                        File.WriteAllText(newFilePath, schema);
                        SchemaPath = newFilePath;
                        ValidationResult = "JSON Schema saved to: " + newFilePath;
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
                schema.Properties.Add(property.Name, GenerateSchemaForToken(property.Value));
            }

            return schema.ToString((SchemaVersion)Formatting.Indented);
        }

        private JSchema GenerateSchemaForToken(JToken token)
        {
            if (token.Type == JTokenType.Object)
            {
                var schema = new JSchema { Type = JSchemaType.Object };

                foreach (var childProperty in token.Children<JProperty>())
                {
                    schema.Properties.Add(childProperty.Name, GenerateSchemaForToken(childProperty.Value));
                }

                return schema;
            }
            else if (token.Type == JTokenType.Array)
            {
                var schema = new JSchema { Type = JSchemaType.Array };

                // Pobieramy unikalne typy elementów tablicy
                var uniqueTypes = token.Children().Select(GetJSchemaType).Distinct().ToList();

                if (uniqueTypes.Count == 1)
                {
                    // Jeśli wszystkie elementy mają ten sam typ, ustawiamy go w "items"
                    schema.Items.Add(new JSchema { Type = uniqueTypes.First() });
                }
                else
                {
                    // Jeśli mamy mieszane typy, dodajemy wszystkie unikalne typy do AnyOf
                    foreach (var type in uniqueTypes)
                    {
                        schema.AnyOf.Add(new JSchema { Type = type });
                    }
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
                _ => JSchemaType.String // Domyślnie traktujemy jako string
            };
        }

        // Validation JSON with JSON Schema

        private async Task SelectValidationJson()
        {
            ValidationJsonPath = await SelectFile("Select JSON file for validation");
            ComparisonResult = !string.IsNullOrEmpty(ValidationJsonPath) ? $"Validation JSON file: {ValidationJsonPath}" : "";
        }

        private async Task SelectValidationSchema()
        {
            ValidationSchemaPath = await SelectFile("Select JSON Schema file");
            ComparisonResult = !string.IsNullOrEmpty(ValidationSchemaPath) ? $"JSON Schema file: {ValidationSchemaPath}" : "";
        }
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

            // ** Rekurencyjne ustawienie ścisłego dopasowania (blokowanie dodatkowych właściwości) **
            ApplyStrictValidation(schema);

            IList<string> errors;
            bool isValid = jsonObject.IsValid(schema, out errors);

            ComparisonResult = isValid ? "Files match." : $"Files DO NOT match!\nErrors:\n{string.Join("\n", errors)}";
        }

        /// <summary>
        /// **Ustawia `AllowAdditionalProperties = false` dla każdego obiektu w schemacie**
        /// </summary>
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


    }
}
