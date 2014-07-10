using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using System.Runtime.Serialization;

namespace MinecraftCL
{
    public enum ModpackType
	{
        MojangVanilla,
	    TechnicPack,
        FeedTheBeast,
        MinecraftCL,
        PlaceholderModpack
	}

    [DataContract]
    [XmlInclude(typeof(FeedTheBeast.FTBModpack))]
    [XmlInclude(typeof(ModpackType))]
    public class Modpack
    {
        public virtual string name { get; set; }
        public virtual string author { get; set;}
        public virtual string mcVersion { get; set; }
        public virtual ModpackType Type { get; set; }
        public string DisplayInformation
        {
            get
            {
                if (Type != ModpackType.PlaceholderModpack)
                {
                    return "Minecraft " + mcVersion + Environment.NewLine +
                           "Author: " + author + Environment.NewLine +
                           "Type: " + Type + Environment.NewLine;
                }
                else
                {
                    return "Placeholder pack. Add your own!";
                }
            }
        }
    }
}
