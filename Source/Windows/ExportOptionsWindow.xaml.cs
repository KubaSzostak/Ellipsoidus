using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;

namespace Ellipsoidus.Windows
{
    /// <summary>
    /// Interaction logic for ExportOptionsWindow.xaml
    /// </summary>
    public partial class ExportOptionsWindow : Window
    {
        public ExportOptionsWindow(bool showMaxDev, bool showFirstPointNo)
        {
            InitializeComponent();

            this.maxDevBox.Text = (0.5).ToString();
            this.geodesicLnDensityBox.Text = Utils.DensifyDist.ToString();

            if (showMaxDev)
            { 
                this.maxDevSection.Visibility = Visibility.Visible;
                //this.geodesicLnSection.Visibility = Visibility.Visible;
            }
            else
            {
                this.maxDevSection.Visibility = Visibility.Collapsed;
                //this.geodesicLnSection.Visibility = Visibility.Collapsed;
            }

            if (showFirstPointNo)
                this.firstPointNoSection.Visibility = Visibility.Visible;
            else
                this.firstPointNoSection.Visibility = Visibility.Collapsed;

            secPrecBox.SelectedIndex = 2;
        }

        public double MaxDeviation
        {
            get
            {
                if (string.IsNullOrEmpty(this.maxDevBox.Text))
                    return double.NaN;
                return double.Parse(this.maxDevBox.Text);
            }
        }

        public double DensifyDist
        {
            get
            {
                if (string.IsNullOrEmpty(this.geodesicLnDensityBox.Text))
                    return double.NaN;
                return double.Parse(this.geodesicLnDensityBox.Text);
            }
        }

        public int FirstPointNo
        {
            get
            {
                return int.Parse(this.firtPointNoBox.Text);
            }
        }

        public string FolderPath
        {
            get
            {
                return this.pathBox.Text;
            }
        }

        public string SecPrecision
        {
            get { return secPrecBox.Text; }
        }

        private void okButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.MaxDeviation < 0.001 || this.MaxDeviation > 10000.0)
            {
                MessageBox.Show("Max deviation must be between 0.001 and 1000.0");
                return;
            }
            if (this.DensifyDist < 10 )
            {
                MessageBox.Show("Max deviation must be greather than 10.0");
                return;
            }
            if (this.FirstPointNo < 1)
            {
                MessageBox.Show("First point number must be greater than 1");
                return;
            }
            var pathRoot = Path.GetPathRoot(FolderPath);
            if (!Path.IsPathRooted(pathRoot))
            {
                MessageBox.Show("Invalid output folder");
                return;
            }
            Directory.CreateDirectory(FolderPath);
            Utils.SecDecPlaces = this.secPrecBox.SelectedIndex;


            base.DialogResult = true;
            base.Close();
        }

        private void cancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void browseForFolderClick(object sender, RoutedEventArgs e)
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog();
            dlg.Description = "Select output folder";
            dlg.SelectedPath = this.FolderPath;

            var result = dlg.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                this.pathBox.Text = dlg.SelectedPath;
            }
        }

        public bool ShowDialog(string folderPath)
        {
            this.pathBox.Text = folderPath;
            this.Owner = App.Current.MainWindow;
            return base.ShowDialog() == true;
        }
    }
}
