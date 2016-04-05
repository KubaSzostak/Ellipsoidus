using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Layers;
using System;
using System.Collections.Generic;

namespace Esri.ArcGISRuntime.Geometry
{
    public class GeodesicMapPoint : MapPoint
    {
        public string Id { get; set; }

        public GeodesicSegment SourceGeometry { get; set; }
        public GeodesicMapPoint SourcePoint { get; set; }
        public string Origin { get; private set; }
        public string XText { get { return Utils.ToDegMinSecString(this.X); } }
        public string YText { get { return Utils.ToDegMinSecString(this.Y); } }

        public GeodesicMapPoint(string id, double x, double y, double z, SpatialReference spatialReference)
            : base(x, y, z, spatialReference)
        {
            this.Id = id;
        }

        public GeodesicMapPoint(string id, double x, double y, SpatialReference spatialReference)
            : base(x, y, spatialReference)
        {
            this.Id = id;
        }

        public GeodesicMapPoint(MapPoint p, string id)
            : base(p.X, p.Y, p.Z, p.SpatialReference)
        {
            this.Id = id;
        }

        public GeodesicMapPoint(string id, double x, double y)
            : base(x, y, SpatialReferences.Wgs84)
        {
            this.Id = id;
        }

        /// <summary>
        /// Update origin only if does not have value
        /// </summary>
        /// <param name="origin"></param>
        public void UpdateOrigin(string origin)
        {
            if (string.IsNullOrEmpty(this.Origin))
                this.Origin = origin;
        }

        public Graphic GetGraphic()
        {
            var res = new Graphic(this, Symbols.Blue2.Point);
            res.Attributes["Id"] = this.Id;
            res.Attributes["Origin"] = this.Origin;

            return res;
        }

        public void CopyFrom(GeodesicMapPoint other)
        {
            this.Id = other.Id;
            this.Origin = other.Origin;
            this.SourceGeometry = other.SourceGeometry;
            this.SourcePoint = other.SourcePoint;
        }

    }




}
