using Esri.ArcGISRuntime.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esri
{
    /// <summary>
    /// Based on http://mathworld.wolfram.com/GnomonicProjection.html
    /// </summary>
    public class GnomonicProjection
    {
        
        public GnomonicProjection(double centerLat, double centerLon)
        {
            this.l0 = centerLon;
            this.p1 = centerLat;
        }

        private double l0; // central longitude lambda_0
        private double p1; // central latitude phi_1

        private double Sin(double v)
        {
            return Math.Sin(v);
        }

        private double Cos(double v)
        {
            return Math.Cos(v);
        }

        public MapPoint FromWgs(double lat, double lon)
        {
            var l = lon;
            var p = lat;

            var l_l0 = l - l0;

            var cosc = Sin(p1) * Sin(p) + Cos(p1) * Cos(p) * Cos(l_l0);

            var x = Cos(p) * Sin(l_l0) / cosc;
            var yCounter = Cos(p1) * Sin(p) - Sin(p1) * Cos(p) * Cos(l_l0);
            var y = yCounter / cosc;

            return new MapPoint(x, y);
        }

        public MapPoint ToWgs(double x, double y)
        {
            var ro = Math.Sqrt(x * x + y * y);
            var c = Math.Atan(ro);

            var phi1 = Cos(c) * Sin(p1);
            var phi2 = y * Sin(c) * Cos(p1) / ro;
            var phi = Math.Asin(phi1 + phi2);

            var lmb1 = x * Sin(c);
            var lmb2 = ro * Cos(p1) * Cos(c) - y * Sin(p1) * Sin(c);
            var lmb = l0 + Math.Atan2(lmb1 , lmb2);

            return new MapPoint(phi, lmb, SpatialReferences.Wgs84);
        }

    }


}
