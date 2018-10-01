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
    public class OffsetDataExportBuilder : Builder
    {
        public string DestDir { get; private set; }
        public double MaxDev { get; private set; }
        public int FirstPointNo { get; private set; }
        public GeodesicOffsetBuilder OffsetBuilder { get; private set; }
        public Polygon EsriBuffer { get; private set; }
        protected string ExeDir = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);

        public OffsetDataExportBuilder(GeodesicOffsetBuilder builder, Polygon esriBuffer, string destDir, double maxDev, int firstPointNo)
        {
            this.Title = "Offset data export";
            this.OffsetBuilder = builder;
            this.DestDir = destDir;
            this.MaxDev = maxDev;
            this.FirstPointNo = firstPointNo;
            this.EsriBuffer = esriBuffer;
        }

        public override void Build()
        {
            var shpDir = Path.Combine(this.DestDir, "shp") + Path.DirectorySeparatorChar;
            Directory.CreateDirectory(shpDir);

            var txtDir = Path.Combine(this.DestDir, "txt") + Path.DirectorySeparatorChar;
            Directory.CreateDirectory(txtDir);

            var points = ShapeFile.SaveLineCombo(this.OffsetBuilder.OffsetSegments, shpDir + "offset.shp", this.MaxDev, this.FirstPointNo);
            TextFile.SavePoints(points, txtDir + "offset_points.txt", this.FirstPointNo);
            TextFile.SavePoints(this.OffsetBuilder.OffsetSegments.GetVertices(), txtDir + "offset_vertices.txt", -1);

            ShapeFile.SaveLineCombo(this.OffsetBuilder.ReferenceLine.Lines, shpDir + "baseline.shp", this.MaxDev, this.FirstPointNo);
            TextFile.SavePoints(Ellipsoidus.Presenter.BaseLine1.Vertices, txtDir + "baseline.txt", -1);
            
            ShapeFile.SaveEsriBuff(this.EsriBuffer.GetPoints().ToList(), shpDir + "esri_buff.shp", this.MaxDev);


            var srcMxd = Path.Combine(ExeDir, "ExportTemplate", "ellipsoidus.mxd");
            var destMxd = Path.Combine(this.DestDir, "ellipsoidus.mxd");
            if (!File.Exists(destMxd))
                File.Copy(srcMxd, destMxd);

            CopyWordShp(shpDir);
        }

        private void CopyWordShp(string destDir)
        {
            try
            {
                var srcFile = Path.Combine(ExeDir, "ExportTemplate", "shp", "countries");
                var destFile = Path.Combine(destDir, "countries");

                if (File.Exists(destFile + ".shp"))
                    return;

                File.Copy(srcFile + ".dbf", destFile + ".dbf");
                File.Copy(srcFile + ".prj", destFile + ".prj");
                File.Copy(srcFile + ".shp", destFile + ".shp");
                File.Copy(srcFile + ".shx", destFile + ".shx");
                File.Copy(srcFile + ".txt", destFile + ".txt");
            }
            catch { }
        }
    }
}
