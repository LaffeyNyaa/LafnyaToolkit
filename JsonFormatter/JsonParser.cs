using System;
using System.Collections.Generic;

namespace JsonFormatter
{
    /// <summary>
    /// A hand-written recursive descent JSON parser that does not reference any
    /// external JSON library. Preserves raw literals for strings and numbers
    /// (does not interpret escapes or convert numbers), and preserves the original
    /// order and duplicate keys of objects.
    /// </summary>
    public class JsonParser
    {
        private readonly string _text;
        private int _index;
        private int _line;
        private int _column;
        private JsonParser(string text)
        {
            _text = text;
            _index = 0;
            _line = 1;
            _column = 1;
            // Skip a single UTF-8 BOM if present.

            if (_text.Length > 0 && _text[0] == '\uFEFF')
            {
                _index = 1;
            }
        }

        /// <summary>
        /// Parses JSON text into a JsonValue abstract syntax tree.
        /// </summary>
        /// <param name="text">The JSON text.</param>
        /// <returns>The parsed root JsonValue.</returns>
        /// <exception cref="FormatException">
        /// Thrown when the text does not conform to JSON syntax.
        /// </exception>
        public static JsonValue Parse(string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            var parser = new JsonParser(text);
            return parser.ParseRoot();
        }

        private JsonValue ParseRoot()
        {
            SkipWhitespace();
            JsonValue value = ParseValue();
            SkipWhitespace();

            if (_index != _text.Length)
            {
                throw Error("trailing characters");
            }

            return value;
        }

