using Esri.ArcGISRuntime.Geometry;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
namespace Esri
{
    public class GeodesicOffsetBuilder
    {
        public List<GeodesicArc> OffsetArcs = new List<GeodesicArc>();
        public List<GeodesicOffsetLine> OffsetLines = new List<GeodesicOffsetLine>();
        public readonly List<GeodesicSegment> OffsetSegments = new List<GeodesicSegment>();
        public readonly List<GeodesicMapPoint> OffsetVertices = new List<GeodesicMapPoint>();
        public readonly List<GeodesicMapPoint> OffsetDensifyPoints = new List<GeodesicMapPoint>();

        public readonly List<GeodesicLine> SourceAuxiliaryLines = new List<GeodesicLine>();
        public readonly List<GeodesicLine> OutputAuxiliaryLines = new List<GeodesicLine>();

        public readonly List<GeodesicSegment> CuttedLines = new List<GeodesicSegment>();
        public readonly List<GeodesicSegment> CuttedArcs = new List<GeodesicSegment>();


        public GeodesicPolyline SourceLine { get; private set; }
        public double BufferDist { get; private set; }
        public TimeSpan BuildTime { get; private set; }

        public GeodesicMapPoint SegmentErrorPoint { get; private set; }

        public GeodesicOffsetBuilder(GeodesicPolyline ln, double dist)
        {
            this.SourceLine = ln;
            this.BufferDist = dist;
        }

        public void Build()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            this.OffsetArcs.Clear();
            this.OffsetLines.Clear();
            this.SourceAuxiliaryLines.Clear();
            this.CuttedLines.Clear();
            this.CuttedArcs.Clear();

            this.BuildLinesAndArcs(this.BufferDist);
            this.CutAll();
            this.RemoveLinesInside();
            this.RemoveArcsInside();

            this.BuildSegments();

            sw.Stop();
            this.BuildTime = sw.Elapsed;
        }

        public Task BuildAsync()
        {
            var action = new Action(this.Build);
            return Task.Run(action, CancellationToken.None);
        }


        public bool IsInside(params MapPoint[] points)
        {
            var minBufferDist = this.BufferDist - Geodesic.DistanceEpsilon * 2.0;
            foreach (var pt in points)
            {
                var dist = this.SourceLine.GeodesicDistTo(pt);
                if (dist < minBufferDist)
                    return true;                
            }
            return false;
        }

        private void BuildSegments()
        {
            SegmentErrorPoint = null;
            var segments = new HashSet<GeodesicSegment>(this.OffsetArcs);
            foreach (var ln in this.OffsetLines)
                segments.Add(ln);

            var cutted = new HashSet<GeodesicSegment>(this.CuttedArcs);
            foreach (var ln in CuttedLines)
                cutted.Add(ln);

            this.OffsetSegments.Clear();

            // first segment is always arc
            GeodesicSegment prevSegm = this.OffsetArcs[0];
            this.OffsetSegments.Add(prevSegm);
            segments.Remove(prevSegm);
            
            while (segments.Count > 0)
            {
                var segm = segments.FindByStartPoint(prevSegm.EndPoint);

                // Maby it is already deleted because of hardware computation errors
                // Check it again
                if (segm == null)
                {
                    segm = this.CuttedLines.FindByStartPoint(prevSegm.EndPoint);
                    if (segm != null) 
                    {
                        this.CuttedLines.Remove(segm); 
                        this.OffsetLines.Add(segm as GeodesicOffsetLine);
                    }
                }
                if (segm == null) 
                { 
                    segm = this.CuttedArcs.FindByStartPoint(prevSegm.EndPoint);
                    if (segm != null) 
                    { 
                        this.CuttedArcs.Remove(segm);
                        this.OffsetArcs.Add(segm as GeodesicArc);
                    }
                }

                // Cannot find next segment 
                if (segm == null)
                {
                    SegmentErrorPoint = prevSegm.EndPoint;
                    return;
                }

                this.OffsetSegments.Add(segm);
                segments.Remove(segm);
                prevSegm = segm;
            }
            BuildOffsetPoints();
        }

