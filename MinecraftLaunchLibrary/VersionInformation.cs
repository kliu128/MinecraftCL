using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MinecraftLaunchLibrary
{
    public class VersionInformation
    {
        public List<string> DownloadedLibraryLocations { get; set; }
        public string AssetIndex { get; set; }
        public string MainClass { get; set; }
        public string MinecraftVersion { get; set; }

        /// <summary>
        /// The time that Minecraft was downloaded.
        /// </summary>
        public DateTime DownloadTime { get; set; }

        /// <summary>
        /// Includes information on how to launch the game, eg.
        /// --username ${auth_player_name}, etc.
        /// </summary>
        public string LaunchArguments { get; set; }
    }
}
