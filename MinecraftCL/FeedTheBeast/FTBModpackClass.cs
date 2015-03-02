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
        /// <remarks/>
        public object script { get; set; }

        /// <remarks/>
        [XmlElementAttribute("modpack")]
        public FTBModpack[] modpack { get; set; }
    }

    /// <remarks/>
    [XmlTypeAttribute()]
    public class FTBModpack : Modpack
    {
        /// <remarks/>
        [XmlAttributeAttribute()]
        public override string author { get; set; }

        /// <remarks/>
        [XmlAttributeAttribute()]
        public string description { get; set; }

        /// <remarks/>
        [XmlAttributeAttribute()]
        public string image { get; set; }

        /// <remarks/>
        [XmlAttributeAttribute()]
        public string logo { get; set; }

        /// <remarks/>
        [XmlAttributeAttribute()]
        public string mods { get; set; }

        /// <remarks/>
        [XmlAttributeAttribute()]
        public override string name { get; set; }

        /// <remarks/>
        [XmlAttributeAttribute()]
        public string oldVersions { get; set; }

        /// <remarks/>
        [XmlAttributeAttribute()]
        public string repoVersion { get; set; }

        /// <remarks/>
        [XmlAttributeAttribute()]
        public string serverPack { get; set; }

        /// <remarks/>
        [XmlAttributeAttribute()]
        public string squareImage { get; set; }

        /// <remarks/>
        [XmlAttributeAttribute()]
        public string url { get; set; }

        /// <remarks/>
        [XmlAttributeAttribute()]
        public string version { get; set; }

        /// <remarks/>
        [XmlAttributeAttribute()]
        public string dir { get; set; }

        /// <remarks/>
        [XmlAttributeAttribute()]
        public override string mcVersion { get; set; }
    }
}
