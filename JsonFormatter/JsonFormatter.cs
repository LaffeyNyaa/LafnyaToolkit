using System;
using System.Collections.Generic;
using System.Text;

namespace JsonFormatter
{
    /// <summary>
    /// JSON serializer that outputs a JsonValue abstract syntax tree according to
    /// formatting rules. Indentation uses 2 spaces, newlines use \n, no trailing
    /// whitespace, and a single trailing newline is appended at end of file.
    /// </summary>
    public static class JsonFormatter
    {
        /// <summary>
        /// Parses and formats JSON text.
        /// </summary>
        /// <param name="text">The raw JSON text.</param>
        /// <returns>The formatted JSON text with a single trailing newline.</returns>
        public static string Format(string text)
        {
            JsonValue root = JsonParser.Parse(text);
            var sb = new StringBuilder();
            SerializeValue(root, 0, sb);
            sb.Append('\n');
            return sb.ToString();
        }

        /// <summary>
        /// Dispatches to the appropriate serialization logic based on the
        /// <see cref="JsonValue.Kind"/> of the value.
        /// </summary>
        /// <param name="value">The value to serialize.</param>
        /// <param name="indent">The current indentation level (2 spaces per level).</param>
        /// <param name="sb">The output buffer.</param>
        private static void SerializeValue(JsonValue value, int indent,
            StringBuilder sb)
        {
            switch (value.Kind)
            {
                case JsonType.Object:
                    SerializeObject(value, indent, sb);
                    break;
                case JsonType.Array:
                    SerializeArray(value, indent, sb);
                    break;
                case JsonType.String:
                case JsonType.Number:
                    sb.Append(value.RawText);
                    break;
                case JsonType.True:
                    sb.Append("true");
                    break;
                case JsonType.False:
                    sb.Append("false");
                    break;
                case JsonType.Null:
                    sb.Append("null");
                    break;
                default:
                    throw new InvalidOperationException(
                        "Unknown JSON type: " + value.Kind);
            }
        }

        private static void SerializeObject(JsonValue value, int indent,
            StringBuilder sb)
        {
            if (value.Properties.Count == 0)
            {
                sb.Append("{}");
                return;
            }

            sb.Append("{\n");
            for (int i = 0; i < value.Properties.Count; i++)
            {
                KeyValuePair<string, JsonValue> pair = value.Properties[i];
                AppendIndent(sb, indent + 1);
                sb.Append(pair.Key);
                sb.Append(": ");
                SerializeValue(pair.Value, indent + 1, sb);
                if (i < value.Properties.Count - 1)
                {
                    sb.Append(',');
                }
                sb.Append('\n');
            }
            AppendIndent(sb, indent);
            sb.Append('}');
        }

        private static void SerializeArray(JsonValue value, int indent,
            StringBuilder sb)
        {
            if (value.Elements.Count == 0)
            {
                sb.Append("[]");
                return;
            }

            sb.Append("[\n");
            for (int i = 0; i < value.Elements.Count; i++)
            {
                AppendIndent(sb, indent + 1);
                SerializeValue(value.Elements[i], indent + 1, sb);
                if (i < value.Elements.Count - 1)
                {
                    sb.Append(',');
                }
                sb.Append('\n');
            }
            AppendIndent(sb, indent);
            sb.Append(']');
        }

        /// <summary>
        /// Appends the specified number of indentation levels (2 spaces per level).
        /// </summary>
        /// <param name="sb">The output buffer.</param>
        /// <param name="indent">The indentation level.</param>
        private static void AppendIndent(StringBuilder sb, int indent)
        {
            for (int i = 0; i < indent; i++)
            {
                sb.Append("  ");
            }
        }
    }
}
