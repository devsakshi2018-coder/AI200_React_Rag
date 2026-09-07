using System;
using System.Collections.Generic;

namespace InferenceApplication.Services
{
    /// <summary>
    /// Helper for splitting text into chunks based on token heuristics.
    /// </summary>
    public class TextChunker
    {
        /// <summary>
        /// Splits the provided text into chunks using approximate token sizing.
        /// </summary>
        /// <param name="text">Input text.</param>
        /// <param name="maxTokens">Maximum tokens per chunk.</param>
        /// <param name="overlapTokens">Number of overlapping tokens between chunks.</param>
        public List<string> ChunkText(string text, int maxTokens, int overlapTokens)
        {
            if (string.IsNullOrWhiteSpace(text)) return new List<string> { string.Empty };

            var approxTokensPerChar = 1.0 / 4.0;
            var maxChars = (int)(maxTokens / approxTokensPerChar);
            var overlapChars = (int)(overlapTokens / approxTokensPerChar);

            var chunks = new List<string>();

            int pos = 0;
            while (pos < text.Length)
            {
                var length = Math.Min(maxChars, text.Length - pos);
                var segment = text.Substring(pos, length);

                var lastPeriod = segment.LastIndexOfAny(new[] { '.', '\n' });
                if (lastPeriod > 0 && length == maxChars)
                {
                    segment = segment.Substring(0, lastPeriod + 1);
                }

                chunks.Add(segment.Trim());

                pos += segment.Length - overlapChars;
                if (pos < 0) pos = 0;
            }

            return chunks;
        }
    }
}
