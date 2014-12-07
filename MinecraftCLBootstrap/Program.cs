using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Windows.Forms;

namespace MinecraftCLBootstrap
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.Title = "Minecraft CL Bootstrap";
            Console.WriteLine("MinecraftCL Bootstrap v" + Application.ProductVersion);
            Console.WriteLine("Current time is " + DateTime.UtcNow + " UTC");
            Console.WriteLine();
            Console.WriteLine("MinecraftCL will be starting soon.");
            Console.WriteLine();
            Console.WriteLine(".NET Version       = " + System.Environment.Version);

            if (File.Exists("CL.exe"))
                Console.WriteLine("MinecraftCL Status = Installed");
            else
            {
                // MinecraftCL is not downloaded, begin download
                Console.WriteLine("MinecraftCL Status = Not Installed");
                Console.WriteLine("Downloading MinecraftCL...");
            }

            using (WebClient client = new WebClient())
            {
                Version latestVersion;
                try
                {
                    latestVersion = Version.Parse(client.DownloadString(ConfigurationManager.AppSettings["LatestVersionURL"]));
                    Console.WriteLine("MinecraftCL Latest Version = " + latestVersion);
                }
                catch
                {
                    Console.WriteLine("Could not check for update.");
                }
            }
            
            
            Console.ReadLine();
        }
    }
}
