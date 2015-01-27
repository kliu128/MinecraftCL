using MinecraftLaunchLibrary;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
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
    /// Interaction logic for ErrorWindow.xaml
    /// </summary>
    public partial class ErrorWindow : Window
    {
        startGameVariables sGameVariables;
        CLProfile usedProfile;
        public ErrorWindow(CLProfile profile, startGameVariables sGV)
        {
            InitializeComponent();
            this.Activate();
            this.BringIntoView();
            this.Focus();

            sGameVariables = sGV;
            usedProfile = profile;
        }

        private void startMinecraft_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Fix
            /*
            WindowState = System.Windows.WindowState.Minimized;
            ShowInTaskbar = false;
            Hide();

            string versionErrorInfo;
            MinecraftUtils.getVersionInformation(ref sGameVariables, out versionErrorInfo);
            if (versionErrorInfo != "")
            {
                errorMessageBox.Text = versionErrorInfo;
                return;
            }

            var startReturn = MinecraftUtils.Start(sGameVariables);
            switch (startReturn.ReturnCode)
            {
                case startMinecraftReturnCode.StartedMinecraft:
                    Application.Current.Shutdown(0);
                    break;
                case startMinecraftReturnCode.MinecraftError:
                    errorMessageBox.Text = startReturn.Error;
                    break;
                default:
                    errorMessageBox.Text = "Unspecified Error.\nError: " + startReturn.Error + "\nReturn Code: " + startReturn.ReturnCode;
                    break;
            }
            ShowInTaskbar = true;
            WindowState = System.Windows.WindowState.Normal;
            Show();*/
        }

        private void validateMinecraftButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Fix
            /*
            downloaderVariables.ValidateFiles = true;
            downloaderVariables.DownloadDialog = new DownloadDialog();
            string downloadReturn = "success";

            var worker = new BackgroundWorker();
            worker.DoWork += (o, x) =>
            {
                // Begin downloading files
                downloadReturn = MinecraftUtils.DownloadGame(downloaderVariables);
            };
            worker.RunWorkerCompleted += (o, x) =>
            {
                if (downloadReturn == "success") // Authenticate, get version information, and launch the game if download was successful
                {
                    startMinecraft_Click(sender, e);
                }
                else
                {
                    // There was an error.
                    errorMessageBox.Text = downloadReturn;
                }
            };
            worker.WorkerReportsProgress = true;
            worker.RunWorkerAsync();
            

            // Show download window
            downloaderVariables.DownloadDialog.ShowDialog();*/
        }

        private void copyToClipboardButton_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(this.errorMessageBox.Text);
            MessageBox.Show("Error information copied to clipboard!");
        }
    }
}
