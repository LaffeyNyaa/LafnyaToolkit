using System;
using System.Collections.Generic;
using System.Text;

namespace JsonFormatter
{
    /// <summary>
    /// JSON serializer that converts a <see cref="JsonValue"/> abstract
    /// syntax tree into formatted text. Indentation uses 2 spaces, newlines
    /// use '\n', no trailing whitespace, and a single trailing newline is
    /// appended at end of file.
    /// </summary>
    public sealed class JsonFormatter
    {
        /// <summary>Number of spaces per indentation level.</summary>
        public const int IndentSize = 2;

        /// <summary>Shared stateless instance.</summary>
        public static readonly JsonFormatter Instance = new JsonFormatter();

        private static readonly string IndentUnit = new string(' ', IndentSize);

        private JsonFormatter()
        {
        }

        /// <summary>
        /// Parses and formats JSON text.
        /// </summary>
        /// <param name="text">The raw JSON text.</param>
        /// <returns>The formatted JSON text with a single trailing newline.</returns>
        public string Format(string text)
        {
            JsonValue root = JsonParser.Instance.Parse(text);
            var sb = new StringBuilder(text.Length + 16);

            SerializeValue(
                root,
                0,
                sb
            );
            sb.Append('\n');
            return sb.ToString();
        }

        private void SerializeValue(
            JsonValue value,
            int indent,
            StringBuilder sb
        )
        {
            switch (value.Kind)
            {
                case JsonType.Object:

                    SerializeObject(
                        value,
                        indent,
                        sb
                    );
                    break;
                case JsonType.Array:

                    SerializeArray(
                        value,
                        indent,
                        sb
                    );
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

                    throw new InvalidOperationException("Unknown JSON type: " +
                        value.Kind);
            }
        }

        private void SerializeObject(
            JsonValue value,
            int indent,
            StringBuilder sb
        )
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

                SerializeValue(
                    pair.Value,
                    indent + 1,
                    sb
                );

                if (i < value.Properties.Count - 1)
                {
                    sb.Append(',');
                }

                sb.Append('\n');
            }

            AppendIndent(sb, indent);
            sb.Append('}');
        }

        private void SerializeArray(
            JsonValue value,
            int indent,
            StringBuilder sb
        )
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

                SerializeValue(
                    value.Elements[i],
                    indent + 1,
                    sb
                );

                if (i < value.Elements.Count - 1)
                {
                    sb.Append(',');
                }

                sb.Append('\n');
            }

            AppendIndent(sb, indent);
            sb.Append(']');
        }

        private static void AppendIndent(StringBuilder sb, int indent)
        {
            for (int i = 0; i < indent; i++)
            {
                sb.Append(IndentUnit);
            }
        }
    }
}
