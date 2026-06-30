using System.Collections.Generic;

namespace GDScriptFormatter
{
    /// <summary>
    /// Encapsulates the intermediate state of the formatting pipeline, caching tokenization,
    /// code mask, line starts, and per-line analysis. All data is computed once at construction
    /// time (or lazily on first access for LineInfo) and reused by multiple processor stages.
    /// </summary>
    internal struct DocumentSnapshot
    {
        /// <summary>The original text.</summary>
        public string Text
        {
            get;
        }

        /// <summary>The split lines of the text.</summary>
        public List<string> Lines
        {
            get;
        }

        /// <summary>The tokenization result of the text.</summary>
        public List<Token> Tokens
        {
            get;
        }

        /// <summary>The code mask of the text (true = Code region).</summary>
        public bool[] IsCode
        {
            get;
        }

        /// <summary>The starting offset of each line in the text.</summary>
        public int[] LineStarts
        {
            get;
        }

        private IndentationProcessor.LineAnalysis[] _lineInfo;
        private bool _lineInfoComputed;

        /// <summary>
        /// Constructs a DocumentSnapshot from the given source text, computing
        /// all cached data immediately except <see cref="LineAnalysis"/>.
        /// </summary>
        /// <param name="text">The source text.</param>
        internal DocumentSnapshot(string text)
        {
            Text = text;
            Lines = TextUtils.SplitLines(text);
            Tokens = Tokenizer.Tokenize(text);
            IsCode = Tokenizer.BuildCodeMask(text, Tokens);
            LineStarts = IndentationProcessor.ComputeLineStarts(Lines);
            _lineInfo = null;
            _lineInfoComputed = false;
        }

        /// <summary>
        /// Returns the per-line analysis, computing it lazily on first access
        /// and caching the result for subsequent calls.
        /// </summary>
        internal IndentationProcessor.LineAnalysis[] GetLineInfo()
        {
            if (!_lineInfoComputed)
            {
                _lineInfo = IndentationProcessor.ComputeLineInfo(
                    Lines, Text, IsCode, LineStarts);

                _lineInfoComputed = true;
            }

            return _lineInfo;
        }
    }
}
