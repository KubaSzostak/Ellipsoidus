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
            double x;
            double y;
            gn.Forward(center.Lat, center.Lon, point.Lat, point.Lon, out x, out y);

            return new PlanarPoint(x, y);
        }

        public static GeoPoint Reverse(this Gnomonic gn, GeoPoint center, PlanarPoint point)
        {
            double lat;
            double lon;
            gn.Reverse(center.Lat, center.Lon, point.X, point.Y, out lat, out lon);

            return new GeoPoint(lat, lon);
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
            double lat;
            double lon;
            g.ArcDirect(p1.Lat, p1.Lon, azi1, a12, out lat, out lon);

            return new GeoPoint(lat, lon);
        }
        public static GeoPoint ArcDirect(this Geodesic g, GeoPoint p1, double azi1, double a12, out double azi2)
        {
            double lat;
            double lon;
            g.ArcDirect(p1.Lat, p1.Lon, azi1, a12, out lat, out lon, out azi2);

            return new GeoPoint(lat, lon);
        }
        public static GeoPoint ArcDirect(this Geodesic g, GeoPoint p1, double azi1, double a12, out double azi2, out double s12)
        {
            double lat;
            double lon;
            g.ArcDirect(p1.Lat, p1.Lon, azi1, a12, out lat, out lon, out azi2, out s12);

            return new GeoPoint(lat, lon);
        }


        public static GeoPoint Direct(this Geodesic g, GeoPoint p1, double azi1, double s12)
        {
            double lat;
            double lon;
            g.Direct(p1.Lat, p1.Lon, azi1, s12, out lat, out lon);

            return new GeoPoint(lat, lon);
        }
        public static GeoPoint Direct(this Geodesic g, GeoPoint p1, double azi1, double s12, out double arc)
        {
            double lat;
            double lon;
            arc = g.Direct(p1.Lat, p1.Lon, azi1, s12, out lat, out lon);

            return new GeoPoint(lat, lon);
        }
        public static GeoPoint Direct(this Geodesic g, GeoPoint p1, double azi1, double s12, out double arc, out double azi2)
        {
            double lat;
            double lon;
            arc = g.Direct(p1.Lat, p1.Lon, azi1, s12, out lat, out lon, out azi2);

            return new GeoPoint(lat, lon);
        }

    }
}
