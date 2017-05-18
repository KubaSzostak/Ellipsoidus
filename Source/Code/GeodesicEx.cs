using Esri.ArcGISRuntime.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System
{

    public static class GeodesicEx
    {
        public static MapPoint GeodesicMove(this MapPoint startPt, double azimuth, double length)
        {
            var gpt = NETGeographicLib.GeodesicUtils.ETRS89.Direct(startPt.ToGeoPoint(), azimuth, length);
            return gpt.ToMapPoint();
        }

        public static double GeodesicDistTo(this MapPoint start, MapPoint point)
        {
            double res;
            NETGeographicLib.GeodesicUtils.ETRS89.Inverse(start.ToGeoPoint(), point.ToGeoPoint(), out res);
            return res;
        }

        public static double GeodesicAzimuthTo(this MapPoint start, MapPoint point)
        {
            double azi1; 
            double azi2;
            NETGeographicLib.GeodesicUtils.ETRS89.Inverse(start.ToGeoPoint(), point.ToGeoPoint(), out azi1, out azi2);
            return azi1;
        }

        public static double GeodesicEndAzimuthAt(this MapPoint start, MapPoint point)
        {
            double azi1;
            double azi2;
            NETGeographicLib.GeodesicUtils.ETRS89.Inverse(start.ToGeoPoint(), point.ToGeoPoint(), out azi1, out azi2);
            return azi2;
        }

        public static GeodesicMapPoint Cast(this MapPoint p, string missedId)
        {
            var res = p as GeodesicMapPoint;
            if (res == null)
            {
                if (p.HasZ)
                    res = new GeodesicMapPoint(missedId, p.X, p.Y, p.Z, p.SpatialReference);
                else
                    res = new GeodesicMapPoint(missedId, p.X, p.Y, p.SpatialReference);
            }

            if (string.IsNullOrEmpty(res.Id))
                res.Id = missedId;

            return res;
        }

        public static GeodesicMapPoint Cast(this MapPoint p, int autoId)
        {

            return p.Cast("AUTO_" + autoId.ToString("0000"));
        }

        public static GeodesicSegment FindByStartPoint(this IEnumerable<GeodesicSegment> segments, MapPoint point)
        {
            foreach (var segm in segments)
            {
                if (segm.StartPoint.IsEqual2d(point))
                    return segm;
            }
            return null;
        }

        public static GeodesicSegment FindByEndPoint(this IEnumerable<GeodesicSegment> segments, MapPoint point)
        {
            foreach (var segm in segments)
            {
                if (segm.EndPoint.IsEqual2d(point))
                    return segm;
            }
            return null;
        }

        public static IEnumerable<MapPoint> GetGeodesicDensifyPoints(this IEnumerable<GeodesicSegment> segments, double maxDeviation)
        {
            var points = new List<MapPoint>();

            foreach (var segm in segments)
            {
                points.Add(segm.StartPoint);
                var densifyPoints = segm.GetDensifyPoints(maxDeviation);
                points.AddRange(densifyPoints);
            }
            var lastSegm = segments.LastOrDefault();
            if (lastSegm != null)
                points.Add(lastSegm.EndPoint);

            return points;
        }

        /// <summary>
        /// This gives extreme precision
        /// </summary>
        public static IEnumerable<MapPoint> GetGeodesicDensifyPoints(this IEnumerable<GeodesicSegment> segments)
        {
            var points = new List<MapPoint>();

            foreach (var segm in segments)
            {
                //TODO: remove duplicated Start/End points?
                points.AddRange(segm.DensifyPoints);
            }

            return points;
        }

        public static List<GeodesicMapPoint> GetVertices(this IEnumerable<GeodesicSegment> segments)
        {
            var vertices = new List<GeodesicMapPoint>();

            foreach (var segm in segments)
            {
                vertices.Add(segm.StartPoint);
            }
            var lastSegm = segments.LastOrDefault();
            if (lastSegm != null)
                vertices.Add(lastSegm.EndPoint);

            return vertices;
        }

        public static GeodesicLineSegment ToLine(this NETGeographicLib.GeodesicLineSegment ln)
        {
            return GeodesicLineSegment.Create(ln.Point1.ToMapPoint(), ln.Point2.ToMapPoint());
        }

        public static double GeodesicLength(this IList<MapPoint> points)
        {
            if (points.Count < 1)
                return 0.0;

            var earth = NETGeographicLib.GeodesicUtils.ETRS89;
            double lenSum = 0.0;
            for (int i = 0; i < points.Count - 1; i++)
            {
                double len;
                earth.Inverse(points[i].ToGeoPoint(), points[i + 1].ToGeoPoint(), out len);
                lenSum += len;
            }
            return lenSum;
        }

        public static double GeodesicArea(this IList<MapPoint> points)
        {
            if (points.Count < 1)
                return 0.0;

            var earth = NETGeographicLib.GeodesicUtils.ETRS89;

            using (var plgArea = new NETGeographicLib.PolygonArea(earth, false))
            {
                foreach (var pt in points)
                {
                    var gpt = pt.ToGeoPoint();
                    plgArea.AddPoint(gpt.Lat, gpt.Lon);
                }
                double perimeter;
                double area;
                plgArea.Compute(false, true, out perimeter, out area);
                return Math.Abs(area);
            }
        }
    }
}
