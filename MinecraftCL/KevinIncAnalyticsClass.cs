using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Web.UI;

namespace MinecraftCL
{
    public enum TimingsMeasureType
    {
        FTBModpackDownload,
        MojangMinecraftDownload,
        MinecraftAuthentication,
        SelectFTBDownloadServer,
        MinecraftCLInitialization
    }

    public static class Analytics
    {
        private struct AnalyticsData
	    {
		    public string MinecraftCLVersion;
            public TimingsMeasureType analyticsType;
            public TimeSpan analyticsTime;
	    }

        private static List<AnalyticsData> analyticsList = new List<AnalyticsData>();

        private static Stopwatch FTBModpackDownloadStopwatch      = new Stopwatch();
        private static Stopwatch MojangMinecraftDownloadStopwatch = new Stopwatch();
        private static Stopwatch MinecraftAuthenticationStopwatch = new Stopwatch();
        private static Stopwatch SelectFTBDownloadServerStopwatch = new Stopwatch();
        private static Stopwatch MinecraftCLInitializationStopwatch = new Stopwatch();
        // TODO: Is there a better way than listing out all the stopwatches?

        public static void BeginTiming(TimingsMeasureType type)
        {
            // Begins timing of a specific task for analytical purposes.
            switch (type)
            {
                case TimingsMeasureType.FTBModpackDownload:
                    FTBModpackDownloadStopwatch.Start();
                    break;
                case TimingsMeasureType.MojangMinecraftDownload:
                    MojangMinecraftDownloadStopwatch.Start();
                    break;
                case TimingsMeasureType.MinecraftAuthentication:
                    MinecraftAuthenticationStopwatch.Start();
                    break;
                case TimingsMeasureType.SelectFTBDownloadServer:
                    SelectFTBDownloadServerStopwatch.Start();
                    break;
                case TimingsMeasureType.MinecraftCLInitialization:
                    MinecraftCLInitializationStopwatch.Start();
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        public static void StopTiming(TimingsMeasureType type)
        {
            TimeSpan analyticsTime;
            switch (type)
            {
                case TimingsMeasureType.FTBModpackDownload:
                    FTBModpackDownloadStopwatch.Stop();
                    analyticsTime = FTBModpackDownloadStopwatch.Elapsed;
                    break;

                case TimingsMeasureType.MojangMinecraftDownload:
                    MojangMinecraftDownloadStopwatch.Stop();
                    analyticsTime = MojangMinecraftDownloadStopwatch.Elapsed;
                    break;

                case TimingsMeasureType.MinecraftAuthentication:
                    MinecraftAuthenticationStopwatch.Stop();
                    analyticsTime = MinecraftAuthenticationStopwatch.Elapsed;
                    break;

                case TimingsMeasureType.SelectFTBDownloadServer:
                    SelectFTBDownloadServerStopwatch.Stop();
                    analyticsTime = SelectFTBDownloadServerStopwatch.Elapsed;
                    break;

                case TimingsMeasureType.MinecraftCLInitialization:
                    MinecraftCLInitializationStopwatch.Stop();
                    analyticsTime = MinecraftCLInitializationStopwatch.Elapsed;
                    break;

                default:
                    throw new NotImplementedException();
            }

            analyticsList.Add(new AnalyticsData { MinecraftCLVersion = Globals.MinecraftCLVersion, analyticsType = type, analyticsTime = analyticsTime } );
        }

        /// <summary>
        /// Uploads gathered analytic data to the server, if enabled.
        /// </summary>
        public static void UploadToServer()
        {
            if (Globals.SendAnalytics)
            {
                try
                {
                    string analyticsURL = "http://mcdonecreative.dynu.net/MinecraftCL/analytics.php";
                    using (WebClient client = new WebClient())
                    {
                        foreach (AnalyticsData data in analyticsList)
                        {
                            // POST data to server
                            client.Headers[HttpRequestHeader.ContentType] = "application/x-www-form-urlencoded";
                            string HtmlResult = client.UploadString(analyticsURL, "version=" + data.MinecraftCLVersion + "&type=" + Enum.GetName(typeof(TimingsMeasureType), data.analyticsType) + "&timespan=" + data.analyticsTime);
                        }
                    }
                }
                catch(Exception e)
                {
                    DebugConsole.Print("Could not send analytic data. Exception: " + e, "Analytics.UploadToServer()", "ERROR");
                    return;
                }
            }
        }
    }
}
