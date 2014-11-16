using Ionic.Zip;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Helpers;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Xml;
using System.Xml.Linq;



namespace MinecraftLaunchLibrary
{
    public struct downloadVariables
    {
        public string mcVersion;
        public string mcInstallDir;
        public bool ValidateFiles;
    }

    public class startGameVariables
    {
        public string Version { get; set; }
        public string InstallDir { get; set; }
        public string MCLibraryArguments { get; set; }
        public string MainClass { get; set; }
        public string StartingArguments { get; set; }
        public string JavaArguments { get; set; }

        public string LastUsedProfile { get; set; }
        public bool AutoBackupWorld { get; set; }
        public dynamic mcVersionDynamic { get; set; }

        #region Authentication Variables
        public string AccessToken { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string userType { get; set; }
        public string UUID { get; set; }
        #endregion
    }

    public enum startMinecraftReturnCode
    {
        StartedMinecraft,
        CouldNotLocateJava
    }

    public struct startGameReturn
    {
        public ProcessStartInfo StartInfo;
        public Process MinecraftProcess;
        public startMinecraftReturnCode ReturnCode;
        public string ErrorInfo;
        public string LaunchParameters;
    }

    public enum DownloadUpdateStage
    {
        DownloadingGenericFile,
        DownloadingLibrary,
        ExtractingNativeLibrary,
        DownloadingMinecraftJar,
        DownloadingAsset,
        CompletedDownload
    }

    public struct DownloadUpdateEventArgs
    {
        public string CurrentFile { get; set; }
        public DownloadUpdateStage Stage { get; set; }
        public string MinecraftVersion { get; set; }
    }

    public static class MinecraftUtils
    {
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool SetWindowText(IntPtr hwnd, String lpString);

        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hwnd, int message, int wParam, IntPtr lParam);

        private const int WM_SETICON = 0x80;
        private const int ICON_SMALL = 0;
        private const int ICON_BIG = 1;

        private class WebClientNoKeepAlive : WebClient
        {
            // Standard WebClient, just with keep-alive set to false
            public new WebRequest GetWebRequest(System.Uri address)
            {
                var request = base.GetWebRequest(address);
                if (request is HttpWebRequest)
                {
                    ((HttpWebRequest)request).KeepAlive = false;
                }

                return request;
            }
        }

        public delegate void DownloadUpdateEventHandler(DownloadUpdateEventArgs e);

        public static event DownloadUpdateEventHandler DownloadUpdateEvent;

        public static bool authenticateMinecraft(ref startGameVariables sGV, out string returnString)
        {
            returnString = "success"; // This will be changed when there is an error in the code
            bool authenticationSuccess = true;
            try
            {
                var responsePayload = "";

                /* Code from AtomLauncher */
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create("https://authserver.mojang.com/authenticate"); //Start WebRequest
                request.Method = "POST";                                                                //Method type, POST
                string json = Newtonsoft.Json.JsonConvert.SerializeObject(new                           //Object to Upload
                {
                    agent = new                 // optional /                                           //This seems to be required for minecraft despite them saying its optional.
                    {                           //          /
                        name = "Minecraft",     // -------- / So far this is the only encountered value
                        version = 1             // -------- / This number might be increased by the vanilla client in the future
                    },                          //          /
                    username = sGV.Username,   // Can be an email address or player name for unmigrated accounts
                    password = sGV.Password
                    //clientToken = "TOKEN"     // Client Identifier: optional
                });
                byte[] uploadBytes = Encoding.UTF8.GetBytes(json);                                      //Convert UploadObject to ByteArray
                request.ContentType = "application/json";                                               //Set Client Header ContentType to "application/json"
                request.ContentLength = uploadBytes.Length;                                             //Set Client Header ContentLength to size of upload
                using (Stream dataStream = request.GetRequestStream())                                  //Start/Close Upload
                {
                    dataStream.Write(uploadBytes, 0, uploadBytes.Length);                               //Upload the ByteArray
                }
                using (WebResponse response = request.GetResponse())                                    //Start/Close Download
                {
                    using (Stream dataStream = response.GetResponseStream())                            //Start/Close Download Content
                    {
                        using (StreamReader reader = new StreamReader(dataStream))                      //Start/Close Reading the Stream
                        {
                            responsePayload = reader.ReadToEnd();                                       //Save Downloaded Content
                        }
                    }
                }
                dynamic responseJson = JsonConvert.DeserializeObject(responsePayload);                  //Convert string to dynamic josn object
                if (responseJson.accessToken != null)                                                   //Detect if this is an error Payload
                {
                    sGV.AccessToken = responseJson.accessToken;                                           //Assign Access Token
                    //mcClientToken = responseJson.clientToken;                                           //Assign Client Token
                    if (responseJson.selectedProfile.id != null)                                        //Detect if this is an error Payload
                    {
                        sGV.UUID = responseJson.selectedProfile.id;                                       //Assign User ID
                        sGV.Username = responseJson.selectedProfile.name;                                 //Assign Selected Profile Name
                        if (responseJson.selectedProfile.legacy == "true")
                        {
                            sGV.userType = "legacy";
                        }
                        else
                        {
                            sGV.userType = "mojang";
                        }
                    }
                    else
                    {
                        returnString = "Error: WebPayLoad: Missing UUID and Username";
                        authenticationSuccess = false;
                    }
                }
                else if (responseJson.errorMessage != null)
                {
                    returnString = "Error: WebPayLoad: " + responseJson.errorMessage;
                    authenticationSuccess = false;
                }
                else
                {
                    returnString = "Error: WebPayLoad: Had an error and the payload was empty.";
                    authenticationSuccess = false;
                }
            }
            catch (System.Net.WebException startGameException)
            {
                if (startGameException.Response != null && (int)((HttpWebResponse)startGameException.Response).StatusCode == 403 && ((HttpWebResponse)startGameException.Response).StatusDescription == "Forbidden")
                {
                    // Invalid credentials
                    returnString = "Invalid credentials. Please check your username and password. For Mojang accounts, use your email as username.";
                }
                else
                {
                    // Catches any other exceptions that may occur
                    returnString = "There was an error. Check your username + password. Exception: " + startGameException;
                }
                authenticationSuccess = false;
            }
            return authenticationSuccess;
        }

