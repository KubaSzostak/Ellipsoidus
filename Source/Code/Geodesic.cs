using Esri.ArcGISRuntime.Geometry;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Esri
{

    public class Geodesic
    {

        public const double OrhtoAzimuth = 90.0;

        public static double GetAngle(double leftAzimuth, double rightAzimuth)
        {
            return NETGeographicLib.GeodesicUtils.GetAngle(leftAzimuth, rightAzimuth);
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
        public virtual List<MapPoint> GetDensifyPoints(double maxDeviation)
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

        internal MapPoint GetSegmentPoint(MapPoint pt)
        {
            if (pt.IsEqual2d(this.StartPoint))
                return this.StartPoint;

            if (pt.IsEqual2d(this.EndPoint))
                return this.EndPoint;

            return pt;
        }
    }

    public class GeodesicLineSegment : GeodesicSegment
    {

        public double StartAzimuth { get; private set; }
        public double EndAzimuth { get; private set; }

        public readonly double Distance;
        public readonly double ArcLength;

        private NETGeographicLib.GeodesicLineSegment Line;

        public GeodesicLineSegment(NETGeographicLib.GeodesicLineSegment ln, IList<MapPoint> points)
            : base(points)
        {
            this.Line = ln;

            this.Distance = ln.Dist12;
            this.ArcLength = ln.Arc12;

            this.StartAzimuth = ln.Azi1;
            this.EndAzimuth = ln.Azi2;

            double midAz;
            var mid = ln.ArcPosition(ln.Arc12 * 0.5, out midAz);
            this.MidPoint = mid.ToMapPoint().Cast();
            this.MidPoint.Id = "Mid(" + this.StartPoint.Id + "," + this.EndPoint.Id + ")";

            this.MidAzimuth = midAz;
        }

        public static GeodesicLineSegment Create(MapPoint start, MapPoint end)
        {
            var ln = new NETGeographicLib.GeodesicLineSegment(start.ToGeoPoint(), end.ToGeoPoint());
            
            // Difference between geodesic straight line (1km) and parallel line (at offset 12M) is 0.1mm
            // But cutting projected lines (1km) causes about 50mm deviations
            double densifyDist = 200.0;

            var geoPoints = ln.GetDensifyPoints(densifyDist);
            var mapPoints = new List<MapPoint>();
            mapPoints.Add(start);
            foreach (var gpt in geoPoints)
            {
                mapPoints.Add(gpt.ToMapPoint());
            }
            mapPoints.Add(end);

            return new GeodesicLineSegment(ln, mapPoints);
        }


        public GeodesicMapPoint MidPoint { get; private set; }
        public double MidAzimuth {get; private set;}

        public override List<MapPoint> GetDensifyPoints(double maxDeviation)
        {
            // This is GeodesicLine, so there are no densify points (all points on this line have deviation=0)
            return new List<MapPoint>();
        }


        public GeodesicOffsetLine Offset(double offsetDist)
        {
            var res = GeodesicOffsetLine.Create(this, offsetDist);

            res.UpdateOrigin("Offset");

            res.StartPoint.SourceGeometry = this;
            res.StartPoint.SourcePoint = this.StartPoint;

            res.EndPoint.SourceGeometry = this;
            res.EndPoint.SourcePoint = this.EndPoint;

            return res;
        }
        
        public double GeodesicDistTo(MapPoint point)
        {
            return this.Line.DistTo(point.ToGeoPoint());
        }

        public GeodesicProximity NearestCoordinate(MapPoint point)
        {
            bool cross;
            double dist;
            
            var gpt = this.Line.NearestCoordinate(point.ToGeoPoint(), out cross);
            this.Line.Geodesic.Inverse(point.ToGeoPoint(), gpt, out dist);

            var res = new GeodesicProximity();
            res.Line = this;
            res.Point = this.GetSegmentPoint(gpt.ToMapPoint());
            res.Distance = dist;

            return res;
        }

    }

    public class GeodesicProximity
    {
        public GeodesicLineSegment Line { get; internal set; }
        public MapPoint Point { get; internal set; }
        public double Distance { get; internal set; }

    }



    public class GeodesicOffsetLine : GeodesicSegment
    {
        private GeodesicOffsetLine(IList<MapPoint> points, GeodesicLineSegment sourceLn, double offsetDist)
            : base(points)
	    {
            this.SourceLine = sourceLn;
            this.OffsetDist = offsetDist;

            var isEven = (points.Count % 2) == 0; // l.parzysta
            if (isEven)
            {
                var midLeft = points.Count / 2 - 1;
                var midRight = midLeft + 1;
                var x = (points[midLeft].X + points[midRight].X) / 2.0;
                var y = (points[midLeft].Y + points[midRight].Y) / 2.0;
                //Trace.WriteLine(points.Count.ToString() + " -> " + midLeft.ToString() + ", " + midRight.ToString());
                this.MidPoint = new MapPoint(x, y, points[midLeft].SpatialReference);
            }
            else
            {
                var midIndex = points.Count / 2;
                //Trace.WriteLine(points.Count.ToString() + " -> " + midIndex.ToString());
                this.MidPoint = points[midIndex];
            }
	    }

        public GeodesicLineSegment SourceLine { get; private set; }
        public double OffsetDist { get; private set; }
        public MapPoint MidPoint { get; private set; }

        public static GeodesicOffsetLine Create(GeodesicLineSegment sourceLn, double offsetDist)
        {
            var points = GetProjectionDensifyPoints(sourceLn, offsetDist);
            var res = new GeodesicOffsetLine(points, sourceLn, offsetDist);
            res.StartPoint.Id = sourceLn.StartPoint.Id;
            res.EndPoint.Id = sourceLn.EndPoint.Id;

            return res;
        }

        private static List<MapPoint> GetProjectionDensifyPoints(GeodesicLineSegment sourceLn, double offsetDist)
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

        public override List<MapPoint> GetDensifyPoints(double maxDeviation)
        {
            var offsetLines = new List<GeodesicLineSegment>();
            offsetLines.Add(GeodesicLineSegment.Create(this.StartPoint, this.EndPoint));
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

        private List<GeodesicLineSegment> GetOffsetGeodesicLines(List<GeodesicLineSegment> offsetLines, double maxDeviation)
        {
            var resLines = new List<GeodesicLineSegment>();

            foreach (var offsetLn in offsetLines)
            {
                var near = GeometryEngine.NearestVertex(this, offsetLn.MidPoint);
                var dist = offsetLn.GeodesicDistTo(near.Point);
                if (dist >= maxDeviation)
                {
                    var ln1 = GeodesicLineSegment.Create(offsetLn.StartPoint, near.Point);
                    var ln2 = GeodesicLineSegment.Create(near.Point, offsetLn.EndPoint);
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

                        if (resLn.StartPoint.IsEqual2d(this.StartPoint))
                            resLn.StartPoint.CopyFrom(this.StartPoint);

                        if (resLn.EndPoint.IsEqual2d(this.EndPoint))
                            resLn.EndPoint.CopyFrom(this.EndPoint);

                        resLn.UpdateOrigin("Cut");
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
        public IList<GeodesicLineSegment> Lines { get; private set; }
        public IList<MapPoint> DensifyPoints { get; private set; }

        private GeodesicPolyline(IList<MapPoint> points, List<GeodesicMapPoint> vertices, IList<GeodesicLineSegment> lines)
            : base(points)
        {
            this.Vertices = vertices;
            this.FirstVertex = vertices[0];
            this.LastVertex = vertices[vertices.Count - 1];
            this.Lines = lines;
            this.DensifyPoints = points;
        }
        public static GeodesicPolyline Create(IList<GeodesicLineSegment> lines)
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
            var lines = new List<GeodesicLineSegment>(points.Count);
            for (int i = 0; i < points.Count - 1; i++)
            {
                var ln = GeodesicLineSegment.Create(points[i], points[i + 1]);
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

        public GeodesicProximity NearestCoordinate(MapPoint point)
        {
            var res = new GeodesicProximity();
            res.Distance = double.MaxValue;

            foreach (var ln in this.Lines)
            {
                var near = ln.NearestCoordinate(point);
                if (near.Distance < res.Distance)
                    res = near;
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
            var maxDevCos = radius / (radius + NETGeographicLib.GeodesicUtils.DistanceEpsilon);
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

        public override List<MapPoint> GetDensifyPoints(double maxDeviation)
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


}
