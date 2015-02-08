using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace MinecraftCL
{
    /// <summary>
    /// Interaction logic for MessageWindow.xaml
    /// </summary>
    public partial class MessageWindow : Window
    {
        public int closeTimeoutMilliseconds { get; set; }
        public MessageWindow()
        {
            InitializeComponent();
        }
        private void MessageWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Hide close button
            var hwnd = new WindowInteropHelper(this).Handle;
            NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_STYLE, NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_STYLE) & ~NativeMethods.WS_SYSMENU);

            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += (o, x) =>
                    {
                        // Sleep for a specified amount of milliseconds
                        if (closeTimeoutMilliseconds == -1)
                        {
                            // If -1, don't close the window
                            while (true)
                            { }
                        }
                        else
                        {
                            Thread.Sleep(closeTimeoutMilliseconds);
                        }
                    };
            worker.RunWorkerCompleted += (o, x) =>
                {
                    this.Close();
                };
            worker.RunWorkerAsync();
        }
    }
}
