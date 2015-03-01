using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;

namespace MinecraftCL.FeedTheBeast
{
    [XmlRoot("modpacks")]
    public partial class FTBModpackList : IEnumerable
    {
        /// <remarks/>
        public object script { get; set; }

        /// <remarks/>
        [XmlElementAttribute("modpack")]
        public FTBModpack[] modpack { get; set; }

        public void Add(object o)
        {
            // Placeholder to appease the XmlSerializer gods.
        }

        public FTBModpack this[int index]
        {
            get { return modpack[index]; }
            set { modpack[index] = value; }
        }

        public IEnumerator GetEnumerator()
        {
            return modpack.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlTypeAttribute()]
    public class FTBModpack : Modpack
    {
        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public new string author { get; set; }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string description { get; set; }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string image { get; set; }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string logo { get; set; }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string mods { get; set; }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public new string name { get; set; }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string oldVersions { get; set; }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string repoVersion { get; set; }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string serverPack { get; set; }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string squareImage { get; set; }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string url { get; set; }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string version { get; set; }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string dir { get; set; }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public new string mcVersion { get; set; }

        public string privatePackCode { get; set; }
    }
}
