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
        public ExportOptionsWindow()
        {
            InitializeComponent();
            this.maxDevBox.Text = 0.1.ToString();

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
            return base.ShowDialog() == true;
        }
    }
}
