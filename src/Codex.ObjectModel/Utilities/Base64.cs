using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics.ContractsLight;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Codex.Sdk.Utilities
{
    public class Base64
    {
        public const int MaxByteLength = 1024;

        public const int MaxCharLength = 2048;

        public enum Format
        {
            // Lossless
            Standard,
            UrlSafe,

            // Lossy
            LowercaseAlphanumeric
        }

        public static readonly string[] base64Formats = new string[]
        {
            // Standard
            "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/=",

            // UrlSafe
            "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_=",

            // LowercaseAlphanumeric
            "abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz0123456789zy="
        };

        internal static void ReplaceBase64CharsForConvert(Span<char> chars)
        {
            foreach (ref var ch in chars)
            {
                if (ch == '-')
                {
                    ch = '+';
                }
                else if (ch == '_')
                {
                    ch = '/';
                }
            }
        }

        public static void ReplaceBase64Chars(Span<char> chars)
        {
            foreach (ref var ch in chars)
            {
                if (ch == '+')
                {
                    ch = '-';
                }
                else if (ch == '/')
                {
                    ch = '_';
                }
            }
        }

        private static ulong Base64ToByte(char c)
        {
            if (c >= 'A' && c <= 'Z')
                return (byte)(c - 'A');
            if (c >= 'a' && c <= 'z')
                return (byte)(c - 'a' + 26);
            if (c >= '0' && c <= '9')
                return (byte)(c - '0' + 52);
            if (c == '-')
                return 62;
            if (c == '_')
                return 63;
            throw new ArgumentException("Invalid base64 character: " + c);
        }

        public static string ToBase64String(ReadOnlySpan<byte> inData, int maxCharLength = MaxCharLength, Format format = Format.UrlSafe)
        {
            return Convert(inData,
                arg: (maxCharLength, 0),
                static (span, arg) =>
                {
                    var length = Math.Min(span.Length, int.MaxValue);
                    return new string(span.Slice(0, length));
                },
                format);
        }


        public static T Convert<T, TArg>(ReadOnlySpan<byte> inData, TArg arg, SpanFunc<char, TArg, T> handleSpan, Format format = Format.UrlSafe)
        {
            var length = inData.Length;
            Contract.Assert(length < 1024, "This method is not intended for conversion of long (> 1024 bytes) byte arrays.");

            int lengthmod3 = length % 3;
            int calcLength = (length - lengthmod3);
            int j = 0;
            int charLength = ((length * 8) + 5) / 6;

            //Convert three bytes at a time to base64 notation.  This will consume 4 chars.
            int i;

            // get a pointer to the base64Table to avoid unnecessary range checking
            Span<char> outChars = stackalloc char[charLength];
            var table = base64Formats[(int)format];
            ReadOnlySpan<char> base64 = table;
            {
                for (i = 0; i < calcLength; i += 3)
                {
                    outChars[j] = base64[(inData[i] & 0xfc) >> 2];
                    outChars[j + 1] = base64[((inData[i] & 0x03) << 4) | ((inData[i + 1] & 0xf0) >> 4)];
                    outChars[j + 2] = base64[((inData[i + 1] & 0x0f) << 2) | ((inData[i + 2] & 0xc0) >> 6)];
                    outChars[j + 3] = base64[(inData[i + 2] & 0x3f)];
                    j += 4;
                }

                //Where we left off before
                i = calcLength;

                switch (lengthmod3)
                {
                    case 2: //One character padding needed
                        outChars[j] = base64[(inData[i] & 0xfc) >> 2];
                        outChars[j + 1] = base64[((inData[i] & 0x03) << 4) | ((inData[i + 1] & 0xf0) >> 4)];
                        outChars[j + 2] = base64[(inData[i + 1] & 0x0f) << 2];
                        j += 4;
                        break;
                    case 1: // Two character padding needed
                        outChars[j] = base64[(inData[i] & 0xfc) >> 2];
                        outChars[j + 1] = base64[(inData[i] & 0x03) << 4];
                        j += 4;
                        break;
                }
            }

            return handleSpan(outChars.Slice(0, charLength), arg);
        }
    }
}
