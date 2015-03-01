using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace MinecraftCL
{
    public static class XmlDAL
    {
        public static void SerializeXml<T>(T serializableInformation, string xmlFile)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(T));

            using (FileStream writer = File.Create(Environment.CurrentDirectory + @"\.mcl\" + xmlFile))
            {
                serializer.Serialize(writer, serializableInformation);
            }
        }
     
        public static T DeserializeXml<T>(string xmlFile)
        {
            XmlSerializer deserializer = new XmlSerializer(typeof(T));

            using (Stream reader = File.OpenRead(System.Environment.CurrentDirectory + @"\.mcl\" + xmlFile))
            {
                return (T)(deserializer.Deserialize(reader));
            }
        }

        public static T DeserializeXml<T>(XmlDocument xml)
        {
            XmlSerializer deserializer = new XmlSerializer(typeof(T));

            using (TextReader reader = new StringReader(xml.OuterXml))
            {
                return (T)(deserializer.Deserialize(reader));
            }
        }
    }
}
