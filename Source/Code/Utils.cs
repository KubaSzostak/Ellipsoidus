using Esri;
using Esri.ArcGISRuntime.Geometry;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace System
{
    public class Utils
    {
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

        public static string DegToString(double angle)
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

            return id + Utils.DegToString(pt.Y) + "  " + Utils.DegToString(pt.X);
        }

        public static string DistToString(GeodesicLine ln)
        {
            return ln.Distance.ToString("0.000") + " (" + Utils.DegToString(ln.ArcLength) + ")";
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

        public static MapPointGraphic StringToPoint(string s)
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
                return new MapPointGraphic(id, x, y);
            }
            catch (Exception ex)
            {
                throw new Exception("Invalid coordinate format: " + s + "\r\n Expected format is: Id  12.345  34.567", ex);
            }
        }

        public static double NormalizeAngle(double a)
        {
            if (a > 360.0)
            {
                a -= 360.0;
                return Utils.NormalizeAngle(a);
            }
            else if (a < 0.0)
            {
                a += 360.0;
                return Utils.NormalizeAngle(a);
            }

            return a;
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
            foreach (MapPoint pt in points)
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

        public static List<MapPointGraphic> LoadFromFile(string filePath)
        {
            var lines = IO.File.ReadAllLines(filePath);
            var res = new List<MapPointGraphic>();

            foreach (var ln in lines)
            {
                if (!string.IsNullOrWhiteSpace(ln) && !ln.StartsWith(";"))
                {
                    var pt = Utils.StringToPoint(ln);
                    res.Add(pt);
                }
            }
            return res;
        }
    }
}
