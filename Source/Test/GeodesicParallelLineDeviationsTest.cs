using Esri;
using Esri.ArcGISRuntime.Geometry;
using System;
using System.Collections.Generic;
using System.Windows.Media;
namespace Ellipsoidus
{
    public class GeodesicParallelLineDeviationsTest : TestBase
    {
        public GeodesicLineSegment SourceLine;
        public GeodesicLineSegment OffsetLine;
        public double OffsetDist { get; private set; }

        public GeodesicParallelLineDeviationsTest(MapPoint startPoint, MapPoint endPoint, double offsetDist)
        {
            this.OffsetDist = offsetDist;
            this.SourceLine = GeodesicLineSegment.Create(startPoint, endPoint);

            var offsLn = this.SourceLine.Offset(offsetDist);
            this.OffsetLine = GeodesicLineSegment.Create(offsLn.StartPoint, offsLn.EndPoint);
        }

        public override void RunTest()
        {
            MapPoint maxDevSrcLnPt = this.SourceLine.StartPoint;
            MapPoint maxDevSrcLnOffsetPt = this.OffsetLine.StartPoint;
            MapPoint maxDevOffsetLnPt = this.OffsetLine.StartPoint;

            double maxDevAzimuth = 0.0;
            double maxDeviation = double.MinValue;

            for (int i = 1; i < this.SourceLine.DensifyPoints.Count - 1; i++)
            {
                var pt = this.SourceLine.DensifyPoints[i];
                var ptLn = GeodesicLineSegment.Create(pt, this.SourceLine.EndPoint);
                var offsetPt = pt.GeodesicMove(ptLn.StartAzimuth + Geodesic.OrhtoAzimuth, this.OffsetDist);
                var near = GeometryEngine.NearestCoordinate(this.OffsetLine, offsetPt);
                if (near.Distance > maxDeviation)
                {
                    maxDeviation = near.Distance;
                    maxDevSrcLnPt = pt;
                    maxDevSrcLnOffsetPt = offsetPt;
                    maxDevOffsetLnPt = near.Point;
                    maxDevAzimuth = ptLn.StartAzimuth + Geodesic.OrhtoAzimuth;
                }
            }

            var deviationLn = GeodesicLineSegment.Create(maxDevSrcLnOffsetPt, maxDevOffsetLnPt);
            var sourceLnMidOffset = this.SourceLine.MidPoint.GeodesicMove(this.SourceLine.MidAzimuth + Geodesic.OrhtoAzimuth, this.OffsetDist);
            var geoDevLn = GeodesicLineSegment.Create(sourceLnMidOffset, this.OffsetLine.MidPoint);

            var lnSymb = Symbols.Blue2;

            this.Layer.Add(this.SourceLine, lnSymb.Line);
            this.Layer.Add(this.SourceLine.StartPoint, lnSymb.Point);
            this.Layer.Add(this.SourceLine.EndPoint, lnSymb.Point);

            this.Layer.Add(this.OffsetLine, lnSymb.Line);
            this.Layer.Add(this.OffsetLine.StartPoint, lnSymb.Point);
            this.Layer.Add(this.OffsetLine.EndPoint, lnSymb.Point);

            var geometricSymb = Symbols.Red2;
            var maxDevOffsetLn = GeodesicLineSegment.Create(maxDevSrcLnPt, maxDevSrcLnPt);
            this.Layer.Add(maxDevOffsetLn, lnSymb.Line);
            this.Layer.Add(maxDevOffsetLn.StartPoint, lnSymb.Point);
            this.Layer.Add(maxDevOffsetLn.EndPoint, lnSymb.Point);

            this.Layer.Add(maxDevOffsetLnPt, geometricSymb.Point);

            var geodesicSymb = Symbols.Magenta2;
            var geodesicLn = GeodesicLineSegment.Create(this.SourceLine.MidPoint, sourceLnMidOffset);
            this.Layer.Add(geodesicLn, lnSymb.Line);
            this.Layer.Add(geodesicLn.StartPoint, lnSymb.Point);
            this.Layer.Add(geodesicLn.EndPoint, lnSymb.Point);

            this.Layer.Add(this.OffsetLine.MidPoint, geodesicSymb.Point);


            this.Raport.AddLn();
            this.Raport.AddLineInfo(this.SourceLine, "SourceLine");
            this.Raport.AddLineInfo(this.OffsetLine, "OffsetLine");
            this.Raport.AddPoint(maxDevSrcLnPt, "Point on SourceLine");
            this.Raport.Add("Orhto-Azimuth on SourceLine: " + Utils.ToDegMinSecString(maxDevAzimuth));
            this.Raport.Add("Offset distance: " + this.OffsetDist.ToString("0.000"));
            this.Raport.AddLn();
            this.Raport.AddPoint(maxDevSrcLnOffsetPt, "Orthogonal offset from SourceLine");
            this.Raport.AddPoint(maxDevOffsetLnPt,    "       Middle point on OffsetLine");
            this.Raport.AddDist(this.SourceLine,      "                SourceLine.Length");
            this.Raport.AddDist(deviationLn,          "       #Geometric Deviation (Red)");
            this.Raport.AddDist(geoDevLn,             "    #Geodesic Deviation (Magenta)");
            this.Raport.AddLn();
            this.Raport.AddLn();
        }
    }
}
