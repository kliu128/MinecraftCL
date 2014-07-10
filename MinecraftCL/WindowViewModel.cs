using MinecraftCL.FeedTheBeast;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace MinecraftCL
{
    public class WindowViewModel : INotifyPropertyChanged
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

        // Observable Collection for Profiles, using an ObservableCollecion so the comboBox updates automatically
        private ObservableCollection<profileSelection> _profileCollection;
        public ObservableCollection<profileSelection> profileCollection
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

        private profileSelection _SelectedProfile;
        public profileSelection SelectedProfile
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
                if (SelectedProfile.VersionType == VersionType.Modpack)
                    return true;
                else
                    return false;
            }
            set
            {
                if (value == true)
                    SelectedProfile.VersionType = VersionType.Modpack;
                else
                    SelectedProfile.VersionType = VersionType.Mojang;
                OnPropertyChanged("useModpackVersion");
            }
        }

        public bool useMojangVersion
        {
            get
            {
                if (SelectedProfile.VersionType == VersionType.Mojang)
                    return true;
                else
                    return false;
            }
            set
            {
                if (value == true)
                    SelectedProfile.VersionType = VersionType.Mojang;
                else
                    SelectedProfile.VersionType = VersionType.Modpack;
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
                _SelectedModpack = value;
                SelectedProfile.ModpackInfo.ID = value.name;
                SelectedProfile.ModpackInfo.Type = value.Type;
            }
        }

        public WindowViewModel()
        {
            profileCollection = new ObservableCollection<profileSelection>();
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
