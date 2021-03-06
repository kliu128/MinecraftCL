using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace MinecraftCL
{
    /// <summary>
    /// Interaction logic for DownloadDialog.xaml
    /// </summary>
    public partial class DownloadDialog : Window
    {
        public bool downloadIsInProgress { get; set; }

        public string downloadUpdateInfo
        { 
            get { return downloadFileDisplay.Text; }
            set { downloadFileDisplay.Text = value; }
        }

        public DownloadDialog()
        {
            InitializeComponent();
            this.downloadFileDisplay.Text = "";
            downloadIsInProgress = true;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // We are currently downloading
            if (downloadIsInProgress)
            {
                e.Cancel = false;
                MessageBoxResult stopDownloadResult = MessageBox.Show(
                    "Closing the window will close the program and cause the download to be unfinished. " +
                    "You will need to revalidate minecraft files. Are you sure you want to cancel the download?", 
                    "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Exclamation);

                if (stopDownloadResult == MessageBoxResult.Yes)
                {
                    Environment.Exit(0);
                }
                else
                {
                    e.Cancel = true;
                }
            }
        }
    }
}
