using Esri.ArcGISRuntime.Geometry;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Esri
{

    public class Geodesic
    {

        private const double EarthRadius = 6371000.0; // meters
        public const double RadToDeg = 180.0 / Math.PI;
        public const double DegToRad = Math.PI / 180.0;
        public const double OrhtoAzimuth = 90.0;

        
        /// <summary>
        /// Represents the smallest distance between two points (meters)
        /// </summary>
        public const double DistanceEpsilon = 0.001;

        /// <summary>
        /// Represents the smallest arc length on the auxiliary sphere between two points (degrees)
        /// </summary>
        public static readonly double ArcEpsilon = Math.Atan(DistanceEpsilon / EarthRadius) * RadToDeg; 
        
        // 0.001m -> 0.000 000 008 99◦

        // Karney:
        // φ:  41.793 310 205 06◦ 
        // λ: 137.844 900 043 77◦
        //      0.123 456 789 01


        public static double GetAngle(double leftAzimuth, double rightAzimuth)
        {
            if (leftAzimuth < 0.0)
                leftAzimuth = leftAzimuth + 360.0;

            if (rightAzimuth < 0.0)
                rightAzimuth = rightAzimuth + 360.0;

            var angle = rightAzimuth - leftAzimuth;
            if (angle < 0.0)
                angle = angle + 360.0;

            return angle;
        }
            
    }

    public class GeodesicProximity
    {
        public double Azimuth { get; private set; }
        public double Distance { get; private set; }
        public double Direction { get; private set; }
        public MapPoint Point { get; private set; }

        public GeodesicProximity( MapPoint pt, double dist, double direction, double az)
        {
            this.Point = pt;
            this.Direction = dist;
            this.Direction = direction;
            this.Azimuth = az;
        }
    }


    public class GeodesicSegment : Polyline
    {
        public GeodesicMapPoint StartPoint { get; private set; }
        public GeodesicMapPoint EndPoint { get; private set; }

        public IList<MapPoint> DensifyPoints { get; private set; }
        public double DensifyDist { get; private set; }

        public string Origin { get; private set; }

        public GeodesicSegment(IList<MapPoint> points)
            : base(points)
        {
            this.StartPoint = points[0].Cast();
            this.EndPoint = points[points.Count - 1].Cast();


            this.DensifyPoints = points;
            this.DensifyDist = points[0].GeodesicDistTo(points[1]);


            this.Origin = this.GetType().Name;
        }

        /// <summary>
        /// Update origin only if does not have value
        /// </summary>
        /// <param name="origin"></param>
        public void UpdateOrigin(string origin)
        {
            if (string.IsNullOrEmpty(this.Origin))
                this.Origin = origin;
            this.StartPoint.UpdateOrigin(origin);
            this.EndPoint.UpdateOrigin(origin);
        }

        /// <summary>
        /// Generates densify points between StartPoint and EndPoint (output list does not conatin StartPoint and EndPoint)
        /// </summary>
        /// <param name="maxDeviation"></param>
        /// <returns></returns>
        public virtual List<MapPoint> GetGeodesicDensifyPoints(double maxDeviation)
        {
            return new List<MapPoint>();
        }

        protected string GetDisplayName(string name, params string[] paramList)
        {
            for (int i = 0; i < paramList.Length; i++)
            {
                if (string.IsNullOrEmpty(paramList[i]))
                    paramList[i] = "#empty";
            }
            return name + "(" + string.Join(", ", paramList) + ")";
        }

        public override string ToString()
        {
            var name = this.GetType().Name.Replace("Geodesic", "");
            return GetDisplayName(name, StartPoint.Id, EndPoint.Id);
        }
    }

    public class GeodesicLine : GeodesicSegment
    {

        public double StartAzimuth { get; private set; }
        public double EndAzimuth { get; private set; }

        public double Distance { get; private set; }
        public double ArcLength { get; private set; }

        public GeodesicLine(GeographicLib.GeodesicData geoData, IList<MapPoint> points)
            : base(points)
        {
            this.Distance = geoData.s12;
            this.ArcLength = geoData.a12;

            this.StartAzimuth = geoData.azi1;
            this.EndAzimuth = geoData.azi2;

            //this.MidPoint = StartPoint.GeodesicMove(this.StartAzimuth, this.Distance * 0.5);
            //this.MidAzimuth = this.MidPoint.GeodesicAzimuthTo(this.EndPoint); 
            var midData = GeographicLib.Geodesic.WGS84.Direct(geoData.lat1, geoData.lon1, this.StartAzimuth, this.Distance * 0.5);
            this.MidPoint = new MapPoint(midData.lon2, midData.lat2);
            this.MidAzimuth = midData.azi2;
        }

        public static GeodesicLine Create(MapPoint start, MapPoint end)
        {
            var geoData = GeographicLib.Geodesic.WGS84.Inverse(start.Y, start.X, end.Y, end.X);

            // Difference between geodesic straight line (1km) and parallel line (at offset 12M) is 0.1mm
            // But cutting projected lines (1km) causes about 50mm deviations
            double densifyDist = 100.0; 

            var points = GeodesicLine.GetDensifyPoints(start.Cast(), end.Cast(), geoData.azi1, geoData.s12, densifyDist);
            return new GeodesicLine(geoData, points);
        }


        public MapPoint MidPoint { get; private set; }
        public double MidAzimuth {get; private set;}

        public override List<MapPoint> GetGeodesicDensifyPoints(double maxDeviation)
        {
            // This is GeodesicLine, so there are no densify points (all points on this line have deviation=0)
            return new List<MapPoint>();
        }


        private static List<MapPoint> GetDensifyPoints(MapPoint startPoint, MapPoint endPoint, double startAz, double lnLength, double maxSegmentLength)
        {
            var res = new List<MapPoint>();
            double segmentCount = Math.Ceiling(lnLength / maxSegmentLength);
            double segmentLength = lnLength / segmentCount;
            double builderLength = 0.0;
            double builderMaxLength = lnLength - maxSegmentLength * 0.01;

            res.Add(startPoint);
            while (builderLength + segmentLength < builderMaxLength)
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
            var densifyPoints = new List<MapPoint>(sourcePoints.Count);
            var offsetLines = new List<GeodesicLine>(sourcePoints.Count);

            for (int i = 0; i < sourcePoints.Count - 1; i++)
            {
                var sourceStart = sourcePoints[i];
                var sourceEnd = sourcePoints[i + 1];
                var sourceLn = GeodesicLine.Create(sourceStart, sourceEnd);

                var sourceLnMidOffset = sourceLn.MidPoint.GeodesicMove(sourceLn.MidAzimuth + Geodesic.OrhtoAzimuth, offsetDist);
                var offsetStart = sourceStart.GeodesicMove(sourceLn.StartAzimuth + Geodesic.OrhtoAzimuth, offsetDist);
                var offsetEnd = sourceEnd.GeodesicMove(sourceLn.EndAzimuth + Geodesic.OrhtoAzimuth, offsetDist);
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

        public GeodesicOffsetLine Offset(double offsetDist)
        {
            return GeodesicOffsetLine.Create(this, offsetDist);
        }

        public MapPoint PointOnLine(double dist)
        {
            return this.StartPoint.GeodesicMove(this.StartAzimuth, dist);
        }
        
        private double GeodesicDistTo(GeodesicLine ln, MapPoint point)
        {
            var startDist = ln.StartPoint.GeodesicDistTo(point);
            var startAz = ln.StartPoint.GeodesicAzimuthTo(point);
            var startAngle = Geodesic.GetAngle(ln.StartAzimuth, startAz);

            // Point is 'before' line
            if ((startAngle >= 90.0 - Geodesic.ArcEpsilon) && (startAngle <= 360.0 - 90.0 + Geodesic.ArcEpsilon))
                return startDist;
                        
            var endDist = ln.EndPoint.GeodesicDistTo(point);
            var endAz = ln.EndPoint.GeodesicAzimuthTo(point);
            var endAngle = Geodesic.GetAngle(ln.EndAzimuth, endAz);

            // Point is 'after' line
            if ((endAngle <= 90.0 + Geodesic.ArcEpsilon) || (endAngle >= 360.0 - 90.0 - Geodesic.ArcEpsilon))
                return endDist;

            var midDist = ln.MidPoint.GeodesicDistTo(point);

            var newStartDist = startDist;
            var newEndDist = midDist;
            var newStartPt = ln.StartPoint;
            var newEndPt = ln.MidPoint;

            if (endDist < startDist)
            {
                newStartPt = ln.MidPoint.Cast();
                newEndPt = ln.EndPoint;
                newStartDist = midDist;
                newEndDist = endDist;
            }

            if (Math.Abs(newStartDist - newEndDist) < Geodesic.DistanceEpsilon * 0.1)
                return (newStartDist + newEndDist) * 0.5;

            GeodesicLine newLn = GeodesicLine.Create(newStartPt, newEndPt);
            return this.GeodesicDistTo(newLn, point);
        }

        public double GeodesicDistTo(MapPoint point)
        {
            return this.GeodesicDistTo(this, point);
        }
    }



    public class GeodesicOffsetLine : GeodesicSegment
    {
        private GeodesicOffsetLine(IList<MapPoint> points, GeodesicLine sourceLn, double offsetDist)
            : base(points)
	    {
            this.SourceLine = sourceLn;
            this.OffsetDist = offsetDist;
	    }

        public GeodesicLine SourceLine { get; private set; }
        public double OffsetDist { get; private set; }

        public static GeodesicOffsetLine Create(GeodesicLine sourceLn, double offsetDist)
        {
            var points = GetProjectionDensifyPoints(sourceLn, offsetDist);
            return new GeodesicOffsetLine(points, sourceLn, offsetDist);
        }

        private static List<MapPoint> GetProjectionDensifyPoints(GeodesicLine sourceLn, double offsetDist)
        {
            var points = new List<MapPoint>();

            var srcAz = sourceLn.StartAzimuth;
            MapPoint srcPt = sourceLn.StartPoint;
            var pt = srcPt.GeodesicMove(srcAz + Geodesic.OrhtoAzimuth, offsetDist);
            points.Add(pt);

            for (int i = 1; i < sourceLn.DensifyPoints.Count-1; i++)
            {             
                srcPt = sourceLn.DensifyPoints[i];
                srcAz = srcPt.GeodesicAzimuthTo(sourceLn.EndPoint);
                pt = srcPt.GeodesicMove(srcAz + Geodesic.OrhtoAzimuth, offsetDist);
                points.Add(pt);
            }

            srcAz = sourceLn.EndAzimuth;
            srcPt = sourceLn.EndPoint;
            pt = srcPt.GeodesicMove(srcAz + Geodesic.OrhtoAzimuth, offsetDist);
            points.Add(pt);

            return points;
        }

        public override List<MapPoint> GetGeodesicDensifyPoints(double maxDeviation)
        {
            var offsetLines = new List<GeodesicLine>();
            offsetLines.Add(GeodesicLine.Create(this.StartPoint, this.EndPoint));
            offsetLines = GetOffsetGeodesicLines(offsetLines, maxDeviation);

            var res = new List<MapPoint>();
            for (int i = 1; i < offsetLines.Count; i++)
            {
                // Ignore StartPoint and EndPoint
                var ln = offsetLines[i];
                res.Add(ln.StartPoint);
            }
            return res;
        }

        private List<GeodesicLine> GetOffsetGeodesicLines(List<GeodesicLine> offsetLines, double maxDeviation)
        {
            var resLines = new List<GeodesicLine>();

            foreach (var offsetLn in offsetLines)
            {
                var near = GeometryEngine.NearestVertex(this, offsetLn.MidPoint);
                var dist = offsetLn.GeodesicDistTo(near.Point);
                if (dist >= maxDeviation)
                {
                    var ln1 = GeodesicLine.Create(offsetLn.StartPoint, near.Point);
                    var ln2 = GeodesicLine.Create(near.Point, offsetLn.EndPoint);
                    resLines.Add(ln1);
                    resLines.Add(ln2);
                }
                else
                {
                    resLines.Add(offsetLn);
                }
            }

            if (resLines.Count == offsetLines.Count)
                return offsetLines;

            return GetOffsetGeodesicLines(resLines, maxDeviation);
        }
        

        public List<GeodesicOffsetLine> Cut(Polyline cutter)
        {
            var cutRes = GeometryEngine.Cut(this, cutter);
            var res = new List<GeodesicOffsetLine>();
            
            foreach (var geom in cutRes)
            {
                var cutLn = geom as Polyline;
                if (cutLn != null)
                {
                    var cutPts = cutLn.GetPoints().ToList();
                    if (cutPts.Count > 1)
                    {
                        var resLn = new GeodesicOffsetLine(cutPts, this.SourceLine, this.OffsetDist);
                        res.Add(resLn);
                    }
                }
            }
            return res;
        }
    }


    public class GeodesicPolyline : Polyline
    {
        public GeodesicMapPoint FirstVertex { get; private set; }
        public GeodesicMapPoint LastVertex { get; private set; }
        public List<GeodesicMapPoint> Vertices { get; private set; }
        public IList<GeodesicLine> Lines { get; private set; }
        public IList<MapPoint> DensifyPoints { get; private set; }

        private GeodesicPolyline(IList<MapPoint> points, List<GeodesicMapPoint> vertices, IList<GeodesicLine> lines)
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
            var vertices = new List<GeodesicMapPoint>(lines.Count + 1);
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

        public double GeodesicDistTo(MapPoint point)
        {
            var res = double.MaxValue;
            foreach (var ln in this.Lines)
            {
                double lnDist = ln.GeodesicDistTo(point);
                res = Math.Min(res, lnDist);
            }
            return res;
        }

        
        public bool IsVertex(MapPoint point)
        {
            foreach (var v in this.Vertices)
            {
                if (v.IsEqual2d(point))
                    return true;
            }
            return false;
        }

        public List<GeodesicPolyline> Cut(Polyline cutter)
        {
            var cutRes = GeometryEngine.Cut(this, cutter);
            var res = new List<GeodesicPolyline>();

            foreach (var geom in cutRes)
            {
                var cutLn = geom as Polyline;
                if (cutLn != null)
                {
                    var cutPts = cutLn.GetPoints().ToList();
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

    public class GeodesicArc : GeodesicSegment
    {
        public GeodesicMapPoint Center { get; private set; }
        public double Radius { get; private set; }
        public double StartAzimuth { get; private set; }
        public double EndAzimuth { get; private set; }


        internal GeodesicArc(GeodesicMapPoint center, double radius, double densifyDist, IList<MapPoint> points, double startAz, double endAz)
            : base(points)
        {
            this.Center = center;
            this.Radius = radius;
            this.StartAzimuth = startAz;
            this.EndAzimuth = endAz;


            this.StartPoint.SourcePoint = this.Center;
            this.EndPoint.SourcePoint = this.Center;
        }

        public static GeodesicArc Create(MapPoint center, double radius)
        {
            return GeodesicArc.Create(center, radius, 0.0, 360.0);
        }

        public static GeodesicArc Create(MapPoint center, double radius, double startAzimuth, double endAzimuth)
        {
            if (radius <= 0.0)
                throw new ArgumentOutOfRangeException("radius");

            if (endAzimuth < startAzimuth)
                endAzimuth += 360.0;
            if (startAzimuth >= endAzimuth)
                throw new ArgumentException("endAzimuth must be greater than startAzimuth");
            
            var points = new List<MapPoint>();
            var maxDevCos = radius / (radius + Geodesic.DistanceEpsilon * 0.2);
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

            return new GeodesicArc(center.Cast(), radius, densifyDist, points, startAzimuth, endAzimuth);
        }

        public override List<MapPoint> GetGeodesicDensifyPoints(double maxDeviation)
        {
            var points = new List<MapPoint>();
            var maxDevCos = this.Radius / (this.Radius + maxDeviation);
            var azDeltaRad = 2.0 * Math.Acos(maxDevCos);
            var densifyDist = this.Radius * azDeltaRad;
            double azDelta = azDeltaRad * 180.0 / Math.PI;

            for (var azimuth = this.StartAzimuth + azDelta; azimuth < this.EndAzimuth; azimuth += azDelta)
            {
                var pt = this.Center.GeodesicMove(azimuth, this.Radius);
                points.Add(pt);
            }

            return points;
        }

        public List<GeodesicArc> Cut(Polyline cutter)
        {
            var res = new List<GeodesicArc>();
            var cutRes = GeometryEngine.Cut(this, cutter);
            foreach (var geom in cutRes)
            {
                var cutArcLn = geom as Polyline;
                if (cutArcLn != null)
                {
                    var cutArcPts = cutArcLn.GetPoints().ToList();
                    if (cutArcPts.Count > 0)
                    {
                        var startPt = cutArcPts[0];
                        var startAz = this.Center.GeodesicAzimuthTo(startPt);

                        var endPt = cutArcPts[cutArcPts.Count-1];
                        var endAz = this.Center.GeodesicAzimuthTo(endPt);

                        var resArc = new GeodesicArc(this.Center, this.Radius, this.DensifyDist, cutArcPts, startAz, endAz);
                        res.Add(resArc);
                    }
                }
            }
            return res;
        }

        public override string ToString()
        {
            var name = this.GetType().Name.Replace("Geodesic", "");
            return GetDisplayName(name,  Center.Id, "R="+Utils.RoundDist(this.Radius));
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

            var geoData = GeographicLib.Geodesic.WGS84.Direct(startPt.Y, startPt.X, azimuth, length, GeographicLib.GeodesicMask.LATITUDE | GeographicLib.GeodesicMask.LONGITUDE);
                          
            return new MapPoint(geoData.lon2, geoData.lat2, SpatialReferences.Wgs84);
        }

        public static double GeodesicDistTo(this MapPoint start, MapPoint point)
        {
            var geoData = GeographicLib.Geodesic.WGS84.Inverse(start.Y, start.X, point.Y, point.X, GeographicLib.GeodesicMask.DISTANCE);
            return geoData.s12;
        }

        public static double GeodesicAzimuthTo(this MapPoint start, MapPoint point)
        {
            var geoData = GeographicLib.Geodesic.WGS84.Inverse(start.Y, start.X, point.Y, point.X, GeographicLib.GeodesicMask.AZIMUTH);
            return geoData.azi1;
        }

        public static GeodesicMapPoint Cast(this MapPoint p)
        {
            if (p is GeodesicMapPoint)
                return p as GeodesicMapPoint;

            if (p.HasZ)
                return new GeodesicMapPoint(null, p.X, p.Y, p.Z, p.SpatialReference);

            return new GeodesicMapPoint(null, p.X, p.Y, p.SpatialReference);
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
                var densifyPoints = segm.GetGeodesicDensifyPoints(maxDeviation);
                points.AddRange(densifyPoints);
            }
            var lastSegm = segments.Last();
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
            var lastSegm = segments.Last();
            vertices.Add(lastSegm.EndPoint);

            return vertices;
        }
    }

}
