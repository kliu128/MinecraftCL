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
using System.Threading.Tasks;
using System.Web.Helpers;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Xml;
using System.Xml.Linq;



namespace MinecraftCL
{


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
        CouldNotLocateJava,
        MinecraftError
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

        public struct startGameReturn
        {
            public startMinecraftReturnCode ReturnCode;
            public ProcessStartInfo ProcessStartInfo;
            public string Error;
        }

        public static bool isRunning { get; set; }

        public static bool authenticateMinecraft(ref startGameVariables sGV, out string returnString)
        {
            returnString = "success"; // This will be changed when there is an error in the code
            bool authenticationSuccess = true;
            if (Globals.HasInternetConnectivity == true)
            {
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
            else
            {
                // No internet connection available
                returnString = "Warning: No internet connection available.";
                sGV.AccessToken = "{}";
                sGV.UUID = "{}";
                sGV.userType = "legacy";
                return true;
            }
        }

        public static startGameReturn Start(startGameVariables sGV)
        {
            

            isRunning = true;
            string installPath = "";
            startMinecraftReturnCode returnCode = startMinecraftReturnCode.StartedMinecraft;
            string mcError = "";

            #region Backup Minecraft worlds if specified
            if (sGV.AutoBackupWorld == true && Directory.Exists(sGV.InstallDir + @"\.minecraft\saves\"))
            {
                MessageWindow backupNotificationBox = new MessageWindow();
                backupNotificationBox.messageText.Text = "Backing up worlds before starting Minecraft...";
                backupNotificationBox.closeTimeoutMilliseconds = -1;
                backupNotificationBox.Show();
                backupNotificationBox.Activate();

                string currentDateTime = DateTime.Now.Hour + "." + DateTime.Now.Minute + "." + DateTime.Now.Millisecond + " - " + DateTime.Now.ToString("MMMM") + " " + DateTime.Now.Day + ", " + DateTime.Now.Year;
                DirectoryCopy.CopyRecursive(sGV.InstallDir + @"\.minecraft\saves\", sGV.InstallDir + "\\Backups\\" + currentDateTime + "\\");

                backupNotificationBox.Close();
            }
            #endregion

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
                returnCode = startMinecraftReturnCode.CouldNotLocateJava;
            }
            #endregion

            string pArguments = "-XX:HeapDumpPath=MojangTricksIntelDriversForPerformance_javaw.exe_minecraft.exe.heapdump " + sGV.JavaArguments + " -Djava.library.path=\""
            + sGV.InstallDir + @"\.minecraft\versions\"
            + sGV.Version + @"\" + sGV.Version + "-natives\" -cp "
            + sGV.MCLibraryArguments + ";\""
            + sGV.InstallDir + @"\.minecraft\versions\" + sGV.Version + @"\" + sGV.Version + ".jar\" " + sGV.MainClass + " " + sGV.StartingArguments;
            startInfo.Arguments = pArguments.Replace("%mcInstallDir%", sGV.InstallDir);
            startInfo.WorkingDirectory = sGV.InstallDir;

            #region Save Settings (Username, Password, Last used profile)
            XmlDocument xDoc = new XmlDocument();
            xDoc.Load(System.Environment.CurrentDirectory + @"\.mcl\MinecraftCLSettings.xml");
            XmlElement xDocRoot = xDoc.DocumentElement;
            // Save username
            if (xDoc.SelectSingleNode("/settings/Username") == null)
            {
                XmlElement usernameElement = xDoc.CreateElement("Username");
                usernameElement.InnerText = sGV.Username;
                xDocRoot.AppendChild(usernameElement);
            }
            else
                xDoc.SelectSingleNode("/settings/Username").InnerText = sGV.Username;

            // Save password
            if (xDoc.SelectSingleNode("/settings/Password") == null)
            {
                XmlElement passwordElement = xDoc.CreateElement("Password");
                passwordElement.InnerText = StringCipher.Encrypt(sGV.Password, "minecraftCLNoOneWillGuessThis");
                xDocRoot.AppendChild(passwordElement);
            }
            else
                xDoc.SelectSingleNode("/settings/Password").InnerText = StringCipher.Encrypt(sGV.Password, "minecraftCLNoOneWillGuessThis");

            // Save last used profile
            if (xDoc.SelectSingleNode("/settings/LastUsedProfile") == null)
            {
                XmlElement lastUsedProfileElement = xDoc.CreateElement("LastUsedProfile");
                lastUsedProfileElement.InnerText = sGV.LastUsedProfile;
                xDocRoot.AppendChild(lastUsedProfileElement);
            }
            else
                xDoc.SelectSingleNode("/settings/LastUsedProfile").InnerText = sGV.LastUsedProfile;

            xDoc.Save(System.Environment.CurrentDirectory + "//.mcl//MinecraftCLSettings.xml");
            #endregion

            // Hide the window and catch any errors
            using (Process process = Process.Start(startInfo))
            {
                // Easter egg!
                if (DateTime.Now.Month == 11 && DateTime.Now.Day == 13)
                {
                    process.WaitForInputIdle();
                    DebugConsole.Print("Easter Egg initialized!", "Minecraft.Start()", "WHEE");
                    SetWindowText(process.MainWindowHandle, "Happy Birthday!");
                    Icon cakeLarge = new Icon(Assembly.GetExecutingAssembly().GetManifestResourceStream("MinecraftCL.Resources.Minecraft_Cake_32.ico"));
                    Icon cakeSmall = new Icon(Assembly.GetExecutingAssembly().GetManifestResourceStream("MinecraftCL.Resources.Minecraft_Cake_16.ico"));
                    SendMessage(process.MainWindowHandle, WM_SETICON, ICON_BIG, cakeLarge.Handle);
                    SendMessage(process.MainWindowHandle, WM_SETICON, ICON_SMALL, cakeSmall.Handle);
                }

                string javaOutput = process.StandardOutput.ReadToEnd();
                string javaError = process.StandardError.ReadToEnd();
                int exitCode = process.ExitCode;
                process.WaitForExit();  // This will quietly wait until minecraft has closed
                if (exitCode != 0)      // An exit code other than 0 is an error
                {
                    if (javaOutput.Contains("---- Minecraft Crash Report ----"))
                    {
                        // This was an official minecraft crash, complete with crash report
                        mcError = javaOutput.Substring(javaOutput.LastIndexOf("---- Minecraft Crash Report ----") + 1);
                    }
                        // Something interesting: the other crashes aren't actually caught by the official Minecraft launcher
                    else if (javaError == "")
                    {
                        mcError = javaOutput;
                    }
                    else
                    {
                        mcError = javaError;
                    }
                    returnCode = startMinecraftReturnCode.MinecraftError;
                }
            }
            // By now, minecraft has closed, either peacefully or with an error.
            // Return success/failure
            isRunning = false;
            return new startGameReturn { ReturnCode = returnCode, ProcessStartInfo = startInfo, Error = mcError };
        }

        public static bool getVersionInformation(ref startGameVariables sGV, out string errorInformation)
        {
            // Change out "latest-release" and "latest-snapshot" to the actual versions
            if (sGV.mcVersionDynamic != null)
            {
                if (sGV.Version == "latest-release")
                {
                    sGV.Version = sGV.mcVersionDynamic.latest.release;
                }
                else if (sGV.Version == "latest-snapshot")
                {
                    sGV.Version = sGV.mcVersionDynamic.latest.snapshot;
                }
            }
            else if (sGV.Version == "latest-release" || sGV.Version == "latest-snapshot")
            {
                // System is not connected to the internet/mojang servers and user is trying to launch a
                // "latest-*****" version, return error.
                errorInformation = "Cannot find the latest version when there is no internet connection.";
                return false;
            }

            bool versionExists = false;
            string mcAssetsVersion = "";
            string startingArguments = "";

            XmlDocument doc = new XmlDocument();
            doc.Load(System.Environment.CurrentDirectory + "\\.mcl\\VersionInformation.xml");
            foreach (XmlNode node in doc.SelectNodes("//version[@version]"))
            {
                if (node.Attributes["version"].Value == sGV.Version)
                {
                    if (sGV.mcVersionDynamic != null)
                    {
                        // Search through mcVersionDynamic to check version updated time if it was able to be
                        // downloaded.
                        foreach (var item in sGV.mcVersionDynamic.versions)
                        {
                            if (item.id == sGV.Version)
                            {
                                DateTime serverReleaseTime = DateTime.ParseExact(item.releaseTime, "yyyy'-'MM'-'dd'T'HH:mm:sszzz", null);
                                DateTime savedDownloadTime = DateTime.Parse(doc.SelectSingleNode(@"//versions/version[@version='" + sGV.Version + "']/savedReleaseTime").InnerText);
                                if (serverReleaseTime > savedDownloadTime)
                                {
                                    errorInformation = "";
                                    return false;
                                }
                            }
                        }
                    }
                    versionExists = true;
                    mcAssetsVersion = doc.SelectSingleNode(@"//versions/version[@version='" + sGV.Version + "']/mcAssetsVersion").InnerText;
                    sGV.MCLibraryArguments = doc.SelectSingleNode(@"//versions/version[@version='" + sGV.Version + "']/minecraftLibraryList").InnerText;
                    sGV.MainClass = doc.SelectSingleNode(@"//versions/version[@version='" + sGV.Version + "']/mainClass").InnerText;
                    startingArguments = doc.SelectSingleNode(@"//versions/version[@version='" + sGV.Version + "']/startingArguments").InnerText;
                    // Replace all of the various variables used with their actual values
                    // TODO: Any better way to do this?
                    startingArguments = startingArguments.Replace("${auth_player_name}", sGV.Username);
                    startingArguments = startingArguments.Replace("${version_name}", sGV.Version);
                    startingArguments = startingArguments.Replace("${game_directory}", "\"" + sGV.InstallDir + "\\.minecraft\"");
                    startingArguments = startingArguments.Replace("${assets_root}", "\"" + sGV.InstallDir + "\\.minecraft\\assets\""); // For 1.7 and above
                    startingArguments = startingArguments.Replace("${game_assets}", "\"" + sGV.InstallDir + "\\.minecraft\\assets\\virtual\\legacy\""); // For legacy versions 1.6.4 or below
                    startingArguments = startingArguments.Replace("${assets_index_name}", mcAssetsVersion);
                    startingArguments = startingArguments.Replace("${auth_uuid}", sGV.UUID);
                    startingArguments = startingArguments.Replace("${auth_access_token}", sGV.AccessToken);
                    startingArguments = startingArguments.Replace("${user_properties}", "{}"); // Yeah, no twitch here...
                    startingArguments = startingArguments.Replace("${user_type}", sGV.userType);
                    startingArguments = startingArguments.Replace("${auth_session}", sGV.AccessToken);
                    sGV.StartingArguments = startingArguments;
                }
            }
            errorInformation = "";
            return versionExists;
        }

        private static int downloadFile(string downloadLocation, string saveLocation, bool validateFiles, string messageToDisplay, DownloadDialog downloadDialog, long specifiedFileSize = new long())
        {
            // return code 0 = downloaded file
            // return code 1 = skipped file
            WebClientNoKeepAlive webClient = new WebClientNoKeepAlive();
            FileInfo savedFile = new FileInfo(saveLocation);
            int returnValue = 0;

            // Notify the user that we are downloading
            downloadDialog.downloadFileDisplay.Dispatcher.BeginInvoke(
                (Action)(() => { downloadDialog.downloadFileDisplay.Text = messageToDisplay; }));

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
            if (Globals.HasInternetConnectivity == true)
            {
                string mcAssetsVersion = "";
                bool validateFiles = downloaderInfo.ValidateFiles;
                string mcMainClass = "";
                string startingArguments = "";
                string minecraftArguments = "";
                string mcVersion = downloaderInfo.mcVersion;
                string mcInstallDir = downloaderInfo.mcInstallDir;
                string mcVersionJSONString;
                DownloadDialog DDialog = downloaderInfo.DownloadDialog;

                // Download versions.json to get version list
                downloadFile("http://s3.amazonaws.com/Minecraft.Download/versions/versions.json", "versions.json", validateFiles, "Downloading files for Minecraft " + mcVersion + ".", DDialog);
                using (StreamReader streamReader = new StreamReader("versions.json", Encoding.UTF8))
                {
                    mcVersionJSONString = streamReader.ReadToEnd();
                }

                var mcVersionList = Json.Decode(mcVersionJSONString);

                // DOWNLOAD ALL MINECRAFT FILES
                string executableFilePath = System.Windows.Application.ResourceAssembly.Location;
                var configFile = ConfigurationManager.OpenExeConfiguration(executableFilePath);
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
                    downloadFile("http://s3.amazonaws.com/Minecraft.Download/versions/" + mcVersion + "/" + mcVersion + ".json", System.Environment.CurrentDirectory + "\\.mcl\\versions\\" + mcVersion + ".json", validateFiles, "Downloading files for Minecraft " + mcVersion + "..." + mcVersion + ".json", DDialog);
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
                        throw new WebException("Error during download of *mcVersion*/*mcVersion*.json.", w);
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
                string libraryLocation = "";
                List<string> downloadedLibraryLocations = new List<string>();
                List<downloadLibraryClass> mcDownloadLibraries = new List<downloadLibraryClass>();

                foreach (var check in checkMcLibraries)
                {
                    bool addDownload = false;
                    bool extractNative = false;
                    string downloadType = "";
                    if (check.rules != null)
                    {
                        if (check.rules[0].action == "allow")
                        {
                            if (check.rules[0].os == null) // Is this library allowed on all systems?
                            {
                                addDownload = true;
                            }
                            else if (check.rules[0].os.name == "windows") // If not, is this library allowed on windows?
                            {
                                addDownload = true;
                            }
                            else
                            {
                                // If neither of these prove true, then don't download the library, as it is not meant to be downloaded on a windows system
                                addDownload = false;
                            }
                        }
                        if (check.rules.Count >= 2)
                        {
                            if (check.rules[1].action == "disallow")
                            {
                                if (check.rules[1].os.name == "windows")
                                {
                                    // Don't add this, it is disallowed on windows systems
                                    addDownload = false;
                                }
                                else
                                {
                                    // It's denied on some other system, download it
                                    addDownload = true;
                                }
                            }
                        }
                    }
                    else
                    {
                        // No rules on downloading, add it to the list
                        addDownload = true;
                    }

                    // Get the architecture, if it is required
                    if (check.natives != null)
                    {
                        downloadType = check.natives.windows;
                    }

                    // See if it needs to be extracted
                    if (check.extract != null)
                        extractNative = true;

                    // if this should be added on windows, add the download to the list
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

                        mcDownloadLibraries.Add(new downloadLibraryClass
                        {
                            Name = check.name,
                            DownloadType = downloadType,
                            ExtractNative = extractNative
                        });
                    }
                }
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
                    }
                    downloadedLibraryLocations.Add("%mcInstallDir%" + libraryLocation + currentDownloadType + ".jar");
                }


                #endregion

                if (!System.IO.Directory.Exists(mcInstallDir + @"\.minecraft\versions\" + mcVersion + @"\"))
                {
                    // Create version directory if it does not exist. Ex. \.minecraft\versions\1.7.5\
                    System.IO.Directory.CreateDirectory(mcInstallDir + @"\.minecraft\versions\" + mcVersion + @"\");
                }

                // Download minecraft jar
                downloadFile("http://s3.amazonaws.com/Minecraft.Download/versions/" + mcVersion + "/" + mcVersion + ".jar", mcInstallDir + @"\.minecraft\versions\" + mcVersion + @"\" + mcVersion + ".jar", validateFiles, "Downloading Files for " + mcVersion + "... " + mcVersion + "/" + mcVersion + ".jar", DDialog);

                #region Download Assets
                if (!System.IO.Directory.Exists(mcInstallDir + @"\.minecraft\assets\indexes\"))
                {
                    // Create assets/indexes directory if it does not exist
                    System.IO.Directory.CreateDirectory(mcInstallDir + @"\.minecraft\assets\indexes\");
                }

                // Download assets information, *assetversion*.json
                downloadFile("https://s3.amazonaws.com/Minecraft.Download/indexes/" + mcAssetsVersion + ".json", mcInstallDir + @"\.minecraft\assets\indexes\" + mcAssetsVersion + ".json", validateFiles, "Downloading files for Minecraft " + mcVersion + "... " + @"assets\indexes\" + mcAssetsVersion + ".json", DDialog);

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
                    Directory.CreateDirectory(mcInstallDir + @"\.minecraft\assets\objects\" + asset.Value.hash.Substring(0, 2)); // Create asset directory, eg. \.minecraft\assets\objects\eb\
                    int downloadFileReturn = downloadFile("http://resources.download.minecraft.net/" + asset.Value.hash.Substring(0, 2) + "/" + asset.Value.hash, mcInstallDir + @"\.minecraft\assets\objects\" + asset.Value.hash.Substring(0, 2) + @"\" + asset.Value.hash, validateFiles, "Downloading files for Minecraft " + mcVersion + "... " + @"assets\objects\" + asset.Value.hash.Substring(0, 2) + @"\" + asset.Value.hash, DDialog, Convert.ToInt64(asset.Value.size));
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
                        new XAttribute("version", mcVersion),
                        new XElement("mcAssetsVersion", mcAssetsVersion),
                        new XElement("minecraftLibraryList", minecraftArguments),
                        new XElement("mainClass", mcMainClass),
                        new XElement("startingArguments", startingArguments),
                        new XElement("savedReleaseTime", DateTime.Now.ToString()));
                    doc.Root.Add(mcXMLValues);
                    doc.Save(mcInstallDir + "\\.mcl\\VersionInformation.xml");
                }
                DDialog.downloadFileDisplay.Dispatcher.BeginInvoke(
                    (Action)(() => { DDialog.downloadFileDisplay.Text = "Download complete for " + mcVersion + "."; }));

                System.Threading.Thread.Sleep(500);

                DownloadDialog.downloadIsInProgress = false;
                DDialog.Dispatcher.BeginInvoke(
                    (Action)(() => { DDialog.Close(); }));

                return "success"; // Return success in downloading
            }
            else
            {
                // No internet connectivity
                DownloadDialog.downloadIsInProgress = false;
                downloaderInfo.DownloadDialog.Dispatcher.BeginInvoke(
                    (Action)(() => { downloaderInfo.DownloadDialog.Close(); }));
                return "No internet connectivity is available, cannot download files.";
            }
            #endregion
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

        #region Information to Download Library Class
        private class downloadLibraryClass
        {
            public string Name { get; set; }
            public string DownloadType { get; set; }
            public bool ExtractNative { get; set; }
        }
        #endregion
    }
}
