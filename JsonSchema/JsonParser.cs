using System.Linq;
using System.Text.Json;

namespace JsonSchemaGenerator.JsonSchema
{
    public class JsonParser
    {
        public object Parse(string json)
        {
            using var doc = JsonDocument.Parse(json);
            return ParseElement(doc.RootElement, json);
        }

        private object ParseElement(JsonElement element, string raw)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Object => element.EnumerateObject()
                    .ToDictionary(p => p.Name, p => ParseElement(p.Value, raw)),

                JsonValueKind.Array => element.EnumerateArray()
                    .Select(el => ParseElement(el, raw)).ToList(),

                JsonValueKind.String => element.GetString(),

                JsonValueKind.Number => GetExactNumberType(element, raw),

                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => null
            };
        }

        private object GetExactNumberType(JsonElement element, string raw)
        {
            string rawString = element.GetRawText();

            // Upewnij się że "1" nie zawiera przecinka ani kropki
            if (rawString.Contains('.') || rawString.Contains(',') || rawString.Contains('/') || rawString.Contains('_') ||
                rawString.Contains(';') || rawString.Contains(':') || rawString.Contains('-') || rawString.Contains('|'))
                return element.GetDouble();

            if (long.TryParse(rawString, out long result))
                return result;

            return element.GetDouble(); // fallback
        }
    }
}
