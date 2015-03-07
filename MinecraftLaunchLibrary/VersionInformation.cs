using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MinecraftLaunchLibrary
{
    public class VersionInformation
    {
        public List<string> LibraryLocations { get; set; }

        /// <summary>
        /// Get list of libraries as formatted string (library.jar;library2.jar;...)
        /// </summary>
        /// <returns></returns>
        public string LibraryLocationsAsString()
        {
            if (LibraryLocations.Count != 0)
            {
                StringBuilder builder = new StringBuilder();
                foreach (string library in LibraryLocations)
                {
                    builder.Append(library + ";");
                }

                // Remove final ; from string
                builder.Remove(builder.Length - 1, 1);
                return builder.ToString();
            }
            else
                return null;
        }

        public string AssetIndex { get; set; }
        public string MainClass { get; set; }
        public string MinecraftVersion { get; set; }

        /// <summary>
        /// Includes information on how to launch the game, eg.
        /// --username ${auth_player_name}, etc.
        /// </summary>
        public string LaunchArguments { get; set; }
    }
}
