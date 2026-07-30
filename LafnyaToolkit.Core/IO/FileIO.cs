using System;
using System.IO;
using System.Text;

namespace LafnyaToolkit.Core.IO
{
    /// <summary>
    /// File I/O utilities shared by every formatter: encoding detection and
    /// atomic file writes.
    /// </summary>
    public static class FileIO
    {
        /// <summary>UTF-8 encoding without BOM, used for all formatted output.</summary>
        public static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        /// <summary>
        /// Detects the encoding of a byte array by inspecting the BOM.
        /// Falls back to UTF-8 (with a zero-length BOM) if no BOM is present
        /// or if UTF-8 decoding succeeds.
        /// </summary>
        /// <param name="bytes">The raw byte array to inspect.</param>
        /// <returns>A tuple of the detected encoding and the length of the BOM (0 if no BOM).</returns>
        public static (Encoding encoding,
            int bomLength) DetectEncoding(byte[] bytes)
        {
            if (bytes.Length >= 3
            && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                return (Encoding.UTF8, 3);
            }

            if (bytes.Length >= 2
            && bytes[0] == 0xFF && bytes[1] == 0xFE)
            {
                if (bytes.Length >= 4
                && bytes[2] == 0x00 && bytes[3] == 0x00)
                {
                    return (Encoding.UTF32, 4);
                }

                return (Encoding.Unicode, 2);
            }

            if (bytes.Length >= 2
            && bytes[0] == 0xFE && bytes[1] == 0xFF)
            {
                return (new UnicodeEncoding(true, true), 2);
            }

            if (bytes.Length >= 4
            && bytes[0] == 0x00 && bytes[1] == 0x00
            && bytes[2] == 0xFE && bytes[3] == 0xFF)
            {
                return (new UTF32Encoding(true, true), 4);
            }

            return (Encoding.UTF8, 0);
        }

        /// <summary>
        /// Reads the file at <paramref name="path"/> and returns its content
        /// as a string. The encoding is auto-detected from any BOM; absent a
        /// BOM, the content is interpreted as UTF-8.
        /// </summary>
        /// <param name="path">The file path.</param>
        /// <returns>The decoded file content.</returns>
        public static string ReadAllTextAutoDetect(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            var (encoding, bomLength) = DetectEncoding(bytes);

            if (bomLength == bytes.Length)
            {
                return string.Empty;
            }

            return encoding.GetString(bytes, bomLength, bytes.Length -
                bomLength);
        }

        /// <summary>
        /// Writes content to the final path atomically by first writing to a
        /// temporary file in the same directory and then replacing the
        /// destination file via <see cref="File.Replace(string,string,string)"/>.
        /// If <c>File.Replace</c> fails (e.g. on a different volume), falls
        /// back to Delete + Move. Residual temporary files are cleaned up in
        /// a finally block.
        /// </summary>
        /// <param name="finalPath">The final file path to write to.</param>
        /// <param name="content">The content to write.</param>
        /// <param name="encoding">The encoding to use when writing.</param>
        public static void WriteFileAtomic(string finalPath, string content,
            Encoding encoding)
        {
            string directory = Path.GetDirectoryName(finalPath);

            string tempPath = Path.Combine(directory,
                Path.GetFileName(finalPath) + ".tmp");

            try
            {
                File.WriteAllText(tempPath, content, encoding);

                try
                {
                    File.Replace(tempPath, finalPath, null);
                }
                catch (Exception)
                {
                    File.Delete(finalPath);
                    File.Move(tempPath, finalPath);
                }
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    try
                    {
                        File.Delete(tempPath);
                    }
                    catch (Exception)
                    {
                    }
                }
            }
        }
    }
}
