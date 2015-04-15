using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Layers;
using System;
using System.Collections.Generic;

namespace Esri
{
    public class MapPointGraphic : MapPoint
    {
        public Graphic Graphic { get; private set; }
        public string Id
        {
            get { return this.Graphic.GetAttributeText("Id"); }
            set { this.Graphic.Attributes["Id"] = value; }
        }

        public MapPointGraphic(string id, double x, double y, double z, SpatialReference spatialReference)
            : base(x, y, z, spatialReference)
        {
            this.Graphic = new Graphic(this);
            this.Id = id;
        }

        public MapPointGraphic(string id, double x, double y, SpatialReference spatialReference)
            : base(x, y, spatialReference)
        {
            this.Graphic = new Graphic(this);
            this.Id = id;
        }

        public MapPointGraphic(MapPoint p, string id)
            : base(p.X, p.Y, p.Z, p.SpatialReference)
        {
            this.Graphic = new Graphic(this);
            this.Id = id;
        }

        public MapPointGraphic(string id, double x, double y)
            : base(x, y, SpatialReferences.Wgs84)
        {
            this.Graphic = new Graphic(this);
            this.Id = id;
        }

        public void CopyProperties(MapPoint source)
        {
            MapPointGraphic mpgSrc = source as MapPointGraphic;
            if (mpgSrc != null)
            {
                this.Id = mpgSrc.Id;
            }
        }

        public void CopyAttributes(MapPoint source)
        {
            MapPointGraphic mpgSrc = source as MapPointGraphic;
            if (mpgSrc != null)
            {
                foreach (KeyValuePair<string, object> kv in mpgSrc.Graphic.Attributes)
                {
                    this.Graphic.Attributes[kv.Key] = kv.Value;
                }
            }
        }

        public bool IsEqual2d(MapPoint other, double maxDeviation)
        {
            var dx = Math.Abs(this.X - other.X);
            var dy = Math.Abs(this.Y - other.Y);
            return (dx <= maxDeviation) && (dy <= maxDeviation);
        }

        public bool IsCoordEqual(MapPoint other)
        {
            if ((this.X != other.X) || (this.Y != other.Y))
                return false;

            if (this.HasZ != other.HasZ)
                return false;
            if (!this.HasZ)
                return true;
            if (this.Z != other.Z)
                return false;

            if (this.HasM != other.HasM)
                return false;
            return this.M == other.M;


        }

        public override bool IsEqual(Geometry other)
        {
            if (other is MapPoint)
                return this.IsCoordEqual(other as MapPoint);

            return base.IsEqual(other);
        }
    }




    public abstract class MapPointComparer : IEqualityComparer<MapPoint>
    {

        public MapPointComparer(double maxDeviation)
        {
            if (maxDeviation < 0.0)
                throw new ArgumentException(this.GetType().Name + ".MaxDeviation must be greater than zero");
            if (double.IsNaN(maxDeviation))
                maxDeviation = 0.0;

            this.MaxDeviation = maxDeviation;
            this.RoundFactor = GetRoundFactor(maxDeviation);
        }

        public readonly double MaxDeviation = 0.001;
        private readonly double RoundFactor = 1.0;

        private double GetRoundFactor(double maxDeviation)
        {
            // Consider maxDeviation=0.01 and x1=12.341, x2=12.342 -> they both should return the same HashCode

            if ((maxDeviation == 0.0) || (maxDeviation == 1.0))
                return 1.0;

            double res = 1.0;

            if (maxDeviation < 1.0) // maxDeviation = 0.004
            {
                // double has 15-16 digits precision
                for (int i = 0; i < 16; i++)
                {
                    var v = maxDeviation * res * 10.0;
                    if (v > 0.0)
                        return res;
                    res = res * 10.0;
                }
            }
            else // maxDeviation = 400
            {
                // double has 15-16 digits precision
                for (int i = 0; i < 16; i++)
                {
                    var v = maxDeviation * res * 0.1;
                    if (v < 0.0)
                        return res;
                    res = res * 0.1;
                }
            }
            return res;
        }

        public abstract bool Equals(MapPoint p1, MapPoint p2);

        protected bool CoordEquals(double c1, double c2)
        {
            if (MaxDeviation == 0.0)
                return c1 == c2;

            var delta = Math.Abs(c1 - c2);
            return delta <= this.MaxDeviation;
        }

        public int GetHashCode(MapPoint p)
        {
            unchecked
            {
                if (MaxDeviation == 0)
                    return p.X.GetHashCode() + p.Y.GetHashCode() + p.Z.GetHashCode();

                var x = Math.Round(p.X + RoundFactor);
                var y = Math.Round(p.Y + RoundFactor);
                var z = Math.Round(p.Z + RoundFactor);

                return x.GetHashCode() + y.GetHashCode() + z.GetHashCode();
            }
        }
    }

    public class MapPointComparer2d : MapPointComparer
    {
        public MapPointComparer2d(double maxDeviation)
            : base(maxDeviation)
        { }

        public override bool Equals(MapPoint p1, MapPoint p2)
        {
            return CoordEquals(p1.X, p2.X) && CoordEquals(p1.Y, p2.Y);
        }
    }

    public class MapPointComparer3d : MapPointComparer2d
    {
        public MapPointComparer3d(double maxDeviation)
            : base(maxDeviation)
        { }

        public override bool Equals(MapPoint p1, MapPoint p2)
        {
            return base.Equals(p1, p2) && CoordEquals(p1.Z, p2.Z);
        }
    }

    public class MapPointHashSet : HashSet<MapPoint>
    {
        public MapPointHashSet(MapPointComparer comparer)
            : base(comparer)
        {

        }

        public void AddRange(IEnumerable<MapPoint> items)
        {
            foreach (var pt in items)
            {
                this.Add(pt);
            }
        }

        public static MapPointHashSet Create2d(double maxDeviation)
        {
            var comp = new MapPointComparer2d(maxDeviation);
            return new MapPointHashSet(comp);
        }

        public static MapPointHashSet Create3d(double maxDeviation)
        {
            var comp = new MapPointComparer3d(maxDeviation);
            return new MapPointHashSet(comp);
        }
    }
}
