using System.Reflection;

namespace System.IO
{
    internal static class StreamReaderExtensions
    {
        public static long GetPosition(this StreamReader reader)
        {
            BindingFlags flags = BindingFlags.DeclaredOnly | BindingFlags.NonPublic | BindingFlags.Instance;
            Type type = reader.GetType();
            FieldInfo? charBufferField = type.GetField("_charBuffer", flags);
            FieldInfo? charPosField = type.GetField("_charPos", flags);
            FieldInfo? charLenField = type.GetField("_charLen", flags);

            ArgumentNullException.ThrowIfNull(charBufferField);
            char[]? charBuffer = (char[]?)charBufferField.GetValue(reader);
            ArgumentNullException.ThrowIfNull(charBuffer);

            ArgumentNullException.ThrowIfNull(charPosField);
            int? charPos = (int?) charPosField.GetValue(reader);
            ArgumentNullException.ThrowIfNull(charPos);

            ArgumentNullException.ThrowIfNull(charLenField);
            int? charLen = (int?)charLenField.GetValue(reader);
            ArgumentNullException.ThrowIfNull(charLen);

            return reader.BaseStream.Position - reader.CurrentEncoding.GetByteCount(charBuffer, charPos.Value, charLen.Value - charPos.Value);
        }
    }
}