        /// <summary>
        /// Starts the game/modpack, providing that the proper values have already been determined
        /// (such as authToken, versionInfo, etc.). It also waits until Minecraft stops and returns
        /// what happened.
        /// </summary>
        /// <returns>Returns the Minecraft java process.</returns>
        public static startGameReturn Start(startGameVariables sGV)
        {
            string installPath = "";

            // Begin to set up Minecraft java process
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;

            #region Find Java installation
            string environmentPath = Environment.GetEnvironmentVariable("JAVA_HOME");
            if (!string.IsNullOrEmpty(environmentPath))
            {
                installPath = environmentPath;
            }

            string javaKey = "SOFTWARE\\JavaSoft\\Java Runtime Environment\\";
            using (Microsoft.Win32.RegistryKey rk = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(javaKey))
            {
                string currentVersion = rk.GetValue("CurrentVersion").ToString();
                using (Microsoft.Win32.RegistryKey key = rk.OpenSubKey(currentVersion))
                {
                    installPath = key.GetValue("JavaHome").ToString();
                }
            }

            string filePath = System.IO.Path.Combine(installPath, "bin\\javaw.exe");
            if (System.IO.File.Exists(filePath))
            {
                startInfo.FileName = filePath;
            }
            else
            {
                return new startGameReturn { StartInfo = null, MinecraftProcess = null, ErrorInfo = "Could not locate Java executable.", ReturnCode = startMinecraftReturnCode.CouldNotLocateJava };
            }
            #endregion

            string pArguments = "-XX:HeapDumpPath=MojangTricksIntelDriversForPerformance_javaw.exe_minecraft.exe.heapdump " + sGV.JavaArguments + " -Djava.library.path=\""
            + sGV.InstallDir + @"\.minecraft\versions\"
            + sGV.Version + @"\" + sGV.Version + "-natives\" -cp "
            + sGV.MCLibraryArguments + ";\""
            + sGV.InstallDir + @"\.minecraft\versions\" + sGV.Version + @"\" + sGV.Version + ".jar\" " + sGV.MainClass + " " + sGV.StartingArguments;
            startInfo.Arguments = pArguments.Replace("%mcInstallDir%", sGV.InstallDir);
            startInfo.WorkingDirectory = sGV.InstallDir;

            

            // Start Minecraft and return
            Process mcProc = Process.Start(startInfo);
            return new startGameReturn { MinecraftProcess = mcProc, StartInfo = startInfo, ReturnCode = startMinecraftReturnCode.StartedMinecraft, ErrorInfo = "", LaunchParameters = startInfo.Arguments };
        }        
        ///
        private static int downloadFile(string downloadLocation, string saveLocation, bool validateFiles, DownloadUpdateEventArgs downloadEventArgs, long specifiedFileSize = new long())
        {
            // return code 0 = downloaded file
            // return code 1 = skipped file
            WebClientNoKeepAlive webClient = new WebClientNoKeepAlive();
            FileInfo savedFile = new FileInfo(saveLocation);
            int returnValue = 0;

            // Notify the user that we are downloading
            if (DownloadUpdateEvent != null)
                DownloadUpdateEvent(downloadEventArgs);

            Thread.Sleep(20000);

            if (validateFiles == true)
            {
                if (specifiedFileSize != default(long))
                {
                    // if the file size is specified, compare the two filesizes
                    if (savedFile.Length != specifiedFileSize)
                    {
                        // If the file sizes don't match, redownload the file
                        webClient.DownloadFile(downloadLocation, saveLocation);
                        returnValue = 0;
                    }
                    else
                    {
                        returnValue = 1;
                    }
                }
                else
                {
                    // If there is no specified file size, simply revalidate the file by redownloading it
                    webClient.DownloadFile(downloadLocation, saveLocation);
                    returnValue = 0;
                }
            }
            else
            {

                // Downloads the file, but only if the web file is newer
                var HEADrequest = (HttpWebRequest)WebRequest.Create(downloadLocation);
                HEADrequest.Method = "HEAD";
                HEADrequest.KeepAlive = false;

                var HEADresponse = (HttpWebResponse)HEADrequest.GetResponse();

                if (HEADresponse.LastModified > savedFile.LastWriteTime)
                {
                    webClient.DownloadFile(downloadLocation, saveLocation);
                    returnValue = 0;
                }

                // Close connection to server, prevents timeouts because of a concurrent thread limit
                HEADresponse.Close();
                returnValue = 1;
            }

            return returnValue;
        }

