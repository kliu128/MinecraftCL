using MinecraftLaunchLibrary;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml;

namespace MinecraftCL
{
    /// <summary>
    /// Interaction logic for SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow : Window
    {
        /// <summary>
        /// Minecraft version list. Includes all versions - old, release, and snapshot.
        /// </summary>
        List<MinecraftVersion> mcVersionList = new List<MinecraftVersion>();

        WebClient mcDownload = new WebClient();
        bool viewSnapshots = false;
        bool viewOldVersions = false;
        bool useCustomMCDir = false;
        string mcInstallDir = System.Environment.CurrentDirectory;
        string mcVersion = "1.7.5";
        bool useCustomJavaArguments = false;
        bool useCustomJavaEXE = false;
        ObservableCollection<Modpack> modpackList = new ObservableCollection<Modpack>();

        public SettingsWindowViewModel ViewModel { get; set; }

        public struct MinecraftVersion
        {
            public string DisplayName { get; set; }
            public string id { get; set; }
            public string type { get; set; }
        }
        
        public void updateVersionBox()
        {
            if (profileSelectBox.SelectedValue != null && ((CLProfile)profileSelectBox.SelectedValue).showOldVersions == true)
            {
                viewOldVersions = true;
            }
            if (profileSelectBox.SelectedValue != null && ((CLProfile)profileSelectBox.SelectedValue).showSnapshots == true)
            {
                viewSnapshots = true;
            }

            MinecraftVersion lastSelectedItem = new MinecraftVersion { };
            if (versionSelectBox.SelectedItem != null)
                lastSelectedItem = ((MinecraftVersion)versionSelectBox.SelectedItem);
            ViewModel.versionCollection.Clear();
            foreach (MinecraftVersion version in mcVersionList)
            {
                // 5 types of versions: snapshot, old_beta, old_alpha, local, and release.
                // The combobox only shows snapshots or old versions if it is told
                // to.
                switch (version.type)
                {
                    case "snapshot":
                        if (viewSnapshots == true)
                        {
                            ViewModel.versionCollection.Add(version);
                        }
                        break;
                    case "old_beta":
                        if (viewOldVersions == true)
                        {
                            ViewModel.versionCollection.Add(version);
                        }
                        break;
                    case "old_alpha":
                        if (viewOldVersions == true)
                        {
                            ViewModel.versionCollection.Add(version);
                        }
                        break;
                    default:
                        ViewModel.versionCollection.Add(version);
                        break;
                }
            }
            // If the version box now contains the last item selected, then put it back
            // as the selected item.
            if (versionSelectBox.Items.Contains(lastSelectedItem))
            {
                versionSelectBox.SelectedItem = lastSelectedItem;
            }
            else if (versionSelectBox.SelectedIndex != -1)
            {
                versionSelectBox.SelectedIndex = 0;
            }
        }

        public List<MinecraftVersion> getMinecraftVersions()
        {
            dynamic mcVersionDynamic = MinecraftServerUtils.GetVersionsJson();
            List<MinecraftVersion> versionList = new List<MinecraftVersion>();

            if (mcVersionDynamic != null) // If there is internet connectivity, add versions from Mojang servers
            {
                // Add latest release version
                versionList.Add(new MinecraftVersion { DisplayName = "Latest Version (" + mcVersionDynamic.latest.release + ")", id = "latest-release", type = "release" });
                // Add latest snapshot version
                versionList.Add(new MinecraftVersion { DisplayName = "Latest Version (" + mcVersionDynamic.latest.snapshot + ")", id = "latest-snapshot", type = "snapshot" });

                foreach (var item in mcVersionDynamic.versions)
                {
                    // Add all other versions
                    versionList.Add(new MinecraftVersion { DisplayName = item.id, id = item.id, type = item.type });
                }
            }

            // Add local versions
            XmlDocument xDoc = new XmlDocument();
            xDoc.Load(System.Environment.CurrentDirectory + @"\.mcl\VersionInformation.xml");
            foreach (XmlNode item in xDoc.SelectNodes(@"/versions/version"))
            {
                bool versionInServer = false;
                XmlAttributeCollection xAC = item.Attributes;
                if (mcVersionDynamic != null)
                {
                    // If mcVersionDynamic isn't null, check through to see if the version is on the server.
                    // Skip this check if mcVersionDynamic is null, which means there is no internet connectivity
                    foreach (var version in mcVersionDynamic.versions)
                    {
                        if (version.id == xAC["version"].Value)
                        {
                            versionInServer = true;
                        }
                    }
                }

                if (versionInServer == false)
                {
                    // Version does not exist in server (or server could not be accessed), it is a local version
                    versionList.Add(new MinecraftVersion
                    {
                        DisplayName = xAC["version"].Value + " (Local)",
                        id = xAC["version"].Value,
                        type = "local"
                    });
                }
            }

            return versionList;
        }

        public SettingsWindow(SettingsWindowViewModel WindowViewModel)
        {
            InitializeComponent();
            ViewModel = WindowViewModel;
            this.DataContext = ViewModel;

            mcVersionList = getMinecraftVersions();

            versionSelectBox.DisplayMemberPath = "DisplayName";
            
            ViewModel.profileCollection.BubbleSort();
            profileSelectBox.SelectedIndex = 0;

            updateVersionBox();

            if (ViewModel.profileCollection[0].showOldVersions == true)
            {
                viewOldVersions = true;
                updateVersionBox();
            }
            ((System.Windows.Controls.ComboBox)versionSelectBox).GetBindingExpression(System.Windows.Controls.ComboBox.SelectedValueProperty)
                    .UpdateTarget();

            // Load modpacks from ModpackSettings.xml
            try
            {
                modpackList = XmlDAL.DeserializeXml<ObservableCollection<Modpack>>("ModpackInformation.xml");
            }
            catch (Exception)
            {
                modpackList.Add(new Modpack { name = "Add a modpack!", Type = ModpackType.PlaceholderModpack });
            }
            ViewModel.modpackList = modpackList;
            modpackSelectBox.SelectedIndex = 0;
        }

        private void saveButton_Click(object sender, RoutedEventArgs e)
        {
            // Save profiles via serialization
            XmlDAL.SerializeXml<ObservableCollection<CLProfile>>(ViewModel.profileCollection, "ProfileInformation.xml");

            // Save modpack list changes
            XmlDAL.SerializeXml<ObservableCollection<Modpack>>(modpackList, "ModpackInformation.xml");

            #region Save settings to MinecraftCLSettings.xml
            XmlDocument settingsDoc = new XmlDocument();
            settingsDoc.Load(System.Environment.CurrentDirectory + "\\.mcl\\MinecraftCLSettings.xml");
            XmlElement settingsDocRoot = settingsDoc.DocumentElement;

            // Save auto-backup worlds setting
            if (settingsDoc.SelectSingleNode("/settings/AutoBackupWorlds") != null)
            {
                // Node exists, we can simply update it
                settingsDoc.SelectSingleNode("/settings/AutoBackupWorlds").InnerText = ViewModel.autoBackupWorlds.ToString();
            }
            else
            {
                // Node does not exist, create it and set its value
                XmlElement autoBackupWorldsElement = settingsDoc.CreateElement("AutoBackupWorlds");
                autoBackupWorldsElement.InnerText = ViewModel.autoBackupWorlds.ToString();
                settingsDocRoot.AppendChild(autoBackupWorldsElement);
            }

            // Save Analytics settings
            if (settingsDoc.SelectSingleNode("/settings/EnableAnalytics") != null)
                // Node exists, update it
                settingsDoc.SelectSingleNode("/settings/EnableAnalytics").InnerText = ViewModel.enableAnalytics.ToString();
            else
            {
                // Node does not exist, create it and set its value
                XmlElement enableAnalyticsElement = settingsDoc.CreateElement("EnableAnalytics");
                enableAnalyticsElement.InnerText = ViewModel.autoBackupWorlds.ToString();
                settingsDocRoot.AppendChild(enableAnalyticsElement);
            }

            settingsDoc.Save(System.Environment.CurrentDirectory + @"\.mcl\MinecraftCLSettings.xml");
            #endregion

            Globals.SendAnalytics = ViewModel.enableAnalytics;

            // Create a Timer with a Normal Priority
            DispatcherTimer fadeTimer = new DispatcherTimer();

            // Set the Interval to 2 seconds
            fadeTimer.Interval = TimeSpan.FromMilliseconds(2000);

            // Set the callback to just show the time ticking away
            // NOTE: We are using a control so this has to run on 
            // the UI thread
            fadeTimer.Tick += new EventHandler(delegate(object s, EventArgs a)
            {
                informationLabel.Content = "";
                fadeTimer.Stop();
            });

            // Start the timer
            fadeTimer.Start();

            informationLabel.Content = "Settings successfully saved.";
        }

        private void versionSelectBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (versionSelectBox.SelectedValue != null)
            {
                mcVersion = versionSelectBox.SelectedValue.ToString();
            }
            if (ViewModel != null && ViewModel.SelectedProfile != null && ViewModel.SelectedProfile.MojangVersion != null && versionSelectBox.SelectedValue != null)
                ((System.Windows.Controls.ComboBox)versionSelectBox).GetBindingExpression(System.Windows.Controls.ComboBox.SelectedValueProperty)
                        .UpdateSource();
        }

