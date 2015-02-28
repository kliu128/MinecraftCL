using Ionic.Zip;
using MinecraftLaunchLibrary;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Xml;

namespace MinecraftCL.FeedTheBeast
{
    public static class FTBUtils
    {
        

        public static List<FTBModpack> GetModpacks(out Exception resultException)
        {
            try
            {
                WebClient webClient = new WebClient();
                XmlDocument modpackDoc = new XmlDocument();
                List<FTBModpack> modpackList = new List<FTBModpack>();

                modpackDoc.Load(FTBLocations.MasterDownloadRepo + FTBLocations.FTB2Static + "modpacks.xml");
                XmlNodeList modpackNodes = modpackDoc.SelectNodes("/modpacks/modpack");
                foreach (XmlNode modpackNode in modpackNodes)
                {
                    modpackList.Add(ParseSingleModpackXML(modpackNode));
                }
                resultException = null;
                return modpackList;
            }
            catch (Exception e)
            {
                resultException = e;
                return null;
            }
        }

        private class FTBPackJSON
        {
            public string minecraftArguments { get; set; }
            public List<Library> libraries { get; set; }
            public string mainClass { get; set; }
            public string id { get; set; }
        }

        /// <summary>
        /// Downloads and saves the modpack with the FTB save format. (one change: natives are saved in /$version/$version-natives/ instead of /natives/, like vanilla minecraft)
        /// </summary>
        /// <param name="download"></param>
        /// <param name="dDialog"></param>
        /// <param name="installDir"></param>
        /// <returns></returns>
        public static bool DownloadModpack(FTBModpack download, DownloadDialog dDialog, string installDir)
        {
            // TODO: Fix
            if (MinecraftServerUtils.GetMojangServerConnectivity() && FTBServerUtils.GetFTBServerConnectivity())
            {
                // Begin timing FTB modpack download
                Analytics.BeginTiming(TimingsMeasureType.FTBModpackDownload);

                AutoResetEvent reset = new AutoResetEvent(false);
                bool downloadSuccess;
                try
                {
                    dDialog.Dispatcher.BeginInvoke((Action)delegate
                    {
                        dDialog.downloadProgressBar.Visibility = System.Windows.Visibility.Visible;
                        dDialog.downloadIsInProgress = true;
                        dDialog.downloadProgressBar.Minimum = 1;
                        dDialog.downloadProgressBar.Maximum = 100;
                        dDialog.Show();
                    });

                    WebClient client = new WebClient();
                    client.DownloadProgressChanged += (o, x) =>
                        {
                            double bytesIn = double.Parse(x.BytesReceived.ToString());
                            double totalBytes = double.Parse(x.TotalBytesToReceive.ToString());
                            double percentage = bytesIn / totalBytes * 100;
                            dDialog.Dispatcher.BeginInvoke((Action)delegate
                            {
                                dDialog.downloadFileDisplay.Text = "Downloading " + download.url + ": " + String.Format("{0:n0}", bytesIn / 1024) + "KB of " + String.Format("{0:n0}", totalBytes / 1024) + "KB.";
                                dDialog.downloadProgressBar.Value = percentage;
                            });
                        };
                    client.DownloadDataCompleted += (o, x) =>
                        {
                            // Extract zip file to install directory
                            MemoryStream modpackStream = new MemoryStream();
                            byte[] modpackZipByteArray = x.Result;
                            modpackStream.Write(modpackZipByteArray, 0, modpackZipByteArray.Length);
                            modpackStream.Flush();
                            modpackStream.Position = 0;
                            using (ZipFile modpackZip = ZipFile.Read(modpackStream))
                            {
                                // Extract each file individually, and keep a running percentage of entries extracted / total entries
                                // to get a psuedo-percentage of extraction progress.
                                int entriesExtracted = 0;
                                foreach (ZipEntry file in modpackZip)
                                {
                                    file.Extract(installDir, ExtractExistingFileAction.OverwriteSilently);
                                    entriesExtracted++;
                                    double percentComplete = (double)entriesExtracted / ((double)modpackZip.Entries.Count) * 100;

                                    dDialog.Dispatcher.BeginInvoke((Action)delegate
                                        {
                                            dDialog.downloadFileDisplay.Text = "Extracting " + download.url + "... " + Math.Round(percentComplete, 0) + "% extracted.";
                                            dDialog.downloadProgressBar.Value = percentComplete;
                                        });
                                }
                            }

                            Directory.Move(installDir + @"\minecraft\", installDir + @"\.minecraft\");

                            // Download the vanilla natives and jar
                            bool versionExists = LaunchGame.checkMinecraftExists(download.mcVersion);
                            if (versionExists == false)
                            {
                                string downloadReturnValue;
                                MinecraftUtils.DownloadGame(download.mcVersion, out downloadReturnValue);

                                if (downloadReturnValue != "success")
                                {
                                    // TODO: Implement
                                }
                            }

                            // Read out pack.json from the modpack zip file
                            string packJSON = File.ReadAllText(installDir + @"\.minecraft\pack.json");
                            FTBPackJSON packJSONClass = JsonConvert.DeserializeObject<FTBPackJSON>(packJSON);

                            // Parse pack.json class
                            foreach (var library in packJSONClass.libraries)
                            {
                                string libraryLocation;
                                // TODO: Find correct server to download libraries from.
                                MinecraftUtils.DownloadLibrary(library, installDir, false, download.mcVersion, out libraryLocation, FTBLocations.MasterDownloadRepo);
                            }

                            // Stop timing ftb modpack download
                            Analytics.StopTiming(TimingsMeasureType.FTBModpackDownload);
                            reset.Set();
                        };
                    
                    // Download the modpack zip file
                    client.DownloadDataAsync(new Uri(FTBLocations.MasterDownloadRepo + FTBLocations.FTB2 + "modpacks/" + download.dir + "/" + download.repoVersion + "/" + download.url));
                    downloadSuccess = true;
                }
                catch (WebException)
                {
                    downloadSuccess = false;
                }

                // Wait for the download and processing to finish before returning
                reset.WaitOne();

                return downloadSuccess;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Loads a single modpack from a URL and parses it.
        /// </summary>
        public static FTBModpack ParseSingleModpackXML(string XMLURL)
        {
            FTBModpack modpack = new FTBModpack();
            try
            {
                XmlDocument modpackDoc = new XmlDocument();
                modpackDoc.Load(XMLURL);
                return ParseSingleModpackXML(modpackDoc.SelectSingleNode("modpacks/modpack"));
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Parses XML of a single modpack and returns a corresponding FTBModpack class.
        /// </summary>
        public static FTBModpack ParseSingleModpackXML(XmlNode modpackElement)
        {
            FTBModpack modpack = new FTBModpack();
            XmlAttributeCollection ma = modpackElement.Attributes;

            if (ma["name"] != null)
                modpack.name = ma["name"].Value;
            if (ma["author"] != null)
                modpack.author = ma["author"].Value;
            if (ma["mcVersion"] != null)
                modpack.mcVersion = ma["mcVersion"].Value;
            if (ma["repoVersion"] != null)
                modpack.repoVersion = ma["repoVersion"].Value;
            if (ma["url"] != null)
                modpack.url = ma["url"].Value;
            if (ma["dir"] != null)
                modpack.dir = ma["dir"].Value;
            if (ma["version"] != null)
                modpack.version = ma["version"].Value;

            return modpack;
        }
    }
}
