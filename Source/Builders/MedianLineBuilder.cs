using Esri.ArcGISRuntime.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ellipsoidus
{
    

    public class MedianLineBuilder : Builder
    {
        private GeodesicPolyline BaseLine1;
        private GeodesicPolyline BaseLine2;
        private GeodesicPolyline CuttingLine;
        private Polygon CuttingPolygon;

        public LinkedList<GeodesicLineSegment> ConstructionLines = new LinkedList<GeodesicLineSegment>();
        public LinkedList<GeodesicLineSegment> SortedConstructionLines = new LinkedList<GeodesicLineSegment>();

        public GeodesicPolyline MedianLine { get; private set; }

        public MedianLineBuilder(GeodesicPolyline baseLine1, GeodesicPolyline baseLine2, GeodesicPolyline cuttingLine)
        {
            this.BaseLine1 = baseLine1;
            this.BaseLine2 = baseLine2;       

            if (cuttingLine != null)
            {
                this.CuttingLine = cuttingLine;
                var plgPoints = new List<MapPoint>(CuttingLine.DensifyPoints);
                this.CuttingPolygon = new Polygon(plgPoints);
            }
        }

        private void LoadConstructionLines()
        {
            this.ConstructionLines.Clear();

            foreach (var v1 in this.BaseLine1.Vertices)
            {
                //var near = this.BaseLine2.NearestVertex(v1);
                foreach (var v2 in this.BaseLine2.Vertices)
                {
                    var ln = GeodesicLineSegment.Create(v1, v2);
                    this.ConstructionLines.AddLast(ln);
                }
            }

            var sortedLines = new List<GeodesicLineSegment>(this.ConstructionLines);
            sortedLines.Sort(this.ConstructionLineComparer);
            this.SortedConstructionLines = new LinkedList<GeodesicLineSegment>(sortedLines);
        }

        public override void Build()
        {
            LoadConstructionLines();
            this.RemoveConstructionLines(ln => this.CrossesBaseLine(ln));
            //this.RemoveCrossingConstructionLines();
            //this.RemoveConstructionLines(ln => !this.IsEquidistant(ln.MidPoint));

            if (this.ConstructionLines.Count < 1)
                throw new Exception(this.GetType().Name + ": All ConstructionLines were filtered out.");

            //this.MedianLine.NearestCoordinate()

            var medianPoints = this.ConstructionLines.Select(ln => ln.MidPoint).Cast<MapPoint>().ToList();
            this.MedianLine = GeodesicPolyline.Create(medianPoints);
        }

        private void RemoveConstructionLines(Func<GeodesicLineSegment, bool> removeCondition)
        {
            var node = this.ConstructionLines.First;
            while (node != null)
            {
                var next = node.Next;
                if (removeCondition(node.Value))
                    this.ConstructionLines.Remove(node);
                node = next;
            }
        }

        private bool CrossesBaseLine(Geometry geom)
        {
            return GeometryEngine.Crosses(geom, this.BaseLine1) || GeometryEngine.Crosses(geom, this.BaseLine2);
        }

        private int ConstructionLineComparer(GeodesicLineSegment x, GeodesicLineSegment y)
        {
            if (x == null)
                return -1;
            if (y == null)
                return 1;
            return x.Length.CompareTo(y.Length);
        }


        private bool CrossesLongerConstructionLineNode(GeodesicLineSegment minLn, GeodesicLineSegment other)
        {
            if (minLn == other)
                return false;
            if (minLn.StartPoint.IsEqual2d(other.StartPoint) || minLn.EndPoint.IsEqual2d(other.EndPoint))
                return false;// They corsses, but they come from the same point, in which they corse

            if (other.Length <= minLn.Length)
                return false;

            return GeometryEngine.Crosses(minLn, other);
        }

        private void RemoveCrossingConstructionLines()
        {
            var sortedNode = this.SortedConstructionLines.First;
            while (sortedNode != null)
            {
                Console.WriteLine(sortedNode.Value.Length);
                var nextSortedNode = sortedNode.Next;

                var node = this.ConstructionLines.First;
                while (node != null)
                {
                    var next = node.Next;
                    if (this.CrossesLongerConstructionLineNode(sortedNode.Value, node.Value))
                    {
                        this.ConstructionLines.Remove(node.Value);
                        this.SortedConstructionLines.Remove(node.Value);
                    }
                    node = next;
                }

                sortedNode = nextSortedNode;
            }
        }

        private bool IsEquidistant(MapPoint point)
        {
            var dist1 = this.BaseLine1.GeodesicDistTo(point);
            System.Console.WriteLine(dist1);
            var dist2 = this.BaseLine2.GeodesicDistTo(point);
            //System.Windows.Forms.MessageBox.Show(dist1.ToString() + "  " + dist2.ToString());
            System.Console.WriteLine(dist1.ToString() + "  " + dist2.ToString() + "   " + Geodesic.IsZeroLength(Math.Abs(dist1 - dist2)).ToString());
            return Geodesic.IsZeroLength(Math.Abs(dist1 - dist2));
        }
    }
}