        private void showSnapshots_Checked(object sender, RoutedEventArgs e)
        {
            viewSnapshots = true;
            updateVersionBox();
        }

        private void showSnapshots_Unchecked(object sender, RoutedEventArgs e)
        {
            viewSnapshots = false;
            updateVersionBox();
        }

        private void showOldVersions_Checked(object sender, RoutedEventArgs e)
        {
            viewOldVersions = true;
            updateVersionBox();
        }

        private void showOldVersions_Unchecked(object sender, RoutedEventArgs e)
        {
            viewOldVersions = false;
            updateVersionBox();
        }

        private void browseMCFolderButton_Click(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog browseFolder = new FolderBrowserDialog();
            if (useCustomMCDir == true)
            {
                browseFolder.ShowDialog();
                minecraftCustomDirectoryBox.Text = browseFolder.SelectedPath;
            }
        }

        private void useCustomMCDirectory_Checked(object sender, RoutedEventArgs e)
        {
            useCustomMCDir = true;
            minecraftCustomDirectoryBox.IsEnabled = true;
            minecraftCustomDirectoryBox.Foreground = Brushes.Black;

        }

        private void useCustomMCDirectory_Unchecked(object sender, RoutedEventArgs e)
        {
            useCustomMCDir = false;
            minecraftCustomDirectoryBox.IsEnabled = false;
            minecraftCustomDirectoryBox.Foreground = Brushes.Gray;
        }

