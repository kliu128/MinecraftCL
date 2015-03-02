using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;

namespace MinecraftCL.FeedTheBeast
{
    [XmlRoot("modpacks")]
    public partial class FTBModpackList
    {
        public object script { get; set; }

        [XmlElementAttribute("modpack")]
        public FTBModpack[] modpack { get; set; }
    }

    [XmlTypeAttribute()]
    public class FTBModpack : Modpack
    {
        [XmlAttributeAttribute()]
        public override string author { get; set; }

        [XmlAttributeAttribute()]
        public string description { get; set; }

        [XmlAttributeAttribute()]
        public string image { get; set; }

        [XmlAttributeAttribute()]
        public string logo { get; set; }

        [XmlAttributeAttribute()]
        public string mods { get; set; }

        [XmlAttributeAttribute()]
        public override string name { get; set; }

        [XmlAttributeAttribute()]
        public string oldVersions { get; set; }

        [XmlAttributeAttribute()]
        public string repoVersion { get; set; }

        [XmlAttributeAttribute()]
        public string serverPack { get; set; }

        [XmlAttributeAttribute()]
        public string squareImage { get; set; }

        [XmlAttributeAttribute()]
        public string url { get; set; }

        [XmlAttributeAttribute()]
        public string version { get; set; }

        [XmlAttributeAttribute()]
        public string dir { get; set; }

        [XmlAttributeAttribute()]
        public override string mcVersion { get; set; }
    }
}
