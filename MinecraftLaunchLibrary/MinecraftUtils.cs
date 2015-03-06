using Ionic.Zip;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Script.Serialization;
using System.Xml;
using System.Xml.Linq;

namespace MinecraftLaunchLibrary
{
    public struct Asset
    {
        public string FileName { get; set; }
        public string Hash { get; set; }
        public string Directory { get { return Hash.Substring(0, 2); } }
        public Int64 Size { get; set; }
    }

    public class startGameVariables
    {
        public string JavaArguments { get; set; }
        public string JavaLocation { get; set; }
        public string MinecraftDirectory { get; set; }
        public string Version { get; set; }

        #region Authentication Variables
        /// <summary>
        /// The username/email used to log into Minecraft, eg. example@example.com or Example.
        /// </summary>
        public string LoginUsername { get; set; }
        public string Password { get; set; }
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

    public class DownloadUpdateEventArgs : EventArgs
    {
        public string CurrentFile { get; set; }
        public DownloadUpdateStage Stage { get; set; }
        public string MinecraftVersion { get; set; }
    }

    public static class MinecraftUtils
    {
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

        private static void TriggerDownloadUpdateEvent(DownloadUpdateEventArgs e)
        {
            DownloadUpdateEventHandler eventCopy = DownloadUpdateEvent;
            if (eventCopy != null)
                eventCopy(e);
        }

