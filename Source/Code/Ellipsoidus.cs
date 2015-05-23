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
        public static GeodesicPolyline BaseLine = null;
        public static GeodesicPolyline CuttingLine = null;
        public static MapView MapView = null;
        public static Action<string> ShowInfoBox;
        public static Action HideInfoBox;
        public static Func<Task, string, Task> StartProgress;

        public static List<GeodesicMapPoint> GetPoints()
        {
            var res = new List<GeodesicMapPoint>();

            if (BaseLine != null)
                res.AddRange(BaseLine.Vertices);

            if (CuttingLine != null)
                res.AddRange(CuttingLine.Vertices);

            return res;
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
            var list = new List<GeodesicMapPoint>();

            int num = 1;
            foreach (var pt in points)
            {
                var idPt = pt.Cast();
                idPt.UpdateOrigin("PickOnMap");
                idPt.Id = num++.ToString();
                list.Add(idPt);
            }

            HideInfoBox();
            return list;
        }
    }
}
