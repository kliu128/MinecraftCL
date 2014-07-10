using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using MinecraftCL.FeedTheBeast;
using System.Xml;
using System.Collections.ObjectModel;

namespace MinecraftCL
{
    /// <summary>
    /// Interaction logic for AddModpackWindow.xaml
    /// </summary>
    public partial class AddModpackWindow : Window
    {
        public ObservableCollection<Modpack> modpackList { get; set; }
        bool ftbPrivatePackCodeBoxClicked = false;

        public AddModpackWindow()
        {
            InitializeComponent();
            ftbPublicPackCombobox.ItemsSource = FTBLocations.PublicModpacks;
            ftbPublicPackCombobox.SelectedIndex = 0;
        }

        private void addModpackButton_Click(object sender, RoutedEventArgs e)
        {
            Modpack newPack = new Modpack();

            if (ftbPublicCheckbox.IsChecked == true)
            {
                newPack = ((FTBModpack)ftbPublicPackCombobox.SelectedValue);
                newPack.Type = ModpackType.FeedTheBeast;
            }

            if (ftbPrivateCheckbox.IsChecked == true)
            {
                newPack = FTBUtils.ParseSingleModpackXML(FTBLocations.MasterDownloadRepo + FTBLocations.FTB2Static + ftbPrivatePackCodeBox.Text + ".xml");
                if (newPack == null)
                {
                    MessageBox.Show("Invalid private pack code.", "Invalid code", MessageBoxButton.OK, MessageBoxImage.Error);
                    ftbPrivatePackCodeBox.Text = "";
                    return;
                }
                else
                {
                    newPack.Type = ModpackType.FeedTheBeast;
                }
            }

            modpackList.Add(newPack);

            // Remove the placeholder "add a modpack" entry if an actual pack is added
            if (modpackList[0].Type == ModpackType.PlaceholderModpack)
                modpackList.Remove(modpackList[0]);

            this.Close();
        }

        private void ftbPrivatePackCodeBox_GotFocus(object sender, RoutedEventArgs e)
        {
            // Clear the textbox the first time it is clicked
            if (!ftbPrivatePackCodeBoxClicked)
            {
                ftbPrivatePackCodeBox.Text = "";
                ftbPrivatePackCodeBoxClicked = true;
            }
        }
    }
}
