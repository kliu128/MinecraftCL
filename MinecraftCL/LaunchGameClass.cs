using MinecraftLaunchLibrary;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Xml;
using System.Xml.Linq;

namespace MinecraftCL
{
    public enum LaunchReturnType
    {
        SuccessfulLaunch,
        CouldNotLocateJava,
        MinecraftError,
        AuthenticationError,
        VersionInformationError,
        DownloadError
    }

    public struct LaunchGameReturn
    {
        public string returnInfo;
        public LaunchReturnType returnType;
    }

    public static class LaunchGame
    {
        /// <summary>
        /// Checks VersionInformation.xml and versions.json to see if the version must be downloaded.
        /// True = Version exists
        /// False = Version must be downloaded
        /// </summary>
        /// <param name="minecraftVersion"></param>
        /// <returns></returns>
        public static bool checkMinecraftExists(string minecraftVersion)
        {
            // TODO: Rewrite this to be compatible with the official Minecraft launcher's
            // json settings file

            dynamic mcVersionDynamic = MinecraftServerUtils.GetVersionsJson();

            // Open VersionInformation.xml
            XmlDocument doc = new XmlDocument();
            doc.Load(System.Environment.CurrentDirectory + "\\.mcl\\VersionInformation.xml");

            if (doc.SelectSingleNode("//versions/version[@version='" + minecraftVersion + "']") != null)
            {
                // If //versions/version[$version] isn't null, the version has already been recorded
                // Now to check to see if the version has been updated.
                if (mcVersionDynamic != null)
                {
                    // Check in mcVersionDynamic (version.json file) to see if the version needs to be redownloaded
                    foreach (var item in mcVersionDynamic.versions)
                    {
                        if (item.id == minecraftVersion)
                        {
                            string itemReleaseTime = item.releaseTime;

                            // ..um, what? Format of date string above is different than versions.json date string.
                            // Format string should be "yyyy'-'MM'-'dd'T'HH:mm:sszzz".
                            DateTime serverReleaseTime = DateTime.Parse(itemReleaseTime, null);
                            DateTime savedDownloadTime = DateTime.Parse(doc.SelectSingleNode(@"//versions/version[@version='" + minecraftVersion + "']/savedReleaseTime").InnerText);
                            if (serverReleaseTime > savedDownloadTime)
                            {
                                return false;
                            }
                        }
                    }
                }
                return true;
            }
            else
                return false;
        }

