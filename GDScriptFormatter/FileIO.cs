using System;
using System.IO;
using System.Text;

namespace GDScriptFormatter
{
    internal static class FileIO
    {
        /// <summary>
        /// Detects the encoding of a byte array by inspecting the BOM.
        /// Falls back to UTF-8 validation and then to Encoding.Default if no BOM is present.
        /// </summary>
        /// <param name="bytes">The raw byte array to inspect.</param>
        /// <returns>A tuple of the detected encoding and the length of the BOM (0 if no BOM).</returns>
        internal static (Encoding encoding,
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

            try
            {
                Encoding.UTF8.GetString(bytes);
                return (Encoding.UTF8, 0);
            }
            catch (Exception)
            {
                return (Encoding.Default, 0);
            }
        }

        /// <summary>
        /// Writes content to the final path atomically by first writing to a temporary file in the same
        /// directory and then replacing the destination file via File.Replace. If File.Replace fails,
        /// falls back to Delete + Move. Residual temporary files are cleaned up in a finally block.
        /// </summary>
        /// <param name="finalPath">The final file path to write to.</param>
        /// <param name="content">The content to write.</param>
        /// <param name="encoding">The encoding to use when writing.</param>
        internal static void WriteFileAtomic(string finalPath, string content,
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
