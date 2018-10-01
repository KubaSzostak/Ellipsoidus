using Esri;
using Esri.ArcGISRuntime.Controls;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Layers;
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
    /// Interaction logic for LineDistAzPanel.xaml
    /// </summary>
    public partial class LineLengthAzPanel : UserControl
    {
        public LineLengthAzPanel(MapView mapView, GraphicsLayer layer)
        {
            InitializeComponent();

            this.MapView = mapView;
            this.Layer = layer;

            this.LinePresenter.PropertyChanged += PresenterChanged;
            this.DataContext = LinePresenter;
        }

        GeodesicLineSegmentPresenter LinePresenter = new GeodesicLineSegmentPresenter();
        MapView MapView;
        GraphicsLayer Layer;
        List<Graphic> Graphics = new List<Graphic>();

        async void PresenterChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (!GeometryEngine.Contains(this.MapView.Extent, LinePresenter.StartPoint.Point))
            {
                await this.MapView.SetViewAsync(LinePresenter.Line.Extent.Expand(2.0));
            }
            UpdateLayer();
        }

        public void ResetPresenter()
        {
            LinePresenter.StartPoint.Point = Ellipsoidus.Presenter.RandomPoint("Ln.Start");
            LinePresenter.EndPoint.Point = Ellipsoidus.Presenter.RandomPoint("Ln.End");
        }

        public void UpdateLayer()
        {
            foreach (var gr in this.Graphics)
            {
                this.Layer.Graphics.Remove(gr);
            }

            Graphics.Add(this.Layer.Add(LinePresenter.Line, Symbols.Green1.Line));
            Graphics.Add(this.Layer.Add(LinePresenter.Line.StartPoint, Symbols.Green1.Point));
            Graphics.Add(this.Layer.Add(LinePresenter.Line.EndPoint, Symbols.Green1.Diamond));
        }

        private void copyToClipboard_Click(object sender, RoutedEventArgs e)
        {
            var s = "";
            s += "Start point:   " + Utils.WgsPointToString(LinePresenter.StartPoint.Point) + "\r\n";
            s += "End point:     " + Utils.WgsPointToString(LinePresenter.EndPoint.Point) + "\r\n";
            s += "\r\n";

            s += "Distance:      " + Utils.DistToString(LinePresenter.Line) + "\r\n";
            s += "Start Azimuth: " +  Utils.ToDegMinSecString(LinePresenter.Line.StartAzimuth) + "\r\n";
            s += "Mid Azimuth:   " +  Utils.ToDegMinSecString(LinePresenter.Line.MidAzimuth) + "\r\n";
            s += "End Azimuth:   " +  Utils.ToDegMinSecString(LinePresenter.Line.EndAzimuth) + "\r\n";

            Clipboard.SetText(s);
        }

        private async void zoomTo_Click(object sender, RoutedEventArgs e)
        {
            await this.MapView.SetViewAsync(LinePresenter.Line.Extent.Expand(2.0));
            UpdateLayer();
        }
    }
}