        public static AuthenticationInformation authenticateMinecraft(string username, string password, out string returnString)
        {
            returnString = "success"; // This will be changed when there is an error in the code
            AuthenticationInformation auth = new AuthenticationInformation();

            try
            {
                var responsePayload = "";

                /* Code from AtomLauncher */
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create("https://authserver.mojang.com/authenticate"); //Start WebRequest
                request.Method = "POST";                                                                //Method type, POST
                string json = JsonConvert.SerializeObject(new                           //Object to Upload
                {
                    agent = new                 // optional /                                           //This seems to be required for minecraft despite them saying its optional.
                    {                           //          /
                        name = "Minecraft",     // -------- / So far this is the only encountered value
                        version = 1             // -------- / This number might be increased by the vanilla client in the future
                    },                          //          /
                    username = username,   // Can be an email address or player name for unmigrated accounts
                    password = password
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
                    auth.AccessToken = responseJson.accessToken;                                           //Assign Access Token
                    //mcClientToken = responseJson.clientToken;                                           //Assign Client Token
                    if (responseJson.selectedProfile.id != null)                                        //Detect if this is an error Payload
                    {
                        auth.UUID = responseJson.selectedProfile.id;                                       //Assign User ID
                        auth.MinecraftUsername = responseJson.selectedProfile.name;                                 //Assign Selected Profile Name
                        if (responseJson.selectedProfile.legacy == "true")
                        {
                            auth.UserType = "legacy";
                        }
                        else
                        {
                            auth.UserType = "mojang";
                        }
                    }
                    else
                    {
                        returnString = "Error: WebPayLoad: Missing UUID and Username";
                    }
                }
                else if (responseJson.errorMessage != null)
                {
                    returnString = "Error: WebPayLoad: " + responseJson.errorMessage;
                }
                else
                {
                    returnString = "Error: WebPayLoad: Had an error and the payload was empty.";
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
            }
            return auth;
        }

        /// <summary>
        /// Attempts to locate Java installation. Returns null if not found.
        /// </summary>
        /// <returns></returns>
        public static string LocateJavaInstallation()
        {
            // Find java installation
            string javaInstallPath;

            string environmentPath = Environment.GetEnvironmentVariable("JAVA_HOME");
            if (!string.IsNullOrEmpty(environmentPath) && Directory.Exists(environmentPath))
            {
                javaInstallPath = environmentPath;
            }
            else
            {
                try
                {
                    string javaKey = "SOFTWARE\\JavaSoft\\Java Runtime Environment\\";
                    using (Microsoft.Win32.RegistryKey rk = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(javaKey))
                    {
                        string currentVersion = rk.GetValue("CurrentVersion").ToString();
                        using (Microsoft.Win32.RegistryKey key = rk.OpenSubKey(currentVersion))
                        {
                            javaInstallPath = key.GetValue("JavaHome").ToString();
                        }
                    }
                }
                catch
                {
                    return null;
                }
            }

            string javaFilePath = System.IO.Path.Combine(javaInstallPath, "bin\\javaw.exe");
            if (System.IO.File.Exists(javaFilePath))
            {
                return javaFilePath;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Starts the game/modpack, providing that the proper values have already been determined
        /// (such as authToken, versionInfo, etc.).
        /// </summary>
        /// <returns>Returns the Minecraft java process.</returns>
        public static startGameReturn Start(startGameVariables sGV, VersionInformation versionInfo)
        {
            // Begin to set up Minecraft java process
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;

            if (String.IsNullOrEmpty(sGV.JavaLocation))
            {
                // Locate java installation if one is not set in sGV.JavaLocation.
                string javaPath = LocateJavaInstallation();
                if (javaPath != null)
                    startInfo.FileName = javaPath;
                else
                    return new startGameReturn
                    {
                        ErrorInfo = "Could not locate java installation.",
                        ReturnCode = startMinecraftReturnCode.CouldNotLocateJava
                    };
            }
            else
                // A java location is set.
                startInfo.FileName = sGV.JavaLocation;

            string libraries = versionInfo.LibraryLocationsAsString();
            libraries = libraries.Replace("%mcInstallDir%", Environment.CurrentDirectory);

            startInfo.Arguments = "-XX:HeapDumpPath=MojangTricksIntelDriversForPerformance_javaw.exe_minecraft.exe.heapdump " + sGV.JavaArguments + " -Djava.library.path=\""
            + Environment.CurrentDirectory + @"\.minecraft\versions\"
            + versionInfo.MinecraftVersion + @"\" + versionInfo.MinecraftVersion + "-natives\" -cp "
            + libraries + ";\""
            + Environment.CurrentDirectory + @"\.minecraft\versions\" + versionInfo.MinecraftVersion + @"\" + versionInfo.MinecraftVersion + ".jar\" " + versionInfo.MainClass + " " + versionInfo.LaunchArguments;
            startInfo.WorkingDirectory = sGV.MinecraftDirectory;

            // Start Minecraft and return
            Process mcProc = Process.Start(startInfo);
            return new startGameReturn { MinecraftProcess = mcProc, StartInfo = startInfo, ReturnCode = startMinecraftReturnCode.StartedMinecraft, ErrorInfo = "", LaunchParameters = startInfo.Arguments };
        }

        /// <summary>
        /// Downloads a file and triggers an event. Supports multiple ways of validation.
        /// </summary>
        /// <param name="downloadLocation"></param>
        /// <param name="saveLocation"></param>
        /// <param name="validateFiles"></param>
        /// <param name="downloadEventArgs"></param>
        /// <param name="specifiedFileSize"></param>
        /// <returns>Whether or not the file was downloaded/updated.</returns>
        private static bool downloadFile(string downloadLocation, string saveLocation, bool validateFiles, DownloadUpdateEventArgs downloadEventArgs, long specifiedFileSize = new long())
        {
            // return code 0 = downloaded file
            // return code 1 = skipped file
            WebClientNoKeepAlive webClient = new WebClientNoKeepAlive();
            FileInfo savedFile = new FileInfo(saveLocation);
            bool fileDownloaded = true;

            // Notify the user that we are downloading
            TriggerDownloadUpdateEvent(downloadEventArgs);

            if (validateFiles == true)
            {
                if (specifiedFileSize != default(long))
                {
                    // if the file size is specified, compare the two filesizes
                    if (savedFile.Length != specifiedFileSize)
                    {
                        // If the file sizes don't match, redownload the file
                        webClient.DownloadFile(downloadLocation, saveLocation);
                        fileDownloaded = true;
                    }
                    else
                    {
                        fileDownloaded = false;
                    }
                }
                else
                {
                    // If there is no specified file size, simply revalidate the file by redownloading it
                    webClient.DownloadFile(downloadLocation, saveLocation);
                    fileDownloaded = true;
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
                    fileDownloaded = true;
                }

                // Close connection to server, prevents timeouts because of a concurrent thread limit
                HEADresponse.Close();
                fileDownloaded = false;
            }

            return fileDownloaded;
        }

        public static VersionInformation DownloadGame(string mcVersion, out string returnValue, bool validate = false)
        {
            try
            {
                string mcAssetsVersion = "";
                string mcMainClass = "";
                string launchArguments = "";
                
                dynamic mcVersionList = MinecraftServerUtils.GetVersionsJson();

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
                        System.Environment.CurrentDirectory + "\\.mcl\\versions\\" + mcVersion + ".json", validate,
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
                        returnValue = "Download error. The information for Minecraft " + mcVersion + " could not be found on the server.";
                        return null;
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
                launchArguments = versionInformationDictionary["minecraftArguments"].ToString();
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

                foreach (var library in checkMcLibraries)
                {
                    string libraryLocation;
                    bool libraryDownloaded = DownloadLibrary(library, Environment.CurrentDirectory, validate, mcVersion, out libraryLocation);
                    if (libraryDownloaded == true)
                    {
                        string currentDownloadType = "";
                        if (library.natives != null)
                            currentDownloadType = library.natives.windows;
                        downloadedLibraryLocations.Add("%mcInstallDir%" + libraryLocation);
                    }
                }
                #endregion

                if (!System.IO.Directory.Exists(Environment.CurrentDirectory + @"\.minecraft\versions\" + mcVersion + @"\"))
                {
                    // Create version directory if it does not exist. Ex. \.minecraft\versions\1.7.5\
                    System.IO.Directory.CreateDirectory(Environment.CurrentDirectory + @"\.minecraft\versions\" + mcVersion + @"\");
                }

                // Download minecraft jar
                downloadFile("http://s3.amazonaws.com/Minecraft.Download/versions/" + mcVersion + "/" + mcVersion + ".jar",
                    Environment.CurrentDirectory + @"\.minecraft\versions\" + mcVersion + @"\" + mcVersion + ".jar",
                    validate,
                    new DownloadUpdateEventArgs
                    {
                        CurrentFile = mcVersion + ".jar",
                        MinecraftVersion = mcVersion,
                        Stage = DownloadUpdateStage.DownloadingMinecraftJar
                    });

                if (!System.IO.Directory.Exists(Environment.CurrentDirectory + @"\.minecraft\assets\indexes\"))
                {
                    // Create assets/indexes directory if it does not exist
                    System.IO.Directory.CreateDirectory(Environment.CurrentDirectory + @"\.minecraft\assets\indexes\");
                }

                // Download assets information, *assetversion*.json
                downloadFile("https://s3.amazonaws.com/Minecraft.Download/indexes/" + mcAssetsVersion + ".json",
                    Environment.CurrentDirectory + @"\.minecraft\assets\indexes\" + mcAssetsVersion + ".json",
                    validate,
                    new DownloadUpdateEventArgs
                    {
                        CurrentFile = mcAssetsVersion + ".json",
                        MinecraftVersion = mcVersion,
                        Stage = DownloadUpdateStage.DownloadingGenericFile
                    });

                string assetInformationString;
                using (StreamReader streamReader = new StreamReader(Environment.CurrentDirectory + @"\.minecraft\assets\indexes\" + mcAssetsVersion + ".json", Encoding.UTF8))
                {
                    assetInformationString = streamReader.ReadToEnd();
                }

                dynamic assetInformation = JsonConvert.DeserializeObject<dynamic>(assetInformationString);

                foreach (dynamic dynamicAsset in assetInformation.objects)
                {
                    Asset asset = new Asset
                    { 
                        FileName = dynamicAsset.Name, 
                        Hash = dynamicAsset.Value.hash, 
                        Size = Convert.ToInt64((string)dynamicAsset.Value.size) 
                    };

                    DownloadAsset(asset, Environment.CurrentDirectory, validate, mcVersion, mcAssetsVersion);
                }

                // Trigger event for download completion
                TriggerDownloadUpdateEvent(new DownloadUpdateEventArgs
                    {
                        CurrentFile = null,
                        MinecraftVersion = mcVersion,
                        Stage = DownloadUpdateStage.CompletedDownload
                    });

                // Return download information that is necessary for starting the game.
                returnValue = "success";
                return new VersionInformation
                {
                    AssetIndex = mcAssetsVersion,
                    LibraryLocations = downloadedLibraryLocations,
                    MainClass = mcMainClass,
                    LaunchArguments = launchArguments,
                    MinecraftVersion = mcVersion
                };
                
            }
            catch (Exception e)
            {
                // An exception occured
                returnValue = "An error occurred while downloading files. " + e.Message;
                return null;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="asset">The asset to download.</param>
        /// <param name="downloadLocation">Where to save the asset.</param>
        /// <param name="validateFiles"></param>
        /// <param name="mcVersion"></param>
        /// <param name="libraryLocation"></param>
        /// <returns>Whether or not the asset was downloaded/updated.</returns>
        private static bool DownloadAsset(Asset asset, string downloadLocation, bool validateFiles, string mcVersion, string assetsVersion)
        {
            // Create directory for the asset (eg. /ac/ac1e...)
            Directory.CreateDirectory(downloadLocation + @"\.minecraft\assets\objects\" + asset.Directory);

            // Download the asset.
            bool downloadFileReturn = downloadFile("http://resources.download.minecraft.net/" + asset.Directory + "/" + asset.Hash,
                downloadLocation + @"\.minecraft\assets\objects\" + asset.Directory + @"\" + asset.Hash,
                validateFiles,
                new DownloadUpdateEventArgs
                {
                    CurrentFile = asset.Hash,
                    MinecraftVersion = mcVersion,
                    Stage = DownloadUpdateStage.DownloadingAsset
                },
                asset.Size);

            // If Minecraft is asset version = legacy, copy the asset to the legacy location.
            if (assetsVersion == "legacy" && downloadFileReturn == true)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(downloadLocation + @"\.minecraft\assets\virtual\legacy\" + asset.FileName));
                System.IO.File.Copy(downloadLocation + @"\.minecraft\assets\objects\" + asset.Directory + @"\" + asset.Hash, downloadLocation + @"\.minecraft\assets\virtual\legacy\" + asset.FileName.Replace('/', '\\'), true);
            }

            return downloadFileReturn;
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
        public static bool DownloadLibrary(Library libraryClass, 
            string downloadLocation, 
            bool validateFiles, 
            string mcVersion, 
            out string libraryLocation, 
            string libraryServer = MinecraftServerUtils.LibraryServer)
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
                downloadType = "-" + libraryClass.natives.windows;
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
                string libraryDownloadPath = libraryServer + "/" + libraryDownloadURL[0].Replace('.', '/') + "/" + libraryDownloadURL[1] + "/" + libraryDownloadURL[2] + "/" + libraryDownloadURL[1] + "-" + libraryDownloadURL[2];
                
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
                    TriggerDownloadUpdateEvent(new DownloadUpdateEventArgs
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
    }

    #region Library Json Classes
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

    #endregion
}
