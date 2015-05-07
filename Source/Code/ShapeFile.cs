using DotSpatial.Data;
using DotSpatial.Projections;
using DotSpatial.Topology;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esri
{


    public class ShapeFile
    {
        public static void SavePoints(IEnumerable<GeodesicMapPoint> points, string filePath)
        {
            using (var shp = new PointShapefile())
            {             
                shp.Projection = ProjectionInfo.FromEpsgCode(4326);

                shp.DataTable.Columns.Add(new DataColumn("point_id", typeof(string)));
                shp.DataTable.Columns.Add(new DataColumn("latitude", typeof(double)));
                shp.DataTable.Columns.Add(new DataColumn("longitude", typeof(double)));
                shp.DataTable.Columns.Add(new DataColumn("src_geom", typeof(string)));
                shp.DataTable.Columns.Add(new DataColumn("src_point", typeof(string)));
                shp.DataTable.Columns.Add(new DataColumn("origin", typeof(string)));

                foreach (var pt in points)
                {
                    var ptg = pt.Cast();

                    var geom = new Point(ptg.X, ptg.Y);
                    var feature = shp.AddFeature(geom);

                    feature.DataRow.BeginEdit();
                    feature.DataRow["point_id"] = ptg.Id;
                    feature.DataRow["latitude"] = ptg.Y;
                    feature.DataRow["longitude"] = ptg.X;

                    if (ptg.SourceGeometry != null)
                        feature.DataRow["src_geom"] = ptg.SourceGeometry.ToString();

                    if (ptg.SourcePoint != null)
                        feature.DataRow["src_point"] = ptg.SourcePoint.Id;

                    feature.DataRow["origin"] = ptg.Origin;
                    feature.DataRow.EndEdit();
                }
                shp.SaveAs(filePath, true);   
            } 
        }

        private static LineString GetLineStringFeature(IEnumerable<Esri.ArcGISRuntime.Geometry.MapPoint> points)
        {
            List<Coordinate> vertices = new List<Coordinate>();
            foreach (var pt in points)
            {
                vertices.Add(new Coordinate(pt.X, pt.Y));
            }
            return new LineString(vertices);
        }

        private static LineShapefile NewLineShapefile()
        {
            var shp = new LineShapefile();

            shp.Projection = ProjectionInfo.FromEpsgCode(4326);

            shp.DataTable.Columns.Add(new DataColumn("src_geom", typeof(string)));
            shp.DataTable.Columns.Add(new DataColumn("src_point", typeof(string)));
            shp.DataTable.Columns.Add(new DataColumn("origin", typeof(string)));
            shp.DataTable.Columns.Add(new DataColumn("max_dev", typeof(double)));

            return shp;
        }

        public static void SaveLineSegments(IEnumerable<GeodesicSegment> segments, string filePath)
        {   
            using (var shp = NewLineShapefile())
            {
                foreach (var segm in segments)
                {
                    var geom = GetLineStringFeature(segm.DensifyPoints);
                    var feature = shp.AddFeature(geom);

                    if (segm is GeodesicOffsetLine)
                        feature.DataRow["src_geom"] = (segm as GeodesicOffsetLine).SourceLine.ToString();

                    if (segm is GeodesicArc)
                        feature.DataRow["src_point"] = (segm as GeodesicArc).Center.Id;

                    feature.DataRow["origin"] = segm.Origin;
                }

                shp.SaveAs(filePath, true);
            }       
        }

        /// <summary>
        /// Save line with extreme precision
        /// </summary>
        public static void SaveLineDensify(IEnumerable<GeodesicSegment> segments, string filePath)
        {
            using (var shp = NewLineShapefile())
            {
                var points = segments.GetGeodesicDensifyPoints();
                var geom = GetLineStringFeature(points);
                var feature = shp.AddFeature(geom);

                feature.DataRow["max_dev"] = 0.000;
                feature.DataRow["origin"] = "Ellipsoidus";

                shp.SaveAs(filePath, true);
            }
        }

        public static void SaveLineDensify(IEnumerable<GeodesicSegment> segments, string filePath, double maxDeviation)
        {
            using (var shp = NewLineShapefile())
            {
                var points = segments.GetGeodesicDensifyPoints(maxDeviation);
                var geom = GetLineStringFeature(points);
                var feature = shp.AddFeature(geom);

                feature.DataRow["max_dev"] = maxDeviation;
                feature.DataRow["origin"] = "Ellipsoidus";

                shp.SaveAs(filePath, true);
            }
        }

        public static void SaveLine(IEnumerable<Esri.ArcGISRuntime.Geometry.MapPoint> points, string filePath)
        {
            using (var shp = NewLineShapefile())
            {
                var geom = GetLineStringFeature(points);
                var feature = shp.AddFeature(geom);
                
                shp.SaveAs(filePath, true);
            }
        }



        public static IEnumerable<GeodesicMapPoint> SaveLineCombo(IEnumerable<GeodesicSegment> segments, string filePath, double maxDeviation)
        {
            string fn = Path.ChangeExtension(filePath, null);

            SaveLineDensify(segments, fn + ".shp", maxDeviation);
            SaveLineDensify(segments, fn + "-geodesic.shp");
            SaveLineSegments(segments, fn + "-segments.shp");

            SavePoints(segments.GetVertices(), fn + "-vertices.shp");

            // Parallel line can have additional points between vertices. The same as SaveLineDensify(segments, fn, _maxDev_).
            var points = segments.GetGeodesicDensifyPoints(maxDeviation).ToGeodesicPoints();
            points.UpdateOrigin("Densify");
            SavePoints(points, fn + "-points.shp");

            return points;
        }

        
        public static void SaveEsriBuff(List<Esri.ArcGISRuntime.Geometry.MapPoint> points, string filePath, double maxDeviation)
        {
            using (var shp = NewLineShapefile())
            {
                points.Add(points[0]);
                var geom = GetLineStringFeature(points);
                var feature = shp.AddFeature(geom);

                feature.DataRow["max_dev"] = maxDeviation;
                feature.DataRow["origin"] = "ESRI Buffer";

                shp.SaveAs(filePath, true);
            }

        }


    }


}
