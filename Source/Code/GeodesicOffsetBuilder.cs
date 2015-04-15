using Esri.ArcGISRuntime.Geometry;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
namespace Esri
{
    public class GeodesicOffsetBuilder
    {
        public List<GeodesicArc> OffsetArcs = new List<GeodesicArc>();
        public List<GeodesicPolyline> OffsetLines = new List<GeodesicPolyline>();

        public List<GeodesicLine> SourceAuxiliaryLines = new List<GeodesicLine>();
        public List<GeodesicLine> OutputAuxiliaryLines = new List<GeodesicLine>();

        public List<Polyline> CuttedLines = new List<Polyline>();
        public List<Polyline> CuttedArcs = new List<Polyline>();

        private readonly double MinBufferDist;

        public GeodesicPolyline SourceLine { get; private set; }
        public double BufferDist { get; private set; }
        public double MaxDeviation { get; private set; }

        public GeodesicOffsetBuilder(GeodesicPolyline ln, double dist, double maxDeviation)
        {
            this.SourceLine = ln;
            this.BufferDist = dist;
            this.MaxDeviation = maxDeviation;
            this.MinBufferDist = this.BufferDist - this.MaxDeviation * 2.0;
        }

        public void Build()
        {
            this.OffsetArcs.Clear();
            this.OffsetLines.Clear();
            this.SourceAuxiliaryLines.Clear();
            this.CuttedLines.Clear();
            this.CuttedArcs.Clear();

            this.BuildLinesAndArcs(this.BufferDist, this.MaxDeviation);
            this.CutAll();
            this.RemoveLinesInside();
            this.RemoveArcsInside();
        }

        public Task BuildAsync()
        {
            var action = new Action(this.Build);
            return Task.Run(action, CancellationToken.None);
        }

        private double DistToSourceLine(MapPoint point)
        {
            return this.SourceLine.GeodesicDistTo(point, this.MaxDeviation * 0.5);
        }

        private void BuildLinesAndArcs(double dist, double maxDeviation)
        {
            GeodesicLine prevLn = null;
            this.OffsetLines.Clear();

            for (int i = 0; i < this.SourceLine.Lines.Count; i++)
            {
                var ln = this.SourceLine.Lines[i];
                var offsetLn = ln.Offset(-dist, maxDeviation);

                this.AddAuxiliary(ln.StartPoint, offsetLn.FirstVertex);
                this.AddAuxiliary(ln.EndPoint, offsetLn.LastVertex);

                GeodesicArc arc = null;
                if (prevLn == null)
                {
                    arc = GeodesicArc.Create(ln.StartPoint, dist, ln.StartAzimuth - 180.0, ln.StartAzimuth - 90.0, maxDeviation);
                }
                else
                {
                    var startAz = Utils.NormalizeAngle(prevLn.EndAzimuth - 90.0);
                    var endAz = Utils.NormalizeAngle(ln.StartAzimuth - 90.0);
                    var angle = Utils.NormalizeAngle(endAz - startAz);
                    if (angle < 180.0)
                    {
                        arc = GeodesicArc.Create(ln.StartPoint, dist, startAz, endAz, maxDeviation);
                    }
                }

                if (arc != null)
                {
                    this.OffsetArcs.Add(arc);
                }

                this.OffsetLines.Add(offsetLn);
                if (i > 0)
                {
                    var prevOffsetLn = this.OffsetLines[i - 1];
                    this.CutLines(prevLn, ln, ref prevOffsetLn, ref offsetLn);
                    this.OffsetLines[i - 1] = prevOffsetLn;
                    this.OffsetLines[i] = offsetLn;
                }

                prevLn = ln;
            }

            var lastLn = this.SourceLine.Lines[this.SourceLine.Lines.Count - 1];
            var lastArc = GeodesicArc.Create(lastLn.EndPoint, dist, lastLn.EndAzimuth - 90.0, lastLn.EndAzimuth, maxDeviation);
            this.OffsetArcs.Add(lastArc);
        }

        private void CutLines(GeodesicLine refLn1, GeodesicLine refLn2, ref GeodesicPolyline offsetLn1, ref GeodesicPolyline offsetLn2)
        {
            var ln1CutRes = offsetLn1.Cut(offsetLn2);
            var ln2CutRes = offsetLn2.Cut(offsetLn1);

            if (ln1CutRes.Count > 2 || ln2CutRes.Count > 2)
            {
                throw new Exception("ERROR: " + base.GetType().Name + ".CutLines() inconsistent result");
            }
            if (ln1CutRes.Count == 2)
            {
                offsetLn1 = this.GetCuttedLine(refLn2, ln1CutRes);
            }
            if (ln2CutRes.Count == 2)
            {
                offsetLn2 = this.GetCuttedLine(refLn1, ln2CutRes);
            }
        }

