using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace JsonSchemaGenerator.JsonSchema
{
    public class CustomJsonSchema
    {
        public string Schema => "http://json-schema.org/draft-07/schema#";
        public string Type => "object";
        public Dictionary<string, CustomJsonSchemaProperty> Properties { get; set; } = new();
    }

    public class CustomJsonSchemaProperty
    {
        public string Type { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public CustomJsonSchemaProperty Items { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, CustomJsonSchemaProperty> Properties { get; set; }
    }
}
