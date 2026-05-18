using System;

namespace Client
{
    public static class ConsoleUi
    {
        public static readonly object Sync = new object();

        public static void Info(string text)
        {
            Write("[INFO] ", ConsoleColor.Cyan, text, ConsoleColor.Gray, true);
        }

        public static void Ok(string text)
        {
            Line(text, ConsoleColor.Green);
        }

        public static void Reject(string text)
        {
            Line(text, ConsoleColor.Red);
        }

        public static void Fault(string tag, string body)
        {
            Write("[" + tag + "] ", ConsoleColor.Red, body, ConsoleColor.Red, true);
        }

        public static void Line(string text, ConsoleColor color)
        {
            lock (Sync)
            {
                var prev = Console.ForegroundColor;
                Console.ForegroundColor = color;
                Console.WriteLine(text);
                Console.ForegroundColor = prev;
            }
        }

        public static void Blank()
        {
            lock (Sync) Console.WriteLine();
        }

        private static void Write(string prefix, ConsoleColor prefixColor, string body, ConsoleColor bodyColor, bool newline)
        {
            lock (Sync)
            {
                var prev = Console.ForegroundColor;
                Console.ForegroundColor = prefixColor;
                Console.Write(prefix);
                Console.ForegroundColor = bodyColor;
                if (newline) Console.WriteLine(body); else Console.Write(body);
                Console.ForegroundColor = prev;
            }
        }
    }
}
