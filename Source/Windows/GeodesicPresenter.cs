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

    public class GeodesicPresenter : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
    }

    public class GeodesicPointPresenter : GeodesicPresenter
    {
        private GeodesicMapPoint _point = Presenter.RandomPoint("");
        public GeodesicMapPoint Point
        {
            get { return _point; }
            set
            {
                _point = value;
                NotifyPropertyChanged("XText");
                NotifyPropertyChanged("YText");
            }
        }

        public string XText
        {
            get { return Utils.ToDegMinSecString(_point.X); }
            set
            {
                var deg = Utils.StringToDeg(value);
                UpdatePoint(deg, _point.Y);
            }
        }

        public string YText
        {
            get { return Utils.ToDegMinSecString(_point.Y); }
            set
            {
                var deg = Utils.StringToDeg(value);
                UpdatePoint(_point.X, deg);
            }
        }

        public string Id
        {
            get { return _point.Id; }
            set
            {
                _point.Id = value;
                NotifyPropertyChanged("Id");
            }
        }

        private void UpdatePoint(double x, double y)
        {
            var pt = new GeodesicMapPoint(Id, x, y);
            if (!pt.IsEqual2d(_point))
            {
                _point = pt;
                NotifyPropertyChanged();
            }
        }
    }

    public class GeodesicLineSegmentPresenter : GeodesicPresenter
    {

        public GeodesicLineSegmentPresenter()
        {
            this.StartPoint = new GeodesicPointPresenter();
            this.StartPoint.PropertyChanged += (s, e) =>
            {
                var spt = this.StartPoint.Point;
                if (!spt.IsEqual2d(Line.StartPoint))
                {
                    Line = GeodesicLineSegment.Create(spt, EndPoint.Point);
                    NotifyPropertyChanged(nameof(StartPoint));
                    NotifyLineChanged();
                }
            };

            this.EndPoint = new GeodesicPointPresenter();
            this.EndPoint.PropertyChanged += (s, e) =>
            {
                var ept = this.EndPoint.Point;
                if (!ept.IsEqual2d(Line.EndPoint))
                {
                    Line = GeodesicLineSegment.Create(StartPoint.Point, ept);
                    NotifyPropertyChanged(nameof(EndPoint));
                    NotifyLineChanged();
                }
            };
            Line = GeodesicLineSegment.Create(StartPoint.Point, EndPoint.Point);
        }

        private void NotifyLineChanged()
        {
            NotifyPropertyChanged(nameof(LengthText));
            NotifyPropertyChanged(nameof(StartAzimuthText));
            NotifyPropertyChanged(nameof(MidAzimuthText));
            NotifyPropertyChanged(nameof(EndAzimuthText));
        }

        public GeodesicPointPresenter StartPoint { get; private set; }
        public GeodesicPointPresenter EndPoint { get; private set; }

        public string LengthText { get { return Utils.DistToString(Line); } }

        public string StartAzimuthText { get { return Utils.ToDegMinSecString(Line.StartAzimuth); } }
        public string MidAzimuthText { get { return Utils.ToDegMinSecString(Line.MidAzimuth); } }
        public string EndAzimuthText { get { return Utils.ToDegMinSecString(Line.EndAzimuth); } }

        public GeodesicLineSegment Line { get; private set; } = GeodesicLineSegment.Create(new MapPoint(1, 2), new MapPoint(33, 44));
    }
}
