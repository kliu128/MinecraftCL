using MinecraftCL.FeedTheBeast;
using MinecraftLaunchLibrary;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Xml;
using System.Xml.Serialization;

namespace MinecraftCL
{
    public enum LaunchReturnType
    {
        SuccessfulLaunch,
        CouldNotLocateJava,
        MinecraftError,
        AuthenticationError,
        VersionInformationError
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
        /// <param name="mcVersionDynamic"></param>
        /// <returns></returns>
        private static bool checkMinecraftExists(string minecraftVersion, dynamic mcVersionDynamic)
        {
            // TODO: Rewrite this to be compatible with the official Minecraft launcher's
            // json settings file

            // Open VersionInformation.xml
            XmlDocument doc = new XmlDocument();
            doc.Load(System.Environment.CurrentDirectory + "\\.mcl\\VersionInformation.xml");

            if (doc.SelectSingleNode("//versions/version[@version='" + minecraftVersion + "']") != null)
            {
                // If //versions/version[$version] is null, the version has not been recorded
                // Now check to see if the version has been updated
                if (mcVersionDynamic != null)
                {
                    // Check in mcVersionDynamic (version.json file) to see if the version needs to be redownloaded
                    foreach (var item in mcVersionDynamic.versions)
                    {
                        if (item.id == minecraftVersion)
                        {
                            DateTime serverReleaseTime = DateTime.ParseExact(item.releaseTime, "yyyy'-'MM'-'dd'T'HH:mm:sszzz", null);
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
        private static bool getVersionInformation(ref startGameVariables sGV, out string errorInformation)
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

            if (checkMinecraftExists(sGV.Version, sGV.mcVersionDynamic) == true)
            {
                // Version already exists
                versionExists = true;

                XmlDocument doc = new XmlDocument();
                doc.Load(System.Environment.CurrentDirectory + "\\.mcl\\VersionInformation.xml");

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

            errorInformation = "";
            return versionExists;
        }

        /// <summary>
        /// Downloads Minecraft if necessary, and then starts Minecraft.
        /// </summary>
        /// <param name="profile">The profile to be launched</param>
        /// <param name="sGV">Start Game Variables</param>
        /// <returns></returns>
        public static LaunchGameReturn BeginLaunch(profileSelection profile, startGameVariables sGV)
        {
            downloadVariables downloadVar = new downloadVariables
            {
                mcInstallDir = sGV.InstallDir,
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
            
            // Get version information for the VANILLA version of minecraft, whether we're launching a modpack or not
            string versionInformationError;
            bool mcVersionExists = getVersionInformation(ref sGV, out versionInformationError);
            if (versionInformationError != "")
                return new LaunchGameReturn { returnInfo = versionInformationError, returnType = LaunchReturnType.VersionInformationError };

            if (!mcVersionExists)
            {
                // Version does not exist, begin setting up the download
                string downloadReturn = "success";
                downloadVar.DownloadDialog = new DownloadDialog();

                LaunchGameReturn? gameReturn = null;
                BackgroundWorker worker = new BackgroundWorker();
                worker.DoWork += (o, x) =>
                    {
                        // Download the game
                        downloadReturn = MinecraftUtils.DownloadGame(downloadVar);
                    };
                worker.RunWorkerCompleted += (o, x) =>
                    {
                        downloadVar.DownloadDialog.downloadIsInProgress = false;
                        downloadVar.DownloadDialog.Close();
                        if (downloadReturn == "success")
                        {
                            // Start the game
                            gameReturn = StartGame(profile, sGV);
                        }
                    };
                worker.RunWorkerAsync();

                while (gameReturn == null)
                {
                }

                return (LaunchGameReturn)gameReturn;
            }
            else
            {
                // The version already exists, launch game
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
            }
        }


        /// <summary>
        /// Starts the game. Requires Minecraft to be downloaded.
        /// </summary>
        /// <param name="profile"></param>
        /// <param name="sGV"></param>
        /// <returns></returns>
        private static LaunchGameReturn StartGame(profileSelection profile, startGameVariables sGV)
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

            startGameReturn startReturn = MinecraftUtils.Start(sGV);

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
                return new LaunchGameReturn { returnInfo = returnInfo, returnType = LaunchReturnType.MinecraftError };
            }
            else if (exitCode == 0)
                return new LaunchGameReturn { returnInfo = "Success.", returnType = LaunchReturnType.SuccessfulLaunch };
            else
                throw new Exception { Source = "Minecraft exited with error code " + exitCode + "." };
        }
    }
}
