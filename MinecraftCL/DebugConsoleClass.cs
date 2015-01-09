using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace MinecraftCL
{
    public static class DebugConsole
    {

        public static void Print(string message, string senderType, string messageType = "INFO")
        {
            if (Globals.DebugOn)
            {
                if (!Directory.Exists(Environment.CurrentDirectory + @"\.mcl\"))
                    Directory.CreateDirectory(Environment.CurrentDirectory + @"\.mcl\");

                if (!File.Exists(System.Environment.CurrentDirectory + @"\.mcl\Console.log"))
                    using (File.Create(System.Environment.CurrentDirectory + @"\.mcl\Console.log")) { }

                string logLine = "[" + DateTime.Now + "] [" + messageType + "] [" + senderType + "]: " + message;
                File.AppendAllText(System.Environment.CurrentDirectory + @"\.mcl\Console.log", logLine + Environment.NewLine);
                Debug.WriteLine(logLine);
            }
        }
    }
}
