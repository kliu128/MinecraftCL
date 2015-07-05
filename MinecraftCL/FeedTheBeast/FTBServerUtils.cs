using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;

namespace MinecraftCL.FeedTheBeast
{
    class FTBServerUtils
    {
        private static bool cachedFTBServerConnectivity = false;
        public static bool GetFTBServerConnectivity()
        {
            if (cachedFTBServerConnectivity == true)
                return true;
            else
            {
                // Opens connection to FTB servers to check internet connectivity.
                // It will check either the first time it is called, or every time
                // if there is no internet connection available.
                try
                {
                    Ping p = new Ping();
                    p.Send(FTBLocations.CurseCDNHostName);
                    p.Send(FTBLocations.CreeperRepoHostName);
                }
                catch
                {
                    cachedFTBServerConnectivity = false;
                    return cachedFTBServerConnectivity;
                }
                // Only reaches here when all tests pass without error.
                cachedFTBServerConnectivity = true;
                return cachedFTBServerConnectivity;
            }
        }

        public static bool DownloadRepoFile(string file, string location)
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
            if (GetFTBServerConnectivity())
            {
                #region Check server balance and select master server
                HttpStatusCode? status = null;
                HttpWebResponse response = null;

                try
                {
                    // Contact the curse server for the balance json
                    HttpWebRequest request = WebRequest.Create(FTBLocations.CurseCDN + "/FTB2/static/balance.json") as HttpWebRequest;
                    response = request.GetResponse() as HttpWebResponse;
                    status = response.StatusCode;

                }
                catch (WebException e)
                {
                    MessageWindow error = new MessageWindow();
                    error.messageText.Text = "Could not connect to Curse CDN. " + e.Message;
                    error.closeTimeoutMilliseconds = 10000;
                    error.Show();
                    DebugConsole.Print("Could not connect to Curse CDN: " + e.Message, "FTBUtils.InitializeDLServers()", "ERROR");
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

                    dynamic curseBalanceJSON = JsonConvert.DeserializeObject(curseCDNBalanceJSONString);
                    // INFO: There is also a "minimumLauncherVersion" json node in the ftblauncher code,
                    // but I don't think it is required.

                    if (curseBalanceJSON.repoSplitCurse != null)
                    {
                        // Not sure what the repoSplitCurse thing is on the servers, 
                        // maybe an indication of server loads?

                        // This function compares repoSplitCurse to a random double between 0.0 and 1.0
                        // to determine what server to use.
                        if (double.Parse((string)curseBalanceJSON.repoSplitCurse, System.Globalization.CultureInfo.InvariantCulture) > r.NextDouble())
                        {
                            DebugConsole.Print("Balance has selected Automatic:CurseCDN.", "FTBUtils.InitializeDLServers()");
                            FTBLocations.SetMasterDownloadRepo(FTBLocations.CurseCDN);
                        }
                        else
                        {
                            DebugConsole.Print("Balance has selected Automatic:CreeperRepo.", "FTBUtils.InitializeDLServers()");
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

                List<string> downloadMirrorList = new List<string>();

                response = null;
                status = null;
                // Contact CreeperRepo for edges.json
                HttpWebRequest creeperRepoRequest = WebRequest.Create(FTBLocations.CreeperRepo + "/edges.json") as HttpWebRequest;
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
                HttpWebRequest curseRequest = WebRequest.Create(FTBLocations.CurseCDN + "/edges.json") as HttpWebRequest;
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
                int randomMirrorIndex = new Random().Next(downloadMirrorList.Count);
                FTBLocations.DownloadServer = downloadMirrorList[randomMirrorIndex];
                DebugConsole.Print("InitializeDLServers() has selected " + FTBLocations.DownloadServer + " as mirror.", "FTBUtils.InitializeDLServers()");
                return true;
            }

            // No internet connectivity, so obviously can't find download servers!
            return false;
        }
    }
}
