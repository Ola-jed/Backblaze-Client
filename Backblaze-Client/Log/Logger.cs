using System;
using System.Globalization;
using System.IO;

namespace Backblaze_Client.Log
{
    /// <summary>
    /// Logger for our package
    /// </summary>
    public class Logger
    {
        private readonly LogLevel _logLevel;
        private const string LogSaveFileName = "BackblazeLogs.txt";

        public Logger(LogLevel logLevel)
        {
            _logLevel = logLevel;
        }

        /// <summary>
        /// Log the message to the correct output
        /// </summary>
        /// <param name="message">The message to log</param>
        /// <exception cref="ArgumentOutOfRangeException">Should not be thrown</exception>
        public void Log(string message)
        {
            switch (_logLevel)
            {
                case LogLevel.None:
                    return;
                case LogLevel.Medium:
                {
                    using StreamWriter file = new(LogSaveFileName, append: true);
                    file.WriteLine($"{DateTime.Now.ToString(CultureInfo.CurrentCulture)}");
                    file.WriteLine(message);
                    file.WriteLine("-------------------------------------");
                    return;
                }
                case LogLevel.High:
                {
                    using StreamWriter file = new(LogSaveFileName, append: true);
                    file.WriteLine($"{DateTime.Now.ToString(CultureInfo.CurrentCulture)}");
                    file.WriteLine(message);
                    file.WriteLine("-------------------------------------");
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine(message);
                    Console.WriteLine("-------------------------------------");
                    return;
                }
                default:
                    throw new ArgumentOutOfRangeException("Oops");
            }
        }
    }
}