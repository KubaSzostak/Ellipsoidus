using DotSpatial.Data;
using DotSpatial.Projections;
using DotSpatial.Topology;
using AGG = Esri.ArcGISRuntime.Geometry;
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
        public static void SavePoints(IEnumerable<AGG.GeodesicMapPoint> points, string filePath)
        {
            using (var shp = new PointShapefile())
            {             
                shp.Projection = ProjectionInfo.FromEpsgCode(4326);

                shp.DataTable.Columns.Add(new DataColumn("point_id", typeof(string)));
                shp.DataTable.Columns.Add(new DataColumn("latitude", typeof(double)));
                shp.DataTable.Columns.Add(new DataColumn("longitude", typeof(double)));
                shp.DataTable.Columns.Add(new DataColumn("lat_dms", typeof(string)));
                shp.DataTable.Columns.Add(new DataColumn("lon_dms", typeof(string)));
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
                    feature.DataRow["lat_dms"] = Utils.ToDegMinSecString(ptg.Y);
                    feature.DataRow["lon_dms"] = Utils.ToDegMinSecString(ptg.X);

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

        public static LineShapefile NewLineShapefile()
        {
            var shp = new LineShapefile();

            shp.Projection = ProjectionInfo.FromEpsgCode(4326);

            shp.DataTable.Columns.Add(new DataColumn("src_geom", typeof(string)));
            shp.DataTable.Columns.Add(new DataColumn("src_point", typeof(string)));
            shp.DataTable.Columns.Add(new DataColumn("origin", typeof(string)));
            shp.DataTable.Columns.Add(new DataColumn("max_dev", typeof(double)));
            shp.DataTable.Columns.Add(new DataColumn("deviation", typeof(double)));
            shp.DataTable.Columns.Add(new DataColumn("geo_len", typeof(double)));
            shp.DataTable.Columns.Add(new DataColumn("segm_type", typeof(string)));

            return shp;
        }

        public static void SaveLineSegments(IEnumerable<AGG.GeodesicSegment> segments, string filePath)
        {   
            using (var shp = NewLineShapefile())
            {
                foreach (var segm in segments)
                {
                    var geom = segm.DensifyPoints.GetLineStringFeature();
                    var feature = shp.AddFeature(geom);

                    if (segm is AGG.GeodesicOffsetLine) 
                    {
                        feature.DataRow["src_geom"] = (segm as AGG.GeodesicOffsetLine).ReferenceLine.ToString();
                    }

                    if (segm is AGG.GeodesicArc)
                    {
                        feature.DataRow["src_geom"] = "Point";
                        feature.DataRow["src_point"] = (segm as AGG.GeodesicArc).Center.Id;
                    }
                    feature.DataRow["segm_type"] = segm.GetType().Name;
                    feature.DataRow["geo_len"] = segm.Length;
                    feature.DataRow["origin"] = segm.Origin;
                }

                shp.SaveAs(filePath, true);
            }       
        }

        /// <summary>
        /// Save line with extreme precision
        /// </summary>
        public static void SaveLineDensify(IEnumerable<AGG.GeodesicSegment> segments, string filePath)
        {
            using (var shp = NewLineShapefile())
            {
                var points = segments.GetGeodesicDensifyPoints();
                var geom = points.GetLineStringFeature();
                var feature = shp.AddFeature(geom);

                feature.DataRow["max_dev"] = 0.000;
                feature.DataRow["origin"] = "Ellipsoidus";

                shp.SaveAs(filePath, true);
            }
        }

        public static void SaveLineDensify(IEnumerable<AGG.GeodesicSegment> segments, string filePath, double maxDeviation)
        {
            using (var shp = NewLineShapefile())
            {
                var points = segments.GetGeodesicDensifyPoints(maxDeviation);
                var geom = points.GetLineStringFeature();
                var feature = shp.AddFeature(geom);

                feature.DataRow["max_dev"] = maxDeviation;
                feature.DataRow["origin"] = "Ellipsoidus";

                feature.DataRow["segm_type"] = "GeodesicDensifyLine";

                shp.SaveAs(filePath, true);
            }
        }

        public static void SaveLine(IEnumerable<Esri.ArcGISRuntime.Geometry.MapPoint> points, string filePath)
        {
            using (var shp = NewLineShapefile())
            {
                var geom = points.GetLineStringFeature();
                var feature = shp.AddFeature(geom);
                
                shp.SaveAs(filePath, true);
            }
        }



        public static IEnumerable<AGG.GeodesicMapPoint> SaveLineCombo(IEnumerable<AGG.GeodesicSegment> segments, string filePath, double maxDeviation)
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
                var geom = points.GetLineStringFeature();
                var feature = shp.AddFeature(geom);

                feature.DataRow["max_dev"] = maxDeviation;
                feature.DataRow["origin"] = "ESRI Buffer";

                shp.SaveAs(filePath, true);
            }

        }


    }

    public static class ShapeFileEx
    {


        public static LineString GetLineStringFeature(this IEnumerable<Esri.ArcGISRuntime.Geometry.MapPoint> points)
        {
            List<Coordinate> vertices = new List<Coordinate>();
            foreach (var pt in points)
            {
                vertices.Add(new Coordinate(pt.X, pt.Y));
            }
            return new LineString(vertices);
        }

        public static void AddLine(this LineShapefile shp, Esri.ArcGISRuntime.Geometry.MapPoint start, Esri.ArcGISRuntime.Geometry.MapPoint end, double deviation, double geoLen)
        {
            var points = new Esri.ArcGISRuntime.Geometry.MapPoint[] { start, end };
            var geom = points.GetLineStringFeature();
            var feature = shp.AddFeature(geom);

            feature.DataRow["deviation"] = deviation;
            feature.DataRow["geo_len"] = geoLen;
        }
    }

}
