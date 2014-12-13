using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
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
            // Print information about environment and bootstrap version.
            Console.Title = "Minecraft CL Bootstrap";
            Console.WriteLine("MinecraftCL Bootstrap v" + Application.ProductVersion);
            Console.WriteLine("Current time is " + DateTime.UtcNow + " UTC");
            Console.WriteLine();
            Console.WriteLine("MinecraftCL will be starting soon.");
            Console.WriteLine();
            Console.WriteLine(".NET Version       = " + System.Environment.Version);

            // Load MinecraftCL version information from the file.
            string minecraftCLVersion = null;
            if (File.Exists("CL.exe"))
            {
                // Get the MinecraftCL version if it is already downloaded.
                minecraftCLVersion = FileVersionInfo.GetVersionInfo("CL.exe").FileVersion;
                Console.WriteLine("MinecraftCL Version = " + minecraftCLVersion);
            }
            else
            {
                // MinecraftCL is not downloaded.
                Console.WriteLine("MinecraftCL Version = Not Installed");
            }
            // Check for updates to MinecraftCL.
            bool updateSuccess = checkForUpdate(minecraftCLVersion);
            if (updateSuccess == true || File.Exists("CL.exe"))
                Process.Start("CL.exe");
            Console.Read();
        }

        /// <summary>
        /// Checks for MinecraftCL update.
        /// </summary>
        /// <param name="minecraftCLVersion"></param>
        /// <returns>Returns false if update failed. Does NOT return false if there was no update required.</returns>
        private static bool checkForUpdate(string minecraftCLVersion)
        {
            Version latestVersion = null;
            using (WebClient client = new WebClient())
            {
                try
                {
                    // Download the Latest Version file from the server, to get the latest MinecraftCL version.
                    latestVersion = Version.Parse(client.DownloadString(ConfigurationManager.AppSettings["LatestVersionURL"]));
                    Console.WriteLine("MinecraftCL Latest Version = " + latestVersion);
                }
                catch
                {
                    // Could not reach server; is it down?
                    Console.WriteLine("Could not check for update.");
                    return false;
                }

                // If MinecraftCL is not downloaded (null), or the latest version is higher than the installed version,
                // download MinecraftCL from server.
                if ((minecraftCLVersion == null) || 
                    (minecraftCLVersion != null && latestVersion > Version.Parse(minecraftCLVersion)))
                {
                    try
                    {
                        // Download MinecraftCL
                        client.DownloadFile(ConfigurationManager.AppSettings["CLDownloadURL"], "CL.exe");
                        Console.WriteLine("MinecraftCL successfully updated to " + latestVersion + ".");
                        return true;
                    }
                    catch
                    {
                        Console.WriteLine("Error downloading MinecraftCL version.");
                        return false;
                    }
                }
                else
                    // No update is needed.
                    return true;
            }
        }
    }
}
