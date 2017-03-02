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
        public static int SecDecPlaces = 4;

        /// <summary>
        /// Difference between geodesic straight line (1km) and parallel line (at offset 12M) is 0.1mm. 
        /// But cutting projected lines (1km) causes about 50mm deviations
        /// </summary>
        public static double DensifyDist = 200.0;

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

        private static double ToDegMinSec(double angle, out double deg, out double min, out double sec, int secDecPlaces)
        {
            deg = Math.Floor(angle);

            angle = (angle - deg) * 60.0; // minutes
            min = Math.Floor(angle);

            angle = (angle - min) * 60.0; // seconds
            if (secDecPlaces < 0)
            {
                sec = angle;
            }
            else
            {
                sec = Math.Round(angle, SecDecPlaces + 3, MidpointRounding.ToEven); // remove rounding precision noise  ("0.000": 36.032500000002443 -> 36.032500)
                sec = Math.Round(sec, secDecPlaces, MidpointRounding.ToEven);       // without above fix result would be 36.033, but it should be 36.032
            }

            return deg + min / 60.0 + sec / 3600.0;
        }

        public static string ToDegMinSecString(double angle, int secDecPlaces)
        {
            double deg, min, sec;
            ToDegMinSec(angle, out deg, out min, out sec, secDecPlaces);

            var secPrec = "00";
            if (secDecPlaces > 0)
            {
                secPrec = secPrec + "." + new string('0', secDecPlaces);
            }

            return deg.ToString("0") + "°" + min.ToString("00") + "'" + sec.ToString(secPrec).Replace(",", ".") + '"';
        }

        public static string ToDegMinSecString(double angle)
        {
            return ToDegMinSecString(angle, Utils.SecDecPlaces);
        }

        private static double RoundDegMinSec(double angle, int secDecPlaces)
        {
            double d, m, s;
            return ToDegMinSec(angle, out d, out m, out s, secDecPlaces);
        }

        public static double RoundDegMinSec(double angle)
        {
            return RoundDegMinSec(angle, Utils.SecDecPlaces);
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

            var id = "";
            if (pt is GeodesicMapPoint)
            {
                var ptg = pt as GeodesicMapPoint;
                if (!string.IsNullOrEmpty(ptg.Id))
                    id = ptg.Id + "  ";
            }
            
            return id + Utils.ToDegMinSecString(pt.Y) + "  " + Utils.ToDegMinSecString(pt.X);
        }

        public static string DistToString(GeodesicLineSegment ln)
        {
            var dist = ln.Length;
            if (dist > 0.1)
                return ln.Length.ToString("0.000") + " (" + Utils.ToDegMinSecString(ln.ArcLength) + ")";
            else
                return ln.Length.ToString("0.0000") + " (" + Utils.ToDegMinSecString(ln.ArcLength) + ")";
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
    }

    public static class TextFile
    {

        public static List<GeodesicMapPoint> LoadPoints(string filePath)
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

        public static void SavePoints(IEnumerable<MapPoint> points, string filePath, int firstPointNo, string header = null)
        {
            var lines = new List<string>();

            if (!string.IsNullOrWhiteSpace(header))
            {
                lines.Add(header);
                lines.Add("");
            }

            var i = firstPointNo - 1;
            if (i < 0)
                i = 0;
            
            foreach (var pt in points)
            {
                i++;
                string ptNo;
                if (firstPointNo > 0)
                {
                    // Do nothing - use numbering based on firstPointNo
                    ptNo = i.ToString();
                }
                else
                {
                    ptNo = pt.Cast(i).Id;
                }
                var ln = ptNo.PadLeft(10) + "  " + Utils.ToDegMinSecString(pt.Y).PadLeft(15) + "  " + Utils.ToDegMinSecString(pt.X).PadLeft(15);
                lines.Add(ln);
            }

            File.WriteAllLines(filePath, lines.ToArray());
        }

    }
}
