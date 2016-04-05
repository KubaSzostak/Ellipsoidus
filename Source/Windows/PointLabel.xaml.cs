using Esri.ArcGISRuntime.Geometry;
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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Ellipsoidus.Windows
{
    /// <summary>
    /// Interaction logic for PointLabel.xaml
    /// </summary>
    public partial class PointLabel : UserControl
    {
        public PointLabel()
        {
            InitializeComponent();
        }


        private void select_Click(object sender, RoutedEventArgs e)
        {
            var wnd = new PointListWindow();
            var pt = wnd.ShowSelectPointDialog();
            if (pt != null)
                this.Point = pt;
        }

        private async void pickOnMap_Click(object sender, RoutedEventArgs e)
        {
            var pt = await Presenter.PickPointAsync();
            this.Point = pt.Cast("PickedOnMap");
        }


        public GeodesicMapPoint Point
        {
            get { return (GeodesicMapPoint)GetValue(PointProperty); }
            set { SetValue(PointProperty, value); }
        }
        public static readonly DependencyProperty PointProperty =
            DependencyProperty.Register("Point", typeof(GeodesicMapPoint), typeof(PointLabel),
            new FrameworkPropertyMetadata(Presenter.RandomPoint(""), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));


        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set {  SetValue(TextProperty, value); }
        }
        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register("Text", typeof(string), typeof(PointLabel), 
            new PropertyMetadata("Point", TextChangedCallback));

        private static void TextChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var lbl = d as PointLabel;
            if (lbl == null)
                return;
            lbl.TextLabel.Text = e.NewValue as string;
        }




    }
}
