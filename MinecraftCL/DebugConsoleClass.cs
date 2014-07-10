using System;
using System.Collections.Generic;
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
                if (!File.Exists(System.Environment.CurrentDirectory + @"\.mcl\Console.log"))
                    File.Create(System.Environment.CurrentDirectory + @"\.mcl\Console.log");

                File.AppendAllText(System.Environment.CurrentDirectory + @"\.mcl\Console.log", "[" + DateTime.Now + "] [" + messageType + "] [" + senderType + "]: " + message + Environment.NewLine);
            }
        }
    }
}
