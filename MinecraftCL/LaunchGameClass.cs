using MinecraftLaunchLibrary;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using System.Linq;
using System.Collections.Generic;

namespace MinecraftCL
{
    public enum LaunchReturnType
    {
        SuccessfulLaunch,
        CouldNotLocateJava,
        MinecraftError,
        AuthenticationError,
        VersionInformationError,
        DownloadError,
        UsingPlaceHolderModpack
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
        /// <param name="version">Either vanilla version (1.7.10 for example) or modded version.</param>
        /// <returns></returns>
        public static bool checkVersionExists(string version, ModpackType type)
        {
            // TODO: Rewrite this to be compatible with the official Minecraft launcher's
            // json settings file

            dynamic mcVersionDynamic = MinecraftServerUtils.GetVersionsJson();
            
            // Change out "latest-release" and "latest-snapshot" to the actual versions
            if (mcVersionDynamic != null)
            {
                if (version == "latest-release")
                {
                    version = mcVersionDynamic.latest.release;
                }
                else if (version == "latest-snapshot")
                {
                    version = mcVersionDynamic.latest.snapshot;
                }
            }
            else if (version == "latest-release" || version == "latest-snapshot")
            {
                // System is not connected to the internet/mojang servers and user is trying to launch a
                // "latest-*****" version, return error.
                return false;
            }

            // Open VersionInformation.xml
            XmlDocument doc = new XmlDocument();
            doc.Load(System.Environment.CurrentDirectory + "\\.mcl\\VersionInformation.xml");

            if (doc.SelectSingleNode("//versions/version[@version='" + version + "']") != null)
            {
                // If //versions/version[$version] isn't null, the version has already been recorded
                // Now to check to see if the version has been updated.
                if (mcVersionDynamic != null)
                {
                    // Check in mcVersionDynamic (version.json file) to see if the version needs to be redownloaded
                    foreach (var item in mcVersionDynamic.versions)
                    {
                        if (item.id == version)
                        {
                            string itemReleaseTime = item.releaseTime;

                            // ..um, what? Format of date string above is different than versions.json date string.
                            // Format string should be "yyyy'-'MM'-'dd'T'HH:mm:sszzz".
                            DateTime serverReleaseTime = DateTime.Parse(itemReleaseTime, null);
                            DateTime savedDownloadTime = DateTime.Parse(doc.SelectSingleNode(@"//versions/version[@version='" + version + "']/savedReleaseTime").InnerText);
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
        public static VersionInformation getVersionInformation(
            string version, 
            ModpackType type,
            AuthenticationInformation auth, 
            string minecraftDirectory,
            out bool versionExists, 
            out string errorInformation)
        {
            dynamic mcVersionDynamic = MinecraftServerUtils.GetVersionsJson();

            // Change out "latest-release" and "latest-snapshot" to the actual versions
            if (mcVersionDynamic != null)
            {
                if (version == "latest-release")
                {
                    version = mcVersionDynamic.latest.release;
                }
                else if (version == "latest-snapshot")
                {
                    version = mcVersionDynamic.latest.snapshot;
                }
            }
            else if (version == "latest-release" || version == "latest-snapshot")
            {
                // System is not connected to the internet/mojang servers and user is trying to launch a
                // "latest-*****" version, return error.
                errorInformation = "Could not locate latest version - are you connected to the internet?";
                versionExists = false;
                return null;
            }

            if (checkVersionExists(version, type) == true)
            {
                // Version already exists
                VersionInformation info = new VersionInformation();

                XmlDocument doc = new XmlDocument();
                doc.Load(System.Environment.CurrentDirectory + "\\.mcl\\VersionInformation.xml");

                info.MainClass = doc.SelectSingleNode(@"//versions/version[@version='" + version + "']/mainClass").InnerText;

                string libraries = doc.SelectSingleNode(@"//versions/version[@version='" + version + "']/minecraftLibraryList").InnerText;
                info.LibraryLocations = new List<string>(libraries.Split(';'));

                string startingArguments = doc.SelectSingleNode(@"//versions/version[@version='" + version + "']/startingArguments").InnerText;
                string mcAssetsVersion = doc.SelectSingleNode(@"//versions/version[@version='" + version + "']/assetIndex").InnerText;

                // Replace all of the various variables used with their actual values
                startingArguments = startingArguments.Replace("${auth_player_name}", auth.MinecraftUsername);
                startingArguments = startingArguments.Replace("${version_name}", version);
                startingArguments = startingArguments.Replace("${game_directory}", "\"" + minecraftDirectory + "\"");
                startingArguments = startingArguments.Replace("${assets_root}", "\"" + Environment.CurrentDirectory + "\\.minecraft\\assets\""); // For 1.7 and above
                startingArguments = startingArguments.Replace("${game_assets}", "\"" + Environment.CurrentDirectory + "\\.minecraft\\assets\\virtual\\legacy\""); // For legacy versions 1.6.4 or below
                startingArguments = startingArguments.Replace("${assets_index_name}", mcAssetsVersion);
                startingArguments = startingArguments.Replace("${auth_uuid}", auth.UUID);
                startingArguments = startingArguments.Replace("${auth_access_token}", auth.AccessToken);
                startingArguments = startingArguments.Replace("${user_properties}", "{}"); // Yeah, no twitch here...
                startingArguments = startingArguments.Replace("${user_type}", auth.UserType);
                startingArguments = startingArguments.Replace("${auth_session}", auth.AccessToken);
                info.LaunchArguments = startingArguments;

                info.MinecraftVersion = version;

                versionExists = true;
                errorInformation = "";
                return info;
            }
            else
            {
                versionExists = false;
                errorInformation = "Version does not exist.";
                return null;
            }
        }

        /// <summary>
        /// Downloads Minecraft if necessary, and then starts Minecraft.
        /// </summary>
        /// <param name="profile">The profile to be launched</param>
        /// <param name="sGV">Start Game Variables</param>
        /// <returns></returns>
        public static LaunchGameReturn DownloadAndStartGame(CLProfile profile, startGameVariables sGV, bool backupWorlds, string lastUsedProfile)
        {
            // Make sure they're not trying to launch using a placeholder modpack
            if (profile.ModpackInfo.Type == ModpackType.PlaceholderModpack)
                return new LaunchGameReturn
                {
                    returnType = LaunchReturnType.UsingPlaceHolderModpack,
                    returnInfo = "Please add a modpack before launching!"
                };

            // See if the vanilla version of Minecraft exists that is required
            bool mcVersionExists = checkVersionExists(sGV.Version, profile.ModpackInfo.Type);

            if (!mcVersionExists)
            {
                // Version does not exist, begin setting up the download
                VersionInformation version = new VersionInformation();
                string downloadReturnValue = null;
                DownloadDialog downloadDialog = new DownloadDialog();
                BackgroundWorker worker = new BackgroundWorker();
                worker.WorkerReportsProgress = true;

                MinecraftUtils.DownloadUpdateEventHandler downloadUpdateDelegate = delegate(DownloadUpdateEventArgs x)
                    {
                        worker.ReportProgress(0, x);
                    };
                MinecraftUtils.DownloadUpdateEvent += downloadUpdateDelegate;

                bool downloadError = false;

                worker.DoWork += (o, x) =>
                    {
                        // Download the game
                        version = MinecraftUtils.DownloadGame(sGV.Version, out downloadReturnValue);
                    };
                worker.ProgressChanged += (o, x) =>
                    {
                        DownloadUpdateEventArgs eventArgs = (DownloadUpdateEventArgs)(x.UserState);
                        UpdateDownloadDialog(eventArgs, downloadDialog);
                    };
                worker.RunWorkerCompleted += (o, x) =>
                    {
                        if (downloadReturnValue == "success")
                            downloadError = false;
                        else
                            downloadError = true;

                        MinecraftUtils.DownloadUpdateEvent -= downloadUpdateDelegate;
                        downloadDialog.downloadIsInProgress = false;
                        downloadDialog.Close();
                    };

                // Start the download thread, and show the download dialog.
                // ShowDialog will wait until the download is complete and the dialog
                // has closed.
                worker.RunWorkerAsync();
                downloadDialog.ShowDialog();

                if (downloadError)
                    // If there was a download error, forward it to the caller.
                    return new LaunchGameReturn { returnType = LaunchReturnType.DownloadError, returnInfo = downloadReturnValue };

                SaveVersionInformation(version);
            }

            // By now, vanilla Minecraft is downloaded. Check if we need to download modpack.
            if (profile.ModpackInfo.Type != ModpackType.MojangVanilla)
            {
                ObservableCollection<Modpack> packList = XmlDAL.DeserializeXml<ObservableCollection<Modpack>>("ModpackInformation.xml");

                /*
                bool packExists = (from pack in packList where pack.name == profile.ModpackInfo.ID select pack).Any();
                if (!packExists)
                {
                    // Download modpacks
                    switch (profile.ModpackInfo.Type)
                    {
                        case ModpackType.TechnicPack:
                            break;
                        case ModpackType.FeedTheBeastPublic:
                            break;
                        case ModpackType.FeedTheBeastPrivate:
                            break;
                        case ModpackType.MinecraftCL:
                            break;
                        default:
                            throw new NotImplementedException();
                    }
                }*/
            }
            
            // If specified, backup worlds before launching.
            if (backupWorlds == true && Directory.Exists(sGV.MinecraftDirectory + @"\saves\"))
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

            return StartGame(profile, sGV, lastUsedProfile);
        }

        private static void SaveVersionInformation(VersionInformation info)
        {
            string[] libraryLocationsArray = info.LibraryLocations.ToArray();
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
        private static LaunchGameReturn StartGame(CLProfile profile, startGameVariables sGV, string lastUsedProfile)
        {
            // Authenticate Minecraft using sGV.Username and sGV.Password
            string authReturnString;
            AuthenticationInformation authInfo = MinecraftUtils.authenticateMinecraft(sGV.LoginUsername, sGV.Password, out authReturnString);
            if (authReturnString != "success")
            {
                // authentication failed
                return new LaunchGameReturn { returnInfo = authReturnString, returnType = LaunchReturnType.AuthenticationError };
            }

            #region Save Settings (Username, Password, Last used profile)
            XmlDocument xDoc = new XmlDocument();
            xDoc.Load(System.Environment.CurrentDirectory + @"\.mcl\MinecraftCLSettings.xml");
            XmlElement xDocRoot = xDoc.DocumentElement;
            // Save username
            if (xDoc.SelectSingleNode("/settings/Username") == null)
            {
                XmlElement usernameElement = xDoc.CreateElement("Username");
                usernameElement.InnerText = sGV.LoginUsername;
                xDocRoot.AppendChild(usernameElement);
            }
            else
                xDoc.SelectSingleNode("/settings/Username").InnerText = sGV.LoginUsername;

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
                lastUsedProfileElement.InnerText = lastUsedProfile;
                xDocRoot.AppendChild(lastUsedProfileElement);
            }
            else
                xDoc.SelectSingleNode("/settings/LastUsedProfile").InnerText = lastUsedProfile;

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
            bool versionExists;
            VersionInformation info = getVersionInformation(sGV.Version, profile.ModpackInfo.Type, authInfo, sGV.MinecraftDirectory, out versionExists, out versionInformationError);
            if (versionInformationError != "" || !versionExists)
                return new LaunchGameReturn { returnInfo = versionInformationError, returnType = LaunchReturnType.VersionInformationError };

            startGameReturn startReturn = MinecraftUtils.Start(sGV, info);

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
