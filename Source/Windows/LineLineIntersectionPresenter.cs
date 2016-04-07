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
       
        public LineLineIntersectionPresenter()
        {
            IntersectionPoint = new GeodesicPointPresenter();

            FirstLine = new GeodesicLineSegmentPresenter();
            FirstLine.PropertyChanged += LineChanged;

            SecondLine = new GeodesicLineSegmentPresenter();
            SecondLine.PropertyChanged += LineChanged;

            Init();
        }

        private bool _disableLineChanged = false;
        void LineChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_disableLineChanged)
                return;

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

        internal void Init()
        {
            _disableLineChanged = true;

            FirstLine.StartPoint.Point = Presenter.RandomPoint("L1.Start");
            FirstLine.EndPoint.Point = Presenter.RandomPoint("L1.End");

            SecondLine.StartPoint.Point = Presenter.RandomPoint("L2.Start");
            SecondLine.EndPoint.Point = Presenter.RandomPoint("L2.End");

            _disableLineChanged = false;
            LineChanged(this, null);
        }
    }
}
