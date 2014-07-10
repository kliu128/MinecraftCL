using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;

namespace MinecraftCL.FeedTheBeast
{
    public class FTBModpack : Modpack
    {
        public string repoVersion { get; set; }
        public string url { get; set; }
        public string dir { get; set; }
    }
}
