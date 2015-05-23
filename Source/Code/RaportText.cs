using Esri;
using Esri.ArcGISRuntime.Geometry;
using System;
using System.Collections.Generic;
namespace Ellipsoidus
{
    public class RaportText : List<string>
    {
        public void AddLn()
        {
            base.Add("");
        }

        public void AddPoint(MapPoint pt, string dsc = null)
        {
            if (!string.IsNullOrEmpty(dsc))
            {
                dsc += ": ";
            }
            base.Add(dsc + Utils.WgsPointToSTring(pt));
        }

        public void AddLineInfo(GeodesicLineSegment ln, string header = null)
        {
            string prefix = "";
            if (!string.IsNullOrEmpty(header))
            {
                base.Add(header);
                prefix = "  ";
            }

            base.Add(prefix + "Start point: " + Utils.WgsPointToSTring(ln.StartPoint));
            base.Add(prefix + "End point: " + Utils.WgsPointToSTring(ln.EndPoint));
            base.Add(prefix + "Distance: " + Utils.DistToString(ln));
            base.Add(prefix + "Start Azimuth: " + Utils.ToDegMinSecString(ln.StartAzimuth));
            base.Add(prefix + "End Azimuth: " + Utils.ToDegMinSecString(ln.EndAzimuth));
            base.Add("");
        }

        public void AddDist(GeodesicLineSegment ln, string dsc = null)
        {
            if (!string.IsNullOrEmpty(dsc))
            {
                dsc += ": ";
            }
            base.Add(dsc + Utils.DistToString(ln));
        }

        public void SaveToFile(string filePath)
        {
            System.IO.File.WriteAllLines(filePath, this);
        }

    }
}
