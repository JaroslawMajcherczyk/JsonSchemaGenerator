using System.Collections.Generic;

namespace JsonSchemaGenerator.JsonSchema
{
    public class JsonValidator
    {
        public (bool IsValid, List<string> Errors) Validate(object json, CustomJsonSchema schema)
        {
            var errors = new List<string>();
            if (json is not Dictionary<string, object> dict)
            {
                errors.Add("Root is not an object.");
                return (false, errors);
            }

            foreach (var prop in schema.Properties)
            {
                if (!dict.TryGetValue(prop.Key, out var value))
                {
                    errors.Add($"Missing property: {prop.Key}");
                    continue;
                }

                ValidateProperty(value, prop.Value, prop.Key, errors);
            }

            return (errors.Count == 0, errors);
        }

        private void ValidateProperty(object value, CustomJsonSchemaProperty schema, string path, List<string> errors)
        {
            if (schema.Type == "object")
            {
                if (value is not Dictionary<string, object> dict)
                {
                    errors.Add($"Expected object at {path}, got {value?.GetType().Name ?? "null"}");
                    return;
                }

                foreach (var sub in schema.Properties)
                {
                    if (!dict.TryGetValue(sub.Key, out var subValue))
                    {
                        errors.Add($"Missing property: {path}.{sub.Key}");
                        continue;
                    }

                    ValidateProperty(subValue, sub.Value, $"{path}.{sub.Key}", errors);
                }
            }
            else if (schema.Type == "array")
            {
                if (value is not List<object> list)
                {
                    errors.Add($"Expected array at {path}, got {value?.GetType().Name ?? "null"}");
                    return;
                }

                for (int i = 0; i < list.Count; i++)
                {
                    ValidateProperty(list[i], schema.Items, $"{path}[{i}]", errors);
                }
            }
            else
            {
                var expected = schema.Type;
                var actual = GetPrimitiveType(value);
                if (expected != actual && !(expected == "number" && actual == "integer"))
                {
                    errors.Add($"Type mismatch at {path}: expected {expected}, got {actual}");
                }
            }
        }

        private string GetPrimitiveType(object value)
        {
            return value switch
            {
                null => "null",
                string => "string",
                bool => "boolean",
                int or long => "integer",
                float or double or decimal => "number",
                _ => "string"
            };
        }
    }

}
