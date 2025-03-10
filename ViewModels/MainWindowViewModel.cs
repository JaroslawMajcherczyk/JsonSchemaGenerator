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

        public string JsonPath { get; set; }
        public string SchemaPath { get; set; }

        public ReactiveCommand<Unit, Unit> SelectJsonCommand { get; }
        public ReactiveCommand<Unit, Unit> SaveSchemaCommand { get; }
        public ReactiveCommand<Unit, Unit> ValidateJsonCommand { get; }

        public MainWindowViewModel(Window parentWindow)
        {
            _parentWindow = parentWindow ?? throw new ArgumentNullException(nameof(parentWindow));

            SelectJsonCommand = ReactiveCommand.CreateFromTask(SelectJson);
            SaveSchemaCommand = ReactiveCommand.CreateFromTask(SaveSchema, this.WhenAnyValue(x => x.JsonPath, jsonPath => !string.IsNullOrEmpty(jsonPath)));
            ValidateJsonCommand = ReactiveCommand.CreateFromTask(ValidateJson, this.WhenAnyValue(x => x.SchemaPath, path => !string.IsNullOrEmpty(path)));
        }

        private async Task SelectJson()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Title = "Wybierz plik JSON",
                Filters = new() { new FileDialogFilter { Name = "Pliki JSON", Extensions = { "json" } } }
            };

            var result = await openFileDialog.ShowAsync(_parentWindow);
            if (result?.FirstOrDefault() is string selectedPath)
            {
                JsonPath = selectedPath;  // Aktualizacja wartości, aby wywołać zmianę stanu
                this.RaisePropertyChanged(nameof(JsonPath)); // Wymuszenie powiadomienia o zmianie
                ValidationResult = "Plik JSON wybrany: " + JsonPath;
            }
        }


        private async Task SaveSchema()
        {
            if (!string.IsNullOrEmpty(JsonPath))
            {
                SaveFileDialog saveFileDialog = new SaveFileDialog
                {
                    Title = "Zapisz JSON Schema",
                    Filters = new() { new FileDialogFilter { Name = "Pliki JSON Schema", Extensions = { "json" } } },
                    DefaultExtension = "json"
                };

                string filePath = await saveFileDialog.ShowAsync(_parentWindow);
                if (!string.IsNullOrEmpty(filePath))
                {
                    string schema = GenerateJsonSchema(JsonPath);

                    if (!string.IsNullOrEmpty(schema) && schema != "{}") // Unikamy pustego schematu
                    {
                        File.WriteAllText(filePath, schema);
                        SchemaPath = filePath;
                        ValidationResult = "Schemat JSON zapisany do: " + filePath;
                    }
                    else
                    {
                        ValidationResult = "Błąd: Nie udało się wygenerować schematu JSON.";
                    }
                }
            }
        }




        private async Task ValidateJson()
        {
            var (jsonFile, schemaFile) = await SelectFilesForValidation();
            if (!string.IsNullOrEmpty(jsonFile) && !string.IsNullOrEmpty(schemaFile))
            {
                bool isValid = ValidateJsonFile(jsonFile, schemaFile);
                ValidationResult = isValid ? "Plik JSON jest zgodny ze schematem." : "Plik JSON NIE jest zgodny!";
            }
        }

        private async Task<(string jsonFile, string schemaFile)> SelectFilesForValidation()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Title = "Wybierz plik JSON do walidacji",
                Filters = new() { new FileDialogFilter { Name = "Pliki JSON", Extensions = { "json" } } }
            };
            var jsonFile = (await openFileDialog.ShowAsync(_parentWindow))?.FirstOrDefault();

            OpenFileDialog schemaDialog = new OpenFileDialog
            {
                Title = "Wybierz plik JSON Schema",
                Filters = new() { new FileDialogFilter { Name = "Pliki JSON Schema", Extensions = { "json" } } }
            };
            var schemaFile = (await schemaDialog.ShowAsync(_parentWindow))?.FirstOrDefault();

            return (jsonFile, schemaFile);
        }
   
        private bool ValidateJsonFile(string jsonPath, string schemaPath)
        {
            string jsonContent = File.ReadAllText(jsonPath);
            string schemaContent = File.ReadAllText(schemaPath);

            JObject jsonObject = JObject.Parse(jsonContent);
            JSchema schema = JSchema.Parse(schemaContent);

            return jsonObject.IsValid(schema);
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



    }
}
