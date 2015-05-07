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

        public readonly GeodesicPolyline CuttingLine;
        private readonly Polygon CuttingPolygon = null;


        public GeodesicPolyline SourceLine { get; private set; }
        public double BufferDist { get; private set; }
        public TimeSpan BuildTime { get; private set; }

        public GeodesicMapPoint SegmentErrorPoint { get; private set; }

        public GeodesicOffsetBuilder(GeodesicPolyline ln, double dist, GeodesicPolyline cuttingLine )
        {
            this.SourceLine = ln;
            this.BufferDist = dist;

            if (cuttingLine != null)
            {
                this.CuttingLine = cuttingLine;
                var plgPoints = new List<MapPoint>(CuttingLine.DensifyPoints);
                for (int i = this.SourceLine.DensifyPoints.Count - 1; i >= 0; i--)
                {
                    var pt = this.SourceLine.DensifyPoints[i];
                    plgPoints.Add(pt);
                }
                this.CuttingPolygon = new Polygon(plgPoints);
            }
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


            this.CutWithCuttingLine(); // Before BuildSegments()
            this.BuildSegments();
            this.RemoveSegmentsOutOfCuttingLine();


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

        private GeodesicArc BuildArc(GeodesicLine prevLn, GeodesicLine ln, double dist)
        {
            GeodesicArc arc = null;
            if (prevLn == null)
            {
                arc = GeodesicArc.Create(ln.StartPoint, Math.Abs(dist), ln.StartAzimuth - 180.0, ln.StartAzimuth - 90.0);
                arc.StartPoint.SourceGeometry = ln;
                arc.EndPoint.SourceGeometry = ln;
            }
            else if (ln == null)
            {
                var lastLn = prevLn;
                arc = GeodesicArc.Create(lastLn.EndPoint, Math.Abs(dist), lastLn.EndAzimuth - 90.0, lastLn.EndAzimuth);
                arc.StartPoint.SourceGeometry = lastLn;
                arc.EndPoint.SourceGeometry = lastLn;
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
                arc.UpdateOrigin("Offset");
            }
            return arc;
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

                this.AddAuxiliary(ln.StartPoint, offsetLn.StartPoint);
                this.AddAuxiliary(ln.EndPoint, offsetLn.EndPoint);

                GeodesicArc arc = BuildArc(prevLn, ln, dist);
                if (arc != null)
                    this.OffsetArcs.Add(arc);

                this.OffsetLines.Add(offsetLn);
                if (i > 0)
                {
                    var prevOffsetLn = this.OffsetLines[i - 1]; // make a copy
                    this.OffsetLines[i - 1] = CutOutLine(prevOffsetLn, offsetLn);
                    this.OffsetLines[i] = CutOutLine(offsetLn, prevOffsetLn); // use the copy
                }

                prevLn = ln;
            }
            var lastLn = this.SourceLine.Lines[this.SourceLine.Lines.Count - 1];
            var lastArc = BuildArc(lastLn, null, dist);
            this.OffsetArcs.Add(lastArc);
        }

        private double GetMinDist(GeodesicLine srcLn, GeodesicOffsetLine ln)
        {
            var spDist = srcLn.GeodesicDistTo(ln.StartPoint);
            var epDist = srcLn.GeodesicDistTo(ln.EndPoint);
            return Math.Min(spDist, epDist);
        }

        public GeodesicOffsetLine CutOutLine( GeodesicOffsetLine ln, GeodesicOffsetLine cutter)
        {
            var cutRes = ln.Cut(cutter);

            if (cutRes.Count > 2)
            {
                throw new Exception("ERROR: " + base.GetType().Name + ".CutLines() inconsistent result");
            }
            if (cutRes.Count < 2)
            {
                return ln;
            }

            // cutRes.Count == 2

            var cut0 = cutRes[0];
            var cut1 = cutRes[1];
            cut0.UpdateOrigin("Cut");
            cut1.UpdateOrigin("Cut");

            var cut0Dist = GetMinDist(cutter.SourceLine, cut0); 
            var cut1Dist = GetMinDist(cutter.SourceLine, cut1);

            if (cut0Dist < cut1Dist)
            {
                this.CuttedLines.Add(cut0);
                return cut1;
            }
            else
            {
                this.CuttedLines.Add(cut1);
                return cut0;
            }    
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

        private List<GeodesicOffsetLine> CutLines(List<GeodesicOffsetLine> lines, List<Polyline> cutters)
        {
            var res = new List<GeodesicOffsetLine>();

            foreach (var ln in lines)
            {
                var lnCutRes = new List<GeodesicOffsetLine>(); // init empty list
                lnCutRes.Add(ln);
                foreach (var cutter in cutters)
                {
                    if (ln != cutter)
                        lnCutRes = this.CutLines(lnCutRes, cutter);
                }
                res.AddRange(lnCutRes);
            }

            return res;
        }

        private List<GeodesicArc> CutArcs(List<GeodesicArc> lines, List<Polyline> cutters)
        {
            var res = new List<GeodesicArc>();

            foreach (var ln in lines)
            {
                var lnCutRes = new List<GeodesicArc>(); // init empty list
                lnCutRes.Add(ln);
                foreach (var cutter in cutters)
                {
                    if (ln != cutter)
                        lnCutRes = this.CutArcs(lnCutRes, cutter);
                }
                res.AddRange(lnCutRes);
            }

            return res;
        }

        private void CutAll()
        {
            var cutters = new List<Polyline>(this.OffsetLines);
            cutters.AddRange(this.OffsetArcs);

            // First: cut lines
            this.OffsetLines = this.CutLines(this.OffsetLines, cutters);

            // Then: cut arcs
            this.OffsetArcs = this.CutArcs(this.OffsetArcs, cutters);
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

        private void CutWithCuttingLine()
        {
            if (this.CuttingLine == null)
                return;

            this.OffsetArcs = CutArcs(this.OffsetArcs, this.CuttingLine);
            this.OffsetLines = CutLines(this.OffsetLines, this.CuttingLine);
        }

        private void RemoveSegmentsOutOfCuttingLine()
        {
            //TODO: how to set start point to build OffsetSegments if OffsetLine is cutted by some CuttingLine?
            // Is there a need for checking anything?

            if (this.CuttingLine == null)
                return;


            var linesInside = new List<GeodesicOffsetLine>();
            var arcsInside = new List<GeodesicArc>();
            foreach (var segm in this.OffsetSegments)
            {
                if (GeometryEngine.Contains(this.CuttingPolygon, segm))
                {
                    if (segm is GeodesicOffsetLine)
                        linesInside.Add(segm as GeodesicOffsetLine);
                    else if (segm is GeodesicArc)
                        arcsInside.Add(segm as GeodesicArc);
                    else
                        throw new NotSupportedException();
                }
                else
                {
                    if (segm is GeodesicOffsetLine)
                        CuttedLines.Add(segm as GeodesicOffsetLine);
                    else if (segm is GeodesicArc)
                        CuttedArcs.Add(segm as GeodesicArc);
                    else
                        throw new NotSupportedException();
                }
            }
            this.OffsetLines = linesInside;
            this.OffsetArcs = arcsInside;
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
