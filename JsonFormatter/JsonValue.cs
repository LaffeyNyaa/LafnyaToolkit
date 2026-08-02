using System.Collections.Generic;

namespace JsonFormatter
{
    /// <summary>
    /// Enumeration of JSON value types recognized by the parser and emitted by
    /// the serializer.
    /// </summary>
    public enum JsonType
    {
        /// <summary>An object: an ordered set of string keys mapped to values.</summary>
        Object,

        /// <summary>An array: an ordered list of values.</summary>
        Array,

        /// <summary>A string literal.</summary>
        String,

        /// <summary>A number literal.</summary>
        Number,

        /// <summary>The boolean value true.</summary>
        True,

        /// <summary>The boolean value false.</summary>
        False,

        /// <summary>The null value.</summary>
        Null
    }

    /// <summary>
    /// Abstract syntax tree representation of a JSON value. Objects preserve
    /// the insertion order of properties and duplicate keys. Scalars preserve
    /// raw literals (no escape interpretation, no numeric conversion).
    /// </summary>
    public sealed class JsonValue
    {
        /// <summary>Gets the type of the value.</summary>
        public JsonType Kind { get; private set; }

        /// <summary>
        /// Gets the raw literal of a scalar value. Used only for
        /// String/Number/True/False/Null types; null for object and array types.
        /// </summary>
        public string RawText { get; private set; }

        /// <summary>
        /// Gets the property list of an object value, preserving insertion
        /// order and duplicate keys. Null for non-object types.
        /// </summary>
        public List<KeyValuePair<string, JsonValue>> Properties { get;
            private set; }

        /// <summary>
        /// Gets the element list of an array value. Null for non-array types.
        /// </summary>
        public List<JsonValue> Elements { get; private set; }

        private JsonValue(
            JsonType kind,
            string rawText,
            List<KeyValuePair<string, JsonValue>> properties,
            List<JsonValue> elements
        )
        {
            Kind = kind;
            RawText = rawText;
            Properties = properties;
            Elements = elements;
        }

        /// <summary>
        /// Creates an empty JSON object value.
        /// </summary>
        /// <returns>A JSON object value with no properties.</returns>
        public static JsonValue FromObject()
        {
            return new JsonValue(
                JsonType.Object,
                null,
                new List<KeyValuePair<string, JsonValue>>(),
                null
            );
        }

        /// <summary>
        /// Creates an empty JSON array value.
        /// </summary>
        /// <returns>A JSON array value with no elements.</returns>
        public static JsonValue FromArray()
        {
            return new JsonValue(
                JsonType.Array,
                null,
                null,
                new List<JsonValue>()
            );
        }

        /// <summary>
        /// Creates a scalar JSON value.
        /// </summary>
        /// <param name="kind">The scalar type; must be one of String/Number/True/False/Null.</param>
        /// <param name="rawText">The raw literal.</param>
        /// <returns>A scalar JSON value.</returns>
        public static JsonValue FromScalar(JsonType kind, string rawText)
        {
            return new JsonValue(
                kind,
                rawText,
                null,
                null
            );
        }
    }
}
