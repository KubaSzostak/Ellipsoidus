using Esri;
using Esri.ArcGISRuntime.Geometry;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace System
{
    public class Utils
    {
        public static string SecPrecision = "00.00";

        private static string TextFilePaht = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Ellipsoidus.txt");

        public static void ShowNotepad(IEnumerable<string> lines)
        {
            File.WriteAllLines(Utils.TextFilePaht, lines.ToArray<string>());
            Process.Start(Utils.TextFilePaht);
        }

        public static void ShowNotepad(string text)
        {
            File.WriteAllText(Utils.TextFilePaht, text);
            Process.Start(Utils.TextFilePaht);
        }

        public static string ToDegMinSecString(double angle, double linearPrec)
        {
            if (linearPrec > 0.4)
                return ToDegMinSecString(angle, "00.00");
            else if (linearPrec > 0.5)
                return ToDegMinSecString(angle, "00.000");
            else
                return ToDegMinSecString(angle, "00.0000");
        }

        public static string ToDegMinSecString(double angle, string secPrecision)
        {
            var deg = Math.Floor(angle);

            angle = (angle - deg) * 60; // minutes
            var min = Math.Floor(angle);

            angle = (angle - min) * 60; // seconds
            var sec = angle;

            return deg.ToString("0") + "°" + min.ToString("00") + "'" + sec.ToString(secPrecision) + '"';
        }

        public static string ToDegString(double angle)
        {
            return angle.ToString("0.00000000") + "°";
        }

        public static double StringToDeg(string s)
        {
            try
            {
                var decSep = Thread.CurrentThread.CurrentCulture.NumberFormat.NumberDecimalSeparator;
                s = s.Replace(",", decSep);
                s = s.Replace(".", decSep);

                var items = s.Split("-'\"°".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                var res = double.Parse(items[0]);
                if (items.Length > 1)
                {
                    res += double.Parse(items[1]) / 60.0;
                }
                if (items.Length > 2)
                {
                    res += double.Parse(items[2]) / 3600.0;
                }
                return res;
            }
            catch 
            {
                throw new Exception("Invalid angle format: " + s);
            }
        }

        public static string WgsPointToSTring(MapPoint pt)
        {
            if (!pt.SpatialReference.IsGeographic)
            {
                throw new Exception("Invalid spatial reference: " + pt.ToString());
            }

            string id = pt.Cast().Id;
            if (!string.IsNullOrWhiteSpace(id))
            {
                id += "  ";
            }

            return id + Utils.ToDegString(pt.Y) + "  " + Utils.ToDegString(pt.X);
        }

        public static string DistToString(GeodesicLineSegment ln)
        {
            var dist = ln.Length;
            if (dist > 0.1)
                return ln.Length.ToString("0.000") + " (" + Utils.ToDegString(ln.ArcLength) + ")";
            else
                return ln.Length.ToString("0.0000") + " (" + Utils.ToDegString(ln.ArcLength) + ")";
        }

        public static string RoundDist(double dist)
        {
            if (dist > 10000.0)
            {
                dist = dist * 0.001;
                return dist.ToString("0.0") + " km";
            }

            if (dist > 100.0)
                return dist.ToString("0.0") + " m";

            if (dist > 10.0)
                return dist.ToString("0.00") + " m";

            return  dist.ToString("0.000") + " m";
        }

        public static GeodesicMapPoint StringToPoint(string s)
        {
            string[] words = s.Split(" \t".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            if (words.Length < 3)
            {
                throw new Exception("Invalid coordinate format: " + s + "\r\n Expected format is: Id  12.345  34.567");
            }

            var id = words[0];
            try
            {
                var y = Utils.StringToDeg(words[1]);
                var x = Utils.StringToDeg(words[2]);
                return new GeodesicMapPoint(id, x, y);
            }
            catch (Exception ex)
            {
                throw new Exception("Invalid coordinate format: " + s + "\r\n Expected format is: Id  12.345  34.567", ex);
            }
        }

        public static List<GeodesicMapPoint> LoadFromFile(string filePath)
        {
            var lines = IO.File.ReadAllLines(filePath);
            var res = new List<GeodesicMapPoint>();

            foreach (var ln in lines)
            {
                var tln = ln.Trim();
                if (!string.IsNullOrWhiteSpace(ln) && !tln.StartsWith(";") && !tln.StartsWith("#"))
                {
                    var pt = Utils.StringToPoint(tln);
                    res.Add(pt);
                }
            }
            return res;
        }

        public static void SaveToFile(IEnumerable<GeodesicMapPoint> points, string filePath, double linearPrec)
        {
            var lines = new List<string>();
            var i = 0;

            foreach (var pt in points)
            {
                i++;
                var id = pt.Id;
                if (string.IsNullOrWhiteSpace(id))
                    id = "#" + i.ToString();

                var ln = id.PadLeft(8) + "  " + ToDegMinSecString(pt.Y, linearPrec).PadLeft(15) + "  " + ToDegMinSecString(pt.X, linearPrec).PadLeft(15);
                lines.Add(ln);
            }
            IO.File.WriteAllLines(filePath, lines, Encoding.UTF8);
        }

        public static void SaveToFile(string filePath, IEnumerable<MapPoint> points, string header = null)
        {
            var lines = new List<string>();

            if (!string.IsNullOrWhiteSpace(header))
            {
                lines.Add(header);
                lines.Add("");
            }

            int id = 1;
            foreach (var pt in points)
            {
                var ptg = pt.Cast();
                if (string.IsNullOrWhiteSpace(ptg.Id))
                {
                    ptg.Id = id++.ToString();
                }
                var ln = Utils.WgsPointToSTring(ptg);
                lines.Add(ln);
            }

            File.WriteAllLines(filePath, lines.ToArray());
        }
    }
}
