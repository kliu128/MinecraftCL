using MinecraftLaunchLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;

namespace MinecraftCL
{
    public static class ExtensionMethods
    {
        /// <summary>
        /// Checks VersionInformation.xml and versions.json to see if the version must be downloaded.
        /// True = Version exists
        /// False = Version must be downloaded
        /// </summary>
        /// <param name="minecraftVersion"></param>
        /// <param name="mcVersionDynamic"></param>
        /// <returns></returns>
        public static bool checkMinecraftExists(this MinecraftUtils utils, string minecraftVersion, dynamic mcVersionDynamic)
        {
            XmlDocument doc = new XmlDocument();
            doc.Load(System.Environment.CurrentDirectory + "\\.mcl\\VersionInformation.xml");
            if (doc.SelectSingleNode("//versions/version[@version='" + minecraftVersion + "']") != null)
            {
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

        public static bool getVersionInformation(this MinecraftUtils utils, ref startGameVariables sGV, out string errorInformation)
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
    }
}
