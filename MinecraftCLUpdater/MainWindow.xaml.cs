using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Diagnostics;
using System.Xml;

namespace MinecraftCLUpdater
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            if (Environment.GetCommandLineArgs().Length > 1)
            {
                // ^ Arguments were passed to the updater ^

                string downloadVersion = Environment.GetCommandLineArgs()[1];
                upgradeVersion.Content = "Upgrading MinecraftCL to " + downloadVersion;

                // Show dialog box about whether to update
                MessageBoxResult update = MessageBox.Show("Would you like to update MinecraftCL to version " + downloadVersion + "?", "Update MinecraftCL", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);

                if (update == MessageBoxResult.Yes)
                {
                    HttpWebRequest request = WebRequest.Create("http://mcdonecreative.dynu.net/MinecraftCL/" + downloadVersion + "/" + downloadVersion + ".xml") as HttpWebRequest;
                    HttpWebResponse response = request.GetResponse() as HttpWebResponse;
                    HttpStatusCode status = response.StatusCode;
                    if (status == HttpStatusCode.OK) // Update file exists and the updater can connect to the internet
                    {
                        // Read out update information from server
                        string UpdateXMLInformation = "";
                        using (System.IO.Stream tmpStream = response.GetResponseStream())
                        {
                            using (System.IO.TextReader tmpReader = new System.IO.StreamReader(tmpStream))
                            {
                                UpdateXMLInformation = tmpReader.ReadToEnd();
                            }
                        }

                        // Parse XML
                        XmlDocument doc = new XmlDocument();
                        doc.LoadXml(UpdateXMLInformation);
                        // These are comma separated lists on the XML document
                        string[] downloadFileArray = null;
                        string downloadFiles = doc.SelectSingleNode(@"//UpdateInformation/DownloadFiles").InnerText;
                        if (downloadFiles != "")
                            // List is not empty
                            downloadFileArray = downloadFiles.Split(',');

                        string[] deleteFileArray = null;
                        string deleteFiles = doc.SelectSingleNode(@"//UpdateInformation/DeleteFiles").InnerText;
                        if (deleteFiles != "")
                            // List is not empty
                            deleteFileArray = deleteFiles.Split(',');

                        BackgroundWorker downloadWorker = new BackgroundWorker();
                        downloadWorker.DoWork += (o, x) =>
                            {
                                #region Delete files if needed
                                if (deleteFileArray != null)
                                {
                                    foreach (string deleteItem in deleteFileArray)
                                    {
                                        File.Delete(System.Environment.CurrentDirectory + "\\" + deleteItem);
                                    }
                                }
                                #endregion

                                #region Download files if needed
                                if (downloadFileArray != null)
                                {
                                    downloadProgress.Dispatcher.BeginInvoke(
                                                    (Action)(() => { downloadProgress.Maximum = downloadFileArray.Length; }));

                                    foreach (string downloadItem in downloadFileArray)
                                    {
                                        // Update status label
                                        currentDownloadItem.Dispatcher.BeginInvoke(
                                                    (Action)(() => { currentDownloadItem.Content = "Downloading " + downloadItem + "..."; }));

                                        WebClient wClient = new WebClient();
                                        wClient.DownloadFile("http://mcdonecreative.dynu.net/MinecraftCL/" + downloadVersion + "/" + downloadItem, System.Environment.CurrentDirectory + @"\" + downloadItem.Replace('/', '\\'));
                                        // Increase progress bar value by one
                                        downloadProgress.Dispatcher.BeginInvoke(
                                                    (Action)(() => { downloadProgress.Value++; }));
                                    }
                                }
                                #endregion
                            };
                        downloadWorker.RunWorkerCompleted += (o, x) =>
                            {
                                Process.Start(System.Environment.CurrentDirectory + @"\MinecraftCL.exe");
                                this.Close();
                            };
                        downloadWorker.RunWorkerAsync();
                    }
                    else
                    {
                        // Could not connect to server/file does not exist/etc.
                        upgradeVersion.Content = "Error: Could not obtain update information.";
                    }
                }
                else
                {
                    Process.Start(System.Environment.CurrentDirectory + @"\MinecraftCL.exe");
                    this.Close();
                }
            }
            else
            {
                // No arguments were passed to updater
                upgradeVersion.Content = "Error: No parameters passed in.";
            }
        }
    }
}
