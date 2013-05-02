using System;
using System.Text;
using System.IO;
using log4net;
using log4net.Core;
using log4net.Repository.Hierarchy;

namespace EQEmulator.Servers.Internals
{
    public class Log4NetWriter : TextWriter
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Log4NetWriter"/> class.
        /// </summary>
        /// <param name="type">The type.</param>
        public Log4NetWriter(Type type)
            : this(type, Level.Debug)
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="Log4NetWriter"/> class.
        /// </summary>
        /// <param name="type">The logger source type.</param>
        /// <param name="level">The log4net log level.</param>
        public Log4NetWriter(Type type, Level level)
        {
            logSrcType = type;
            this.level = level;
            open = true;
        }

        /// <summary>
        /// Releases the unmanaged resources used by the <see cref="T:System.IO.TextWriter"/> and optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        protected override void Dispose(bool disposing)
        {
            open = false;
            base.Dispose(disposing);
        }

        /// <summary>
        /// Writes a character to the text stream.
        /// </summary>
        /// <param name="value">The character to write to the text stream.</param>
        /// <exception cref="T:System.ObjectDisposedException">The <see cref="T:System.IO.TextWriter"/> is closed. </exception>
        /// <exception cref="T:System.IO.IOException">An I/O error occurs. </exception>
        public override void Write(char value)
        {
            if (!open)
                throw new ObjectDisposedException(null);
            Log(value.ToString());
        }

        /// <summary>
        /// Writes a string to the text stream.
        /// </summary>
        /// <param name="value">The string to write.</param>
        /// <exception cref="T:System.ObjectDisposedException">The <see cref="T:System.IO.TextWriter"/> is closed. </exception>
        /// <exception cref="T:System.IO.IOException">An I/O error occurs. </exception>
        public override void Write(string value)
        {
            if (!open)
                throw new ObjectDisposedException(null);
            if (value != null)
                Log(value);
        }

        /// <summary>
        /// Writes a subarray of characters to the text stream.
        /// </summary>
        /// <param name="buffer">The character array to write data from.</param>
        /// <param name="index">Starting index in the buffer.</param>
        /// <param name="count">The number of characters to write.</param>
        /// <exception cref="T:System.ArgumentException">The buffer length minus <paramref name="index"/> is less than <paramref name="count"/>. </exception>
        /// <exception cref="T:System.ArgumentNullException">The <paramref name="buffer"/> parameter is null. </exception>
        /// <exception cref="T:System.ArgumentOutOfRangeException">
        ///     <paramref name="index"/> or <paramref name="count"/> is negative. </exception>
        /// <exception cref="T:System.ObjectDisposedException">The <see cref="T:System.IO.TextWriter"/> is closed. </exception>
        /// <exception cref="T:System.IO.IOException">An I/O error occurs. </exception>
        public override void Write(char[] buffer, int index, int count)
        {
            if (!open)
                throw new ObjectDisposedException(null);
            if (buffer == null || index < 0 || count < 0 || buffer.Length - index < count)
                base.Write(buffer, index, count); // delegate throw exception to base class
            
            Log(new string(buffer, index, count));
        }

        /// <summary>
        /// When overridden in a derived class, returns the <see cref="T:System.Text.Encoding"/> in which the output is written.
        /// </summary>
        /// <value></value>
        /// <returns>The Encoding in which the output is written.</returns>
        public override Encoding Encoding
        {
            get
            {
                if (encoding == null)
                    encoding = new UnicodeEncoding(false, false);
                return encoding;
            }
        }


        /// <summary>
        /// Writes a message to log4net 
        /// </summary>
        /// <param name="message"></param>
        private void Log(string message)
        {
            Hierarchy hierarchy = ((Hierarchy)LogManager.GetRepository());
            ILoggerFactory loggerFactory = ((Hierarchy)LogManager.GetRepository()).LoggerFactory;
            Logger logger = hierarchy.GetLogger(logSrcType.FullName, loggerFactory);
            logger.Log(level, message, null);
        }


        private bool open;
        private static UnicodeEncoding encoding;
        private readonly Level level;
        private Type logSrcType;
    }
}
