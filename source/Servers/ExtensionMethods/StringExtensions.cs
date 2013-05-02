using System;
using System.Text;
using System.Text.RegularExpressions;

namespace EQEmulator.Servers.ExtensionMethods
{
    public static class StringExtensions
    {
        public static string RemoveDigits(this String s)
        {
            //StringBuilder sb = new StringBuilder(s);

            //for (int i = 0; i < s.Length; i++)
            //{
            //    if (Char.IsDigit(s, i))
            //        sb.Remove(i, 1);
            //}

            //return sb.ToString();

            return Regex.Replace(s, "[0-9]", "");   // Strip any numbers
        }

        public static byte[] ToAnsi(this String s)
        {
            var strBytes = new byte[s.Length];
            for (int i = 0; i < s.Length; i++)
                strBytes[i] = Convert.ToByte(s.Substring(i, 1), 16);

            return strBytes;
        }

        public static byte[] ToAnsiSZ(this String s)
        {
            var strBytes = new byte[s.Length + 1];
            for (int i = 0; i < s.Length; i++)
                strBytes[i] = Convert.ToByte(s.Substring(i, 1), 16);

            strBytes[s.Length] = 0;
            return strBytes;
        }
    }
}
