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
                            // Balance has selected Automatic:CurseCDN
                            FTBLocations.SetMasterDownloadRepo(FTBLocations.CurseCDN);
                        }
                        else
                        {
                            // Balance has selected Automatic:CreeperRepo
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

        public static bool DownloadModpack(FTBModpack download, DownloadDialog dDialog, string installDir)
        {
            if (Globals.HasInternetConnectivity)
            {
                try
                {
                    dDialog.Dispatcher.BeginInvoke((Action)delegate
                    {
                        dDialog.downloadProgressBar.Visibility = System.Windows.Visibility.Visible;
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
                            MemoryStream modpackStream = new MemoryStream();
                            byte[] modpackZipByteArray = x.Result;
                            modpackStream.Write(modpackZipByteArray, 0, modpackZipByteArray.Length);
                            ZipFile modpackZip = ZipFile.Read(modpackStream);
                            modpackZip.ExtractAll(installDir, ExtractExistingFileAction.OverwriteSilently);
                        };
                    
                    // Download the modpack zip file
                    client.DownloadDataAsync(new Uri(FTBLocations.MasterDownloadRepo + FTBLocations.FTB2 + "modpacks/" + download.dir + "/" + download.repoVersion + "/" + download.url));
                    return true;
                }
                catch (WebException)
                {
                    return false;
                }
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
