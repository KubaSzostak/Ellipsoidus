using Esri.ArcGISRuntime.Geometry;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Ellipsoidus
{


    public class LineLineIntersectionPresenter : GeodesicPresenter
    {

        private static Random Rnd = new Random();

        public LineLineIntersectionPresenter()
        {
            FirstLine = new GeodesicLineSegmentPresenter();
            FirstLine.PropertyChanged += LineChanged;

            SecondLine = new GeodesicLineSegmentPresenter();
            SecondLine.PropertyChanged += LineChanged;

            IntersectionPoint = new GeodesicPointPresenter();
            LineChanged(this, null);
        }

        void LineChanged(object sender, PropertyChangedEventArgs e)
        {
            var pt = FirstLine.Line.IntersectionPoint(SecondLine.Line);
            IntersectionPoint.Point = pt.Cast("IntersectionPoint");
            NotifyPropertyChanged("IntersectionPoint");
        }

        public GeodesicLineSegmentPresenter FirstLine { get; private set; }
        public GeodesicLineSegmentPresenter SecondLine { get; private set; }
        public GeodesicPointPresenter IntersectionPoint { get; private set; }

        public Envelope Extent()
        {
            var res = FirstLine.Line.Extent;
            res = res.Union(SecondLine.Line.Extent);
            res = res.Union(IntersectionPoint.Point);

            return res;
        }
    }
}
