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

        public readonly List<GeodesicLineSegment> SourceAuxiliaryLines = new List<GeodesicLineSegment>();
        public readonly List<GeodesicLineSegment> OutputAuxiliaryLines = new List<GeodesicLineSegment>();

        public readonly List<GeodesicSegment> CuttedLines = new List<GeodesicSegment>();
        public readonly List<GeodesicSegment> CuttedArcs = new List<GeodesicSegment>();

        public readonly GeodesicPolyline CuttingLine;
        private readonly Polygon CuttingPolygon = null;


        public GeodesicPolyline ReferenceLine { get; private set; }
        public double BufferDist { get; private set; }
        public TimeSpan BuildTime { get; private set; }

        public GeodesicMapPoint SegmentErrorPoint { get; private set; }

        public GeodesicOffsetBuilder(GeodesicPolyline ln, double dist, GeodesicPolyline cuttingLine )
        {
            this.ReferenceLine = ln;
            this.BufferDist = dist;

            if (cuttingLine != null)
            {
                this.CuttingLine = cuttingLine;
                var plgPoints = new List<MapPoint>(CuttingLine.DensifyPoints);

                // Omit first and lat densify point - they could lay out of CuttingLine

                for (int i = this.ReferenceLine.DensifyPoints.Count - 2; i >= 1; i--)
                {
                    var pt = this.ReferenceLine.DensifyPoints[i];
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
            var minBufferDist = this.BufferDist - NETGeographicLib.GeodesicUtils.DistanceEpsilon * 20.0;  // Probably ArcGIS limitation to 2mm
            foreach (var pt in points)
            {
                var dist = this.ReferenceLine.GeodesicDistTo(pt);
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

        private GeodesicArc BuildArc(GeodesicLineSegment prevLn, GeodesicLineSegment ln, double dist)
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
            GeodesicLineSegment prevLn = null;
            this.OffsetLines.Clear();
            this.OffsetArcs.Clear();

            for (int i = 0; i < this.ReferenceLine.Lines.Count; i++)
            {
                var ln = this.ReferenceLine.Lines[i];
                var offsetLn = ln.Offset(-dist);
                this.OffsetLines.Add(offsetLn);

                this.AddAuxiliary(ln.StartPoint, offsetLn.StartPoint);
                this.AddAuxiliary(ln.EndPoint, offsetLn.EndPoint);

                GeodesicArc arc = BuildArc(prevLn, ln, dist);
                if (arc != null) 
                {
                    this.OffsetArcs.Add(arc);
                }
                else if (i > 0)
                {
                    var prevOffsetLn = this.OffsetLines[i - 1]; // make a copy
                    if (!prevOffsetLn.EndPoint.IsEqual2d(offsetLn.StartPoint)) // they can be parallel
                    {
                        var cuttedPrevOffsetLn = CutOutLine(prevOffsetLn, offsetLn);
                        var cuttedOffsetLn = CutOutLine(offsetLn, prevOffsetLn); // use the copy

                        if ((prevOffsetLn != cuttedPrevOffsetLn) || (offsetLn != cuttedOffsetLn))
                        {
                            this.OffsetLines[i - 1] = cuttedOffsetLn;
                            this.OffsetLines[i] = cuttedPrevOffsetLn;

                            // if they are quasi-parallel, e.g. (StartAz-EndAz) = 0.0004" tricky things happens due to round/calulation errors
                            // just add missing line
                            //BuildLinesAndArcsCheckAndFix(cuttedPrevOffsetLn, cuttedOffsetLn);
                        };
                    }
                }

                prevLn = ln;
            }
            var lastLn = this.ReferenceLine.Lines[this.ReferenceLine.Lines.Count - 1];
            var lastArc = BuildArc(lastLn, null, dist);
            this.OffsetArcs.Add(lastArc);
        }

        private void BuildLinesAndArcsCheckAndFix(GeodesicOffsetLine prevOffsetLn, GeodesicOffsetLine offsetLn)
        {
            if (prevOffsetLn.EndPoint.IsEqual2d(offsetLn.StartPoint))
                return;

            // OffsetDist: 22 224 m
            // ----------------------------------
            // Deviation: 0.1 mm -> Length: 2.1 m
            // Deviation: 1.0 mm -> Length: 6.6 m
            // Deviation: 10  mm -> Length: 21  m
            // Deviation: 100 mm -> Length: 67  m
 
            double len;
            NETGeographicLib.GeodesicUtils.ETRS89.Inverse(prevOffsetLn.EndPoint.ToGeoPoint(), offsetLn.StartPoint.ToGeoPoint(), out len);
            if (len < 5.0)
            {
                var points = new MapPoint[] {prevOffsetLn.EndPoint, offsetLn.StartPoint};
                var midLn = new GeodesicOffsetLine(points, offsetLn.ReferenceLine, offsetLn.OffsetDist, len);
                this.OffsetLines.Add(offsetLn);
            }
            else
            {
                Trace.TraceError(this.GetType().Name + ".BuildLinesAndArcs() computation dead end.");
                Trace.Indent();
                Trace.TraceError("Reference line: " + offsetLn.ReferenceLine.ToString());
                Trace.TraceError("Offset line: " + offsetLn.ToString());
                Trace.Unindent();
            }
        }

        private double GetMinDist(GeodesicLineSegment srcLn, GeodesicOffsetLine ln)
        {
            var spDist = srcLn.GeodesicDistTo(ln.StartPoint);
            var epDist = srcLn.GeodesicDistTo(ln.EndPoint);
            return Math.Min(spDist, epDist);
        }

        private List<GeodesicOffsetLine> RemoveEmptySegments(List<GeodesicOffsetLine> segments)
        {
            var removedSegments = new List<GeodesicOffsetLine>();

            for (int i = segments.Count - 1; i >= 0; i--)
            {
                if (Geodesic.IsZeroLength(segments[i].Length))
                {
                    removedSegments.Add(segments[i]);
                    segments.RemoveAt(i);
                }
            }

            return removedSegments;
        }

        private string GetSegmentsInfoText(List<GeodesicOffsetLine> segments, string header)
        {
            string res = "";
            foreach (var s in segments)
            {
                res += "\r\n" + s.ToString();
            }

            if (!string.IsNullOrEmpty(res) && !string.IsNullOrEmpty(header))
                res = header + res;

            return res;
        }

        public GeodesicOffsetLine CutOutLine( GeodesicOffsetLine ln, GeodesicOffsetLine cutter)
        {
            var cutRes = ln.Cut(cutter);

            if ((cutRes.Count == 1) && (cutRes[0] == ln))
            {
                return ln;
            }

            var removedSegments = RemoveEmptySegments(cutRes);
            if (cutRes.Count == 1) 
                return cutRes[0];

            var removedSegmentsInfo = GetSegmentsInfoText(removedSegments, "\r\n\r\n" + "Removed segments: ");

            if (cutRes.Count < 1)
            {
                throw new Exception("ERROR: Empty " + this.GetType().Name + ".CutOutLine() result." + removedSegmentsInfo);
            }
            if (cutRes.Count > 2)
            {
                var cutResSegmentsInfo = GetSegmentsInfoText(cutRes, "\r\n" + "Segments: ");
                throw new Exception("ERROR: " + base.GetType().Name + ".CutLines() inconsistent result" + cutResSegmentsInfo + removedSegmentsInfo);
            }

            // cutRes.Count == 2

            var cut0 = cutRes[0];
            var cut1 = cutRes[1];

            var minCut0Dist = GetMinDist(cutter.ReferenceLine, cut0); 
            var minCut1Dist = GetMinDist(cutter.ReferenceLine, cut1);

            // this happens if lines are quasi-parallel
            if (Geodesic.IsZeroLength(Math.Abs(cut0.OffsetDist) - minCut0Dist) && Geodesic.IsZeroLength(Math.Abs(cut1.OffsetDist) - minCut1Dist))
            {
                if (cut0.Length < cut1.Length)
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

            if (minCut0Dist < minCut1Dist)
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
            var ln = GeodesicLineSegment.Create(p1, p2);
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

            var removedSegments = new List<GeodesicSegment>();
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
                    removedSegments.Add(segm);
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
            foreach (var segm in removedSegments)
            {
                this.OffsetSegments.Remove(segm);
            }
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
