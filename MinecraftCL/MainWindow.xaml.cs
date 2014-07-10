using Ionic.Zip;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Web.Helpers;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml;
using System.Xml.Linq;
using MinecraftCL.FeedTheBeast;
using System.Xml.Serialization;
using System.Runtime.Serialization;

namespace MinecraftCL
{
    public struct downloadVariables
    {
        public string mcVersion;
        public string mcInstallDir;
        public bool ValidateFiles;
        public DownloadDialog DownloadDialog;
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        /* MINECRAFTCL VERSION */
        const double mCLVersion = 0.02;
        /* ------------------- */

        bool usernameEntered = false;
        bool passwordEntered = false;
        string mcInstallDir = "";
        dynamic mcVersionDynamic = null;
        bool autoBackupWorlds = false;

        public class comboBox
        {
            public string Text { get; set; }

            public override string ToString()
            {
                return Text;
            }
        }
        public WindowViewModel ViewModel = new WindowViewModel();
        public MainWindow()
        {
            InitializeComponent();
            Globals.DebugOn = true;
            DebugConsole.Print("Begin start of MinecraftCL MainWindow.xaml.cs at " + DateTime.Now + ".", "MainWindow");

            #region Internet Check
            // Opens connection to google.com to check internet connectivity
            try
            {
                using (var client = new WebClient())
                using (var stream = client.OpenRead("http://www.google.com"))
                {
                    Globals.HasInternetConnectivity = true;
                }
            }
            catch
            {
                Globals.HasInternetConnectivity = false;
                playButton.Content = "Play Offline!";
            }
            #endregion

            #region Infrastructure Check (Check if configuration XMLs and updater exist, download/create them if needed. Also set up FTB download servers)
            // Create .mcl directory
            if (!Directory.Exists(System.Environment.CurrentDirectory + @"\.mcl\"))
                Directory.CreateDirectory(System.Environment.CurrentDirectory + @"\.mcl\");

            // Create ProfileInformation.xml
            if (!File.Exists(System.Environment.CurrentDirectory + @"\.mcl\ProfileInformation.xml"))
            {
                // Create the file with a default "Latest Version" profile"
                XmlSerializer serializer = new XmlSerializer(typeof(ObservableCollection<profileSelection>));
                using (TextWriter writer = File.CreateText(Environment.CurrentDirectory + @"\.mcl\ProfileInformation.xml"))
                {
                    ObservableCollection<profileSelection> defaultProfile = new ObservableCollection<profileSelection>();
                    defaultProfile.Add(
                        new profileSelection
                        {
                            Name = "Latest Version",
                            VersionType = MinecraftCL.VersionType.Mojang,
                            MojangVersion = "latest-release",

                            useCustomMinecraftDirectory = false,
                            customMinecraftDirectory = "",

                            useCustomJavaArguments = false,
                            javaArguments = "-Xmx1024M",

                            useCustomJavaEXE = false,
                            customJavaEXE = ""
                        });

                    serializer.Serialize(writer, defaultProfile);
                }
            }

            // Create VersionInformation.xml
            if (!File.Exists(System.Environment.CurrentDirectory + @"\.mcl\VersionInformation.xml"))
            {
                // Create the version information file if it doesn't exist
                XmlTextWriter XMLCreate = new XmlTextWriter(System.Environment.CurrentDirectory + @"\.mcl\VersionInformation.xml", null);
                XMLCreate.WriteStartDocument();
                XMLCreate.WriteStartElement("versions", "");
                XMLCreate.WriteEndElement();
                XMLCreate.WriteEndDocument();
                XMLCreate.Close();
            }

            // Create MinecraftCLSettings.xml
            if (!File.Exists(System.Environment.CurrentDirectory + @"\.mcl\MinecraftCLSettings.xml"))
            {
                // Create the version information file if it doesn't exist
                XmlTextWriter XMLCreate = new XmlTextWriter(System.Environment.CurrentDirectory + @"\.mcl\MinecraftCLSettings.xml", null);
                XMLCreate.WriteStartDocument();
                XMLCreate.WriteStartElement("settings", "");
                XMLCreate.WriteEndElement();
                XMLCreate.WriteEndDocument();
                XMLCreate.Close();
            }

            if (!File.Exists(System.Environment.CurrentDirectory + @"\.mcl\ModpackSettings.xml"))
            {
                File.Create(System.Environment.CurrentDirectory + @"\.mcl\ModpackSettings.xml");
            }

            // Download MinecraftCLUpdater
            if (Globals.HasInternetConnectivity == true)
            {
                HttpWebResponse updaterFileResponse = null;
                try
                {
                    HttpWebRequest updaterFile = (HttpWebRequest)WebRequest.Create("http://mcdonecreative.dynu.net/MinecraftCL/MinecraftCLUpdater.exe");
                    updaterFile.Method = "HEAD";
                    updaterFile.Timeout = 5000;
                    updaterFileResponse = (HttpWebResponse)updaterFile.GetResponse();
                }
                catch (Exception)
                {
                    DebugConsole.Print("Could not check for an update to the updater.", "MainWindow", "WARN");
                }
                finally
                {
                    if (updaterFileResponse != null && updaterFileResponse.StatusCode == HttpStatusCode.OK)
                    {
                        DateTime localFileModifiedTime = File.GetLastWriteTime(System.Environment.CurrentDirectory + @"\.mcl\MinecraftCLUpdater.exe");
                        DateTime onlineFileModifiedTime = updaterFileResponse.LastModified;
                        if (onlineFileModifiedTime > localFileModifiedTime)
                        {
                            using (WebClient wC = new WebClient())
                            {
                                wC.DownloadFile("http://mcdonecreative.dynu.net/MinecraftCL/MinecraftCLUpdater.exe", System.Environment.CurrentDirectory + @"\.mcl\MinecraftCLUpdater.exe");
                            }
                        }
                    }
                }
            }

            // Set up FTB download servers and grab modpack list asynchronously
            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += (o, x) =>
                {
                    FTBLocations.DownloadServersInitialized = FTBUtils.InitializeDLServers();
                    Exception getModpackResult;
                    FTBLocations.PublicModpacks = FTBUtils.GetModpacks(out getModpackResult);
                };
            worker.RunWorkerAsync();
            #endregion

            #region Update Check
            if (Globals.HasInternetConnectivity == true && File.Exists(System.Environment.CurrentDirectory + @"\.mcl\MinecraftCLUpdater.exe"))
            {
                HttpStatusCode? status = null;
                HttpWebResponse response = null;
                try
                {
                    // Contact server for latest version
                    HttpWebRequest request = WebRequest.Create("http://mcdonecreative.dynu.net/MinecraftCL/latest.txt") as HttpWebRequest;
                    request.Timeout = 1000; // Set quick timeout; we want to start the program as fast as possible
                    response = request.GetResponse() as HttpWebResponse;
                    status = response.StatusCode;
                }
                catch (Exception e)
                {
                    MessageWindow updateErrorWindow = new MessageWindow();
                    updateErrorWindow.closeTimeoutMilliseconds = 2500;
                    if (e is WebException)
                        updateErrorWindow.messageText.Text = "Warning: Could not connect to the update server.";
                    else
                        updateErrorWindow.messageText.Text = "An error occurred during the update check. " + e;

                    updateErrorWindow.ShowDialog();
                }
                finally
                {
                    if (status != null && status == HttpStatusCode.OK)
                    {
                        string latestLauncherVersion = "";
                        // We have internet connection and the file is available, read it out
                        using (System.IO.Stream tmpStream = response.GetResponseStream())
                        {
                            using (System.IO.TextReader tmpReader = new System.IO.StreamReader(tmpStream))
                            {
                                latestLauncherVersion = tmpReader.ReadToEnd();
                            }
                        }

                        Assembly executingAssembly = Assembly.GetExecutingAssembly();
                        FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(executingAssembly.Location);

                        if (Version.Parse(latestLauncherVersion) > Version.Parse(fvi.ProductVersion))
                        {
                            // A new version is available
                            ProcessStartInfo updaterStartInfo = new ProcessStartInfo();
                            updaterStartInfo.FileName = System.Environment.CurrentDirectory + @"\.mcl\MinecraftCLUpdater.exe";
                            updaterStartInfo.Arguments = latestLauncherVersion;
                            Process.Start(updaterStartInfo);    // Start updater with arguments
                            Environment.Exit(0);
                        }
                    }
                }
            }
            #endregion

            #region Load Settings (Username, Password, etc.)
            string savedLastUsedProfile = "";
            XmlDocument settingsDoc = new XmlDocument();
            settingsDoc.Load(System.Environment.CurrentDirectory + @"\.mcl\MinecraftCLSettings.xml");

            if (settingsDoc.SelectSingleNode("/settings/Username") != null)
                // Load saved username
                usernameBox.Text = settingsDoc.SelectSingleNode("/settings/Username").InnerText;
            else
                usernameBox.Text = "Username/Email Address";

            if (settingsDoc.SelectSingleNode("/settings/Password") != null)
                // Load saved password
                passwordBox.Password = StringCipher.Decrypt(settingsDoc.SelectSingleNode("/settings/Password").InnerText, "minecraftCLNoOneWillGuessThis");

            if (settingsDoc.SelectSingleNode("/settings/LastUsedProfile") != null)
                // Load last used profile
                savedLastUsedProfile = settingsDoc.SelectSingleNode("/settings/LastUsedProfile").InnerText;

            if (settingsDoc.SelectSingleNode("/settings/AutoBackupWorlds") != null)
            {
                // Load auto backup world setting
                autoBackupWorlds = Convert.ToBoolean(settingsDoc.SelectSingleNode("/settings/AutoBackupWorlds").InnerText);
                ViewModel.autoBackupWorlds = autoBackupWorlds;
            }
            #endregion

            // Load profiles
            using (FileStream stream = new FileStream(System.Environment.CurrentDirectory + @"\.mcl\ProfileInformation.xml", FileMode.Open))
            {
                XmlSerializer deserializer = new XmlSerializer(typeof(ObservableCollection<profileSelection>));
                ViewModel.profileCollection = (ObservableCollection<profileSelection>)deserializer.Deserialize(stream);
            }
            ViewModel.profileCollection.BubbleSort();
            this.DataContext = ViewModel;

            profileSelectBox.SelectedIndex = 0;

            // Set selected profile to the one last used
            int index = -1;
            foreach (profileSelection profileSelectBoxItem in ViewModel.profileCollection)
            {
                //TODO: For some reason, profileSelectBox.Items.Count == 0. Had to use ViewModel.profileCollection in foreach instead.
                index++;
                if (profileSelectBoxItem.Name == savedLastUsedProfile)
                {
                    profileSelectBox.SelectedIndex = index;
                }
            }

            // Get version info
            if (Globals.HasInternetConnectivity == true)
            {
                try
                {
                    using (WebClient webClient = new WebClient())
                    {
                        webClient.DownloadFile("http://s3.amazonaws.com/Minecraft.Download/versions/versions.json", System.Environment.CurrentDirectory + @"\.mcl\versions.json");
                    }
                    string mcVersionJSONString;
                    using (StreamReader streamReader = new StreamReader(System.Environment.CurrentDirectory + @"\.mcl\versions.json", Encoding.UTF8))
                    {
                        mcVersionJSONString = streamReader.ReadToEnd();
                    }
                    mcVersionDynamic = Json.Decode(mcVersionJSONString);
                }
                catch (WebException wE)
                {
                    MessageWindow downloadErrorWindow = new MessageWindow();
                    downloadErrorWindow.messageText.Text = "Could not connect to Mojang servers. Exception: " + wE.Message;
                    downloadErrorWindow.closeTimeoutMilliseconds = 2500;
                    downloadErrorWindow.ShowDialog();
                }
            }
        }

        public void Button_Click(object sender, RoutedEventArgs e)
        {
            // Begin starting the game
            DebugConsole.Print("Starting minecraft version " + ((profileSelection)profileSelectBox.SelectedValue).MojangVersion + ".", "MainWindow");

            downloadVariables downloadVar = new downloadVariables();

            startGameVariables sGV = new startGameVariables
            {
                Username = usernameBox.Text,
                Password = passwordBox.Password,

                InstallDir = mcInstallDir,
                LastUsedProfile = ((profileSelection)profileSelectBox.SelectedItem).ToString(),
                mcVersionDynamic = mcVersionDynamic,
                JavaArguments = ((profileSelection)profileSelectBox.SelectedValue).javaArguments,
                Version = ((profileSelection)profileSelectBox.SelectedValue).MojangVersion,
                AutoBackupWorld = autoBackupWorlds
            };

            string authenticateReturnString;
            bool authReturn = MinecraftUtils.authenticateMinecraft(ref sGV, out authenticateReturnString);
            debugLabel.Text = authenticateReturnString;

            if (authReturn == false)
            {
                DebugConsole.Print("Could not authenticate using Yggdrasil for " + usernameBox.Text, "MainWindow", "WARN");
                return;
            }

            // This function retrieves the list of required libraries for minecraft, substitutes in the
            // authentication information, and replaces the "latest-release" and "latest-snapshot" with
            // the actual versions.
            string versionErrorInfo;
            bool getVersionReturn = MinecraftUtils.getVersionInformation(ref sGV, out versionErrorInfo);
            
            if (versionErrorInfo != "")
            {
                debugLabel.Text = versionErrorInfo;
                return;
            }
            
            string downloadReturn = "success";

            downloadVar.mcVersion = ((profileSelection)profileSelectBox.SelectedValue).MojangVersion;
            downloadVar.mcInstallDir = mcInstallDir;
            downloadVar.ValidateFiles = false;

            if (getVersionReturn == false)
            {
                // Disable all UI elements
                usernameBox.IsEnabled = false;
                usernameBox.Foreground = Brushes.Gray;
                passwordBox.IsEnabled = false;
                passwordBox.Foreground = Brushes.Gray;
                settingsButton.IsEnabled = false;
                playButton.IsEnabled = false;
                playButton.Foreground = Brushes.Gray;
                profileSelectBox.IsEnabled = false;
                profileSelectBox.Foreground = Brushes.Gray;

                downloadVar.DownloadDialog = new DownloadDialog();

                // Create new thread to download files
                var worker = new BackgroundWorker();
                worker.DoWork += (o, x) =>
                    {
                        // Fill in the values for downloading and begin
                        // downloading game files
                        downloadReturn = MinecraftUtils.DownloadGame(downloadVar);
                    };
                worker.RunWorkerCompleted += (o, x) =>
                    {
                        downloadVar.DownloadDialog.downloadIsInProgress = false;

                        if (downloadReturn == "success")
                        {
                            // If the download was successful, get the version information and start the game
                            getVersionReturn = MinecraftUtils.getVersionInformation(ref sGV, out versionErrorInfo);
                            if (versionErrorInfo != "")
                            {
                                debugLabel.Text = versionErrorInfo;
                                return;
                            }

                            WindowState = System.Windows.WindowState.Minimized;
                            ShowInTaskbar = false;
                            Hide();

                            var startGameResult = MinecraftUtils.Start(sGV);
                            switch (startGameResult.ReturnCode)
                            {
                                case startMinecraftReturnCode.StartedMinecraft:
                                    // We're golden! Close the program
                                    this.Close();
                                    break;
                                case startMinecraftReturnCode.CouldNotLocateJava:
                                    Show();
                                    ShowInTaskbar = true;
                                    WindowState = System.Windows.WindowState.Normal;

                                    debugLabel.Text = "Could not locate Java. Install Java or specify a custom java path in the profile settings.";
                                    break;
                                case startMinecraftReturnCode.MinecraftError:
                                    // Looks like minecraft encountered an error. :(
                                    ErrorWindow eWindow = new ErrorWindow(sGV, downloadVar);
                                    eWindow.errorMessageBox.Text = startGameResult.Error;
                                    eWindow.Show();

                                    this.Close();
                                    break;
                            }
                        }
                        else
                        {
                            // Display the error, if there was one
                            debugLabel.Text = downloadReturn;

                            // Reenable UI elements
                            usernameBox.IsEnabled = true;
                            usernameBox.Foreground = Brushes.Black;
                            passwordBox.IsEnabled = true;
                            passwordBox.Foreground = Brushes.Black;
                            settingsButton.IsEnabled = true;
                            playButton.IsEnabled = true;
                            playButton.Foreground = Brushes.Black;
                            profileSelectBox.IsEnabled = true;
                            profileSelectBox.Foreground = Brushes.Black;
                        }
                    };
                worker.WorkerReportsProgress = true;
                worker.RunWorkerAsync();
                downloadVar.DownloadDialog.downloadIsInProgress = true;
                downloadVar.DownloadDialog.ShowDialog();
            }
            else
            {
                WindowState = System.Windows.WindowState.Minimized;
                ShowInTaskbar = false;
                Hide();
                    
                var startGameResult = MinecraftUtils.Start(sGV);
                switch (startGameResult.ReturnCode)
                {
                    case startMinecraftReturnCode.StartedMinecraft:
                        // We're golden! Close the program
                        this.Close();
                        break;
                    case startMinecraftReturnCode.CouldNotLocateJava:
                        Show();
                        ShowInTaskbar = true;
                        WindowState = System.Windows.WindowState.Normal;

                        debugLabel.Text = "Could not locate Java. Install Java or specify a custom java path in the profile settings.";
                        break;
                    case startMinecraftReturnCode.MinecraftError:
                        // Looks like minecraft encountered an error. :(
                        ErrorWindow eWindow = new ErrorWindow(sGV, downloadVar);
                        eWindow.errorMessageBox.Text = startGameResult.Error;
                        eWindow.Show();

                        this.Close();
                        break;
                }
            }
        }

        

        

        private void settingsButton_Click(object sender, RoutedEventArgs e)
        {
            SettingsWindow settingsWindow = new SettingsWindow(ViewModel, mcVersionDynamic);
            settingsWindow.ShowDialog();
        }
        private void usernameBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (usernameEntered == false)
            {
                usernameBox.Text = "";
                usernameEntered = true;
            }

        }

        private void passwordBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (passwordEntered == false)
            {
                passwordBox.Password = "";
                passwordEntered = true;
            }

        }

        private void profileSelectBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (((profileSelection)profileSelectBox.SelectedValue).useCustomMinecraftDirectory == false)
            {
                // If there is no custom directory specified for the current profile, set it to the current directory
                mcInstallDir = System.Environment.CurrentDirectory;
            }
            else
            {
                // Otherwise, use the one specified
                mcInstallDir = ((profileSelection)profileSelectBox.SelectedValue).customMinecraftDirectory;
            }
        }
    }
}
