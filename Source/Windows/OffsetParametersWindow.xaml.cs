using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Ellipsoidus.Windows
{
    /// <summary>
    /// Interaction logic for OffsetParametersWindow.xaml
    /// </summary>
    public partial class OffsetParametersWindow : Window
    {
        private string oldPrec;
        private string oldDist;

        public OffsetParametersWindow()
        {
            this.InitializeComponent();
            this.precBox.Text = 0.1.ToString();
        }

        public double Precision
        {
            get
            {
                if (string.IsNullOrEmpty(this.precBox.Text))
                    return double.NaN;
                return double.Parse(this.precBox.Text);
            }
        }

        public double Distance
        {
            get { return double.Parse(this.distBox.Text); }
        }

        private void okButton_Click(object sender, RoutedEventArgs e)
        {
            double maxPrec = Math.Min(this.Distance * 0.001, 1000.0);
            if (this.Precision < 0.001 || this.Precision > 1000.0)
            {
                MessageBox.Show("Precision must be between 0.001 and 1000.0");
            }
            else if (this.Distance <= 1.0)
            {
                MessageBox.Show("Distance must be greater than 1.0");
            }
            else
            {
                base.DialogResult = new bool?(true);
                base.Close();
            }
        }

        private void cancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.precBox.Text = this.oldPrec;
            this.distBox.Text = this.oldDist;
        }

        public bool ShowDialog(bool precisionEnabled, bool distEnabled)
        {
            this.oldPrec = this.precBox.Text;
            this.oldDist = this.distBox.Text;

            this.precBox.IsEnabled = precisionEnabled;
            this.distBox.IsEnabled = distEnabled;
            if (!distEnabled)
                this.Title = "Precision parameters";

            return base.ShowDialog() == true;
        }
    }
}
