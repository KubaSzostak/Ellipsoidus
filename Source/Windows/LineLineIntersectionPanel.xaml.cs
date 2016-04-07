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
    /// Interaction logic for LineLineIntersectionPanel.xaml
    /// </summary>
    public partial class LineLineIntersectionPanel : UserControl
    {
        public LineLineIntersectionPanel(MapView mapView, GraphicsLayer layer)
        {
            InitializeComponent();

            this.MapView = mapView;
            this.Layer = layer;

            this.Presenter.PropertyChanged += PresenterChanged;
            this.DataContext = Presenter;
        }

        LineLineIntersectionPresenter Presenter = new LineLineIntersectionPresenter();
        MapView MapView;
        GraphicsLayer Layer;
        List<Graphic> Graphics = new List<Graphic>();

        async void PresenterChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (!GeometryEngine.Contains(this.MapView.Extent, Presenter.IntersectionPoint.Point))
            {
                await this.MapView.SetViewAsync(Presenter.Extent().Expand(2.0));
            }
            UpdateLayer();
        }

        public void ResetPresenter()
        {
            Presenter.Init();
        }

        public void UpdateLayer()
        {
            foreach (var gr in this.Graphics)
            {
                this.Layer.Graphics.Remove(gr);
            }

            var firstLnEx = GeodesicLineSegment.Create(Presenter.FirstLine.StartPoint.Point, Presenter.IntersectionPoint.Point);
            Graphics.Add(this.Layer.Add(firstLnEx, Symbols.Green1.DotLine));

            Graphics.Add(this.Layer.Add(Presenter.FirstLine.Line, Symbols.Green1.Line));
            Graphics.Add(this.Layer.Add(Presenter.FirstLine.Line.StartPoint, Symbols.Green1.Point));
            Graphics.Add(this.Layer.Add(Presenter.FirstLine.Line.EndPoint, Symbols.Green1.Point));

            var secondLnEx = GeodesicLineSegment.Create(Presenter.SecondLine.StartPoint.Point, Presenter.IntersectionPoint.Point);
            Graphics.Add(this.Layer.Add(secondLnEx, Symbols.Blue1.DotLine));

            Graphics.Add(this.Layer.Add(Presenter.SecondLine.Line, Symbols.Blue1.Line));
            Graphics.Add(this.Layer.Add(Presenter.SecondLine.Line.StartPoint, Symbols.Blue1.Point));
            Graphics.Add(this.Layer.Add(Presenter.SecondLine.Line.EndPoint, Symbols.Blue1.Point));

            Graphics.Add(this.Layer.Add(Presenter.IntersectionPoint.Point, Symbols.Maroon2.Point));
        }

        private void copyToClipboard_Click(object sender, RoutedEventArgs e)
        {
            var s = "";

            s += "First line:" + "\r\n";
            s += Utils.WgsPointToSTring(Presenter.FirstLine.StartPoint.Point) + "\r\n";
            s += Utils.WgsPointToSTring(Presenter.FirstLine.EndPoint.Point) + "\r\n";
            s += "\r\n";

            s += "Second line:" + "\r\n";
            s += Utils.WgsPointToSTring(Presenter.SecondLine.StartPoint.Point) + "\r\n";
            s += Utils.WgsPointToSTring(Presenter.SecondLine.EndPoint.Point) + "\r\n";
            s += "\r\n";

            s += "Intersection point:" + "\r\n";
            s += Utils.WgsPointToSTring(Presenter.IntersectionPoint.Point) + "\r\n";

            Clipboard.SetText(s);
        }

        private async void zoomTo_Click(object sender, RoutedEventArgs e)
        {
            await this.MapView.SetViewAsync(Presenter.Extent().Expand(2.0));
            UpdateLayer();
        }
    }
}
