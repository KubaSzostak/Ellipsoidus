using Esri;
using Esri.ArcGISRuntime.Geometry;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
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
    /// Interaction logic for PointListWindow.xaml
    /// </summary>
    public partial class PointListWindow : Window, INotifyPropertyChanged
    {
        public PointListWindow()
        {
            InitializeComponent();
            FilteredPoints = new ObservableCollection<GeodesicMapPoint>(this.Points);
            this.DataContext = this;
        }

        List<GeodesicMapPoint> Points = Ellipsoidus.Presenter.GetPoints();
        public ObservableCollection<GeodesicMapPoint> FilteredPoints { get; private set; }

        private void FilterById(string filter)
        {
            if (string.IsNullOrEmpty(filter))
            {
                this.FilteredPoints = new ObservableCollection<GeodesicMapPoint>(this.Points);
            }
            else 
            {
                filter = filter.ToLower();
                var pts = new List<GeodesicMapPoint>();
                foreach (var pt in this.Points)
                {
                    var id = (pt.Id + "").ToLower();
                    if (id.Contains(filter))
                        pts.Add(pt);
                } 
                this.FilteredPoints = new ObservableCollection<GeodesicMapPoint>(pts);
            }

            if (this.lvPoints.SelectedItem == null)
                this.lvPoints.SelectedItem = this.FilteredPoints.FirstOrDefault();


            NotifyPropertyChanged("FilteredPoints");
        }


        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public GeodesicMapPoint ShowSelectPointDialog()
        {
            if (ShowDialog() == true)
            {
                return lvPoints.SelectedItem as GeodesicMapPoint;
            }
            return null;
        }

        private void okButton_Click(object sender, RoutedEventArgs e)
        {
            if (lvPoints.SelectedItem == null)
            {
                MessageBox.Show("Point not selected.", "Info", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            base.DialogResult = true;
            base.Close();
        }

        private void cancelButton_Click(object sender, RoutedEventArgs e)
        {
            base.DialogResult = false;
            base.Close();
        }

        private void filter_Changed(object sender, TextChangedEventArgs e)
        {
            FilterById(filterBox.Text);
        }

        private void lvPoints_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            okButton_Click(sender, e);
        }

    }
}
