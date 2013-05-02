using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Runtime.InteropServices;
using System.Net;

namespace EQEmulator.Servers.Internals
{
    static internal class Utility
    {
        /// <summary>Copies a string into a byte array and always terminates with a null (zero) character.</summary>
        /// <param name="buffer">Byte array the string will be copied into.</param>
        /// <param name="offset">Offset in byte array to start the copy at.</param>
        /// <param name="val">The string from which to copy.</param>
        /// <param name="len">The total length of the characters to copy (including zeroes).</param>
        internal static void NullPadBuffer(ref byte[] buffer, int offset, string val, int len)
        {
            NullPadBuffer(ref buffer, offset, val, len, false);
        }

        /// <summary>Copies a string into a byte array and always terminates with a null (zero) character.</summary>
        /// <param name="buffer">Byte array the string will be copied into.</param>
        /// <param name="offset">Offset in byte array to start the copy at.</param>
        /// <param name="val">The string from which to copy.</param>
        /// <param name="len">The total length of the characters to copy (including zeroes).</param>
        /// <param name="alwaysNullTerm">Whether or not to always place a null terminating character at the end of len.</param>
        internal static void NullPadBuffer(ref byte[] buffer, int offset, string val, int len, bool alwaysNullTerm)
        {
            if (len == 0 || val.Length == 0)
                buffer[0] = 0;
            else
            {
                Buffer.BlockCopy(Encoding.ASCII.GetBytes(val), 0, buffer, offset, val.Length);

                for (int i = val.Length; i < len; i++)
                    buffer[i] = 0;  // pad w/ zeroes

                if (alwaysNullTerm)
                    buffer[len - 1] = 0;    // terminate w/ zero
            }
        }

        /// <summary>Copies a string into a byte array and tries to terminate with a zero.</summary>
        /// <param name="buffer">Byte array the string will be copied into.</param>
        /// <param name="offset">Offset in byte array to start the copy into.</param>
        /// <param name="val">The string from which to copy.</param>
        internal static void StringCopyNullTerm(ref byte[] buffer, int offset, string val)
        {
            if (val.Length == 0)
                buffer[0] = 0;
            else
            {
                Buffer.BlockCopy(Encoding.ASCII.GetBytes(val), 0, buffer, offset, val.Length);

                if (val.Length < buffer.Length)
                    buffer[val.Length] = 0;    // terminate w/ zero
            }
        }

        internal static int FloatToEQ19(float d)
        {
            return (int)(d * (float)(1 << 3));
        }

        internal static float EQ19ToFloat(int d)
        {
            return ((float)d / (float)(1 << 3));
        }

        internal static int FloatToEQ13(float d)
        {
            return (int)(d * (float)(1 << 6));
        }

        internal static string DumpStruct(object dumpStruct)
        {
            StringBuilder sb = new StringBuilder(10000);

            Type type = dumpStruct.GetType();
            foreach (FieldInfo field in type.GetFields())
            {
                if (field.GetValue(dumpStruct) == null)
                    continue;

                Type fieldType = field.FieldType;
                if (fieldType.IsArray)
                {
                    Type elementType = fieldType.GetElementType();
                    if (elementType == typeof(System.Byte))
                        sb.AppendFormat("{0} {1} {2}\n", field.FieldType.Name, field.Name, BitConverter.ToString((byte[])field.GetValue(dumpStruct)));
                    else
                    {
                        Array array = (Array)field.GetValue(dumpStruct);
                        sb.AppendFormat("{0} {1} {2}\n", field.FieldType.Name, field.Name, DumpArray(array, elementType));
                    }
                }
                else
                    sb.AppendFormat("{0} {1} {2}\n", field.FieldType.Name, field.Name, field.GetValue(dumpStruct));
            }

            return sb.ToString();
        }

        internal static string DumpArray(Array array, Type elementType)
        {
            StringBuilder sb = new StringBuilder(1000);
            for (int i = 0; i < array.Length; i++)
                sb.Append(array.GetValue(i));

            return sb.ToString();
        }

        internal static byte[] SerializeStruct<T>(object structIn)
        {
            int dataSize = Marshal.SizeOf(default(T));
            byte[] structData = new byte[dataSize];
            GCHandle handle = GCHandle.Alloc(structData, GCHandleType.Pinned);
            IntPtr buffer = handle.AddrOfPinnedObject();
            Marshal.StructureToPtr(structIn, buffer, false);
            handle.Free();

            return structData;
        }

        internal static byte[] SerializeStructUnsafe<T>(object structIn) where T : struct
        {
            byte[] bytes = new byte[Marshal.SizeOf((T)structIn)];
            
            unsafe
            {
                fixed (byte* pData = bytes)
                {
                    Marshal.StructureToPtr(structIn, (IntPtr)pData, false);
                }
            }

            return bytes;
        }

        internal static DateTime DateTimeFromTimeT(int seconds)
        {
            DateTime dt = new DateTime(1970, 1, 1).AddSeconds(seconds);
            return dt;
        }

        internal static uint TimeTFromDateTime(DateTime dt)
        {
            DateTime tempDate = new DateTime(1970, 1, 1);
            return (uint)dt.Subtract(tempDate).TotalSeconds;
        }

        internal static bool IsIpInNetwork(IPAddress ipOne, IPAddress ipTwo, IPAddress mask)
        {
            long ipOneL = (long)BitConverter.ToUInt32(ipOne.GetAddressBytes(), 0);
            long ipTwoL = (long)BitConverter.ToUInt32(ipTwo.GetAddressBytes(), 0);
            long maskL = (long)BitConverter.ToUInt32(mask.GetAddressBytes(), 0);

            return (maskL & ipOneL) == (maskL & ipTwoL);
        }
    }
}
