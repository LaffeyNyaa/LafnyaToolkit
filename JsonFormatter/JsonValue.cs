using System.Collections.Generic;

namespace JsonFormatter
{
    /// <summary>
    /// Enumeration of JSON value types.
    /// </summary>
    public enum JsonType
    {
        /// <summary>
        /// An object type.
        /// </summary>
        Object,

        /// <summary>
        /// An array type.
        /// </summary>
        Array,

        /// <summary>
        /// A string type.
        /// </summary>
        String,

        /// <summary>
        /// A number type.
        /// </summary>
        Number,

        /// <summary>
        /// The boolean value true.
        /// </summary>
        True,

        /// <summary>
        /// The boolean value false.
        /// </summary>
        False,

        /// <summary>
        /// A null value.
        /// </summary>
        Null
    }

    /// <summary>
    /// Abstract syntax tree representation of a JSON value. Objects preserve the
    /// insertion order of properties and duplicate keys. Scalars preserve raw
    /// literals (does not interpret escapes or convert numbers).
    /// </summary>
    public class JsonValue
    {
        /// <summary>
        /// Gets the type of the value.
        /// </summary>
        public JsonType Kind { get; }

        /// <summary>
        /// Gets the raw literal of a scalar value. Used only for
        /// String/Number/True/False/Null types; null for object and array types.
        /// </summary>
        public string RawText { get; }

        /// <summary>
        /// Gets the property list of an object value, preserving insertion order
        /// and duplicate keys. Null for non-object types.
        /// </summary>
        public List<KeyValuePair<string, JsonValue>> Properties { get; }

        /// <summary>
        /// Gets the element list of an array value. Null for non-array types.
        /// </summary>
        public List<JsonValue> Elements { get; }

        private JsonValue(JsonType kind, string rawText,
            List<KeyValuePair<string, JsonValue>> properties,
            List<JsonValue> elements)
        {
            Kind = kind;
            RawText = rawText;
            Properties = properties;
            Elements = elements;
        }

        /// <summary>
        /// Creates an empty JSON object value.
        /// </summary>
        /// <returns>A JSON object value.</returns>
        public static JsonValue FromObject()
        {
            return new JsonValue(JsonType.Object, null,
                new List<KeyValuePair<string, JsonValue>>(), null);
        }

        /// <summary>
        /// Creates an empty JSON array value.
        /// </summary>
        /// <returns>A JSON array value.</returns>
        public static JsonValue FromArray()
        {
            return new JsonValue(JsonType.Array, null, null,
                new List<JsonValue>());
        }

        /// <summary>
        /// Creates a scalar JSON value.
        /// </summary>
        /// <param name="kind">
        /// The scalar type; must be one of String/Number/True/False/Null.
        /// </param>
        /// <param name="rawText">The raw literal.</param>
        /// <returns>A scalar JSON value.</returns>
        public static JsonValue FromScalar(JsonType kind, string rawText)
        {
            return new JsonValue(kind, rawText, null, null);
        }
    }
}