        private void browseForJavaEXE_Click(object sender, RoutedEventArgs e)
        {
            if (useCustomJavaEXE == true)
            {
                var ofd = new Microsoft.Win32.OpenFileDialog() { Filter = "Executable Files (*.exe)|*.exe|Everything (*.*)|*.*" };
                var result = ofd.ShowDialog();
                if (result == false) return;
                customJavaEXEBox.Text = ofd.FileName;
            }
        }

        private void profileSelectBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Insane. Do not touch
            if (profileSelectBox.SelectedValue != null)
                if (((CLProfile)profileSelectBox.SelectedValue).showOldVersions != viewOldVersions || ((CLProfile)profileSelectBox.SelectedValue).showSnapshots != viewSnapshots)
                    updateVersionBox();

            // Force update versionSelectBox, vanillaVersion, modpackVersion
            ((System.Windows.Controls.ComboBox)versionSelectBox).GetBindingExpression(System.Windows.Controls.ComboBox.SelectedValueProperty)
                    .UpdateTarget();

            ((System.Windows.Controls.RadioButton)useVanillaVersionCheckbox).GetBindingExpression(System.Windows.Controls.RadioButton.IsCheckedProperty)
                    .UpdateTarget();

            ((System.Windows.Controls.RadioButton)useModpackCheckbox).GetBindingExpression(System.Windows.Controls.RadioButton.IsCheckedProperty)
                    .UpdateTarget();
        }

        private void createNewProfileButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.profileCollection.Add(new CLProfile
            {
                Name = "New Profile",
                MojangVersion = "latest-release",

                useCustomJavaEXE = false,
                customJavaEXE = "",

                useCustomMinecraftDirectory = false,
                customMinecraftDirectory = "",

                useCustomJavaArguments = false,
                javaArguments = "-Xmx1024M"
            });
            ViewModel.profileCollection.BubbleSort();

            // Set the new profile to be the selected one
            foreach (CLProfile profile in profileSelectBox.ItemsSource)
            {
                if (profile.Name == "New Profile")
                {
                    profileSelectBox.SelectedItem = profile;
                }
            }
        }

        private void removeProfileButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.profileCollection.Remove(((CLProfile)profileSelectBox.SelectedValue));
            profileSelectBox.SelectedIndex = 0;
            ViewModel.profileCollection.BubbleSort();
        }

        private void javaArgumentsCheckbox_Checked(object sender, RoutedEventArgs e)
        {
            if (customJavaArgumentsBox != null)
            {
                useCustomJavaArguments = true;
                customJavaArgumentsBox.IsEnabled = true;
                customJavaArgumentsBox.Foreground = Brushes.Black;
            }
        }

        private void javaArgumentsCheckbox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (customJavaArgumentsBox != null)
            {
                useCustomJavaArguments = false;
                customJavaArgumentsBox.IsEnabled = false;
                customJavaArgumentsBox.Foreground = Brushes.Gray;
                customJavaArgumentsBox.Text = "-Xmx1024M";
            }
        }

        private void javaExecutableCheckbox_Checked(object sender, RoutedEventArgs e)
        {
            if (customJavaEXEBox != null)
            {
                useCustomJavaEXE = true;
                customJavaEXEBox.IsEnabled = true;
                customJavaEXEBox.Foreground = Brushes.Black;
            }
        }

        private void javaExecutableCheckbox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (customJavaEXEBox != null)
            {
                useCustomJavaEXE = false;
                customJavaEXEBox.IsEnabled = false;
                customJavaEXEBox.Foreground = Brushes.Gray;
            }
        }

        private void removeModpackButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void addModpackButton_Click(object sender, RoutedEventArgs e)
        {
            AddModpackWindow addWindow = new AddModpackWindow(modpackList);
            addWindow.ShowDialog();
            modpackSelectBox.SelectedIndex = 0;
        }

        private void useModpackCheckbox_Checked(object sender, RoutedEventArgs e)
        {
            // If a modpack is not selected, select one
            if (modpackSelectBox.SelectedIndex == -1)
                modpackSelectBox.SelectedIndex = 0;

            ViewModel.SelectedProfile.ModpackInfo.Type = ((Modpack)modpackSelectBox.SelectedValue).Type;
            ViewModel.SelectedProfile.ModpackInfo.ID = ((Modpack)modpackSelectBox.SelectedValue).name;
            ViewModel.SelectedProfile.MojangVersion = ((Modpack)modpackSelectBox.SelectedValue).mcVersion;
        }

        private void useVanillaVersionCheckbox_Checked(object sender, RoutedEventArgs e)
        {
            ViewModel.SelectedProfile.ModpackInfo.Type = ModpackType.MojangVanilla;
            ViewModel.SelectedProfile.MojangVersion = ((string)versionSelectBox.SelectedValue);
        }
    }
}