        private GeodesicPolyline GetCuttedLine(GeodesicLine oppositeRefLn, List<GeodesicPolyline> offsetLines)
        {
            if (offsetLines.Count > 2 || offsetLines.Count < 1)
            {
                throw new Exception("ERROR: " + base.GetType().Name + ".GetCutLine() inconsistent result: to many cutted lines");
            }

            if (offsetLines.Count == 1)
                return offsetLines[0];

            var ln0 = offsetLines[0];
            var ln1 = offsetLines[1];

            var ln0DistFirst = oppositeRefLn.GeodesicDistTo(ln0.FirstVertex, this.MaxDeviation * 0.5);
            var ln0DistLast = oppositeRefLn.GeodesicDistTo(ln0.LastVertex, this.MaxDeviation * 0.5);

            if (ln0DistFirst >= this.MinBufferDist && ln0DistLast >= this.MinBufferDist)
            {
                this.CuttedLines.Add(ln1);
                return ln0;
            }

            var ln1DistFirst = oppositeRefLn.GeodesicDistTo(ln1.FirstVertex, this.MaxDeviation * 0.5);
            var ln1DistLast = oppositeRefLn.GeodesicDistTo(ln1.LastVertex, this.MaxDeviation * 0.5);

            if (ln1DistFirst < this.MinBufferDist || ln1DistLast < this.MinBufferDist)
            {
                throw new Exception("ERROR: " + base.GetType().Name + ".GetCutLine() inconsistent result: offset criteria not met");
            }
            this.CuttedLines.Add(ln0);
            return ln1;            
        }

        private List<GeodesicPolyline> CutLines(List<GeodesicPolyline> lines, Polyline cutter)
        {
            var res = new List<GeodesicPolyline>(lines.Count);
            foreach (GeodesicPolyline ln in lines)
            {
                if (GeometryEngine.Crosses(ln, cutter))
                    res.AddRange(ln.Cut(cutter));
                else
                    res.Add(ln);
            }
            return res;
        }

        private List<GeodesicArc> CutArcs(List<GeodesicArc> arcs, Polyline cutter)
        {
            var res = new List<GeodesicArc>(arcs.Count);
            foreach (GeodesicArc arc in arcs)
            {
                if (GeometryEngine.Crosses(arc, cutter))
                    res.AddRange(arc.Cut(cutter));
                else
                    res.Add(arc);
            }
            return res;
        }

        private void CutAll()
        {
            var cutters = new List<Polyline>(this.OffsetLines);
            cutters.AddRange(this.OffsetArcs);
            var buffLines = new List<GeodesicPolyline>(this.OffsetLines.Count);

            foreach (GeodesicPolyline ln in this.OffsetLines)
            {
                List<GeodesicPolyline> lnCutRes = new List<GeodesicPolyline>();
                lnCutRes.Add(ln);
                foreach (Polyline cutter in cutters)
                {
                    if (ln != cutter)
                        lnCutRes = this.CutLines(lnCutRes, cutter);
                }
                buffLines.AddRange(lnCutRes);
            }

            this.OffsetLines = buffLines;
            var buffArcs = new List<GeodesicArc>(this.OffsetArcs.Count);
            foreach (GeodesicArc arc in this.OffsetArcs)
            {
                var arcCutRes = new List<GeodesicArc>();
                arcCutRes.Add(arc);
                foreach (Polyline cutter in cutters)
                {
                    if (cutter != arc)
                        arcCutRes = this.CutArcs(arcCutRes, cutter);
                }
                buffArcs.AddRange(arcCutRes);
            }

            this.OffsetArcs = buffArcs;
        }

        private void RemoveLinesInside()
        {
            var buffLines = new List<GeodesicPolyline>(this.OffsetLines.Count);
            foreach (GeodesicPolyline ln in this.OffsetLines)
            {
                var firstDist = this.DistToSourceLine(ln.FirstVertex);
                var lastDist = this.DistToSourceLine(ln.LastVertex);
                if (firstDist < this.MinBufferDist || lastDist < this.MinBufferDist)
                    this.CuttedLines.Add(ln);
                else
                    buffLines.Add(ln);
            }
            this.OffsetLines = buffLines;
        }

        private void RemoveArcsInside()
        {
            var buffArcs = new List<GeodesicArc>(this.OffsetArcs.Count);
            foreach (GeodesicArc arc in this.OffsetArcs)
            {
                var firstDist = this.DistToSourceLine(arc.FirstVertex);
                var lastDist = this.DistToSourceLine(arc.LastVertex);
                if (firstDist < this.MinBufferDist || lastDist < this.MinBufferDist)
                    this.CuttedArcs.Add(arc);
                else
                    buffArcs.Add(arc);
            }
            this.OffsetArcs = buffArcs;
        }

        private void AddAuxiliary(MapPoint p1, MapPoint p2)
        {
            var ln = GeodesicLine.Create(p1, p2);
            this.SourceAuxiliaryLines.Add(ln);
        }

        public IEnumerable<MapPoint> GetCalculatedPoints()
        {
            //var res = MapPointHashSet.Create2d(this.MaxDeviation);
            var res = new List<MapPoint>();
            foreach (GeodesicArc arc in this.OffsetArcs)
            {
                res.Add(arc.FirstVertex);
                res.Add(arc.LastVertex);
            }
            foreach (GeodesicPolyline ln in this.OffsetLines)
            {
                res.AddRange(ln.Vertices);
            }
            return res;
        }

        public IEnumerable<MapPoint> GetDensifyPoints()
        {
            //var res = MapPointHashSet.Create2d(this.MaxDeviation);
            var res = new List<MapPoint>();
            foreach (GeodesicArc arc in this.OffsetArcs)
            {
                res.AddRange(arc.DensifyPoints);
            }
            foreach (GeodesicPolyline ln in this.OffsetLines)
            {
                res.AddRange(ln.DensifyPoints);
            }
            return res;
        }
    }
}
