using MinecraftCL.FeedTheBeast;
using MinecraftLaunchLibrary;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Media;
using System.IO;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Xml;
using System.Xml.Serialization;

namespace MinecraftCL
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        bool usernameEntered = false;
        bool passwordEntered = false;
        string mcInstallDir = "";
        bool autoBackupWorlds = false;

        public class comboBox
        {
            public string Text { get; set; }

            public override string ToString()
            {
                return Text;
            }
        }
        public SettingsWindowViewModel ViewModel = new SettingsWindowViewModel();
        public MainWindow()
        {
            Globals.DebugOn = true;

            // Begin timing initialization of MainWindow()
            Analytics.BeginTiming(TimingsMeasureType.MinecraftCLInitialization);

            // Set up event for uploading analytics data to server when program exits
            Application.Current.Exit += (o, x) =>
                {
                    Analytics.UploadToServer();
                };
            
            // Set up global exception handler
            AppDomain currentAppDomain = AppDomain.CurrentDomain;
            currentAppDomain.UnhandledException += (o, x) =>
                {
                    UnhandledExceptionWindow exceptionWindow = new UnhandledExceptionWindow();
                    exceptionWindow.exceptionDetailsBox.Text = ((Exception)x.ExceptionObject).ToString();
                    exceptionWindow.ShowDialog();
                    Environment.Exit(1);
                };

            InitializeComponent();
            
            DebugConsole.Print("Begin start of MinecraftCL MainWindow.xaml.cs at " + DateTime.Now + ".", "MainWindow");

            #region Infrastructure Check (Check if configuration XMLs and updater exist, download/create them if needed. Also set up FTB download servers)
            // Create .mcl directory
            if (!Directory.Exists(System.Environment.CurrentDirectory + @"\.mcl\"))
                Directory.CreateDirectory(System.Environment.CurrentDirectory + @"\.mcl\");

            // Create ProfileInformation.xml
            if (!File.Exists(System.Environment.CurrentDirectory + @"\.mcl\ProfileInformation.xml"))
            {
                // Create the file with a default "Latest Version" profile"
                ObservableCollection<CLProfile> defaultProfile = new ObservableCollection<CLProfile>();
                defaultProfile.Add(
                    new CLProfile
                    {
                        Name = "Latest Version",
                        MojangVersion = "latest-release",

                        useCustomMinecraftDirectory = false,
                        customMinecraftDirectory = "",

                        useCustomJavaArguments = false,
                        javaArguments = "-Xmx1024M",

                        useCustomJavaEXE = false,
                        customJavaEXE = ""
                    });

                XmlDAL.SerializeXml<ObservableCollection<CLProfile>>(defaultProfile, "ProfileInformation.xml");
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

            // Create MinecraftCLSettings.xml if it doesn't exist
            if (!File.Exists(System.Environment.CurrentDirectory + @"\.mcl\MinecraftCLSettings.xml"))
            {
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

            // Set up FTB download servers and grab modpack list asynchronously
            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += (o, x) =>
                {
                    FTBLocations.DownloadServersInitialized = FTBServerUtils.InitializeDLServers();
                    Exception getModpackResult;
                    FTBLocations.PublicModpacks = FTBUtils.GetModpacks(out getModpackResult);
                };
            worker.RunWorkerAsync();
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
            if (settingsDoc.SelectSingleNode("/settings/EnableAnalytics") != null)
            {
                // Load Analytics setting
                ViewModel.enableAnalytics = Convert.ToBoolean(settingsDoc.SelectSingleNode("/settings/EnableAnalytics").InnerText);
                Globals.SendAnalytics = Convert.ToBoolean(settingsDoc.SelectSingleNode("/settings/EnableAnalytics").InnerText);
            }
            #endregion

            // Load profiles
            ViewModel.profileCollection = XmlDAL.DeserializeXml<ObservableCollection<CLProfile>>("ProfileInformation.xml");
            ViewModel.profileCollection.BubbleSort();
            this.DataContext = ViewModel;

            profileSelectBox.SelectedIndex = 0;

            // Set selected profile to the one last used
            int index = -1;
            foreach (CLProfile profileSelectBoxItem in ViewModel.profileCollection)
            {
                index++;
                if (profileSelectBoxItem.Name == savedLastUsedProfile)
                {
                    profileSelectBox.SelectedIndex = index;
                }
            }

            Analytics.StopTiming(TimingsMeasureType.MinecraftCLInitialization);
        }

        public void Button_Click(object sender, RoutedEventArgs e)
        {
            // Begin starting the game
            DebugConsole.Print("Starting minecraft version " + ((CLProfile)profileSelectBox.SelectedValue).MojangVersion + ".", "MainWindow");

            ControlsGrid.IsEnabled = false;

            LaunchGame.MinecraftStartedEvent += LaunchGame_MinecraftStartedEvent;
            CLProfile currentProfile = ((CLProfile)profileSelectBox.SelectedValue);

            startGameVariables sGV = new startGameVariables
            {
                Username = usernameBox.Text,
                Password = passwordBox.Password,

                Version = currentProfile.MojangVersion,
                JavaArguments = currentProfile.javaArguments,
            };

            LaunchGameReturn launchReturn = LaunchGame.DownloadAndStartGame(currentProfile, sGV, autoBackupWorlds, ((CLProfile)profileSelectBox.SelectedItem).ToString());

            if (launchReturn.returnType == LaunchReturnType.SuccessfulLaunch)
            {
                DebugConsole.Print("Successful Minecraft session - closing!", "MainWindow.Button_Click()");
                this.Close();
                return;
            }
            else if (launchReturn.returnType == LaunchReturnType.AuthenticationError)
            {
                debugLabel.Text = "Authentication error. Check your username and password.";
                debugLabel.Foreground = Brushes.Red;
            }
            else
            {
                ErrorWindow errorWindow = new ErrorWindow(((CLProfile)(profileSelectBox.SelectedValue)), sGV);
                errorWindow.errorMessageBox.Text = launchReturn.returnInfo;
                switch (launchReturn.returnType)
                {
                    case LaunchReturnType.CouldNotLocateJava:
                        errorWindow.errorTypeBox.Content = "We could not locate your Java installation. Try reinstalling Java.";
                        break;
                    case LaunchReturnType.MinecraftError:
                        errorWindow.errorTypeBox.Content = "It looks like an error occurred while running Minecraft.";
                        break;
                    case LaunchReturnType.VersionInformationError:
                        errorWindow.errorTypeBox.Content = "It looks like an error occurred while getting Minecraft version info.";
                        break;
                    case LaunchReturnType.DownloadError:
                        errorWindow.errorTypeBox.Content = "It looks like an error occurred while downloading.";
                        break;
                    default:
                        throw new Exception();
                }
                errorWindow.ShowDialog();
            }

            ControlsGrid.IsEnabled = true;
            LaunchGame.MinecraftStartedEvent -= LaunchGame_MinecraftStartedEvent;

            this.WindowState = System.Windows.WindowState.Normal;
            this.ShowInTaskbar = true;
            this.Show();
        }

        void LaunchGame_MinecraftStartedEvent()
        {
            this.Hide();
            this.ShowInTaskbar = false;
            this.WindowState = System.Windows.WindowState.Minimized;

            DebugConsole.Print("Hid MainWindow due to Minecraft starting.", "LaunchGame_MinecraftStartedEvent()");
        }

        private void settingsButton_Click(object sender, RoutedEventArgs e)
        {
            SettingsWindow settingsWindow = new SettingsWindow(ViewModel);
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
            if (profileSelectBox.SelectedValue != null)
            {
                if (((CLProfile)profileSelectBox.SelectedValue).useCustomMinecraftDirectory == false)
                {
                    // If there is no custom directory specified for the current profile, set it to the current directory
                    mcInstallDir = System.Environment.CurrentDirectory;
                }
                else
                {
                    // Otherwise, use the one specified
                    mcInstallDir = ((CLProfile)profileSelectBox.SelectedValue).customMinecraftDirectory;
                }
            }
        }
    }
}
