using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
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
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class UnhandledExceptionWindow : Window
    {
        public UnhandledExceptionWindow()
        {
            InitializeComponent();
        }

        private void sendDetailsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Sends details to email
                SmtpClient client = new SmtpClient();
                client.Port = 587;
                client.Host = "smtp.gmail.com";
                client.EnableSsl = true;
                client.Timeout = 10000;
                client.DeliveryMethod = SmtpDeliveryMethod.Network;
                client.UseDefaultCredentials = false;
                client.Credentials = new System.Net.NetworkCredential("error.report.minecraftcl@gmail.com", "minecraftcl");

                MailMessage mm = new MailMessage("error.report.minecraftcl@gmail.com", "batchfiles99@gmail.com", "Exception unhandled in program", exceptionDetailsBox.Text);
                mm.BodyEncoding = UTF8Encoding.UTF8;
                mm.DeliveryNotificationOptions = DeliveryNotificationOptions.OnFailure;

                client.Send(mm);
                MessageBox.Show("Successfully sent details!");
            }
            catch
            {
                MessageBox.Show("Failed to send details.");
            }
        }
    }
}
