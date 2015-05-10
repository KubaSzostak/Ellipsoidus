using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Layers;
using Esri.ArcGISRuntime.Symbology;
using System;
using System.Collections.Generic;
using System.Linq;



namespace Esri
{
    

    public static class ArcRuntimeEx
    {
        public static object GetAttribute(this Graphic graphic, string name)
        {
            object res = null;
            graphic.Attributes.TryGetValue(name, out res);
            return res;
        }

        public static string GetAttributeText(this Graphic graphic, string name)
        {
            object res = graphic.GetAttribute(name);
            if (res == null)
                return "";
            return res.ToString();
        }

        public static T? GetAttribute<T>(this Graphic graphic, string name) where T : struct
        {
            object res = graphic.GetAttribute(name);
            if (res == null)
                return null;
            else
                return (T)res;
        }


        public static Graphic Add(this GraphicsLayer layer, Esri.ArcGISRuntime.Geometry.Geometry geometry, Symbol symbol)
        {
            Graphic graphic;
            if (geometry is GeodesicMapPoint)
                graphic = (geometry as GeodesicMapPoint).GetGraphic();
            else
                graphic = new Graphic(geometry);
            graphic.Symbol = symbol;

            layer.Graphics.Add(graphic);
            return graphic;
        }

        public static void AddRange(this GraphicsLayer layer, IEnumerable<Esri.ArcGISRuntime.Geometry.Geometry> geometries, Symbol symbol)
        {
            foreach (var geometry in geometries)
            {
                var graphic = new Graphic(geometry, symbol);
                layer.Graphics.Add(graphic);
            }
        }

        public static void AddPoints(this GraphicsLayer layer, IEnumerable<GeodesicMapPoint> points, MarkerSymbol symbol)
        {
            foreach (var pt in points)
            {
                var graphic = pt.GetGraphic();
                if (symbol != null)
                {
                    graphic.Symbol = symbol;
                }
                layer.Graphics.Add(graphic);
            }
        }

        public static void AddPointLabelling(this GraphicsLayer layer, Symbols symb, string attrName)
        {
            AttributeLabelClass lbl = symb.GetLabelClass(attrName, LabelPlacement.PointAboveRight);
            layer.Labeling.LabelClasses.Add(lbl);
            layer.Labeling.IsEnabled = true;
        }

        public static void AddLineLabelling(this GraphicsLayer layer, Symbols symb, string attrName)
        {
            AttributeLabelClass lbl = symb.GetLabelClass(attrName, LabelPlacement.LineAboveAlong);
            layer.Labeling.LabelClasses.Add(lbl);
            layer.Labeling.IsEnabled = true;
        }

        public static bool HasLabelling(this GraphicsLayer layer)
        {
            return layer.Labeling.IsEnabled;
        }


        public static bool IsEqual2d(this MapPoint point, MapPoint other)
        {
            var eps = NETGeographicLib.GeodesicUtils.ArcEpsilon * 2.0;
            var dx = Math.Abs(point.X - other.X);
            var dy = Math.Abs(point.Y - other.Y);

            return (dx <= eps) && (dy <= eps);
        }

        public static IEnumerable<MapPoint> GetPoints(this Polyline ln)
        {
            if (ln.Parts.Count < 1)
                return new MapPoint[0];
            else
                return ln.Parts[0].GetPoints();
        }

        public static IEnumerable<MapPoint> GetPoints(this Polygon plg)
        {
            if (plg.Parts.Count < 1)
                return new MapPoint[0];
            else
                return plg.Parts[0].GetPoints();
        }

        public static void UpdateOrigin(this IEnumerable<GeodesicMapPoint> points, string origin)
        {
            foreach (var pt in points)
            {
                pt.UpdateOrigin(origin);
            }
        }

        public static IEnumerable<GeodesicMapPoint> ToGeodesicPoints(this IEnumerable<MapPoint> points)
        {
            var res = new List<GeodesicMapPoint>();
            foreach (var pt in points)
            {
                res.Add(pt.Cast());
            }
            return res;
        }

        public static NETGeographicLib.GeoPoint ToGeoPoint(this MapPoint point)
        {
            if (point.SpatialReference != null && !point.SpatialReference.IsGeographic)
            {
                point = (GeometryEngine.Project(point, SpatialReferences.Wgs84) as MapPoint);
            }
            return new NETGeographicLib.GeoPoint() { Lat = point.Y, Lon = point.X };
        }

        public static MapPoint ToMapPoint(this NETGeographicLib.GeoPoint point)
        {
            return new MapPoint(point.Lon, point.Lat, SpatialReferences.Wgs84);
        }
    }
}
