using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NETGeographicLib
{

    public class GeodesicUtils
    {
        public static readonly Geodesic WGS84 = new Geodesic();
        public static readonly Gnomonic Gnomonic = new Gnomonic(WGS84);

        private const double EarthRadius = 6371000.0; // meters
        public const double RadToDeg = 180.0 / Math.PI;
        public const double DegToRad = Math.PI / 180.0;
        public const double OrhtoAzimuth = 90.0;

        /// <summary>
        /// Represents the smallest distance between two points (meters)
        /// </summary>
        public const double DistanceEpsilon = 0.0001;

        /// <summary>
        /// Represents the smallest arc length on the auxiliary sphere between two points (degrees)
        /// </summary>
        public static readonly double ArcEpsilon = Math.Atan(DistanceEpsilon / EarthRadius) * RadToDeg;

        // 0.001m -> 0.000 000 008 99◦

        // Karney:
        // φ:  41.793 310 205 06◦ 
        // λ: 137.844 900 043 77◦
        //      0.123 456 789 01

        public static double GetAngle(double leftAz, double rightAz)
        {
            if (leftAz < 0.0)
                leftAz = leftAz + 360.0;

            if (rightAz < 0.0)
                rightAz = rightAz + 360.0;

            var angle = rightAz - leftAz;
            if (angle < 0.0)
                angle = angle + 360.0;

            return angle;
        }

    }

    public class GeodesicLineSegment 
    {
        public readonly GeoPoint Point1 = new GeoPoint();
        public readonly GeoPoint Point2 = new GeoPoint();

        public readonly double Azi1;
        public readonly double Azi2;
        public readonly double Dist12;
        public readonly double Arc12;

        internal readonly GeodesicLine Line;
        internal readonly Geodesic Geodesic;
        internal readonly Gnomonic Gnonomic;

        public GeodesicLineSegment(Geodesic g, GeoPoint p1, GeoPoint p2)
        {
            this.Geodesic = g;
            this.Gnonomic = new Gnomonic(g);

            this.Point1 = p1;
            this.Point2 = p2;

            this.Arc12 = Geodesic.Inverse(p1, p2, out Dist12, out Azi1, out Azi2);
            this.Line = new GeodesicLine(g, p1.Lat, p1.Lon, Azi1, Mask.ALL);
        }

        public GeodesicLineSegment(GeoPoint p1, GeoPoint p2)
            : this(GeodesicUtils.WGS84, p1, p2)
        { }


        public GeoPoint ArcPosition(double arc)
        {
            var res = new GeoPoint();
            Line.ArcPosition(arc, out res.Lat, out res.Lon);
            return res;
        }
        public GeoPoint ArcPosition(double arc, out double azi)
        {
            var res = new GeoPoint();
            Line.ArcPosition(arc, out res.Lat, out res.Lon, out azi);
            return res;
        }
        public GeoPoint ArcPosition(double arc, out double azi, out double s)
        {
            var res = new GeoPoint();
            Line.ArcPosition(arc, out res.Lat, out res.Lon, out azi, out s);
            return res;
        }

        public GeoPoint Position(double s)
        {
            var res = new GeoPoint();
            Line.Position(s, out res.Lat, out res.Lon);
            return res;
        }
        public GeoPoint Position(double s, out double arc)
        {
            var res = new GeoPoint();
            arc = Line.Position(s, out res.Lat, out res.Lon);
            return res;
        }
        public GeoPoint Position(double s, out double arc, out double azi)
        {
            var res = new GeoPoint();
            arc =  Line.Position(s, out res.Lat, out res.Lon, out azi);
            return res;
        }

        public GeoPoint NearestCoordinate(GeoPoint point, out bool perpendicularCross)
        {
            double temp;
            double az1;
            Geodesic.Inverse(this.Point1, point, out az1, out temp);                
            var startAngle = GeodesicUtils.GetAngle(this.Azi1, az1);

            // Point is 'before' line
            if ((startAngle >= 90.0 - GeodesicUtils.ArcEpsilon) && (startAngle <= 360.0 - 90.0 + GeodesicUtils.ArcEpsilon))
            {
                perpendicularCross = false;
                return this.Point1;
            }

            double az2;
            Geodesic.Inverse(this.Point2, point, out az2, out temp);
            var endAngle = GeodesicUtils.GetAngle(this.Azi2, az2);

            // Point is 'after' line
            if ((endAngle <= 90.0 + GeodesicUtils.ArcEpsilon) || (endAngle >= 360.0 - 90.0 - GeodesicUtils.ArcEpsilon))
            {
                perpendicularCross = false;
                return this.Point2;
            }

            perpendicularCross = true;

            var estimatedRes = new GeoPoint();
            estimatedRes.Lat = (this.Point1.Lat + this.Point2.Lat) * 0.5;
            estimatedRes.Lon = (this.Point1.Lon + this.Point2.Lon) * 0.5;

            do
            {
                var res = GnomonicNearest(point, estimatedRes);
                if (res.IsEqual2d(estimatedRes))
                    return res;
                estimatedRes = res;
            } 
            while (true);

        }

        private GeoPoint GnomonicNearest(GeoPoint point, GeoPoint estimatedNearest)
        {
            var gn = this.Gnonomic;    
            var center = estimatedNearest;

            var p1 = gn.Forward(center, this.Point1);
            var p2 = gn.Forward(center, this.Point2);
            var p = gn.Forward(center, point);

            var ln = new PlanarLine(p1, p2);
            var orthoLn = ln.Perpendicular(p);

            var pRes = ln.Intersection(orthoLn);

            return gn.Reverse(center, pRes);
        }

        public double DistTo(GeoPoint point)
        {
            bool cross;
            double res;

            var near = NearestCoordinate(point, out cross);
            this.Geodesic.Inverse(near, point, out res);

            return res;
        }

        public List<GeoPoint> GetDensifyPoints(double maxSegmentLength)
        {
            double segmentCount = Math.Ceiling(this.Dist12 / maxSegmentLength);
            double arcSegmLen = this.Arc12 / segmentCount;

            // ArcPosition() is faster than Position()
            double arcLen = 0.0 + arcSegmLen;
            double maxArcLen = this.Arc12 - arcSegmLen * 0.1;

            var res = new List<GeoPoint>();
            while (arcLen < maxArcLen)
            {
                var pt = this.ArcPosition(arcLen);
                res.Add(pt);
                arcLen += arcSegmLen;
            }

            return res;
        }
    }



    public class GeoPoint
    {
        public double Lat = double.NaN;
        public double Lon = double.NaN;

        public bool IsEqual2d(GeoPoint other)
        {
            if (Math.Abs(this.Lat - other.Lat) > GeodesicUtils.ArcEpsilon)
                return false;

            if (Math.Abs(this.Lon - other.Lon) > GeodesicUtils.ArcEpsilon)
                return false;

            return true;
        }

        public override string ToString()
        {
            return "Lat=" + Lat.ToString() + ", Lon=" + Lon.ToString();
        }
    }



    public class PlanarPoint
    {
        public double X = double.NaN;
        public double Y = double.NaN;

        public bool IsEqual2d(PlanarPoint other)
        {
            if (Math.Abs(this.X - other.X) > GeodesicUtils.DistanceEpsilon)
                return false;

            if (Math.Abs(this.Y - other.Y) > GeodesicUtils.DistanceEpsilon)
                return false;

            return true;
        }

        public override string ToString()
        {
            return "X=" + X.ToString() + ", Y=" + Y.ToString();
        }
    }



    public class PlanarLine
    {
        public readonly double A;
        public readonly double B;
        public readonly double C;

        public PlanarLine(PlanarPoint p1, PlanarPoint p2)
        {
            A = p2.Y - p1.Y;
            B = p1.X - p2.X;
            C = p1.Y * p2.X - p1.X * p2.Y;
        }

        public PlanarLine(double a, double b, double c)
        {
            this.A = a;
            this.B = b;
            this.C = c;
        }

        public override string ToString()
        {
            return this.A.ToString() + "x + " + this.B.ToString() + "y + " + this.C.ToString() + " = 0";
        }

        public PlanarPoint Intersection(PlanarLine other)
        {
            var w = this.A * other.B - other.A * this.B;
            if (w == 0)
                throw new Exception(this.GetType().Name + ".Intersection() impossible - parallel lines.");

            var wx = -this.C * other.B + other.C * this.B;
            var wy = -this.A * other.C + other.A * this.C;

            var res = new PlanarPoint();
            res.X = wx / w;
            res.Y = wy / w;

            return res;
        }

        public PlanarLine Perpendicular(PlanarPoint p)
        {
            var c = this.A * p.Y - this.B * p.X;
            return new PlanarLine(this.B, -this.A, c);
        }

        public PlanarLine Parallel(PlanarPoint p)
        {
            var c = -this.A * p.X - this.B * p.Y;
            return new PlanarLine(this.A, this.B, c);
        }
    }

}