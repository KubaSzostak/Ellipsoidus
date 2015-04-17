using Ellipsoidus.Windows;
using Esri;
using Esri.ArcGISRuntime.Controls;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Layers;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Threading;

namespace Ellipsoidus
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
		private static Cursor MapCursor = Cursors.Hand;
		private Timer hideInfoTimer = new Timer(9000.0);

		private GeodesicPolyline SourceLine = null;
		private Polygon BufferOutput = null;
		private GeodesicOffsetBuilder OffsetBuilder = null;

		private Symbols srcGeoSymb = Symbols.Blue3;
		private Symbols srcPrjSymb = Symbols.Blue2;

		public MainWindow()
		{
			this.InitializeComponent();
			this.MapView.Map.SpatialReference = SpatialReference.Create(4326);

			this.MeasurementsLayer.AddLineLabelling(Symbols.Black2, "Value");
			this.SourceLineGeodesicLayer.AddPointLabelling(this.srcGeoSymb, "Id");
			this.OffsetResultsLayer.AddLineLabelling(Symbols.Magenta2, "Value");

			this.MapView.Cursor = MainWindow.MapCursor;

			this.hideInfoTimer.Elapsed += delegate(object s, ElapsedEventArgs e)
			{
				base.Dispatcher.Invoke<Visibility>(() => this.infoBox.Visibility = Visibility.Collapsed);
			};

			this.HideInfoBox();
			this.progressBox.Visibility = Visibility.Collapsed;
			this.progressBar.IsIndeterminate = true;
		}

		public void ShowInfoBox(string info)
		{
			this.hideInfoTimer.Stop();
			this.infoBox.Visibility = Visibility.Visible;
			this.infoTextBlock.Text = info;
			this.hideInfoTimer.Start();
		}

		public void HideInfoBox()
		{
			this.hideInfoTimer.Stop();
			this.infoBox.Visibility = Visibility.Collapsed;
		}

		public async Task StartProgress(Task task, string info)
		{
			this.progressBox.Visibility = Visibility.Visible;
			this.statusInfo.Content = info;
			await task;
			this.progressBox.Visibility = Visibility.Collapsed;
			this.statusInfo.Content = "";
		}

		public static void DoEvents()
		{
            Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new Action(delegate { }));
		}

		private void MyMapView_LayerLoaded(object sender, LayerLoadedEventArgs e)
		{
			if (e.LoadError != null)
			{
                var errMsg = string.Format("Error while loading layer '{0}': \r\n - {1}", e.Layer.ID, e.LoadError.Message);
				if (e.LoadError.InnerException != null)
				{
                    errMsg += "\r\n - " + e.LoadError.InnerException.Message;
				}

                if (e.Layer == this.BasemapLayer)
                {
                    errMsg += "\r\n - Check internet connection.";
                    errMsg += "\r\n\r\n" + "Basemap will be not displayed";
                    MessageBox.Show(errMsg, "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    MessageBox.Show(errMsg, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
					Application.Current.Shutdown();
                }
			}
		}

		private void MyMapView_MouseMove(object sender, MouseEventArgs e)
		{
			if (this.MapView.Extent != null)
			{
                var screenPoint = e.GetPosition(this.MapView);
				var mapPoint = this.MapView.ScreenToLocation(screenPoint);
				if (mapPoint != null)
				{
					mapPoint = (GeometryEngine.NormalizeCentralMeridian(mapPoint) as MapPoint);
					if (!double.IsNaN(mapPoint.X))
					{
						try
						{
							this.wgsCoordsBlock.Content = "WGS84: " + ConvertCoordinate.ToDegreesMinutesSeconds(mapPoint, 1);
							this.utmCoordsBlock.Content = "UTM: " + ConvertCoordinate.ToUtm(mapPoint, UtmConversionMode.UseNorthSouthLatitudeIndicators, true);
							this.wgsCoordsBlock.Visibility = Visibility.Visible;
							this.utmCoordsBlock.Visibility = Visibility.Visible;
						}
						catch (Exception)
						{
							this.wgsCoordsBlock.Visibility = Visibility.Hidden;
							this.utmCoordsBlock.Visibility = Visibility.Hidden;
						}
					}
				}
			}
		}
		private void bufferButton_Click(object sender, RoutedEventArgs e)
		{
			if (this.SourceLine == null)
			{
				this.ShowInfoBox("Add source line");
			}
			else
			{
                var paramsWnd = new OffsetParametersWindow();
				if (paramsWnd.ShowDialog(true))
				{
					this.AddBuffer(paramsWnd.Distance, paramsWnd.Precision);
				}
			}
		}

		private void offsetButton_Click(object sender, RoutedEventArgs e)
		{
			if (this.SourceLine == null)
			{
				this.ShowInfoBox("Add source line");
			}
			else
			{
                var paramsWnd = new OffsetParametersWindow();
				if (paramsWnd.ShowDialog(true))
				{
					this.AddOffset(paramsWnd.Distance, paramsWnd.Precision);
				}
			}
		}

		private void clearToolsResultsButton_Click(object sender, RoutedEventArgs e)
		{
			this.ClearToolsResults();
		}

		private async void addLine_Click(object sender, RoutedEventArgs e)
		{
			this.ShowInfoBox("Double click to end line");
			this.MapView.Cursor = Cursors.Cross;
            var geometry = await this.MapView.Editor.RequestShapeAsync(DrawShape.Polyline, Symbols.Gray1.DashLine, null);
			this.MapView.Cursor = MainWindow.MapCursor;

			e.Handled = true;
            var points = (geometry as Polyline).GetPoints();
            var list = new List<MapPointGraphic>();

			int num = 1;
			foreach (MapPoint pt in points)
			{
                var idPt = pt.Cast();
				idPt.Id = num++.ToString();
				list.Add(idPt);
			}
			this.AddSourceLine(list);
			this.HideInfoBox();
		}

		private async void loadFromFile_Click(object sender, RoutedEventArgs e)
		{
            var dlg = new OpenFileDialog();
            dlg.Filter = "Text files|*.txt;*.nxy";

            if (dlg.ShowDialog() != true)
                return;

            var points = Utils.LoadFromFile(dlg.FileName);
            AddSourceLine(points);
            await MapView.SetViewAsync(this.SourceLine.Extent.Expand(2.0));
		}

		private void websiteButton_Click(object sender, RoutedEventArgs e)
		{
            var url = "http://github.com/kubaszostak/ellipsoidus/";

            var senderCtl = sender as Control;
			if (senderCtl != null && senderCtl.Tag != null)
			{
				url = senderCtl.Tag.ToString();
			}
			Process.Start(url);
		}
		private async void deviationsGeodesicParallelLineButton_Click(object sender, RoutedEventArgs e)
		{
            var startPoint = await this.PickPoint();
            var endPoint = await this.PickPoint();

            var offsetParametersWindow = new OffsetParametersWindow();
			if (offsetParametersWindow.ShowDialog(false))
			{
				GeodesicParallelLineDeviationsTest test = new GeodesicParallelLineDeviationsTest(startPoint, endPoint, offsetParametersWindow.Distance);
				test.Layer = this.TestsLayer;
				test.RunTest();
				test.ShowRaport();
			}
		}
		private async void measureGeodesicDist_Click(object sender, RoutedEventArgs e)
		{
            var gray = Symbols.Gray1;
            var mapPoint = await this.PickPoint();

            var graphic = new Graphic();
			graphic.Geometry = mapPoint;
			graphic.Symbol = gray.Point;

			this.MeasurementsLayer.Graphics.Add(graphic);


            var mapPoint2 = await this.PickPoint();
            var geometry = new Polyline(new MapPoint[] { mapPoint, mapPoint2 });

            var graphic2 = new Graphic(geometry, gray.Line);
            var dist = GeometryEngine.GeodesicDistance(mapPoint, mapPoint2, LinearUnits.Meters);
            var text = Utils.RoundDist(dist);
			graphic2.Attributes["Value"] = text;

			this.MeasurementsLayer.Graphics.Add(graphic2);
			this.MeasurementsLayer.Graphics.Add(new Graphic(geometry, gray.Point));
			this.MeasurementsLayer.Graphics.Remove(graphic);

			this.ShowInfoBox(text);
		}
		private async void measureGeodesicDistToSrcLn_Click(object sender, RoutedEventArgs e)
		{
			if (this.SourceLine == null)
			{
				this.ShowInfoBox("Add source line");
			}
			else
			{
                var point = await this.PickPoint();
                var dist = this.SourceLine.GeodesicDistTo(point, 1.0);
				this.ShowInfoBox(Utils.RoundDist(dist));
			}
		}

		private void clearMeasurements_Click(object sender, RoutedEventArgs e)
		{
			this.MeasurementsLayer.Graphics.Clear();
		}

		private void clearTests_Click(object sender, RoutedEventArgs e)
		{
			this.TestsLayer.Graphics.Clear();
		}

		private async Task<MapPoint> PickPoint()
		{
			this.MapView.Cursor = Cursors.Cross;
			var res = await this.MapView.Editor.RequestPointAsync();
			this.MapView.Cursor = MainWindow.MapCursor;

			return res;
		}

		private void ClearToolsResults()
		{
			this.OffsetSourceAuxiliaryLinesLayer.Graphics.Clear();
			this.OffsetOutputAuxiliaryLinesLayers.Graphics.Clear();
			this.OffsetCuttedLinesLayer.Graphics.Clear();
			this.OffsetResultsLayer.Graphics.Clear();
			this.BufferLayer.Graphics.Clear();
			this.BufferOutput = null;
			this.OffsetBuilder = null;
		}

		private void AddSourceLine(IList<MapPointGraphic> points)
		{
			this.ClearToolsResults();
			this.SourceLineGeodesicLayer.Graphics.Clear();
			this.SourceLineProjectedLayer.Graphics.Clear();

            var projectedLine = new Polyline(points);
			this.SourceLine = GeodesicPolyline.Create(points.Cast<MapPoint>().ToList());

			this.SourceLineProjectedLayer.Add(projectedLine, this.srcPrjSymb.DashLine);
			this.SourceLineProjectedLayer.AddRange(points, this.srcPrjSymb.Point);
			this.SourceLineGeodesicLayer.Add(this.SourceLine, this.srcGeoSymb.Line);
			this.SourceLineGeodesicLayer.AddPoints(points, this.srcGeoSymb.Point);

			this.offsetButton.IsEnabled = true;
			this.bufferButton.IsEnabled = true;
		}

		private void AddBuffer(double dist, double precision)
		{
			if (this.SourceLine != null)
			{
                var srcLn = new Polyline(this.SourceLine.Vertices);
				this.BufferOutput = GeometryEngine.GeodesicBuffer(srcLn, dist, LinearUnits.Meters, precision, GeodeticCurveType.Geodesic) as Polygon;

                var points = this.BufferOutput.GetPoints().ToList();
                var symb = Symbols.Orange2;

				this.BufferLayer.Add(this.BufferOutput, symb.Fill);
				this.BufferLayer.AddRange(points, symb.Point);
				this.ShowInfoBox("Vertex count: " + points.Count.ToString());
			}
		}

		private async void AddOffset(double dist, double precision)
		{
			if (this.SourceLine != null)
			{
				this.OffsetBuilder = new GeodesicOffsetBuilder(this.SourceLine, dist, precision);
				await this.StartProgress(this.OffsetBuilder.BuildAsync(), "Building offset...");

                var symbols = Symbols.Gray1;
				foreach (GeodesicLine sax in this.OffsetBuilder.SourceAuxiliaryLines)
				{
					this.OffsetSourceAuxiliaryLinesLayer.Add(sax, symbols.Line);
				}

				symbols = Symbols.Magenta1;
				foreach (Polyline carc in this.OffsetBuilder.CuttedArcs)
				{
                    var pts = carc.GetPoints().ToList<MapPoint>();
					this.OffsetCuttedLinesLayer.Add(carc, symbols.DashLine);
					this.OffsetCuttedLinesLayer.Add(pts.First<MapPoint>(), symbols.X);
					this.OffsetCuttedLinesLayer.Add(pts.Last<MapPoint>(), symbols.X);
				}

				symbols = Symbols.Red1;
				foreach (Polyline cln in this.OffsetBuilder.CuttedLines)
				{
                    var pts2 = cln.GetPoints();
					this.OffsetCuttedLinesLayer.Add(cln, symbols.DashLine);
					this.OffsetCuttedLinesLayer.Add(pts2.First<MapPoint>(), symbols.X);
					this.OffsetCuttedLinesLayer.Add(pts2.Last<MapPoint>(), symbols.X);
				}

				symbols = Symbols.Magenta2;
				foreach (GeodesicArc arc in this.OffsetBuilder.OffsetArcs)
				{
                    var gr = this.OffsetResultsLayer.Add(arc, symbols.Line);
					gr.Attributes["Value"] = arc.Center.Id;
					this.OffsetResultsLayer.Add(arc.FirstVertex, symbols.Point);
					this.OffsetResultsLayer.Add(arc.LastVertex, symbols.Point);
				}

				symbols = Symbols.Red2;
				foreach (GeodesicPolyline ln in this.OffsetBuilder.OffsetLines)
				{
					this.OffsetResultsLayer.Add(ln, symbols.Line);
					this.OffsetResultsLayer.Add(ln.FirstVertex, symbols.Point);
					this.OffsetResultsLayer.Add(ln.LastVertex, symbols.Point);
				}
			}
		}

		private string GetFilePathDlg(string fileName)
		{
            var dlg = new SaveFileDialog();
            dlg.AddExtension = false;
			dlg.FileName = fileName;
			dlg.Filter = "File|*.shp;*.txt;*.json";

			if (dlg.ShowDialog() != true)
                return null;

			return Path.ChangeExtension(dlg.FileName, null);
		}

		private void offsetButtonSave_Click(object sender, RoutedEventArgs e)
		{
			if (this.OffsetBuilder == null)
			{
				this.ShowInfoBox("Add source line and build offset");
			}
			else
			{
				string filePath = this.GetFilePathDlg("offset");
				if (!string.IsNullOrEmpty(filePath))
				{
					Utils.SaveToFile(filePath + "-calculated.txt", this.OffsetBuilder.GetCalculatedPoints(), "; Ellipsoidus - Offset coordinates (calculated)");
					Utils.SaveToFile(filePath + "-densify.txt", this.OffsetBuilder.GetDensifyPoints(), "; Ellipsoidus - Offset coordinates (all)");
					Utils.SaveToFile(filePath + "-src-ln.txt", this.OffsetBuilder.SourceLine.DensifyPoints, "; Ellipsoidus - Source line geodesic coordinates");
				}
			}
		}

		private void bufferButtonSave_Click(object sender, RoutedEventArgs e)
		{
			if (this.BufferOutput == null)
			{
				this.ShowInfoBox("Add source line and build buffer");
			}
			else
			{
				string filePath = this.GetFilePathDlg("buffer");
				if (!string.IsNullOrEmpty(filePath))
				{
					Utils.SaveToFile(filePath + ".txt", this.BufferOutput.GetPoints(), "; Ellipsoidus - ESRI Buffer coordinates");
                    var jsonText = this.BufferOutput.ToJson();
					File.WriteAllText(filePath + ".json", jsonText, Encoding.UTF8);
				}
			}
		}
    }
}
