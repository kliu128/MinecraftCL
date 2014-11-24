using System;
using System.Collections.Generic;
using System.Data;

namespace MinecraftCL.FeedTheBeast
{
    public static class FTBLocations
    {
        public const string FTB2 = "/FTB2/";
        public const string FTB2Static = "/FTB2/static/";

        public const string CurseCDN = "http://" + CurseCDNHostName;
        public const string CreeperRepo = "http://" + CreeperRepoHostName;

        public const string CurseCDNHostName = "ftb.cursecdn.com";
        public const string CreeperRepoHostName = "new.creeperrepo.net";

        public const string FullFTBMavenRepo = CurseCDN + FTB2 + "maven/";

        private static string _masterDownloadRepo;
        public static string MasterDownloadRepo
        {
            get
            {
                return _masterDownloadRepo;
            }
            private set 
            {
                _masterDownloadRepo = value;
            }
        }
        public static void SetMasterDownloadRepo(string repo)
        {
            // The master download repo can only be set once, by the Balance check class ( FTBDownload.InitializeDLServers() )
            if (Object.Equals(MasterDownloadRepo, default(string)))
                MasterDownloadRepo = repo;
            else
                throw new ReadOnlyException();
        }

        public static bool chEnabled { get; set; }
        // Set by FTBDownload.InitializeDLServers(), picked randomly
        public static string DownloadServer { get; set; }
        public static bool DownloadServersInitialized { get; set; }

        public static List<FTBModpack> PublicModpacks { get; set; }
    }
}