        /// <summary>
        /// Gets Minecraft version information from VersionInformation.xml such as MainClass, libraries, etc.
        /// </summary>
        /// <param name="sGV"></param>
        /// <param name="errorInformation"></param>
        /// <returns></returns>
        public static bool getVersionInformation(ref startGameVariables sGV, out string errorInformation)
        {
            dynamic mcVersionDynamic = MinecraftServerUtils.GetVersionsJson();

            // Change out "latest-release" and "latest-snapshot" to the actual versions
            if (mcVersionDynamic != null)
            {
                if (sGV.Version == "latest-release")
                {
                    sGV.Version = mcVersionDynamic.latest.release;
                }
                else if (sGV.Version == "latest-snapshot")
                {
                    sGV.Version = mcVersionDynamic.latest.snapshot;
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

            if (checkMinecraftExists(sGV.Version) == true)
            {
                // Version already exists
                versionExists = true;

                XmlDocument doc = new XmlDocument();
                doc.Load(System.Environment.CurrentDirectory + "\\.mcl\\VersionInformation.xml");

                mcAssetsVersion = doc.SelectSingleNode(@"//versions/version[@version='" + sGV.Version + "']/assetIndex").InnerText;
                sGV.MCLibraryArguments = doc.SelectSingleNode(@"//versions/version[@version='" + sGV.Version + "']/minecraftLibraryList").InnerText;
                sGV.MainClass = doc.SelectSingleNode(@"//versions/version[@version='" + sGV.Version + "']/mainClass").InnerText;
                startingArguments = doc.SelectSingleNode(@"//versions/version[@version='" + sGV.Version + "']/startingArguments").InnerText;

                // Replace all of the various variables used with their actual values
                startingArguments = startingArguments.Replace("${auth_player_name}", sGV.Username);
                startingArguments = startingArguments.Replace("${version_name}", sGV.Version);
                startingArguments = startingArguments.Replace("${game_directory}", "\"" + sGV.MinecraftDirectory + "\"");
                startingArguments = startingArguments.Replace("${assets_root}", "\"" + Environment.CurrentDirectory + "\\.minecraft\\assets\""); // For 1.7 and above
                startingArguments = startingArguments.Replace("${game_assets}", "\"" + Environment.CurrentDirectory + "\\.minecraft\\assets\\virtual\\legacy\""); // For legacy versions 1.6.4 or below
                startingArguments = startingArguments.Replace("${assets_index_name}", mcAssetsVersion);
                startingArguments = startingArguments.Replace("${auth_uuid}", sGV.UUID);
                startingArguments = startingArguments.Replace("${auth_access_token}", sGV.AccessToken);
                startingArguments = startingArguments.Replace("${user_properties}", "{}"); // Yeah, no twitch here...
                startingArguments = startingArguments.Replace("${user_type}", sGV.userType);
                startingArguments = startingArguments.Replace("${auth_session}", sGV.AccessToken);
                sGV.LaunchArguments = startingArguments;
            }

            errorInformation = "";
            return versionExists;
        }

        /// <summary>
        /// Downloads Minecraft if necessary, and then starts Minecraft.
        /// </summary>
        /// <param name="profile">The profile to be launched</param>
        /// <param name="sGV">Start Game Variables</param>
        /// <returns></returns>
        public static LaunchGameReturn DownloadAndStartGame(CLProfile profile, startGameVariables sGV)
        {
            downloadVariables downloadVar = new downloadVariables
            {
                mcVersion = sGV.Version,
                ValidateFiles = false
            };
            string authenticationReturnString;
            // Authenticate Minecraft using sGV.Username and sGV.Password
            bool authReturn = MinecraftUtils.authenticateMinecraft(ref sGV, out authenticationReturnString);
            if (authReturn == false)
            {
                // authentication failed
                return new LaunchGameReturn { returnInfo = authenticationReturnString, returnType = LaunchReturnType.AuthenticationError };
            }
            
            bool mcVersionExists = checkMinecraftExists(sGV.Version);

            if (!mcVersionExists)
            {
                // Version does not exist, begin setting up the download
                downloadGameReturn downloadReturn = new downloadGameReturn();
                DownloadDialog downloadDialog = new DownloadDialog();
                LaunchGameReturn? gameReturn = null;
                BackgroundWorker worker = new BackgroundWorker();
                worker.WorkerReportsProgress = true;

                MinecraftUtils.DownloadUpdateEventHandler downloadUpdateDelegate = delegate(DownloadUpdateEventArgs x)
                    {
                        worker.ReportProgress(0, x);
                    };
                MinecraftUtils.DownloadUpdateEvent += downloadUpdateDelegate;

                bool downloadError = false;
                AutoResetEvent reset = new AutoResetEvent(false);

                worker.DoWork += (o, x) =>
                    {
                        // Download the game
                        downloadReturn = MinecraftUtils.DownloadGame(downloadVar);
                    };
                worker.ProgressChanged += (o, x) =>
                    {
                        DownloadUpdateEventArgs eventArgs = (DownloadUpdateEventArgs)(x.UserState);
                        UpdateDownloadDialog(eventArgs, downloadDialog);
                    };
                worker.RunWorkerCompleted += (o, x) =>
                    {
                        MinecraftUtils.DownloadUpdateEvent -= downloadUpdateDelegate;
                        downloadDialog.downloadIsInProgress = false;
                        downloadDialog.Close();
                        if (downloadReturn.ReturnValue == "success")
                        {
                            SaveVersionInformation(downloadReturn);

                            // Start the game
                            gameReturn = StartGame(profile, sGV);
                        }
                        else
                            downloadError = true;

                        reset.Set();
                    };

                // Start the download thread, and show the download dialog.
                worker.RunWorkerAsync();
                downloadDialog.ShowDialog();

                // This will wait until either the game has started and ended, or until a download error occurs.
                reset.WaitOne();

                if (downloadError == true)
                    // If there was a download error, forward it to the caller.
                    return new LaunchGameReturn { returnType = LaunchReturnType.DownloadError, returnInfo = downloadReturn.ReturnValue };
                else
                    // Otherwise return the LaunchGameReturn provided by StartGame();
                    return (LaunchGameReturn)gameReturn;
            }
            else
            {
                // The version already exists, launch game
                #region Backup Minecraft worlds if specified
                if (sGV.AutoBackupWorld == true && Directory.Exists(sGV.MinecraftDirectory + @"\saves\"))
                {
                    MessageWindow backupNotificationBox = new MessageWindow();
                    backupNotificationBox.messageText.Text = "Backing up worlds before starting Minecraft...";
                    backupNotificationBox.closeTimeoutMilliseconds = -1;
                    backupNotificationBox.Show();
                    backupNotificationBox.Activate();

                    string currentDateTime = DateTime.Now.Hour + "." + DateTime.Now.Minute + "." + DateTime.Now.Millisecond + " - " + DateTime.Now.ToString("MMMM") + " " + DateTime.Now.Day + ", " + DateTime.Now.Year;
                    DirectoryCopy.CopyRecursive(sGV.MinecraftDirectory + @"\.minecraft\saves\", sGV.MinecraftDirectory + "\\Backups\\" + currentDateTime + "\\");

                    backupNotificationBox.Close();
                }
                #endregion

                LaunchGameReturn gameReturn = StartGame(profile, sGV);
                return gameReturn;
            }
        }

        private static void SaveVersionInformation(downloadGameReturn info)
        {
            string[] libraryLocationsArray = info.DownloadedLibraryLocations.ToArray();
            string libraryLaunchString = String.Join("\";\"", libraryLocationsArray);
            libraryLaunchString = "\"" + libraryLaunchString + "\"";

            bool versionInformationExists = false;

            using (XmlReader reader = XmlReader.Create(Environment.CurrentDirectory + "\\.mcl\\VersionInformation.xml"))
            {
                while (reader.Read())
                {
                    if (reader.IsStartElement() && reader.Name == "version" && reader.GetAttribute("version") == info.MinecraftVersion)
                    {
                        // If the version is already in the XML, update the information
                        reader.Close();
                        versionInformationExists = true;
                        XmlDocument versionInformationXML = new XmlDocument();
                        versionInformationXML.Load(Environment.CurrentDirectory + "\\.mcl\\VersionInformation.xml");
                        versionInformationXML.DocumentElement.SelectSingleNode(@"//versions/version[@version='" + info.MinecraftVersion + "']/assetIndex").InnerText = info.AssetIndex;
                        versionInformationXML.DocumentElement.SelectSingleNode(@"//versions/version[@version='" + info.MinecraftVersion + "']/minecraftLibraryList").InnerText = libraryLaunchString;
                        versionInformationXML.DocumentElement.SelectSingleNode(@"//versions/version[@version='" + info.MinecraftVersion + "']/mainClass").InnerText = info.MainClass;
                        versionInformationXML.DocumentElement.SelectSingleNode(@"//versions/version[@version='" + info.MinecraftVersion + "']/startingArguments").InnerText = info.LaunchArguments;
                        versionInformationXML.DocumentElement.SelectSingleNode(@"//versions/version[@version='" + info.MinecraftVersion + "']/savedReleaseTime").InnerText = DateTime.Now.ToString();
                        versionInformationXML.Save(Environment.CurrentDirectory + "\\.mcl\\VersionInformation.xml");
                    }
                }
            }

            if (versionInformationExists == false)
            {
                // Add the version into the XML file if it does not already exist.
                XDocument doc = XDocument.Load(Environment.CurrentDirectory + "\\.mcl\\VersionInformation.xml");

                XElement mcXMLValues = new XElement("version",
                    new XAttribute("version", info.MinecraftVersion), new XAttribute("type", "MojangVanilla"), // TODO: change MojangVanilla to the modpack type
                    new XElement("assetIndex", info.AssetIndex),
                    new XElement("minecraftLibraryList", libraryLaunchString),
                    new XElement("mainClass", info.MainClass),
                    new XElement("startingArguments", info.LaunchArguments),
                    new XElement("savedReleaseTime", DateTime.Now.ToString()));
                doc.Root.Add(mcXMLValues);
                doc.Save(Environment.CurrentDirectory + "\\.mcl\\VersionInformation.xml");
            }
        }

        private static void UpdateDownloadDialog(DownloadUpdateEventArgs eventArgs, DownloadDialog dialog)
        {
            string downloadStringPrefix = null;
            switch (eventArgs.Stage)
            {
                case DownloadUpdateStage.DownloadingGenericFile:
                    downloadStringPrefix = "Downloading file ";
                    break;
                case DownloadUpdateStage.DownloadingLibrary:
                    downloadStringPrefix = "Downloading library ";
                    break;
                case DownloadUpdateStage.ExtractingNativeLibrary:
                    downloadStringPrefix = "Extracting library ";
                    break;
                case DownloadUpdateStage.DownloadingMinecraftJar:
                    downloadStringPrefix = "Downloading Minecraft jar file ";
                    break;
                case DownloadUpdateStage.DownloadingAsset:
                    downloadStringPrefix = "Downloading asset ";
                    break;
                case DownloadUpdateStage.CompletedDownload:
                    dialog.downloadUpdateInfo = "Download completed.";
                    DebugConsole.Print("Download completed for Minecraft version" + eventArgs.MinecraftVersion, "DownloadGame()", "INFO/DOWNLOAD");
                    return;
                default:
                    throw new Exception();
            }

            string downloadMessage = downloadStringPrefix + eventArgs.CurrentFile + " for Minecraft version " + eventArgs.MinecraftVersion + "...";

            dialog.downloadUpdateInfo = downloadMessage;
            DebugConsole.Print(downloadMessage, "DownloadGame()", "INFO/DOWNLOAD");
        }

        public delegate void MinecraftStartedEventHandler();
        public static event MinecraftStartedEventHandler MinecraftStartedEvent;

        /// <summary>
        /// Starts the game. Requires Minecraft to be downloaded.
        /// </summary>
        /// <param name="profile"></param>
        /// <param name="sGV"></param>
        /// <returns></returns>
        private static LaunchGameReturn StartGame(CLProfile profile, startGameVariables sGV)
        {
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

            // Set custom Minecraft directory if specified in profile.
            if (profile.useCustomMinecraftDirectory)
                sGV.MinecraftDirectory = profile.customMinecraftDirectory;
            else
                sGV.MinecraftDirectory = Environment.CurrentDirectory + @"\.minecraft";

            // Set custom java location if specified in profile.
            if (profile.useCustomJavaEXE)
                sGV.JavaLocation = profile.customJavaEXE;

            string versionInformationError;
            getVersionInformation(ref sGV, out versionInformationError);
            if (versionInformationError != "")
                return new LaunchGameReturn { returnInfo = versionInformationError, returnType = LaunchReturnType.VersionInformationError };

            startGameReturn startReturn = MinecraftUtils.Start(sGV);

            switch (startReturn.ReturnCode)
            {
                case startMinecraftReturnCode.StartedMinecraft:
                    if (MinecraftStartedEvent != null)
                        MinecraftStartedEvent();

                    Process mcProcess = startReturn.MinecraftProcess;
                    string javaOutput = mcProcess.StandardOutput.ReadToEnd();
                    string javaError = mcProcess.StandardError.ReadToEnd();
                    int exitCode = mcProcess.ExitCode;

                    mcProcess.WaitForExit();  // This will quietly wait until minecraft has closed
                    if (exitCode != 0)      // An exit code other than 0 is an error
                    {
                        string returnInfo = "";
                        if (javaOutput.Contains("---- Minecraft Crash Report ----"))
                        {
                            // This was an official minecraft crash, complete with crash report
                            returnInfo = javaOutput.Substring(javaOutput.LastIndexOf("---- Minecraft Crash Report ----") + 1);
                        }
                        // Something interesting: the other crashes aren't actually caught by the official Minecraft launcher
                        else if (javaError == "")
                        {
                            returnInfo = javaOutput;
                        }
                        else
                        {
                            returnInfo = javaError;
                        }
                        returnInfo += Environment.NewLine + Environment.NewLine + "Launch parameters: " + startReturn.LaunchParameters;
                        return new LaunchGameReturn { returnInfo = returnInfo, returnType = LaunchReturnType.MinecraftError };
                    }
                    else if (exitCode == 0)
                        return new LaunchGameReturn { returnInfo = "Success.", returnType = LaunchReturnType.SuccessfulLaunch };
                    else
                        throw new Exception { Source = "Minecraft exited with error code " + exitCode + "." };

                case startMinecraftReturnCode.CouldNotLocateJava:
                    DebugConsole.Print("Could not locate java installation.", "LaunchGame()", "ERROR");
                    return new LaunchGameReturn
                    {
                        returnInfo = startReturn.ErrorInfo,
                        returnType = LaunchReturnType.CouldNotLocateJava
                    };

                default:
                    throw new NotImplementedException();
            }
        }
    }
}
