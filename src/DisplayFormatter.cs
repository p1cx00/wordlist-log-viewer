using System;
using System.Globalization;
using System.Text;

namespace WordlistViewer
{
    internal static class DisplayFormatter
    {
        public static string Decode(byte[] bytes, int offset, int count, Encoding encoding, ViewerDisplayMode mode)
        {
            if (bytes == null || count <= 0) return string.Empty;
            string decoded = encoding.GetString(bytes, offset, count);
            if (mode == ViewerDisplayMode.Hex)
            {
                return FormatHex(bytes, offset, count, 0);
            }

            char[] characters = decoded.ToCharArray();
            for (int i = 0; i < characters.Length; i++)
            {
                char value = characters[i];
                if (mode == ViewerDisplayMode.VisibleCharacters)
                {
                    if (value == '\0') characters[i] = '\u2400';
                    else if (value == '\t') characters[i] = '\u21E5';
                    else if (value == ' ') characters[i] = '\u00B7';
                    else if (value < 32 || value == 127) characters[i] = '\u25A1';
                }
                else
                {
                    if ((value < 32 && value != '\t') || value == 127) characters[i] = ' ';
                }
            }
            return new string(characters);
        }

        public static string FormatHex(byte[] bytes, int offset, int count, long absoluteOffset)
        {
            if (bytes == null || count <= 0) return string.Empty;
            StringBuilder builder = new StringBuilder(count * 4 + 64);
            int end = offset + count;
            for (int row = offset; row < end; row += 16)
            {
                int rowCount = Math.Min(16, end - row);
                builder.Append((absoluteOffset + row - offset).ToString("X12", CultureInfo.InvariantCulture));
                builder.Append("  ");
                for (int i = 0; i < 16; i++)
                {
                    if (i < rowCount) builder.Append(bytes[row + i].ToString("X2", CultureInfo.InvariantCulture));
                    else builder.Append("  ");
                    if (i == 7) builder.Append("  ");
                    else builder.Append(' ');
                }
                builder.Append(" | ");
                for (int i = 0; i < rowCount; i++)
                {
                    byte value = bytes[row + i];
                    builder.Append(value >= 32 && value <= 126 ? (char)value : '·');
                }
                if (row + rowCount < end) builder.AppendLine();
            }
            return builder.ToString();
        }

        public static string FormatHexRow(byte[] bytes, int offset, int count)
        {
            StringBuilder builder = new StringBuilder(72);
            int safeCount = Math.Max(0, Math.Min(16, Math.Min(count, bytes.Length - offset)));
            for (int i = 0; i < 16; i++)
            {
                if (i < safeCount) builder.Append(bytes[offset + i].ToString("X2", CultureInfo.InvariantCulture));
                else builder.Append("  ");
                builder.Append(i == 7 ? "  " : " ");
            }
            builder.Append("| ");
            for (int i = 0; i < safeCount; i++)
            {
                byte value = bytes[offset + i];
                builder.Append(value >= 32 && value <= 126 ? (char)value : '·');
            }
            return builder.ToString();
        }

        public static string MakePreview(string text, int maximumLength)
        {
            if (string.IsNullOrEmpty(text)) return "(boş satır)";
            StringBuilder builder = new StringBuilder(Math.Min(text.Length, maximumLength));
            bool previousSpace = false;
            for (int i = 0; i < text.Length && builder.Length < maximumLength; i++)
            {
                char value = text[i];
                bool whitespace = char.IsWhiteSpace(value);
                if (whitespace)
                {
                    if (!previousSpace) builder.Append(' ');
                }
                else
                {
                    builder.Append(value);
                }
                previousSpace = whitespace;
            }
            if (text.Length > maximumLength) builder.Append('…');
            return builder.ToString().Trim();
        }

        public static int CountControls(byte[] bytes, int offset, int count)
        {
            int result = 0;
            int end = Math.Min(bytes.Length, offset + count);
            for (int i = Math.Max(0, offset); i < end; i++)
            {
                byte value = bytes[i];
                if ((value < 32 && value != 9) || value == 127) result++;
            }
            return result;
        }
    }
}
