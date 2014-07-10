using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Xml.Serialization;

namespace MinecraftCL
{
    #region profileSelection Class + Required things for it

    public enum VersionType
    {
        Mojang,
        Modpack
    }

    public static class ListExtension
    {
        public static void BubbleSort(this IList o)
        {
            for (int i = o.Count - 1; i >= 0; i--)
            {
                for (int j = 1; j <= i; j++)
                {
                    object o1 = o[j - 1];
                    object o2 = o[j];
                    if (((IComparable)o1).CompareTo(o2) > 0)
                    {
                        o.Remove(o1);
                        o.Insert(j, o1);
                    }
                }
            }
        }
    }

    public class ModpackInfo
    {
        private ModpackType _Type;
        public ModpackType Type
        {
            get { return _Type; }
            set
            {
                _Type = value;
                OnPropertyChanged("Type");
            }
        }

        private string _ID;
        public string ID
        {
            get { return _ID; }
            set
            {
                _ID = value;
                OnPropertyChanged("ID");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyProfileSelection)
        {
            PropertyChangedEventHandler handler = PropertyChanged;

            var e = new PropertyChangedEventArgs(propertyProfileSelection);
            if (handler != null)
            {
                handler(this, e);
            }
        }
    }

    // Profile selection class with various properties of the selected profile
    [DataContract]
    [XmlInclude(typeof(VersionType))]
    [XmlInclude(typeof(ModpackInfo))]
    public class profileSelection : IComparable, INotifyPropertyChanged
    {
        private string _Name;
        public string Name // Profile Name
        {
            get { return _Name; }
            set
            {
                _Name = value;
                OnPropertyChanged("Name");
            }
        }

        private VersionType _VersionType;
        public VersionType VersionType
        {
            get { return _VersionType; }
            set
            {
                _VersionType = value;
                OnPropertyChanged("VersionType");
            }
        }

        private string _MojangVersion;
        public string MojangVersion   // Minecraft Version to use when launching with that profile
        {
            get { return _MojangVersion; }
            set
            {
                _MojangVersion = value;
                OnPropertyChanged("Version");
            }
        }

        private ModpackInfo _ModpackInfo = new ModpackInfo();
        public ModpackInfo ModpackInfo
        {
            get { return _ModpackInfo; }
            set
            {
                _ModpackInfo = value;
                OnPropertyChanged("ModpackInfo");
            }
        }

        private string _customJavaEXE;
        public string customJavaEXE    // The custom java executable to use
        {
            get { return _customJavaEXE; }
            set
            {
                _customJavaEXE = value;
                OnPropertyChanged("customJavaEXE");
            }
        }

        private string _javaArguments;
        public string javaArguments  // The memory allocation to use, ex. "2048M" or "3G"
        {
            get { return _javaArguments; }
            set
            {
                _javaArguments = value;
                OnPropertyChanged("javaArguments");
            }
        }

        private string _customMinecraftDirectory;
        public string customMinecraftDirectory    // The custom minecraft directory to use
        {
            get { return _customMinecraftDirectory; }
            set
            {
                _customMinecraftDirectory = value;
                OnPropertyChanged("customMinecraftDirectory");
            }
        }

        private bool _useCustomJavaEXE;
        public bool useCustomJavaEXE    // Use a custom java executable or not
        {
            get { return _useCustomJavaEXE; }
            set
            {
                _useCustomJavaEXE = value;
                OnPropertyChanged("useCustomJavaEXE");
            }
        }

        private bool _useCustomMinecraftDirectory;
        public bool useCustomMinecraftDirectory    // Use a different minecraft directory or not
        {
            get { return _useCustomMinecraftDirectory; }
            set
            {
                _useCustomMinecraftDirectory = value;
                OnPropertyChanged("useCustomMinecraftDirectory");
            }
        }

        private bool _useCustomJavaArguments;
        public bool useCustomJavaArguments      // Whether to use custom memory settings, ex. "2048M" or "3G"
        {
            get { return _useCustomJavaArguments; }
            set
            {
                _useCustomJavaArguments = value;
                OnPropertyChanged("useCustomJavaArguments");
            }
        }

        private bool _showSnapshots;
        public bool showSnapshots      // Whether to show developmental versions, or snapshots (ex. 14w11a)
        {
            get { return _showSnapshots; }
            set
            {
                _showSnapshots = value;
                OnPropertyChanged("showSnapshots");
            }
        }

        private bool _showOldVersions;
        public bool showOldVersions      // Whether to show the older versions (alpha + beta versions)
        {
            get { return _showOldVersions; }
            set
            {
                _showOldVersions = value;
                OnPropertyChanged("showOldVersions");
            }
        }

        public override string ToString() { return this.Name; }

        public int CompareTo(object obj)
        {
            profileSelection person = obj as profileSelection;
            if (person == null)
            {
                throw new ArgumentException("Object is not Present, NullReferenceError :(");
            }
            return this.Name.CompareTo(person.Name);
        }

        // Raised when a property in the class has a new value
        public event PropertyChangedEventHandler PropertyChanged;

        // Raises the object's PropertyChanged event
        protected virtual void OnPropertyChanged(string propertyProfileSelection)
        {
            PropertyChangedEventHandler handler = PropertyChanged;

            var e = new PropertyChangedEventArgs(propertyProfileSelection);
            if (handler != null)
            {
                handler(this, e);
            }
        }
    }
    #endregion
}
