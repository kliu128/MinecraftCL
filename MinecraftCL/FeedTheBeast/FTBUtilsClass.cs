using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web.Helpers;
using System.Xml;
using System.Xml.Serialization;
using Ionic.Zip;

namespace MinecraftCL.FeedTheBeast
{
    public static class FTBUtils
    {
        public static bool DownloadRepoFile(string file)
        {
            // Usable repos:
            // ftb.cursecdn.com/FTB2/static/...
            // and others?
            return true;
        }

        public static bool InitializeDLServers()
        {
            // Much things copied from the FTB launcher to figure out what
            // it does.
            if (Globals.HasInternetConnectivity)
            {
                #region Check server balance and select master server
                HttpStatusCode? status = null;
                HttpWebResponse response = null;

                try
                {
                    // Contact the curse server for the balance json
                    HttpWebRequest request = WebRequest.Create(FTBLocations.CurseCDN + "FTB2/static/balance.json") as HttpWebRequest;
                    response = request.GetResponse() as HttpWebResponse;
                    status = response.StatusCode;

                }
                catch (WebException e)
                {
                    MessageWindow error = new MessageWindow();
                    error.messageText.Text = "Could not connect to Curse CDN. " + e.Message;
                    error.closeTimeoutMilliseconds = 10000;
                    error.Show();
                    return false;
                }
                if (status != null && status == HttpStatusCode.OK)
                {
                    string curseCDNBalanceJSONString = null;
                    Random r = new Random();

                    using (Stream responseStream = response.GetResponseStream())
                    {
                        using (TextReader reader = new StreamReader(responseStream))
                        {
                            curseCDNBalanceJSONString = reader.ReadToEnd();
                        }
                    }

                    var curseBalanceJSON = Json.Decode(curseCDNBalanceJSONString);

                    // INFO: There is also a "minimumLauncherVersion" json node in the ftblauncher code,
                    // but I don't think it is required.

                    if (curseBalanceJSON.repoSplitCurse != null)
                    {
                        // Not sure what the repoSplitCurse thing is on the servers, 
                        // maybe an indication of server loads?

                        // This function compares repoSplitCurse to a random double between 0.0 and 1.0
                        // to determine what server to use.
                        if (double.Parse(curseBalanceJSON.repoSplitCurse, System.Globalization.CultureInfo.InvariantCulture) > r.NextDouble())
                        {
                            DebugConsole.Print("Balance has selected Automatic:CurseCDN.", "FTBUtils.InitializeDLServers");
                            FTBLocations.SetMasterDownloadRepo(FTBLocations.CurseCDN);
                        }
                        else
                        {
                            DebugConsole.Print("Balance has selected Automatic:CreeperRepo.", "FTBUtils.InitializeDLServers");
                            FTBLocations.SetMasterDownloadRepo(FTBLocations.CreeperRepo);
                        }
                    }
                    if (curseBalanceJSON.chEnabled != null)
                    {
                        try
                        {
                            FTBLocations.chEnabled = (bool)curseBalanceJSON.chEnabled;
                        }
                        catch (Exception)
                        {
                            FTBLocations.chEnabled = false;
                        }
                    }
                    else
                        FTBLocations.chEnabled = false;
                }
                #endregion

                // TODO: This is just a simple version of the server check, edges.json. Improve?
                List<string> downloadMirrorList = new List<string>();
                
                response = null;
                status = null;
                // Contact CreeperRepo for edges.json
                HttpWebRequest creeperRepoRequest = WebRequest.Create(FTBLocations.CreeperRepo + "edges.json") as HttpWebRequest;
                response = creeperRepoRequest.GetResponse() as HttpWebResponse;
                status = response.StatusCode;
                string creeperRepoList = null;
                if (status != null && status == HttpStatusCode.OK)
                {
                    using (Stream responseStream = response.GetResponseStream())
                    {
                        using (TextReader reader = new StreamReader(responseStream))
                        {
                            creeperRepoList = reader.ReadToEnd();
                        }
                    }
                }

                Dictionary<string, string> creeperRepoListJSON = JsonConvert.DeserializeObject<Dictionary<string, string>>(creeperRepoList);
                foreach (var item in creeperRepoListJSON)
                {
                    downloadMirrorList.Add(item.Value);
                }

                response = null;
                status = null;
                // Contact the curse server for edges.json
                HttpWebRequest curseRequest = WebRequest.Create(FTBLocations.CurseCDN + "edges.json") as HttpWebRequest;
                response = curseRequest.GetResponse() as HttpWebResponse;
                status = response.StatusCode;
                string curseMirrorList = null;
                if (status != null && status == HttpStatusCode.OK)
                {
                    using (Stream responseStream = response.GetResponseStream())
                    {
                        using (TextReader reader = new StreamReader(responseStream))
                        {
                            curseMirrorList = reader.ReadToEnd();
                        }
                    }
                }

                Dictionary<string, string> curseMirrorListJSON = JsonConvert.DeserializeObject<Dictionary<string, string>>(curseMirrorList);
                foreach (var item in curseMirrorListJSON)
                {
                    downloadMirrorList.Add(item.Value);
                }

                // Pick a random server from the list
                // TODO: Make this non-random?
                int randomMirrorIndex = new Random().Next(downloadMirrorList.Count);
                FTBLocations.DownloadServer = downloadMirrorList[randomMirrorIndex];
                DebugConsole.Print("InitializeDLServers() has selected " + FTBLocations.DownloadServer + " as mirror.", "FTBUtils.InitializeDLServers()");
                return true;
            }
            // No internet connectivity, so obviously can't find download servers!
            return false;
        }

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

        private class FTBLibrary
        {
            public string name { get; set; }
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
        public static bool DownloadModpack(FTBModpack download, DownloadDialog dDialog, string installDir, dynamic mcVersionDynamic)
        {
            if (Globals.HasInternetConnectivity)
            {
                // Begin timing FTB modpack download
                Analytics.BeginTiming(TimingsMeasureType.FTBModpackDownload);

                bool downloadFinished = false;
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

                            // Rename the "/minecraft/" folder to the modpack's name
                            Directory.Move(installDir + @"\minecraft\", installDir + @"\" + download.name);

                            // Download the vanilla natives and jar
                            bool versionExists = MinecraftUtils.checkMinecraftExists(download.mcVersion, mcVersionDynamic);
                            if (versionExists == false)
                            {
                                downloadVariables downloadGameVariables = new downloadVariables { DownloadDialog = dDialog, mcInstallDir = installDir, ValidateFiles = false, mcVersion = download.mcVersion };
                                MinecraftUtils.DownloadGame(downloadGameVariables);
                            }

                            // Read out pack.json from the modpack zip file
                            string packJSON = File.ReadAllText(installDir + @"\" + download.name + @"\pack.json");
                            FTBPackJSON packJSONClass = JsonConvert.DeserializeObject<FTBPackJSON>(packJSON);

                            // Parse pack.json class
                            foreach (var library in packJSONClass.libraries)
                            {
                                
                            }

                            // Stop timing ftb modpack download
                            Analytics.StopTiming(TimingsMeasureType.FTBModpackDownload);
                            downloadFinished = true;
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
                while(downloadFinished == false)
                { }

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

            return modpack;
        }
    }
}
