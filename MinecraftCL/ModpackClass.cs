using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using System.Runtime.Serialization;
using System.Xml;
using MinecraftLaunchLibrary;
using MinecraftCL.FeedTheBeast;

namespace MinecraftCL
{
    public enum ModpackType
	{
        MojangVanilla,
	    TechnicPack,
        FeedTheBeastPublic,
        FeedTheBeastPrivate,
        MinecraftCL,
        PlaceholderModpack
	}

    public static class ModpackTypeExtensions
    {
        public static string ToFriendlyString(this ModpackType pack)
        {
            switch (pack)
            {
                case ModpackType.MojangVanilla:
                    return "MojangVanilla";
                case ModpackType.TechnicPack:
                    return "TechnicPack";
                case ModpackType.FeedTheBeastPublic:
                    return "FeedTheBeastPublic";
                case ModpackType.FeedTheBeastPrivate:
                    return "FeedTheBeastPrivate";
                case ModpackType.MinecraftCL:
                    return "MinecraftCL";
                case ModpackType.PlaceholderModpack:
                    return "PlaceholderModpack";
                default:
                    throw new InvalidOperationException { Source = "ModpackTypeExtensions.ToFriendlyString()" };
            }
        }
    }

    [XmlInclude(typeof(FTBModpack))]
    public class Modpack
    {
        [XmlIgnore]
        public virtual string name { get; set; }
        [XmlIgnore]
        public virtual string author { get; set; }
        [XmlIgnore]
        public virtual string mcVersion { get; set; }
        [XmlIgnore]
        public virtual ModpackType Type { get; set; }
        // Version can be "Recommended" for latest, or a specific version
        [XmlIgnore]
        public virtual string Version { get; set; }
        [XmlIgnore]
        public string DisplayInformation
        {
            get
            {
                if (Type != ModpackType.PlaceholderModpack)
                {
                    return "Minecraft " + mcVersion + Environment.NewLine +
                           "Author: " + author + Environment.NewLine +
                           "Version: " + Version + Environment.NewLine;
                }
                else
                {
                    return "Placeholder pack." + Environment.NewLine +
                           "Add your own!";
                }
            }
        }

        public struct modpackInformationReturn
        {
            public bool modpackDownloadRequired { get; set; }
            public bool mojangVersionDownloadRequired { get; set; }
            public bool versionDownloadRequired
            {
                get
                {
                    if (mojangVersionDownloadRequired == true || modpackDownloadRequired == true)
                        return true;
                    else
                        return false;
                }
            }
        }

        /// <summary>
        /// Gets information required to start the modpack. Returns a bool value depending on
        /// whether it could successfully find the information.
        /// </summary>
        /// <param name="pack"></param>
        /// <returns></returns>
        public modpackInformationReturn getModpackInformation(Modpack pack, ref startGameVariables sGV)
        {
            XmlDocument versionInformationDoc = new XmlDocument();
            versionInformationDoc.Load(System.Environment.CurrentDirectory + @"\.mcl\VersionInformation.xml");

            if (versionInformationDoc.SelectSingleNode("/versions/modpacks/modpack[@type='" + pack.Type.ToFriendlyString() + "'][@name='" + pack.name + "']") != null)
            {
                // The modpack exists

                // Replace "Recommended" in the pack version with the latest version
                XmlNode modpackNode = versionInformationDoc.SelectSingleNode("/versions/modpacks/modpack[@type='" + pack.Type.ToFriendlyString() + "'][@name='" + pack.name + "']");
                
                // Set the "version" to the vanilla minecraft version specified so getVersionInformation() can find the info
                // for the vanilla version of minecraft
                sGV.Version = modpackNode.SelectSingleNode("mcVersion").InnerText;

                bool minecraftVersionExists = LaunchGame.checkMinecraftExists(sGV.Version);

                // Set the version to {$modpack-type}-{$modpack-name}
                sGV.Version = modpackNode.Attributes["type"].Value + "-" + modpackNode.Attributes["name"].Value;

                return new modpackInformationReturn
                {
                    modpackDownloadRequired = false,
                    mojangVersionDownloadRequired = minecraftVersionExists
                };
            }
            else
            {
                // Version was not found
                return new modpackInformationReturn
                {
                    modpackDownloadRequired = true,
                    mojangVersionDownloadRequired = true
                };
            }
        }
    }
}
