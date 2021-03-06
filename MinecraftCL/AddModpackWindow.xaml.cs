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
        private ObservableCollection<Modpack> modpackList = new ObservableCollection<Modpack>();
        bool ftbPrivatePackCodeBoxClicked = false;

        public AddModpackWindow(ObservableCollection<Modpack> _modpackList)
        {
            InitializeComponent();

            modpackList = _modpackList;

            ftbPublicPackCombobox.ItemsSource = FTBLocations.PublicModpacks;
            ftbPublicPackCombobox.SelectedIndex = 0;
        }

        private void addModpackButton_Click(object sender, RoutedEventArgs e)
        {
            if (ftbPublicCheckbox.IsChecked == true)
            {
                FTBModpack newPack = ((FTBModpack)ftbPublicPackCombobox.SelectedValue);
                newPack.Type = ModpackType.FeedTheBeastPublic;

                modpackList.Add(newPack);
            }

            if (ftbPrivateCheckbox.IsChecked == true)
            {
                FTBModpack newPack;
                bool? packExists = FTBUtils.GetPrivatePack(ftbPrivatePackCodeBox.Text, out newPack);
                
                if (packExists == null)
                {
                    // Was unable to determine whether pack exists
                    // TODO: Implement method of showing user that pack does not exist.
                }
                else if (newPack == null && packExists == false)
                {
                    // Invalid pack code, packExists == false
                    MessageBox.Show("Invalid private pack code.", "Invalid code", MessageBoxButton.OK, MessageBoxImage.Error);
                    ftbPrivatePackCodeBox.Text = "";
                    return;
                }
                else
                {
                    // Pack is valid
                    newPack.Type = ModpackType.FeedTheBeastPrivate;
                }

                modpackList.Add(newPack);
            }

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
