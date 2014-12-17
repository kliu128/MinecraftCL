using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Linq;

namespace MinecraftCLBootstrap
{
    class Program
    {
        static void Main(string[] args)
        {
            // Print information about environment and bootstrap version.
            Console.Title = "Minecraft CL Bootstrapper";
            Console.WriteLine("MinecraftCL Bootstrap v" + Application.ProductVersion);
            Console.WriteLine("Current time is " + DateTime.UtcNow + " UTC");
            Console.WriteLine();
            Console.WriteLine("MinecraftCL will be starting soon.");
            Console.WriteLine();
            Console.WriteLine(".NET Version = " + System.Environment.Version);

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
            Console.WriteLine("Checking for MinecraftCL update...");
            Version newVersion;
            bool updateRequired = CheckForUpdate(minecraftCLVersion, out newVersion);
            if (updateRequired && newVersion != null)
                DownloadUpdate(newVersion);


            if (File.Exists("CL.exe"))
            {
                Console.WriteLine("Starting MinecraftCL...");
                Process.Start("CL.exe");
            }
            else
            {
                Console.WriteLine("ERROR: MinecraftCL not found. Press any key to exit.");
                Console.ReadKey();
            }
        }

        /// <summary>
        /// Checks for MinecraftCL update.
        /// </summary>
        /// <param name="minecraftCLVersion"></param>
        /// <returns>Returns false if update failed. Does NOT return false if there was no update required.</returns>
        private static bool CheckForUpdate(string minecraftCLVersion, out Version latestVersion)
        {
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
                    Console.WriteLine("ERROR: Could not check for update.");
                    latestVersion = null;
                    return false;
                }
            }

            // If MinecraftCL is not downloaded (null), or the latest version is higher than the installed version,
            // return true.
            if ((minecraftCLVersion == null) ||
                (minecraftCLVersion != null && latestVersion > Version.Parse(minecraftCLVersion)))
            {
                return true;
            }
            else
            {
                // No update is needed.
                Console.WriteLine("No update required.");
                return false;
            }
        }
        /// <summary>
        /// Downloads an update to MinecraftCL.
        /// </summary>
        /// <param name="version">Version to be downloaded from MinecraftCL servers.</param>
        /// <returns>bool: success of download or failure</returns>
        private static bool DownloadUpdate(Version version)
        {
            Console.WriteLine();
            Console.WriteLine("**** Downloading MinecraftCL version " + version + " ****");

            try
            {
                string clVersionInfoURL = ConfigurationManager.AppSettings["VersionInformationURL"].Replace("${version}", version.ToString());
                string versionFolderURL = ConfigurationManager.AppSettings["VersionFolderURL"].Replace("${version}", version.ToString());
                
                using (WebClient client = new WebClient())
                {
                    XElement versionInfo = XElement.Parse(client.DownloadString(clVersionInfoURL));

                    string[] updateFiles = versionInfo.Element("Files")
                        .Value
                        .Split(new string[] {","}, StringSplitOptions.RemoveEmptyEntries);

                    Console.WriteLine("Files to download: " + versionInfo.Element("Files").Value);

                    foreach (string file in updateFiles)
                    {
                        // Download each file sequentially that is included in the update.
                        Console.WriteLine("Downloading file " + file + " for MinecraftCL " + version + ".");
                        Directory.CreateDirectory(Path.GetDirectoryName(Environment.CurrentDirectory + @"\" + file));
                        client.DownloadFile(versionFolderURL + file, Environment.CurrentDirectory + @"\" + file);
                    }
                }

                Console.WriteLine("**** Completed download of MinecraftCL version " + version + " ****");
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
