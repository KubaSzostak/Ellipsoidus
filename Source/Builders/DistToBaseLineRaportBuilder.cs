using Esri;
using Esri.ArcGISRuntime.Geometry;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ellipsoidus
{
    public class DistToBaseLineRaportBuilder : RaportBuilder
    {
        public IEnumerable<GeodesicMapPoint> Points { get; private set; }
        public string FileName { get; private set; }
        public GeodesicPolyline BaseLine1 { get; private set; }
        public GeodesicPolyline BaseLine2 { get; private set; }

        public DistToBaseLineRaportBuilder(IEnumerable<GeodesicMapPoint> points, GeodesicPolyline baseLine1, GeodesicPolyline baseLine2, string fileName)
        {
            this.Title = "Distance to baseline raport";
            this.Points = points;
            this.FileName = fileName;
            this.BaseLine1 = baseLine1;
            this.BaseLine2 = baseLine2;
        }

        public override void Build()
        {
            var devList = new List<double>();
            double devDist = Utils.OffsetDistance;

            var rap = new RaportText();
            var fnShp = Path.ChangeExtension(this.FileName, null);

            using (var shp = ShapeFile.NewLineShapefile())
            {
                foreach (var pt in this.Points)
                {

                    var near1 = this.BaseLine1.NearestCoordinate(pt);
                    var gln1 = GeodesicLineSegment.Create(pt, near1.Point);
                    rap.AddLineInfo(gln1);

                    if (this.BaseLine2 == null)
                    {
                        var dev = Math.Abs(near1.Distance - devDist);
                        devList.Add(dev);
                        shp.AddLine(pt, near1.Point, dev, near1.Distance);

                        rap.Add("Deviation from " + devDist.ToString() + " m:");
                        rap.Add(dev.ToString("0.000").PadLeft(15));
                    }
                    else
                    {
                        var near2 = this.BaseLine2.NearestCoordinate(pt);
                        var gln2 = GeodesicLineSegment.Create(pt, near2.Point);
                        rap.AddLineInfo(gln2);

                        var dev = Math.Abs(near1.Distance - near2.Distance);
                        devList.Add(dev);
                        shp.AddLine(pt, near1.Point, dev, near1.Distance);
                        shp.AddLine(pt, near2.Point, dev, near2.Distance);

                        rap.Add("Deviation beetwen distances:");
                        rap.Add(dev.ToString("0.000").PadLeft(15));
                    }

                    rap.AddLn();
                    rap.Add("---");
                    rap.AddLn();
                }

                shp.SaveAs(fnShp + ".shp", true);
            }

            rap.AddLn();
            rap.Add("   # SUMMARY # ");
            rap.Add("Max deviation: " + devList.Max().ToString("0.000"));
            rap.Add("Min deviation: " + devList.Min().ToString("0.000"));

            ShapeFile.SavePoints(this.Points, fnShp + "_points.shp", 1);

            rap.SaveToFile(this.FileName);
        }
        
    }
}
