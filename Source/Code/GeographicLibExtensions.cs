using NETGeographicLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System
{

    public static class GeographicLibEx
    {
        public static PlanarPoint Forward(this Gnomonic gn, GeoPoint center, GeoPoint point)
        {
            var res = new PlanarPoint();
            gn.Forward(center.Lat, center.Lon, point.Lat, point.Lon, out res.X, out res.Y);
            return res;
        }

        public static GeoPoint Reverse(this Gnomonic gn, GeoPoint center, PlanarPoint point)
        {
            var res = new GeoPoint();
            gn.Reverse(center.Lat, center.Lon, point.X, point.Y, out res.Lat, out res.Lon);
            return res;
        }

        public static double Inverse(this Geodesic g, GeoPoint p1, GeoPoint p2, out double s12)
        {
            return g.Inverse(p1.Lat, p1.Lon, p2.Lat, p2.Lon, out s12);
        }

        public static double Inverse(this Geodesic g, GeoPoint p1, GeoPoint p2, out double s12, out double azi1, out double azi2)
        {
            return g.Inverse(p1.Lat, p1.Lon, p2.Lat, p2.Lon, out s12, out azi1, out azi2);
        }

        public static double Inverse(this Geodesic g, GeoPoint p1, GeoPoint p2, out double azi1, out double azi2)
        {
            return g.Inverse(p1.Lat, p1.Lon, p2.Lat, p2.Lon, out azi1, out azi2);
        }


        public static GeoPoint ArcDirect(this Geodesic g, GeoPoint p1, double azi1, double a12)
        {
            var p2 = new GeoPoint();
            g.ArcDirect(p1.Lat, p1.Lon, azi1, a12, out p2.Lat, out p2.Lon);
            return p2;
        }
        public static GeoPoint ArcDirect(this Geodesic g, GeoPoint p1, double azi1, double a12, out double azi2)
        {
            var p2 = new GeoPoint();
            g.ArcDirect(p1.Lat, p1.Lon, azi1, a12, out p2.Lat, out p2.Lon, out azi2);
            return p2;
        }
        public static GeoPoint ArcDirect(this Geodesic g, GeoPoint p1, double azi1, double a12, out double azi2, out double s12)
        {
            var p2 = new GeoPoint();
            g.ArcDirect(p1.Lat, p1.Lon, azi1, a12, out p2.Lat, out p2.Lon, out azi2, out s12);
            return p2;
        }


        public static GeoPoint Direct(this Geodesic g, GeoPoint p1, double azi1, double s12)
        {
            var p2 = new GeoPoint();
            g.Direct(p1.Lat, p1.Lon, azi1, s12, out p2.Lat, out p2.Lon);
            return p2;
        }
        public static GeoPoint Direct(this Geodesic g, GeoPoint p1, double azi1, double s12, out double arc)
        {
            var p2 = new GeoPoint();
            arc = g.Direct(p1.Lat, p1.Lon, azi1, s12, out p2.Lat, out p2.Lon);
            return p2;
        }
        public static GeoPoint Direct(this Geodesic g, GeoPoint p1, double azi1, double s12, out double arc, out double azi2)
        {
            var p2 = new GeoPoint();
            arc = g.Direct(p1.Lat, p1.Lon, azi1, s12, out p2.Lat, out p2.Lon, out azi2);
            return p2;
        }

    }
}