        private void BuildOffsetPoints()
        {
            this.OffsetVertices.Clear();
            this.OffsetDensifyPoints.Clear();

            foreach (var segm in this.OffsetSegments)
            {
                this.OffsetVertices.Add(segm.StartPoint);
            }
        }

        private void BuildLinesAndArcs(double dist)
        {
            GeodesicLine prevLn = null;
            this.OffsetLines.Clear();
            this.OffsetArcs.Clear();

            for (int i = 0; i < this.SourceLine.Lines.Count; i++)
            {
                var ln = this.SourceLine.Lines[i];
                var offsetLn = ln.Offset(-dist);
                offsetLn.UpdateOrigin("Offset");

                offsetLn.StartPoint.SourceGeometry = ln;
                offsetLn.StartPoint.SourcePoint = ln.StartPoint;

                offsetLn.EndPoint.SourceGeometry = ln;
                offsetLn.EndPoint.SourcePoint = ln.EndPoint;

                this.AddAuxiliary(ln.StartPoint, offsetLn.StartPoint);
                this.AddAuxiliary(ln.EndPoint, offsetLn.EndPoint);

                GeodesicArc arc = null;
                if (prevLn == null)
                {
                    arc = GeodesicArc.Create(ln.StartPoint, Math.Abs(dist), ln.StartAzimuth - 180.0, ln.StartAzimuth - 90.0);
                    arc.StartPoint.SourceGeometry = ln;
                    arc.EndPoint.SourceGeometry = ln;
                }
                else
                {
                    var startAz = prevLn.EndAzimuth - 90.0; 
                    var endAz = ln.StartAzimuth - 90.0; 
                    var angle = Geodesic.GetAngle(startAz, endAz);
                    if (angle < 180.0)
                    {
                        arc = GeodesicArc.Create(ln.StartPoint, Math.Abs(dist), startAz, endAz);
                        arc.StartPoint.SourceGeometry = prevLn;
                        arc.EndPoint.SourceGeometry = ln;
                    }
                }

                if (arc != null)
                {
                    this.OffsetArcs.Add(arc);
                    arc.UpdateOrigin("Offset");
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
            var lastArc = GeodesicArc.Create(lastLn.EndPoint, Math.Abs(dist), lastLn.EndAzimuth - 90.0, lastLn.EndAzimuth);
            lastArc.UpdateOrigin("Offset");
            lastArc.StartPoint.SourceGeometry = lastLn;
            lastArc.EndPoint.SourceGeometry = lastLn;
            this.OffsetArcs.Add(lastArc);
        }

        private void CutLines(GeodesicLine refLn1, GeodesicLine refLn2, ref GeodesicOffsetLine offsetLn1, ref GeodesicOffsetLine offsetLn2)
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
                offsetLn1.UpdateOrigin("Cut");
            }
            if (ln2CutRes.Count == 2)
            {
                offsetLn2 = this.GetCuttedLine(refLn1, ln2CutRes);
                offsetLn2.UpdateOrigin("Cut");
            }
        }

        private GeodesicOffsetLine GetCuttedLine(GeodesicLine oppositeRefLn, List<GeodesicOffsetLine> offsetLines)
        {
            if (offsetLines.Count > 2 || offsetLines.Count < 1)
            {
                throw new Exception("ERROR: " + base.GetType().Name + ".GetCutLine() inconsistent result: to many cutted lines");
            }

            if (offsetLines.Count == 1)
                return offsetLines[0];

            var ln0 = offsetLines[0];
            var ln1 = offsetLines[1];

            var ln0DistFirst = oppositeRefLn.GeodesicDistTo(ln0.StartPoint);
            var ln0DistLast = oppositeRefLn.GeodesicDistTo(ln0.EndPoint);
            var ln0Dist = Math.Min(ln0DistFirst, ln0DistLast);

            var ln1DistFirst = oppositeRefLn.GeodesicDistTo(ln1.StartPoint);
            var ln1DistLast = oppositeRefLn.GeodesicDistTo(ln1.EndPoint);
            var ln1Dist = Math.Min(ln1DistFirst, ln1DistLast);

            if (ln0Dist < ln1Dist)
            {
                this.CuttedLines.Add(ln0);
                return ln1; 
            }
            else
            {
                this.CuttedLines.Add(ln1);
                return ln0;
            }    
        }