        /// <summary>
        /// Skips spaces, tabs, and newline characters, updating line and column
        /// numbers.
        /// </summary>
        private void SkipWhitespace()
        {
            while (_index < _text.Length)
            {
                char c = _text[_index];

                if (c == ' ' || c == '\t' || c == '\n' || c == '\r')
                {
                    ReadChar();
                }
                else
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Reads and consumes the current character, updating line and column
        /// numbers. A lone '\r' (old-Mac line ending) increments the line
        /// counter, and a '\r\n' sequence is treated as a single line break by
        /// consuming the '\n'.
        /// </summary>
        /// <returns>The character that was consumed.</returns>
        private char ReadChar()
        {
            if (_index >= _text.Length)
            {
                throw new InvalidOperationException("ReadChar past end of input.");
            }

            char c = _text[_index];
            _index++;

            if (c == '\r')
            {
                _line++;
                _column = 1;
                // Treat \r\n as a single line break by consuming the \n.

                if (_index < _text.Length && _text[_index] == '\n')
                {
                    _index++;
                }
            }
            else if (c == '\n')
            {
                _line++;
                _column = 1;
            }
            else
            {
                _column++;
            }

            return c;
        }

        private JsonValue ParseValue()
        {
            if (_index >= _text.Length)
            {
                throw Error("unexpected end of input");
            }

            char c = _text[_index];

            switch (c)
            {
                case '{':
                    return ParseObject();
                case '[':
                    return ParseArray();
                case '"':
                    return ParseString();
                case 't':
                    return ParseKeyword("true", JsonType.True);
                case 'f':
                    return ParseKeyword("false", JsonType.False);
                case 'n':
                    return ParseKeyword("null", JsonType.Null);
                case '-':
                case '0':
                case '1':
                case '2':
                case '3':
                case '4':
                case '5':
                case '6':
                case '7':
                case '8':
                case '9':
                    return ParseNumber();
                default:
                    throw Error("unexpected character '" + c + "'");
            }
        }

        private JsonValue ParseObject()
        {
            ReadChar(); // Consume '{'
            JsonValue obj = JsonValue.FromObject();
            SkipWhitespace();

            if (_index < _text.Length && _text[_index] == '}')
            {
                ReadChar();
                return obj;
            }

            while (true)
            {
                SkipWhitespace();

                if (_index >= _text.Length || _text[_index] != '"')
                {
                    throw Error("expected string key");
                }

                JsonValue key = ParseString();
                SkipWhitespace();

                if (_index >= _text.Length || _text[_index] != ':')
                {
                    throw Error("expected ':'");
                }

                ReadChar(); // Consume ':'
                SkipWhitespace();
                JsonValue value = ParseValue();

                obj.Properties.Add(
                    new KeyValuePair<string, JsonValue>(key.RawText, value));

                SkipWhitespace();

                if (_index >= _text.Length)
                {
                    throw Error("unexpected end of input");
                }

                char c = _text[_index];

                if (c == ',')
                {
                    ReadChar();
                    continue;
                }

                if (c == '}')
                {
                    ReadChar();
                    break;
                }

                throw Error("expected ',' or '}'");
            }

            return obj;
        }

        private JsonValue ParseArray()
        {
            ReadChar(); // Consume '['
            JsonValue arr = JsonValue.FromArray();
            SkipWhitespace();

            if (_index < _text.Length && _text[_index] == ']')
            {
                ReadChar();
                return arr;
            }

            while (true)
            {
                SkipWhitespace();
                JsonValue value = ParseValue();
                arr.Elements.Add(value);
                SkipWhitespace();

                if (_index >= _text.Length)
                {
                    throw Error("unexpected end of input");
                }

                char c = _text[_index];

                if (c == ',')
                {
                    ReadChar();
                    continue;
                }

                if (c == ']')
                {
                    ReadChar();
                    break;
                }

                throw Error("expected ',' or ']'");
            }

            return arr;
        }

        /// <summary>
        /// Parses a string literal, preserving the surrounding quotes and raw escape
        /// sequences without interpreting escapes.
        /// </summary>
        private JsonValue ParseString()
        {
            int start = _index;
            ReadChar(); // Consume opening '"'

            while (_index < _text.Length)
            {
                char c = _text[_index];

                if (c == '\\')
                {
                    ReadChar(); // Consume '\'

                    if (_index >= _text.Length)
                    {
                        throw Error("unterminated string");
                    }

                    ReadChar(); // Consume the character following the escape
                    continue;
                }

                if (c == '"')
                {
                    ReadChar(); // Consume closing '"'

                    return JsonValue.FromScalar(JsonType.String,
                        _text.Substring(start, _index - start));
                }

                ReadChar();
            }

            throw Error("unterminated string");
        }

        /// <summary>
        /// Parses a number literal conforming to RFC 8259 number syntax, preserving
        /// the raw literal without converting to a numeric value.
        /// </summary>
        private JsonValue ParseNumber()
        {
            int start = _index;
            // Optional '-'

            if (_index < _text.Length && _text[_index] == '-')
            {
                ReadChar();
            }

            if (_index >= _text.Length)
            {
                throw Error("invalid number");
            }

            // Integer part
            char c = _text[_index];

            if (c == '0')
            {
                ReadChar();
            }
            else if (c >= '1' && c <= '9')
            {
                ReadChar();

                while (_index < _text.Length &&
                    _text[_index] >= '0' && _text[_index] <= '9')
                {
                    ReadChar();
                }
            }
            else
            {
                throw Error("invalid number");
            }

            // Optional fractional part

            if (_index < _text.Length && _text[_index] == '.')
            {
                ReadChar();

                if (_index >= _text.Length ||
                    _text[_index] < '0' || _text[_index] > '9')
                {
                    throw Error("invalid number");
                }

                while (_index < _text.Length &&
                    _text[_index] >= '0' && _text[_index] <= '9')
                {
                    ReadChar();
                }
            }

            // Optional exponent part

            if (_index < _text.Length &&
                (_text[_index] == 'e' || _text[_index] == 'E'))
            {
                ReadChar();

                if (_index < _text.Length &&
                    (_text[_index] == '+' || _text[_index] == '-'))
                {
                    ReadChar();
                }

                if (_index >= _text.Length ||
                    _text[_index] < '0' || _text[_index] > '9')
                {
                    throw Error("invalid number");
                }

                while (_index < _text.Length &&
                    _text[_index] >= '0' && _text[_index] <= '9')
                {
                    ReadChar();
                }
            }

            return JsonValue.FromScalar(JsonType.Number,
                _text.Substring(start, _index - start));
        }

        /// <summary>
        /// Matches a keyword literal character by character.
        /// </summary>
        /// <param name="expected">The expected keyword string.</param>
        /// <param name="kind">The JsonType corresponding to the keyword.</param>
        private JsonValue ParseKeyword(string expected, JsonType kind)
        {
            for (int i = 0; i < expected.Length; i++)
            {
                if (_index >= _text.Length || _text[_index] != expected[i])
                {
                    throw Error("invalid keyword '" + expected + "'");
                }

                ReadChar();
            }

            return JsonValue.FromScalar(kind, expected);
        }

        private FormatException Error(string message)
        {
            return new FormatException(
                $"JSON parse error at line {_line}, column {_column}: {message}");
        }
    }
}
