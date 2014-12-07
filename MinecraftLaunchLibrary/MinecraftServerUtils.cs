using Newtonsoft.Json;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;

namespace MinecraftLaunchLibrary
{
    public static class MinecraftServerUtils
    {
        /// <summary>
        /// Server where Minecraft assets are downloaded, such as textures, sounds, etc.
        /// </summary>
        public const string AssetServer = "http://" + AssetServerHostName;
        public const string AssetServerHostName = "resources.download.minecraft.net";

        /// <summary>
        /// Server where Minecraft libraries are downloaded, such as LWJGL and realms.
        /// </summary>
        public const string LibraryServer = "https://" + LibraryServerHostName;
        public const string LibraryServerHostName = "libraries.minecraft.net";

        /// <summary>
        /// Server where Minecraft jars are downloaded, specifically $version.jar. Also has versions.json, a list of all versions.
        /// </summary>
        public const string JarServer = "http://" + JarServerHostName + "/Minecraft.Download";
        public const string JarServerHostName = "s3.amazonaws.com";

        private static bool cachedInternetConnectivity = false;
        public static bool GetMojangServerConnectivity()
        {
            if (cachedInternetConnectivity == true)
                return true;
            else
            {
                // Opens connection to mojang servers to check internet connectivity.
                // It will check either the first time it is called, or every time
                // if there is no internet connection available.
                try
                {
                    using (Ping p = new Ping())
                    {
                        p.Send(JarServerHostName);
                        p.Send(LibraryServerHostName);
                        p.Send(AssetServerHostName);
                    }
                }
                catch
                {
                    cachedInternetConnectivity = false;
                    return cachedInternetConnectivity;
                }
                // Only reaches here when all tests pass without error.
                cachedInternetConnectivity = true;
                return true;
            }
        }

        private static dynamic cachedVersionsJson = null;
        /// <summary>
        /// Gets versions.json from either local storage, Mojang servers, or cache.
        /// </summary>
        public static dynamic GetVersionsJson()
        {
            string versionJsonString = null;

            if (cachedVersionsJson != null)
                // Return the cached versions.json in memory if it exists.
                return cachedVersionsJson;

            else if (GetMojangServerConnectivity() == true)
            {
                // Download versions.json from Mojang servers if it's not cached in memory.
                using (WebClient client = new WebClient())
                {
                    try
                    {
                        versionJsonString = client.DownloadString(JarServer + "/versions/versions.json");
                    }
                    catch (WebException)
                    {
                        // If there's an exception, try loading it from a local version.
                        if (File.Exists(System.Environment.CurrentDirectory + @"\.mcl\versions.json"))
                        {
                            // Load from a stored file if it cannot be downloaded. This might be out of date,
                            // but it's better than not having it at all.
                            versionJsonString = File.ReadAllText(System.Environment.CurrentDirectory + @"\.mcl\versions.json");
                        }
                    }
                }
            }

            else if (File.Exists(System.Environment.CurrentDirectory + @"\.mcl\versions.json"))
            {
                // Load from a stored file if it cannot be downloaded. This might be out of date,
                // but it's better than not having it at all.
                versionJsonString = File.ReadAllText(System.Environment.CurrentDirectory + @"\.mcl\versions.json");
            }

            else
                // All methods of getting versions.json have failed, return null.
                return null;

            // Deserialize versions.json string as dynamic object.
            dynamic versionJson = JsonConvert.DeserializeObject(versionJsonString);
            cachedVersionsJson = versionJson;
            return versionJson;
        }
    }
}
