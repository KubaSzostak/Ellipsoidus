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
        public OffsetParametersWindow()
        {
            this.InitializeComponent();
            this.maxDevBox.Text = 0.1.ToString();
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

        public double Distance
        {
            get { return double.Parse(this.distBox.Text); }
        }

        private void okButton_Click(object sender, RoutedEventArgs e)
        {
            double maxPrec = Math.Min(this.Distance * 0.1, 1000.0);
            if (this.MaxDeviation < 0.001 || this.MaxDeviation > maxPrec)
            {
                MessageBox.Show("Max deviation must be between 0.001 and " + maxPrec.ToString("0.0"));
                return;
            }
            if (this.Distance <= 1.0)
            {
                MessageBox.Show("Distance must be greater than 1.0");
                return;
            }
            base.DialogResult = true;
            base.Close();
        }

        private void cancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        public bool ShowDialog(bool precisionEnabled)
        {

            if (!precisionEnabled)
                maxDevPanel.Visibility = Visibility.Collapsed;

            return base.ShowDialog() == true;
        }
    }
}
