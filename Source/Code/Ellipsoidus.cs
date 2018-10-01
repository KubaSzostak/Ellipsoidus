using Esri;
using Esri.ArcGISRuntime.Controls;
using Esri.ArcGISRuntime.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Ellipsoidus
{
    public class Presenter
    {
        public static readonly Cursor MapCursor = Cursors.Hand;
        public static GeodesicPolyline BaseLine1 = null;
        public static GeodesicPolyline BaseLine2 = null;
        public static GeodesicPolyline CuttingLine = null;
        public static MapView MapView = null;
        public static Action<string> ShowInfoBox;
        public static Action HideInfoBox;

        public static GeodesicAreaPresenter GeodesicArea = new GeodesicAreaPresenter();


        public static List<GeodesicMapPoint> GetPoints()
        {
            var res = new List<GeodesicMapPoint>();

            if (BaseLine1 != null)
                res.AddRange(BaseLine1.Vertices);

            if (CuttingLine != null)
                res.AddRange(CuttingLine.Vertices);

            return res;
        }

        private static Envelope GetExtent()
        {
            if ((Presenter.MapView != null) && (Presenter.MapView.Extent != null))
                return Presenter.MapView.Extent.Expand(0.7);

            
            var pts = GetPoints();
            if (pts.Count < 1)
                return new Envelope(12, 12, 60, 60, SpatialReferences.Wgs84).Extent;

            var ext = pts[0].Extent;
            foreach (var pt in pts)
            {
                ext = ext.Union(pt);
            }

            return ext;
        }

        private static Random Rnd = new Random();
        public static GeodesicMapPoint RandomPoint(string id)
        {
            var ext = GetExtent();

            var x = ext.XMin + Rnd.NextDouble() * ext.Width;
            var y = ext.YMin + Rnd.NextDouble() * ext.Height;
            return new GeodesicMapPoint(id, x, y);
        }


        public static async Task<MapPoint> PickPointAsync()
        {
            MapView.Cursor = Cursors.Cross;
            var res = await MapView.Editor.RequestPointAsync();
            MapView.Cursor = MapCursor;

            return res;
        }

        public static async Task<List<GeodesicMapPoint>> PickLineAsync()
        {
            ShowInfoBox("Double click to end line");
            MapView.Cursor = Cursors.Cross;
            var geometry = await MapView.Editor.RequestShapeAsync(DrawShape.Polyline, Symbols.Gray1.DashLine, null);
            MapView.Cursor = MapCursor;

            var points = (geometry as Polyline).GetPoints();
            var list = points.ToGeodesicPoints();

            int num = 1;
            foreach (var pt in list)
            {
                pt.UpdateOrigin("PickOnMap");
                pt.Id = num++.ToString();
            }

            HideInfoBox();
            return list;
        }
    }

    public class GeodesicAreaPresenter
    {
        public Polygon Polygon { get; private set; } = null;
        public readonly List<string> SourceFilePath = new List<string>();
        public readonly List<GeodesicMapPoint> Points = new List<GeodesicMapPoint>();

        public void Clear()
        {
            SourceFilePath.Clear();
            Points.Clear();
            Polygon = null;
        }

        public bool HasData { get { return Polygon?.Parts.FirstOrDefault()?.FirstOrDefault() != null; } }

        public void AddPoints(List<GeodesicMapPoint> points, string srcFilePath, string origin)
        {
            foreach (var pt in points)
            {
                pt.UpdateOrigin(origin);
            }
            this.Points.AddRange(points);

            var mapPoints = this.Points.Cast<MapPoint>().ToList();
            if (!mapPoints.First().IsEqual2d(mapPoints.Last()))
                mapPoints.Add(mapPoints.First());

            var plgPart = new List<IEnumerable<MapPoint>>();
            plgPart.Add(mapPoints);
            Polygon = new Polygon(plgPart);

            SourceFilePath.Add(srcFilePath);
            AreaAG = GeometryEngine.GeodesicArea(Polygon, GeodeticCurveType.Geodesic);
            AreaGL = mapPoints.GeodesicArea();

        }

        // GeographicLib/Karney
        public double AreaGL { get; private set; }

        // ArcGIS Runtime
        public double AreaAG { get; private set; }

        public string GetInfoText()
        {
            return "Geodesic area \r\n"
                + "GeographicLib:  " + (AreaGL * 0.0001).ToString("0.0000") + " ha \r\n"
                + "ArcGIS Runtime: " + (AreaAG * 0.0001).ToString("0.0000") + " ha";

        }
    }
}
