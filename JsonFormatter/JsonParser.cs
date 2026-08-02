using System;
using System.Collections.Generic;

namespace JsonFormatter
{
    /// <summary>
    /// A hand-written recursive descent JSON parser that does not reference
    /// any external JSON library. Preserves raw literals for strings and
    /// numbers (does not interpret escapes or convert numbers) and preserves
    /// the original order and duplicate keys of objects.
    /// </summary>
    public sealed class JsonParser
    {
        /// <summary>Shared stateless instance.</summary>
        public static readonly JsonParser Instance = new JsonParser();

        private const char Utf8Bom = '\uFEFF';

        private string _text;
        private int _index;
        private int _line;
        private int _column;

        private JsonParser()
        {
        }

        /// <summary>
        /// Parses JSON text into a <see cref="JsonValue"/> abstract syntax
        /// tree. The parser is reused across calls; the per-call state is
        /// re-initialized at the start of every invocation.
        /// </summary>
        /// <param name="text">The JSON text.</param>
        /// <returns>The parsed root value.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
        /// <exception cref="FormatException">Thrown when the text does not conform to JSON syntax.</exception>
        public JsonValue Parse(string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            Initialize(text);
            return ParseRoot();
        }

        private void Initialize(string text)
        {
            _text = text;
            _index = 0;
            _line = 1;
            _column = 1;

            if (_text.Length > 0 && _text[0] == Utf8Bom)
            {
                _index = 1;
            }
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

        private void SkipWhitespace()
        {
            while (_index < _text.Length)
            {
                char c = _text[_index];

                if (c == ' ' || c == '\t' || c == '\n' || c == '\r')
                {
                    ReadChar();
                    continue;
                }

                break;
            }
        }

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

            switch (_text[_index])
            {
                case '{': return ParseObject();
                case '[': return ParseArray();
                case '"': return ParseString();
                case 't': return ParseKeyword("true", JsonType.True);
                case 'f': return ParseKeyword("false", JsonType.False);
                case 'n': return ParseKeyword("null", JsonType.Null);
                default:

                    if (IsNumberStart(_text[_index]))
                    {
                        return ParseNumber();
                    }

                    throw Error("unexpected character '" + _text[_index] + "'");
            }
        }

        private static bool IsNumberStart(char c)
        {
            return c == '-' || (c >= '0' && c <= '9');
        }

        private JsonValue ParseObject()
        {
            ReadChar();
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

                ReadChar();
                SkipWhitespace();
                JsonValue value = ParseValue();

                obj.Properties.Add(new KeyValuePair<string, JsonValue>
                    (key.RawText, value));

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
                    return obj;
                }

                throw Error("expected ',' or '}'");
            }
        }

        private JsonValue ParseArray()
        {
            ReadChar();
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
                    return arr;
                }

                throw Error("expected ',' or ']'");
            }
        }

        private JsonValue ParseString()
        {
            int start = _index;
            ReadChar();

            while (_index < _text.Length)
            {
                char c = _text[_index];

                if (c == '\\')
                {
                    ReadChar();

                    if (_index >= _text.Length)
                    {
                        throw Error("unterminated string");
                    }

                    ReadChar();
                    continue;
                }

                if (c == '"')
                {
                    ReadChar();

                    return JsonValue.FromScalar(JsonType.String,
                        _text.Substring(start, _index - start));
                }

                ReadChar();
            }

            throw Error("unterminated string");
        }

        private JsonValue ParseNumber()
        {
            int start = _index;

            if (_text[_index] == '-')
            {
                ReadChar();

                if (_index >= _text.Length)
                {
                    throw Error("invalid number");
                }
            }

            char c = _text[_index];

            if (c == '0')
            {
                ReadChar();
            }
            else if (c >= '1' && c <= '9')
            {
                ReadChar();
                ReadDigits();
            }
            else
            {
                throw Error("invalid number");
            }

            if (_index < _text.Length && _text[_index] == '.')
            {
                ReadChar();
                RequireDigit();
                ReadDigits();
            }

            if (_index < _text.Length && (_text[_index] == 'e' ||
                _text[_index] == 'E'))
            {
                ReadChar();

                if (_index < _text.Length && (_text[_index] == '+' ||
                    _text[_index] == '-'))
                {
                    ReadChar();
                }

                RequireDigit();
                ReadDigits();
            }

            return JsonValue.FromScalar(JsonType.Number, _text.Substring(start,
                _index - start));
        }

        private void RequireDigit()
        {
            if (_index >= _text.Length || _text[_index] < '0' || _text[_index] >
                '9')
            {
                throw Error("invalid number");
            }
        }

        private void ReadDigits()
        {
            while (_index < _text.Length && _text[_index] >= '0' &&
                _text[_index] <= '9')
            {
                ReadChar();
            }
        }

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
