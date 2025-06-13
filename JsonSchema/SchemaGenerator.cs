using System.Collections.Generic;
using System.Linq;

namespace JsonSchemaGenerator.JsonSchema
{
    public class SchemaGenerator
    {
        public CustomJsonSchema Generate(object parsed)
        {
            var schema = new CustomJsonSchema();

            if (parsed is Dictionary<string, object> dict)
            {
                foreach (var kv in dict)
                {
                    schema.Properties[kv.Key] = Analyze(kv.Value);
                }
            }

            return schema;
        }

        private CustomJsonSchemaProperty Analyze(object value)
        {
            if (value is Dictionary<string, object> obj)
            {
                return new CustomJsonSchemaProperty
                {
                    Type = "object",
                    Properties = obj.ToDictionary(kv => kv.Key, kv => Analyze(kv.Value))
                };
            }

            if (value is List<object> list)
            {
                return new CustomJsonSchemaProperty
                {
                    Type = "array",
                    Items = list.FirstOrDefault() != null ? Analyze(list.First()) : new CustomJsonSchemaProperty { Type = "string" }
                };
            }

            return new CustomJsonSchemaProperty
            {
                Type = GetPrimitiveType(value)
            };
        }

        private string GetPrimitiveType(object value)
        {
            if (value is null) return "null";
            if (value is string str)
            {
                if (decimal.TryParse(str.Replace(",", "."), out _))
                {
                    return str.Contains('.') || str.Contains(',') || str.Contains('-') || str.Contains('/') || str.Contains('_')
                        ? "number"
                        : "integer";
                }

                return "string";
            }

            return value switch
            {
                int or long => "integer",
                float or double or decimal => "number",
                bool => "boolean",
                _ => "string"
            };
        }
    }

}