        private List<GeodesicOffsetLine> CutLines(List<GeodesicOffsetLine> lines, Polyline cutter)
        {
            var res = new List<GeodesicOffsetLine>();
            foreach (var ln in lines)
            {
                if (GeometryEngine.Crosses(ln, cutter))
                {
                    var cutRes = ln.Cut(cutter);
                    foreach (var cr in cutRes)
                    {
                        cr.UpdateOrigin("Cut");
                        res.Add(cr);
                    }
                }
                else
                    res.Add(ln);
            }
            return res;
        }

        private List<GeodesicArc> CutArcs(List<GeodesicArc> arcs, Polyline cutter)
        {
            var res = new List<GeodesicArc>(arcs.Count);
            foreach (var arc in arcs)
            {
                if (GeometryEngine.Crosses(arc, cutter))
                {
                    var cutRes = arc.Cut(cutter);
                    foreach (var cr in cutRes)
                    {
                        cr.UpdateOrigin("Cut");
                        res.Add(cr);
                    }
                }
                else
                    res.Add(arc);
            }
            return res;
        }

        private void CutAll()
        {
            var cutters = new List<Polyline>(this.OffsetLines);
            cutters.AddRange(this.OffsetArcs);
            var buffLines = new List<GeodesicOffsetLine>(this.OffsetLines.Count);


            // First: cut lines
            foreach (var ln in this.OffsetLines)
            {
                var lnCutRes = new List<GeodesicOffsetLine>();
                lnCutRes.Add(ln);
                foreach (var cutter in cutters)
                {
                    if (ln != cutter)
                        lnCutRes = this.CutLines(lnCutRes, cutter);
                }
                buffLines.AddRange(lnCutRes);
            }

            // Then: cut arcs
            this.OffsetLines = buffLines;
            var buffArcs = new List<GeodesicArc>(this.OffsetArcs.Count);
            foreach (var arc in this.OffsetArcs)
            {
                var arcCutRes = new List<GeodesicArc>();
                arcCutRes.Add(arc);
                foreach (var cutter in cutters)
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
            var buffLines = new List<GeodesicOffsetLine>(this.OffsetLines.Count);
            foreach (var ln in this.OffsetLines)
            {
                if (this.IsInside(ln.StartPoint, ln.EndPoint))
                    this.CuttedLines.Add(ln);
                else
                    buffLines.Add(ln);
            }
            this.OffsetLines = buffLines;
        }

        private void RemoveArcsInside()
        {
            var buffArcs = new List<GeodesicArc>(this.OffsetArcs.Count);
            foreach (var arc in this.OffsetArcs)
            {
                if (this.IsInside(arc.StartPoint, arc.EndPoint))
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
            foreach (var arc in this.OffsetArcs)
            {
                res.Add(arc.StartPoint);
                res.Add(arc.EndPoint);
            }
            foreach (var ln in this.OffsetLines)
            {
                res.Add(ln.StartPoint);
                res.Add(ln.EndPoint);
            }
            return res;
        }

        public IEnumerable<MapPoint> GetDensifyPoints()
        {
            //var res = MapPointHashSet.Create2d(this.MaxDeviation);
            var res = new List<MapPoint>();
            foreach (var arc in this.OffsetArcs)
            {
                res.AddRange(arc.DensifyPoints);
            }
            foreach (var ln in this.OffsetLines)
            {
                res.AddRange(ln.DensifyPoints);
            }
            return res;
        }
    }
}
