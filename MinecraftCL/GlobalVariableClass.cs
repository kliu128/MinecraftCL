using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;

namespace MinecraftCL
{
    static class Globals
    {
        public static bool DebugOn { get; set; }
        public static bool SendAnalytics { get; set; }

        // This is set once, the first time get is run. After, it just pulls from _MinecraftCLVersion.
        private static string _MinecraftCLVersion = null;
        public static string MinecraftCLVersion
        {
            get
            {
                if (_MinecraftCLVersion == null)
                {
                    Assembly executingAssembly = Assembly.GetExecutingAssembly();
                    FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(executingAssembly.Location);

                    _MinecraftCLVersion = fvi.ProductVersion;
                }
                return _MinecraftCLVersion;
            }
        }
    }
}