        public static string DownloadGame(downloadVariables downloaderInfo)
        {
            try
            {
                string mcAssetsVersion = "";
                bool validateFiles = downloaderInfo.ValidateFiles;
                string mcMainClass = "";
                string startingArguments = "";
                string minecraftArguments = "";
                string mcVersion = downloaderInfo.mcVersion;
                string mcInstallDir = downloaderInfo.mcInstallDir;
                string mcVersionJSONString;

                // Download versions.json to get version list
                downloadFile("http://s3.amazonaws.com/Minecraft.Download/versions/versions.json", "versions.json", validateFiles, new DownloadUpdateEventArgs { CurrentFile = "versions.json", MinecraftVersion = mcVersion, Stage = DownloadUpdateStage.DownloadingGenericFile });
                using (StreamReader streamReader = new StreamReader("versions.json", Encoding.UTF8))
                {
                    mcVersionJSONString = streamReader.ReadToEnd();
                }

                var mcVersionList = Json.Decode(mcVersionJSONString);

                // DOWNLOAD ALL MINECRAFT FILES
                string executableFilePath = System.Windows.Application.ResourceAssembly.Location;
                if (mcVersion == "latest-release")
                {
                    mcVersion = mcVersionList.latest.release;
                }
                if (mcVersion == "latest-snapshot")
                {
                    mcVersion = mcVersionList.latest.snapshot;
                }

                // Create directory to store JSON
                if (!System.IO.Directory.Exists(System.Environment.CurrentDirectory + "\\.mcl\\versions\\"))
                {
                    System.IO.Directory.CreateDirectory(System.Environment.CurrentDirectory + "\\.mcl\\versions\\");
                }

                try
                {
                    // Try to download *version*.json, with detailed information on how to run that version of minecraft
                    downloadFile("http://s3.amazonaws.com/Minecraft.Download/versions/" + mcVersion + "/" + mcVersion + ".json",
                        System.Environment.CurrentDirectory + "\\.mcl\\versions\\" + mcVersion + ".json", validateFiles,
                        new DownloadUpdateEventArgs
                        {
                            CurrentFile = mcVersion + ".json",
                            MinecraftVersion = mcVersion,
                            Stage = DownloadUpdateStage.DownloadingGenericFile
                        });
                }
                catch (WebException w)
                {
                    if (w.Status == WebExceptionStatus.ProtocolError && (int)((HttpWebResponse)w.Response).StatusCode == 403) // Note: Mojang download servers give 403 error when file is not found
                    {
                        // If the file wasn't found on the server, raise an error
                        return "Download error. The information for Minecraft " + mcVersion + " could not be found on the server.";
                    }
                    else
                    {
                        throw new WebException("Error during download of " + mcVersion + "/" + mcVersion + ".json.", w);
                    }
                }
                string versionInformation;
                using (StreamReader streamReader = new StreamReader(System.Environment.CurrentDirectory + "\\.mcl\\versions\\" + mcVersion + ".json", Encoding.UTF8))
                {
                    versionInformation = streamReader.ReadToEnd();
                }

                Dictionary<string, object> versionInformationDictionary = JsonConvert.DeserializeObject<Dictionary<string, object>>(Convert.ToString(versionInformation));
                mcMainClass = versionInformationDictionary["mainClass"].ToString();
                startingArguments = versionInformationDictionary["minecraftArguments"].ToString();
                if (versionInformationDictionary.ContainsKey("assets"))
                {
                    mcAssetsVersion = versionInformationDictionary["assets"].ToString();
                }
                else
                {
                    mcAssetsVersion = "legacy";
                }

                #region Download + extract libraries

                string mcLibraryValues = versionInformationDictionary["libraries"].ToString();

                var checkMcLibraries = new JavaScriptSerializer().Deserialize<IEnumerable<Library>>(mcLibraryValues);
                // Downloads libraries if they are not present
                List<string> downloadedLibraryLocations = new List<string>();
                List<downloadLibraryClass> mcDownloadLibraries = new List<downloadLibraryClass>();

                foreach (var library in checkMcLibraries)
                {
                    string libraryLocation;
                    bool libraryDownloaded = DownloadLibrary(library, mcInstallDir, validateFiles, mcVersion, out libraryLocation);
                    if (libraryDownloaded == true)
                    {
                        string currentDownloadType = "";
                        if (library.natives != null)
                            currentDownloadType = library.natives.windows;
                        downloadedLibraryLocations.Add("%mcInstallDir%" + libraryLocation);
                    }
                }
                /*
                foreach (var item in mcDownloadLibraries)
                {
                    // Download the libraries and extract them if needed
                    string currentDownloadType = "";
                    string[] libraryDownloadURL = new string[2];
                    string pathString = "";
                    string libraryDownloadPath = "";
                    string librarySavePath = "";

                    libraryDownloadURL = item.Name.Split(':');
                    libraryDownloadPath = "https://libraries.minecraft.net/" + libraryDownloadURL[0].Replace('.', '/') + "/" + libraryDownloadURL[1] + "/" + libraryDownloadURL[2] + "/" + libraryDownloadURL[1] + "-" + libraryDownloadURL[2];
                    librarySavePath = mcInstallDir + @"\.minecraft\libraries\" + libraryDownloadURL[0].Replace('.', '\\') + "\\" + libraryDownloadURL[1] + "\\" + libraryDownloadURL[2] + "\\" + libraryDownloadURL[1] + "-" + libraryDownloadURL[2];
                    libraryLocation = @"\.minecraft\libraries\" + libraryDownloadURL[0].Replace('.', '\\') + "\\" + libraryDownloadURL[1] + "\\" + libraryDownloadURL[2] + "\\" + libraryDownloadURL[1] + "-" + libraryDownloadURL[2];
                    if (item.DownloadType != "")
                    {
                        currentDownloadType = "-" + item.DownloadType;
                    }

                    pathString = mcInstallDir + @"\.minecraft\libraries\" + libraryDownloadURL[0].Replace('.', '\\') + @"\" + libraryDownloadURL[1] + @"\" + libraryDownloadURL[2] + @"\";
                    if (!System.IO.Directory.Exists(pathString))
                    {
                        System.IO.Directory.CreateDirectory(pathString);
                    }

                    // Download minecraft libary jar
                    downloadFile(libraryDownloadPath + currentDownloadType + ".jar", librarySavePath + currentDownloadType + ".jar", validateFiles, "Downloading files for Minecraft " + mcVersion + "... " + libraryDownloadURL[1] + "-" + libraryDownloadURL[2] + currentDownloadType + ".jar", DDialog);

                    // Extract the libary, if it is needed
                    if (item.ExtractNative == true)
                    {
                        DDialog.downloadFileDisplay.Dispatcher.BeginInvoke(
                            (Action)(() => { DDialog.downloadFileDisplay.Text = "Extracting native library for Minecraft " + mcVersion + "... " + libraryDownloadURL[1] + "-" + libraryDownloadURL[2] + currentDownloadType + ".jar"; }));

                        if (!System.IO.Directory.Exists(mcInstallDir + "\\.minecraft\\versions\\" + mcVersion + "\\" + mcVersion + "-natives\\"))
                        {
                            System.IO.Directory.CreateDirectory(mcInstallDir + "\\.minecraft\\versions\\" + mcVersion + "\\" + mcVersion + "-natives\\");
                        }
                        using (ZipFile zip1 = ZipFile.Read(librarySavePath + currentDownloadType + ".jar"))
                        {
                            foreach (ZipEntry zipExtract in zip1)
                            {
                                if (zipExtract.FileName != "META-INF")
                                    zipExtract.Extract(mcInstallDir + "\\.minecraft\\versions\\" + mcVersion + "\\" + mcVersion + "-natives\\", ExtractExistingFileAction.OverwriteSilently);
                            }
                        }
                        if (System.IO.Directory.Exists(mcInstallDir + "\\.minecraft\\versions\\" + mcVersion + "\\" + mcVersion + "-natives\\META-INF\\"))
                        {
                            System.IO.Directory.Delete(mcInstallDir + "\\.minecraft\\versions\\" + mcVersion + "\\" + mcVersion + "-natives\\META-INF\\", true);
                        }
                    }*/



                #endregion

                if (!System.IO.Directory.Exists(mcInstallDir + @"\.minecraft\versions\" + mcVersion + @"\"))
                {
                    // Create version directory if it does not exist. Ex. \.minecraft\versions\1.7.5\
                    System.IO.Directory.CreateDirectory(mcInstallDir + @"\.minecraft\versions\" + mcVersion + @"\");
                }

                // Download minecraft jar
                downloadFile("http://s3.amazonaws.com/Minecraft.Download/versions/" + mcVersion + "/" + mcVersion + ".jar",
                    mcInstallDir + @"\.minecraft\versions\" + mcVersion + @"\" + mcVersion + ".jar",
                    validateFiles,
                    new DownloadUpdateEventArgs
                    {
                        CurrentFile = mcVersion + ".jar",
                        MinecraftVersion = mcVersion,
                        Stage = DownloadUpdateStage.DownloadingMinecraftJar
                    });

                #region Download Assets
                if (!System.IO.Directory.Exists(mcInstallDir + @"\.minecraft\assets\indexes\"))
                {
                    // Create assets/indexes directory if it does not exist
                    System.IO.Directory.CreateDirectory(mcInstallDir + @"\.minecraft\assets\indexes\");
                }

                // Download assets information, *assetversion*.json
                downloadFile("https://s3.amazonaws.com/Minecraft.Download/indexes/" + mcAssetsVersion + ".json",
                    mcInstallDir + @"\.minecraft\assets\indexes\" + mcAssetsVersion + ".json",
                    validateFiles,
                    new DownloadUpdateEventArgs
                    {
                        CurrentFile = mcAssetsVersion + ".json",
                        MinecraftVersion = mcVersion,
                        Stage = DownloadUpdateStage.DownloadingGenericFile
                    });

                string assetInformation;
                using (StreamReader streamReader = new StreamReader(mcInstallDir + @"\.minecraft\assets\indexes\" + mcAssetsVersion + ".json", Encoding.UTF8))
                {
                    assetInformation = streamReader.ReadToEnd();
                }

                Dictionary<string, object> assetInformationDictionary = JsonConvert.DeserializeObject<Dictionary<string, object>>(Convert.ToString(assetInformation));
                string assetJSONString = assetInformationDictionary["objects"].ToString();
                string[] unwantedAssetValues = new string[] { "[", "{", "}", "]", ",", "\"hash\":", "\r\n", " ", ":", "\"size\":" };
                var assetInfo = Json.Decode(assetJSONString);

                foreach (var asset in assetInfo)
                {
                    string assetDirectory = asset.Value.hash.Substring(0, 2);
                    Directory.CreateDirectory(mcInstallDir + @"\.minecraft\assets\objects\" + assetDirectory); // Create asset directory, eg. \.minecraft\assets\objects\eb\
                    int downloadFileReturn = downloadFile("http://resources.download.minecraft.net/" + asset.Value.hash.Substring(0, 2) + "/" + asset.Value.hash,
                        mcInstallDir + @"\.minecraft\assets\objects\" + asset.Value.hash.Substring(0, 2) + @"\" + asset.Value.hash,
                        validateFiles,
                        new DownloadUpdateEventArgs
                        {
                            CurrentFile = asset.Value.hash,
                            MinecraftVersion = mcVersion,
                            Stage = DownloadUpdateStage.DownloadingAsset
                        },
                        Convert.ToInt64(asset.Value.size));
                    if (mcAssetsVersion == "legacy" && downloadFileReturn == 0) // If legacy assets must be copied, and the file was downloaded/updated, then recopy the legacy file
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(mcInstallDir + @"\.minecraft\assets\virtual\legacy\" + asset.Key));
                        System.IO.File.Copy(mcInstallDir + @"\.minecraft\assets\objects\" + asset.Value.hash.Substring(0, 2) + @"\" + asset.Value.hash, mcInstallDir + @"\.minecraft\assets\virtual\legacy\" + asset.Key.Replace('/', '\\'), true);
                    }
                }

                #endregion

                #region Save version information to XML

                string[] minecraftArgumentsArray = downloadedLibraryLocations.ToArray<string>();
                minecraftArguments = String.Join("\";\"", minecraftArgumentsArray);
                minecraftArguments = "\"" + minecraftArguments + "\"";

                bool versionInformationExists = false;

                using (XmlReader reader = XmlReader.Create(mcInstallDir + "\\.mcl\\VersionInformation.xml"))
                {
                    while (reader.Read())
                    {
                        if (reader.IsStartElement() && reader.Name == "version" && reader.GetAttribute("version") == mcVersion)
                        {
                            reader.Close();
                            // Update version information
                            versionInformationExists = true;
                            XmlDocument versionInformationXML = new XmlDocument();
                            versionInformationXML.Load(mcInstallDir + "\\.mcl\\VersionInformation.xml");
                            versionInformationXML.DocumentElement.SelectSingleNode(@"//versions/version[@version='" + mcVersion + "']/mcAssetsVersion").InnerText = mcAssetsVersion;
                            versionInformationXML.DocumentElement.SelectSingleNode(@"//versions/version[@version='" + mcVersion + "']/minecraftLibraryList").InnerText = minecraftArguments;
                            versionInformationXML.DocumentElement.SelectSingleNode(@"//versions/version[@version='" + mcVersion + "']/mainClass").InnerText = mcMainClass;
                            versionInformationXML.DocumentElement.SelectSingleNode(@"//versions/version[@version='" + mcVersion + "']/startingArguments").InnerText = startingArguments;
                            versionInformationXML.DocumentElement.SelectSingleNode(@"//versions/version[@version='" + mcVersion + "']/savedReleaseTime").InnerText = DateTime.Now.ToString();
                            versionInformationXML.Save(mcInstallDir + "\\.mcl\\VersionInformation.xml");
                        }
                    }
                }

                if (versionInformationExists == false)
                {
                    // Add the version into the XML file
                    XDocument doc = XDocument.Load(mcInstallDir + "\\.mcl\\VersionInformation.xml");
                    XElement mcXMLValues = new XElement("version",
                        new XAttribute("version", mcVersion), new XAttribute("type", "MojangVanilla"), // TODO: change MojangVanilla to the modpack type
                        new XElement("mcAssetsVersion", mcAssetsVersion),
                        new XElement("minecraftLibraryList", minecraftArguments),
                        new XElement("mainClass", mcMainClass),
                        new XElement("startingArguments", startingArguments),
                        new XElement("savedReleaseTime", DateTime.Now.ToString()));
                    doc.Root.Add(mcXMLValues);
                    doc.Save(mcInstallDir + "\\.mcl\\VersionInformation.xml");
                }

                // Trigger event for download completion
                if (DownloadUpdateEvent != null)
                    DownloadUpdateEvent(new DownloadUpdateEventArgs
                    {
                        CurrentFile = null,
                        MinecraftVersion = mcVersion,
                        Stage = DownloadUpdateStage.CompletedDownload
                    });

                return "success"; // Return success in downloading
            }
            catch (WebException e)
            {
                // An exception occured
                return "An error occurred while downloading files. " + e.Message;
            }
            #endregion
        }

        /// <summary>
        /// Checks and downloads a single library from the minecraft servers.
        /// Will only download if necessary on that system.
        /// Also, comments. ALL THE COMMENTS EVER.
        /// (is heavily commented)
        /// </summary>
        /// <param name="libraryClass"></param>
        /// <param name="downloadLocation"></param>
        /// <param name="validateFiles"></param>
        /// <param name="DDialog"></param>
        /// <param name="mcVersion"></param>
        private static bool DownloadLibrary(Library libraryClass, string downloadLocation, bool validateFiles, string mcVersion, out string libraryLocation)
        {
            bool addDownload = false;
            bool extractNative = false;
            string downloadType = "";
            libraryLocation = null;

            #region Check to see if the library needs to be downloaded on Windows
            if (libraryClass.rules != null)
            {
                // Check the rules for downloading the library
                // and make sure it can be downloaded on windows
                // if addDownload = true, then download it.
                if (libraryClass.rules[0].action == "allow")
                {
                    // Is allowed on some systems, let's see what systems it is allowed on
                    if (libraryClass.rules[0].os == null)
                    {
                        // It is allowed on all systems, download it
                        addDownload = true;
                    }
                    else if (libraryClass.rules[0].os.name == "windows")
                    {
                        // It is allowed on "windows", download it
                        addDownload = true;
                    }
                    else
                    {
                        // If neither of these prove true, then don't download the library, as it is not meant to be downloaded on a windows system
                        addDownload = false;
                    }
                }
                if (libraryClass.rules.Count >= 2)
                {
                    /* There is another rule, such as:
                     *
                     * "rules": [
                     * {
                     *    "action": "allow"
                     *  },
                     *  {
                     *    "action": "disallow", <-- Second rule to be tested
                     *    "os": {
                     *    "name": "osx",
                     *    "version": "^10\\.5\\.\\d$"
                     *  }
                     * }
                     */

                    if (libraryClass.rules[1].action == "disallow")
                    {
                        // The second rule disallows this library on some systems, let's see what systems it's denied on
                        if (libraryClass.rules[1].os.name == "windows")
                        {
                            // "disallowed" on "windows", we should not download this library
                            addDownload = false;
                        }
                        else
                        {
                            // The library is not disallowed on windows, download it
                            addDownload = true;
                        }
                    }
                }
            }
            else
            {
                // No rules are specified, we should download it
                addDownload = true;
            }
            #endregion

            // Get the architecture, if it is required
            if (libraryClass.natives != null)
            {
                // Architecture may be "natives-windows",
                // or maybe "natives-windows-{$arch}" if there is a
                // specific architecture.
                downloadType = libraryClass.natives.windows;
            }

            // See if it needs to be extracted
            if (libraryClass.extract != null)
                extractNative = true;

            // Download the library, if everything checks out
            if (addDownload == true)
            {
                if (downloadType.Contains("${arch}"))
                {
                    // Replace the ${arch} in the download type with the actual architecture of the system,
                    // if ${arch} is found
                    if (System.Environment.Is64BitOperatingSystem == true)
                    {
                        // Processor type is x64, download the 64 bit library
                        downloadType = downloadType.Replace("${arch}", "64");
                    }
                    else
                    {
                        // Processor type is x32, download the 32 bit library
                        downloadType = downloadType.Replace("${arch}", "32");
                    }
                }

                string[] libraryDownloadURL = libraryClass.name.Split(':');

                // The jar name of the library. Ex. "lwjgl-2.9.1-nightly-20131120.jar"
                string libraryJarName = libraryDownloadURL[1] + "-" + libraryDownloadURL[2] + downloadType + ".jar";

                // The URL of the library to download it from. Ex. 
                // https://libraries.minecraft.net/org/lwjgl/lwjgl/lwjgl-platform/2.9.1-nightly-20130708-debug3/lwjgl-platform-2.9.1-nightly-20130708-debug3-natives-osx.jar
                string libraryDownloadPath = "https://libraries.minecraft.net/" + libraryDownloadURL[0].Replace('.', '/') + "/" + libraryDownloadURL[1] + "/" + libraryDownloadURL[2] + "/" + libraryDownloadURL[1] + "-" + libraryDownloadURL[2];
                
                // Where to save the library. Ex.
                // C:\MinecraftCL\.minecraft\libraries\org\lwjgl\lwjgl\lwjgl\2.9.1-nightly-20131120
                string librarySavePath = downloadLocation + @"\.minecraft\libraries\" + libraryDownloadURL[0].Replace('.', '\\') + "\\" + libraryDownloadURL[1] + "\\" + libraryDownloadURL[2] + "\\" + libraryDownloadURL[1] + "-" + libraryDownloadURL[2];

                // The full path of the library starting from the .minecraft folder, once saved.
                // Ex. "\.minecraft\libraries\org\lwjgl\lwjgl\lwjgl\2.9.1-nightly-20131120\lwjgl-2.9.1-nightly-20131120.jar"
                libraryLocation = @"\.minecraft\libraries\" + libraryDownloadURL[0].Replace('.', '\\') + "\\" + libraryDownloadURL[1] + "\\" + libraryDownloadURL[2] + "\\" + libraryDownloadURL[1] + "-" + libraryDownloadURL[2] + downloadType + ".jar";
                
                // Create library folder path if it does not exist.
                Directory.CreateDirectory(Path.GetDirectoryName(librarySavePath));

                // Download the library.
                downloadFile(libraryDownloadPath + downloadType + ".jar", 
                    librarySavePath + downloadType + ".jar", 
                    validateFiles,
                    new DownloadUpdateEventArgs
                    {
                        CurrentFile = libraryJarName,
                        MinecraftVersion = mcVersion,
                        Stage = DownloadUpdateStage.DownloadingLibrary
                    });
                
                // Extract library if needed to natives folder
                if (extractNative == true)
                {
                    // Trigger download progress update event for extracting natives
                    if (DownloadUpdateEvent != null)
                        DownloadUpdateEvent(new DownloadUpdateEventArgs
                        { 
                            MinecraftVersion = mcVersion, 
                            CurrentFile = libraryJarName, 
                            Stage = DownloadUpdateStage.ExtractingNativeLibrary 
                        });

                    if (!System.IO.Directory.Exists(downloadLocation + "\\.minecraft\\versions\\" + mcVersion + "\\" + mcVersion + "-natives\\"))
                    {
                        // If the natives folder does not exist (eg. \.minecraft\versions\1.7.10\1.7.10-natives\),
                        // create it
                        System.IO.Directory.CreateDirectory(downloadLocation + "\\.minecraft\\versions\\" + mcVersion + "\\" + mcVersion + "-natives\\");
                    }

                    using (ZipFile libraryJar = ZipFile.Read(librarySavePath + downloadType + ".jar"))
                    {
                        // Extract all files in the library jar (except for META-INF)
                        foreach (ZipEntry entry in libraryJar)
                        {
                            if (entry.FileName != "META-INF")
                                // Do not extract the META-INF folder, it is just metadata for the jar file.
                                // Extract everything else.
                                entry.Extract(downloadLocation + "\\.minecraft\\versions\\" + mcVersion + "\\" + mcVersion + "-natives\\", ExtractExistingFileAction.OverwriteSilently);
                        }
                    }
                }
            }

            return addDownload;
        }

        #region JSON Classes
        private class Os
        {
            public string name { get; set; }
            public string version { get; set; }
        }

        private class Rule
        {
            public string action { get; set; }
            public Os os { get; set; }
        }

        private class Natives
        {
            public string linux { get; set; }
            public string windows { get; set; }
            public string osx { get; set; }
        }

        private class Extract
        {
            public List<string> exclude { get; set; }
        }

        private class Library
        {
            public string name { get; set; }
            public List<Rule> rules { get; set; }
            public Natives natives { get; set; }
            public Extract extract { get; set; }
        }

        private class Asset
        {
            public string name { get; set; }
            public string hash { get; set; }
            public string size { get; set; }
        }

        #endregion
    }

    #region Information to Download Library Class
    public class downloadLibraryClass
    {
        public string Name { get; set; }
        public string DownloadType { get; set; }
        public bool ExtractNative { get; set; }
    }
    #endregion

    #region JSON Classes
    public class Os
    {
        public string name { get; set; }
        public string version { get; set; }
    }

    public class Rule
    {
        public string action { get; set; }
        public Os os { get; set; }
    }

    public class Natives
    {
        public string linux { get; set; }
        public string windows { get; set; }
        public string osx { get; set; }
    }

    public class Extract
    {
        public List<string> exclude { get; set; }
    }

    public class Library
    {
        public string name { get; set; }
        public List<Rule> rules { get; set; }
        public Natives natives { get; set; }
        public Extract extract { get; set; }
    }

    public class Asset
    {
        public string name { get; set; }
        public string hash { get; set; }
        public string size { get; set; }
    }

    #endregion
}
