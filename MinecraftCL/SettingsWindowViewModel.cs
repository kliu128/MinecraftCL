using MinecraftCL.FeedTheBeast;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace MinecraftCL
{
    public class SettingsWindowViewModel : INotifyPropertyChanged
    {
        private ObservableCollection<Modpack> _modpackList;
        public ObservableCollection<Modpack> modpackList
        {
            get
            {
                return _modpackList;
            }
            set
            {
                _modpackList = value;
                OnPropertyChanged("modpackList");
            }
        }

        private ObservableCollection<MinecraftCL.SettingsWindow.MinecraftVersion> _versionCollection;
        public ObservableCollection<MinecraftCL.SettingsWindow.MinecraftVersion> versionCollection
        {
            get
            {
                return _versionCollection;
            }
            set
            {
                _versionCollection = value;
                OnPropertyChanged("versionList");
            }
        }

        // Observable Collection for Profiles, using an ObservableCollecion so the comboBox updates automatically
        private ObservableCollection<CLProfile> _profileCollection;
        public ObservableCollection<CLProfile> profileCollection
        {
            get
            {
                return _profileCollection;
            }
            set
            {
                _profileCollection = value;
                OnPropertyChanged("profileCollection");
           }
        }

        private CLProfile _SelectedProfile;
        public CLProfile SelectedProfile
        {
                
            get
            {
                return _SelectedProfile;
            }
            set
            {
                _SelectedProfile = value;
                OnPropertyChanged("SelectedProfile");
            }
        }

        public bool useModpackVersion
        {
            get
            {
                if (SelectedProfile.ModpackInfo.Type != ModpackType.MojangVanilla)
                    return true;
                else
                    return false;
            }
            set
            {
                if (value == false)
                    SelectedProfile.ModpackInfo.Type = ModpackType.MojangVanilla;
                else
                    SelectedProfile.ModpackInfo.Type = SelectedModpack.Type;
                OnPropertyChanged("useModpackVersion");
            }
        }

        public bool useMojangVersion
        {
            get
            {
                if (SelectedProfile.ModpackInfo.Type == ModpackType.MojangVanilla)
                    return true;
                else
                    return false;
            }
            set
            {
                if (value == true)
                    SelectedProfile.ModpackInfo.Type = ModpackType.MojangVanilla;
                else
                    SelectedProfile.ModpackInfo.Type = SelectedModpack.Type;
                OnPropertyChanged("useMojangVersion");
            }
        }

        private bool _autoBackupWorlds;
        public bool autoBackupWorlds
        {
            get
            {
                return _autoBackupWorlds;
            }
            set
            {
                _autoBackupWorlds = value;
                OnPropertyChanged("autoBackupWorlds");
            }
        }

        private Modpack _SelectedModpack;
        public Modpack SelectedModpack
        {
            get
            {
                return _SelectedModpack;
            }
            set
            {
                if (value != null)
                {
                    _SelectedModpack = value;
                    SelectedProfile.ModpackInfo.ID = value.name;
                    SelectedProfile.ModpackInfo.Type = value.Type;
                }
            }
        }

        private bool _enableAnalytics;
        public bool enableAnalytics
        {
            get
            {
                return _enableAnalytics;
            }
            set
            {
                _enableAnalytics = value;
                OnPropertyChanged("enableAnalytics");
            }
        }

        public SettingsWindowViewModel()
        {
            profileCollection = new ObservableCollection<CLProfile>();
            versionCollection = new ObservableCollection<SettingsWindow.MinecraftVersion>();
        }

        // Raised when a property in the class has a new value
        public event PropertyChangedEventHandler PropertyChanged;

        // Raises the object's PropertyChanged event
        protected virtual void OnPropertyChanged(string propertySelectedProfile)
        {
            PropertyChangedEventHandler handler = PropertyChanged;

            var e = new PropertyChangedEventArgs(propertySelectedProfile);
            if (handler != null)
            {
                handler(this, e);
            }
        }
    }
}
