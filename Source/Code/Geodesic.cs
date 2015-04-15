using Esri.ArcGISRuntime.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Esri
{


    public class GeodesicLine : Polyline
    {
        public MapPointGraphic StartPoint { get; private set; }
        public MapPointGraphic MidPoint { get; private set; }
        public MapPointGraphic EndPoint { get; private set; }

        public double StartAzimuth { get; private set; }
        public double MidAzimuth { get; private set; }
        public double EndAzimuth { get; private set; }

        public double Distance { get; private set; }
        public double ArcLength { get; private set; }

        public IList<MapPoint> DensifyPoints { get; private set; }
        public double DensifyDist { get; private set; }

        public GeodesicLine(GeographicLib.GeodesicData geoData, IList<MapPoint> points)
            : base(points)
        {
            this.Distance = geoData.s12;
            this.ArcLength = geoData.a12;

            this.StartAzimuth = geoData.azi1;
            this.EndAzimuth = geoData.azi2;

            this.StartPoint = points[0].Cast();
            this.EndPoint = points[points.Count - 1].Cast();

            this.MidPoint = this.StartPoint.GeodesicMove(this.StartAzimuth, this.Distance * 0.5).Cast();
            GeographicLib.GeodesicData midGeoData = GeographicLib.Geodesic.WGS84.Inverse(this.MidPoint.Y, this.MidPoint.X, this.EndPoint.Y, this.EndPoint.X);
            this.MidAzimuth = midGeoData.azi1;

            this.DensifyPoints = points;
            this.DensifyDist = points[0].GeodesicDistTo(points[1]);
        }

        public static GeodesicLine Create(MapPoint start, MapPoint end)
        {
            var geoData = GeographicLib.Geodesic.WGS84.Inverse(start.Y, start.X, end.Y, end.X);
            var points = GeodesicLine.GetDensifyPoints(start.Cast(), end.Cast(), geoData.azi1, geoData.s12, 1000.0);
            return new GeodesicLine(geoData, points);
        }

        private static List<MapPoint> GetDensifyPoints(MapPoint startPoint, MapPoint endPoint, double startAz, double lnLength, double maxSegmentLength = 1000.0)
        {
            var res = new List<MapPoint>();
            double segmentCount = Math.Ceiling(lnLength / maxSegmentLength);
            double segmentLength = lnLength / segmentCount;
            double builderLength = 0.0;

            res.Add(startPoint);
            while (builderLength + segmentLength < lnLength)
            {
                builderLength += segmentLength;
                var builderPt = startPoint.GeodesicMove(startAz, builderLength);
                res.Add(builderPt);
            }
            res.Add(endPoint);

            return res;
        }
        private GeodesicPolyline GetOffsetLines(IList<MapPoint> sourcePoints, double offsetDist, double maxDeviation)
        {
            double orthoAz = 90.0;
            var densifyPoints = new List<MapPoint>(sourcePoints.Count);
            var offsetLines = new List<GeodesicLine>(sourcePoints.Count);

            for (int i = 0; i < sourcePoints.Count - 1; i++)
            {
                var sourceStart = sourcePoints[i];
                var sourceEnd = sourcePoints[i + 1];
                var sourceLn = GeodesicLine.Create(sourceStart, sourceEnd);

                var sourceLnMidOffset = sourceLn.MidPoint.GeodesicMove(sourceLn.MidAzimuth + orthoAz, offsetDist);
                var offsetStart = sourceStart.GeodesicMove(sourceLn.StartAzimuth + orthoAz, offsetDist);
                var offsetEnd = sourceEnd.GeodesicMove(sourceLn.EndAzimuth + orthoAz, offsetDist);
                var offsetLn = GeodesicLine.Create(offsetStart, offsetEnd);
                offsetLines.Add(offsetLn);

                var midLn = GeodesicLine.Create(sourceLnMidOffset, offsetLn.MidPoint);
                densifyPoints.Add(sourceStart);
                if (midLn.Distance > maxDeviation)
                {
                    densifyPoints.Add(sourceLn.MidPoint);
                }
            }
            densifyPoints.Add(sourcePoints[sourcePoints.Count - 1]);

            if (densifyPoints.Count > sourcePoints.Count)
                return this.GetOffsetLines(densifyPoints, offsetDist, maxDeviation);

            return GeodesicPolyline.Create(offsetLines);

        }
        public GeodesicPolyline Offset(double offsetDist, double maxDeviation)
        {
            return this.GetOffsetLines(new MapPoint[] { this.StartPoint, this.EndPoint }, offsetDist, maxDeviation);
        }

        public MapPoint PointOnLine(double dist)
        {
            return this.StartPoint.GeodesicMove(this.StartAzimuth, dist);
        }

        private double GeodesicDistTo(GeodesicLine ln, MapPoint point, double maxDeviation)
        {
            var startDist = ln.StartPoint.GeodesicDistTo(point);
            var startPtMinus = ln.StartPoint.GeodesicMove(ln.StartAzimuth, -maxDeviation);
            var startPtMinusDist = startPtMinus.GeodesicDistTo(point);

            if (startPtMinusDist < startDist)
                return startDist;

            var endDist = ln.EndPoint.GeodesicDistTo(point);
            var endPtPlus = ln.EndPoint.GeodesicMove(ln.EndAzimuth, maxDeviation);
            var endPtPlusDist = endPtPlus.GeodesicDistTo(point);

            if (endPtPlusDist < endDist)
                return endDist;


            var midDist = ln.MidPoint.GeodesicDistTo(point);

            var newStartDist = startDist;
            var newEndDist = midDist;
            var newStartPt = ln.StartPoint;
            var newEndPt = ln.MidPoint;

            if (endDist < startDist)
            {
                newStartPt = ln.MidPoint;
                newEndPt = ln.EndPoint;
                newStartDist = midDist;
                newEndDist = endDist;
            }

            if (Math.Abs(newStartDist - newEndDist) < maxDeviation)
                return (newStartDist + newEndDist) * 0.5;

            GeodesicLine newLn = GeodesicLine.Create(newStartPt, newEndPt);
            return this.GeodesicDistTo(newLn, point, maxDeviation);
        }

        public double GeodesicDistTo(MapPoint point, double maxDeviation)
        {
            return this.GeodesicDistTo(this, point, maxDeviation);
        }
    }


    public class GeodesicPolyline : Polyline
    {
        public MapPointGraphic FirstVertex { get; private set; }
        public MapPointGraphic LastVertex { get; private set; }
        public List<MapPointGraphic> Vertices { get; private set; }
        public IList<GeodesicLine> Lines { get; private set; }
        public IList<MapPoint> DensifyPoints { get; private set; }

        private GeodesicPolyline(IList<MapPoint> points, List<MapPointGraphic> vertices, IList<GeodesicLine> lines)
            : base(points)
        {
            this.Vertices = vertices;
            this.FirstVertex = vertices[0];
            this.LastVertex = vertices[vertices.Count - 1];
            this.Lines = lines;
            this.DensifyPoints = points;
        }
        public static GeodesicPolyline Create(IList<GeodesicLine> lines)
        {
            var vertices = new List<MapPointGraphic>(lines.Count + 1);
            var points = new List<MapPoint>(lines.Count + 1);

            for (int i = 0; i < lines.Count - 1; i++)
            {
                var ln = lines[i];
                var ln2 = lines[i + 1];
                vertices.Add(ln.StartPoint);
                points.AddRange(ln.DensifyPoints);
            }

            var lastLn = lines[lines.Count - 1];
            vertices.Add(lastLn.StartPoint);
            vertices.Add(lastLn.EndPoint);
            points.AddRange(lastLn.DensifyPoints);

            return new GeodesicPolyline(points, vertices, lines);
        }

        public static GeodesicPolyline Create(IList<MapPoint> points)
        {
            var lines = new List<GeodesicLine>(points.Count);
            for (int i = 0; i < points.Count - 1; i++)
            {
                var ln = GeodesicLine.Create(points[i], points[i + 1]);
                lines.Add(ln);
            }
            return GeodesicPolyline.Create(lines);
        }

        public double GeodesicDistTo(MapPoint point, double maxDeviation)
        {
            var res = double.MaxValue;
            foreach (GeodesicLine ln in this.Lines)
            {
                double lnDist = ln.GeodesicDistTo(point, maxDeviation);
                res = Math.Min(res, lnDist);
            }
            return res;
        }

        
        public bool IsVertex(MapPoint point)
        {
            foreach (MapPointGraphic v in this.Vertices)
            {
                if (v.IsCoordEqual(point))
                    return true;
            }
            return false;
        }

        public List<GeodesicPolyline> Cut(Polyline cutter)
        {
            var cutRes = GeometryEngine.Cut(this, cutter);
            var res = new List<GeodesicPolyline>();

            foreach (Geometry geom in cutRes)
            {
                var cutLn = geom as Polyline;
                if (cutLn != null)
                {
                    var cutPts = cutLn.GetPoints().ToList<MapPoint>();
                    if (cutPts.Count > 1)
                    {
                        var resLn = this.GetCutResult(cutPts);
                        res.Add(resLn);
                    }
                }
            }
            return res;
        }

        private GeodesicPolyline GetCutResult(IList<MapPoint> points)
        {
            var vertices = new List<MapPoint>();
            vertices.Add(points[0]);
            for (int i = 1; i < points.Count - 1; i++)
            {
                var pt = points[i];
                if (this.IsVertex(pt))
                    vertices.Add(pt);
            }
            vertices.Add(points[points.Count - 1]);

            return GeodesicPolyline.Create(vertices);
        }
    }

    public class GeodesicArc : Polyline
    {
        public MapPointGraphic Center { get; private set; }
        public double Radius { get; private set; }

        public IList<MapPoint> DensifyPoints { get; private set; }
        public double DensifyDist { get; private set; }

        public MapPointGraphic FirstVertex { get; private set; }
        public MapPointGraphic LastVertex { get; private set; }

        internal GeodesicArc(MapPointGraphic center, double radius, double densifyDist, IList<MapPoint> points)
            : base(points)
        {
            this.Center = center;
            this.Radius = radius;
            this.DensifyPoints = points;
            this.DensifyDist = densifyDist;
            this.FirstVertex = points[0].Cast();
            this.LastVertex = points[points.Count - 1].Cast();
        }

        public static GeodesicArc Create(MapPoint center, double radius, double maxDeviation)
        {
            return GeodesicArc.Create(center, radius, 0.0, 360.0, maxDeviation);
        }

        public static double GetDensifyDist(double radius, double maxDeviation)
        {
            double maxDevCos = radius / (radius + maxDeviation);
            double azDeltaRad = 2.0 * Math.Acos(maxDevCos);
            return radius * azDeltaRad;
        }

        public static GeodesicArc Create(MapPoint center, double radius, double startAzimuth, double endAzimuth, double maxDeviation)
        {
            if (radius <= 0.0)
                throw new ArgumentOutOfRangeException("radius");

            if (endAzimuth < startAzimuth)
                endAzimuth += 360.0;
            if (startAzimuth >= endAzimuth)
                throw new ArgumentException("endAzimuth must be greater than startAzimuth");

            if (double.IsNaN(maxDeviation) || maxDeviation <= 0.0 || maxDeviation > radius / 10.0)
            {
                maxDeviation = radius / 100.0;
            }

            var points = new List<MapPoint>();
            var maxDevCos = radius / (radius + maxDeviation);
            var azDeltaRad = 2.0 * Math.Acos(maxDevCos);
            var densifyDist = radius * azDeltaRad;
            if (densifyDist > 1000.0)
            {
                densifyDist = 1000.0;
                azDeltaRad = densifyDist / radius;
            }
            double azDelta = azDeltaRad * 180.0 / Math.PI;

            for (var azimuth = startAzimuth; azimuth < endAzimuth; azimuth += azDelta)
            {
                var pt = center.GeodesicMove(azimuth, radius);
                points.Add(pt);
            }
            var lastPt = center.GeodesicMove(endAzimuth, radius);
            points.Add(lastPt);

            return new GeodesicArc(center.Cast(), radius, densifyDist, points);
        }
        public List<GeodesicArc> Cut(Polyline cutter)
        {
            var res = new List<GeodesicArc>();
            var cutRes = GeometryEngine.Cut(this, cutter);
            foreach (Geometry geom in cutRes)
            {
                var cutArcLn = geom as Polyline;
                if (cutArcLn != null)
                {
                    var cutArcPts = cutArcLn.GetPoints().ToList<MapPoint>();
                    if (cutArcPts.Count > 0)
                    {
                        var resArc = new GeodesicArc(this.Center, this.Radius, this.DensifyDist, cutArcPts);
                        res.Add(resArc);
                    }
                }
            }
            return res;
        }
    }


    public static class GeodesicEx
    {
        public static MapPoint GeodesicMove(this MapPoint startPt, double azimuth, double length)
        {
            if (startPt.SpatialReference != null && !startPt.SpatialReference.IsGeographic)
            {
                startPt = (GeometryEngine.Project(startPt, SpatialReferences.Wgs84) as MapPoint);
            }

            var geoData = GeographicLib.Geodesic.WGS84.Direct(startPt.Y, startPt.X, azimuth, length);
            return new MapPoint(geoData.lon2, geoData.lat2, SpatialReferences.Wgs84);
        }

        public static MapPointGraphic Cast(this MapPoint p)
        {
            if (p is MapPointGraphic)
                return p as MapPointGraphic;

            if (p.HasZ)
                return new MapPointGraphic(null, p.X, p.Y, p.Z, p.SpatialReference);

            return new MapPointGraphic(null, p.X, p.Y, p.SpatialReference);
        }

        public static double GeodesicDistTo(this MapPoint start, MapPoint point)
        {
            var geoData = GeographicLib.Geodesic.WGS84.Inverse(start.Y, start.X, point.Y, point.X);
            return geoData.s12;
        }
    }

}
