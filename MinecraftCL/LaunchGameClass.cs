﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace MinecraftCL
{
    public static class LaunchGame
    {
        /// <summary>
        /// Launches minecraft and handles all errors.
        /// </summary>
        /// <param name="profile"></param>
        /// <param name="sGV"></param>
        /// <returns></returns>
        public static string Launch(profileSelection profile, startGameVariables sGV)
        {
            if (profile.VersionType == VersionType.Modpack)
            {
                // Register the correct version of minecraft and download modpack files if necessary.
                try
                {
                    XmlSerializer deserializer = new XmlSerializer(typeof(ObservableCollection<Modpack>));
                    ObservableCollection<Modpack> modpackList = new ObservableCollection<Modpack>();

                    using (FileStream stream = File.OpenRead(System.Environment.CurrentDirectory + @"\.mcl\ModpackSettings.xml"))
                    {
                        modpackList = (ObservableCollection<Modpack>)deserializer.Deserialize(stream);
                    }

                    foreach (Modpack pack in modpackList)
                    {
                        if (pack.name == profile.ModpackInfo.ID)
                        {

                        }
                    }
                }
                catch (Exception e)
                {
                    return "Modpack information error: " + e;
                }
            }

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
                return "Authentication Error: " + authenticationReturnString;
            }
            
            // Get version information for the specified version of minecraft, and check if it needs to
            // be downloaded.
            string versionInformationError;
            bool mcVersionExists = MinecraftUtils.getVersionInformation(ref sGV, out versionInformationError);
            if (versionInformationError != "")
                return "Version Information Error: " + versionInformationError;

            if (!mcVersionExists)
            {
                // If the version requires downloading, begin setting up the download
                string downloadReturn = "success";
                downloadVar.DownloadDialog = new DownloadDialog();

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
                            startGame(profile, sGV);
                        }
                    };
                worker.RunWorkerAsync();
            }
            else
            {
                // The version already exists, launch game
            }
            
            return "success";
        }

        /// <summary>
        /// Starts the game/modpack, providing that the proper values have already been determined
        /// (such as authToken, versionInfo, etc.)
        /// </summary>
        private static MinecraftCL.MinecraftUtils.startGameReturn startGame(profileSelection profile, startGameVariables sGV)
        {
            if (profile.VersionType == VersionType.Mojang)
            {

            }
        }
    }
}
