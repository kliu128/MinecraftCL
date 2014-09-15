using MinecraftCL.FeedTheBeast;
using MinecraftLaunchLibrary;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
        StartedMinecraft,
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
        /// Launches minecraft and handles all errors.
        /// </summary>
        /// <param name="profile"></param>
        /// <param name="sGV"></param>
        /// <returns></returns>
        public static LaunchGameReturn Launch(profileSelection profile, startGameVariables sGV)
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
                return new LaunchGameReturn { returnInfo = authenticationReturnString, returnType = LaunchReturnType.AuthenticationError;
            }
            
            // Get version information for the VANILLA version of minecraft, whether we're launching a modpack or not
            string versionInformationError;
            bool mcVersionExists = MinecraftUtils.getVersionInformation(ref sGV, out versionInformationError);
            if (versionInformationError != "")
                return new LaunchGameReturn { returnInfo = versionInformationError, returnType = LaunchReturnType.VersionInformationError;

            if (!mcVersionExists)
            {
                // If the version requires downloading, begin setting up the download
                string downloadReturn = "success";
                downloadVar.DownloadDialog = new DownloadDialog();

                LaunchGameReturn? gameReturn = null;
                BackgroundWorker worker = new BackgroundWorker();
                worker.DoWork += (o, x) =>
                    {
                        downloadReturn = MinecraftUtils.DownloadGame(downloadVar);
                    };
                worker.RunWorkerCompleted += (o, x) =>
                    {
                        downloadVar.DownloadDialog.downloadIsInProgress = false;

                        if (downloadReturn == "success")
                        {
                            mcVersionExists = MinecraftUtils.getVersionInformation(ref sGV, out versionInformationError);
                            startGameReturn startReturn = MinecraftUtils.Start(sGV);
                            switch (startReturn.ReturnCode)
	                        {
                                case startMinecraftReturnCode.MinecraftError:
                                    gameReturn = new LaunchGameReturn { returnInfo = startReturn.Error, returnType = LaunchReturnType.MinecraftError};
                                    break;
                                case startMinecraftReturnCode.CouldNotLocateJava:
                                    gameReturn
		                        default:
                                   break;
	                        }
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

                startGameReturn startReturn = MinecraftUtils.Start(sGV);
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
                return new LaunchGameReturn { returnInfo = "AttemptedStart", startReturn = startReturn };
            }
        }
    }
}
