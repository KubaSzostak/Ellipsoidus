using Ellipsoidus.Properties;
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

        private string ExeDir = null;
        private Settings Settings = Settings.Default;

		public MainWindow()
		{
			this.InitializeComponent();
            ExeDir = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
            if (string.IsNullOrEmpty(Settings.LoadBaseLineFile))
                Settings.LoadBaseLineFile = Path.Combine(ExeDir, "SampleData", "de-base-line.txt");

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

        private void Window_Closed(object sender, EventArgs e)
        {
            this.Settings.Save();
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
            this.progressBar.IsIndeterminate = false; // allow starting animation from beginning
		}

		public async Task StartProgress(Task task, string info)
		{
            this.progressBar.IsIndeterminate = true; // start animation from beginning

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
				if (paramsWnd.ShowDialog(true, true))
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
				if (paramsWnd.ShowDialog(false, true))
				{
					this.AddOffset(paramsWnd.Distance);
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
            var list = new List<GeodesicMapPoint>();

			int num = 1;
			foreach (var pt in points)
			{
                var idPt = pt.Cast();
				idPt.Id = num++.ToString();
				list.Add(idPt);
			}
			this.AddSourceLine(list, "PickOnMap");
			this.HideInfoBox();
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
			if (offsetParametersWindow.ShowDialog(false, true))
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
                var dist = this.SourceLine.GeodesicDistTo(point);
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

		private void AddSourceLine(IList<GeodesicMapPoint> points, string origin)
		{
			this.ClearToolsResults();
			this.SourceLineGeodesicLayer.Graphics.Clear();
			this.SourceLineProjectedLayer.Graphics.Clear();

            var projectedLine = new Polyline(points);
            this.SourceLine = GeodesicPolyline.Create(points.Cast<MapPoint>().ToList());
            foreach (var ln in SourceLine.Lines)
            {
                ln.UpdateOrigin(origin);
            }

			this.SourceLineProjectedLayer.Add(projectedLine, this.srcPrjSymb.DashLine);
			this.SourceLineProjectedLayer.AddRange(points, this.srcPrjSymb.Point);
			this.SourceLineGeodesicLayer.Add(this.SourceLine, this.srcGeoSymb.Line);
			this.SourceLineGeodesicLayer.AddPoints(points, this.srcGeoSymb.Point);

			this.offsetButton.IsEnabled = true;
			this.bufferButton.IsEnabled = true;

            this.OffsetBuilder = null;
		}

        private Polygon GetEsriBuffer(double dist, double precision)
        {
            if (this.SourceLine == null)
                return null;

            var srcLn = new Polyline(this.SourceLine.Vertices);
            return GeometryEngine.GeodesicBuffer(srcLn, dist, LinearUnits.Meters, precision, GeodeticCurveType.Geodesic) as Polygon;
        }

		private void AddBuffer(double dist, double precision)
		{
            this.BufferOutput = GetEsriBuffer(dist, precision);
            if (this.BufferOutput != null)
			{
                var points = this.BufferOutput.GetPoints().ToList();
                var symb = Symbols.Orange2;

				this.BufferLayer.Add(this.BufferOutput, symb.Fill);
				this.BufferLayer.AddRange(points, symb.Point);
				this.ShowInfoBox("Vertex count: " + points.Count.ToString());
			}
		}

		private async void AddOffset(double dist)
		{
			if (this.SourceLine != null)
			{
				this.OffsetBuilder = new GeodesicOffsetBuilder(this.SourceLine, dist);
				await this.StartProgress(this.OffsetBuilder.BuildAsync(), "Building offset...");
                this.ShowInfoBox("Offset built in " + this.OffsetBuilder.BuildTime.TotalSeconds.ToString("0") + " sec.");

                if (this.OffsetBuilder.SegmentErrorPoint != null)
                {
                    OffsetBuilder.SegmentErrorPoint.Id += " # ERROR";
                    SourceLineGeodesicLayer.Add(OffsetBuilder.SegmentErrorPoint, Symbols.Black3.Point);
                    MessageBox.Show("Segment builder error! Balck point added at error location. " + OffsetBuilder.SegmentErrorPoint.Id);
                }

                var symbols = Symbols.Gray1;
				foreach (var sax in this.OffsetBuilder.SourceAuxiliaryLines)
				{
					this.OffsetSourceAuxiliaryLinesLayer.Add(sax, symbols.Line);
				}

				symbols = Symbols.Magenta1;
				foreach (var carc in this.OffsetBuilder.CuttedArcs)
				{
                    var pts = carc.GetPoints().ToList();
					this.OffsetCuttedLinesLayer.Add(carc, symbols.DashLine);
					this.OffsetCuttedLinesLayer.Add(pts.First<MapPoint>(), symbols.X);
					this.OffsetCuttedLinesLayer.Add(pts.Last<MapPoint>(), symbols.X);
				}

				symbols = Symbols.Red1;
				foreach (var cln in this.OffsetBuilder.CuttedLines)
				{
                    var pts2 = cln.GetPoints();
					this.OffsetCuttedLinesLayer.Add(cln, symbols.DashLine);
					this.OffsetCuttedLinesLayer.Add(pts2.First<MapPoint>(), symbols.X);
					this.OffsetCuttedLinesLayer.Add(pts2.Last<MapPoint>(), symbols.X);
				}

				symbols = Symbols.Magenta2;
				foreach (var arc in this.OffsetBuilder.OffsetArcs)
				{
                    var gr = this.OffsetResultsLayer.Add(arc, symbols.Line);
					gr.Attributes["Value"] = arc.Center.Id;
					this.OffsetResultsLayer.Add(arc.StartPoint, symbols.Point);
					this.OffsetResultsLayer.Add(arc.EndPoint, symbols.Point);
				}

				symbols = Symbols.Red2;
				foreach (var ln in this.OffsetBuilder.OffsetLines)
				{
					this.OffsetResultsLayer.Add(ln, symbols.Line);
					this.OffsetResultsLayer.Add(ln.StartPoint, symbols.Point);
					this.OffsetResultsLayer.Add(ln.EndPoint, symbols.Point);
				}
			}
		}

		private string GetFilePathDlg()
		{
            
            var dlg = new System.Windows.Forms.FolderBrowserDialog();
            dlg.Description = "Select Shapefiles + MXD output folder";
            dlg.SelectedPath = this.Settings.ExportOutputDir; 

            var result = dlg.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                this.Settings.ExportOutputDir = dlg.SelectedPath;
                return dlg.SelectedPath;
            }
            return null;

		}

        private async void loadFromFile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog();
            dlg.Filter = "Text files|*.txt;*.nxy";
            dlg.InitialDirectory = Path.GetDirectoryName(this.Settings.LoadBaseLineFile);
            //dlg.FileName = this.Settings.LoadBaseLineFile;

            if (dlg.ShowDialog() != true)
                return;

            this.Settings.LoadBaseLineFile = dlg.FileName;

            var points = Utils.LoadFromFile(dlg.FileName);
            AddSourceLine(points, "KeyedIn");
            await MapView.SetViewAsync(this.SourceLine.Extent.Expand(2.0));
        }

        private void baseLineSave_Click(object sender, RoutedEventArgs e)
        {
			if (this.SourceLine == null)
			{
				this.ShowInfoBox("Add base line");
                return;
			}

            var dlg = new SaveFileDialog();
            dlg.Filter = "Shapefiles|*.shp";
            dlg.AddExtension = true;
            dlg.InitialDirectory = Path.GetDirectoryName(this.Settings.LoadBaseLineFile);
            dlg.FileName = Path.GetFileNameWithoutExtension(this.Settings.LoadBaseLineFile) + ".shp";

            if (dlg.ShowDialog() != true)
                return;

            this.Settings.LoadBaseLineFile = dlg.FileName;
            var fn = Path.ChangeExtension(dlg.FileName, null);

            Shapefile.SaveLine(this.SourceLine.Vertices, fn + ".shp");
            Shapefile.SaveLineDensify(this.SourceLine.Lines, fn + "-geodesic.shp");
            Shapefile.SavePoints(this.SourceLine.Vertices, fn + "-points.shp");

            ShowInfoBox("Exported.");
        }

        private void CopyWordShp(string destDir)
        {
            try
            {
                var srcFile = Path.Combine(ExeDir, "ExportTemplate", "shp", "world");
                var destFile = Path.Combine(destDir, "world");

                if (File.Exists(destFile + ".shp"))
                    return;

                File.Copy(srcFile + ".dbf", destFile + ".dbf");
                File.Copy(srcFile + ".prj", destFile + ".prj");
                File.Copy(srcFile + ".shp", destFile + ".shp");
                File.Copy(srcFile + ".shpx", destFile + ".shpx");
                File.Copy(srcFile + ".shx", destFile + ".shx");
            }
            catch { }
        }

        private void ExportOffsetData(string destDir, double maxDev)
        {

            var shpDir = Path.Combine(destDir, "shp") + Path.DirectorySeparatorChar;
            Directory.CreateDirectory(shpDir);

            Shapefile.SaveLineCombo(this.OffsetBuilder.OffsetSegments, shpDir + "offset.shp", maxDev);
            Shapefile.SaveLineCombo(this.OffsetBuilder.SourceLine.Lines, shpDir + "base-line.shp", maxDev);

            var esriBuff = GetEsriBuffer(this.OffsetBuilder.BufferDist, maxDev);
            Shapefile.SaveEsriBuff(esriBuff.GetPoints().ToList(), shpDir + "esri-buff.shp", maxDev);

            var srcMxd = Path.Combine(ExeDir, "ExportTemplate", "ellipsoidus.mxd");
            var destMxd = Path.Combine(destDir, "ellipsoidus.mxd");
            if (!File.Exists(destMxd))
                File.Copy(srcMxd, destMxd);

            CopyWordShp(shpDir);
        }

		private void offsetButtonSave_Click(object sender, RoutedEventArgs e)
		{
			if (this.OffsetBuilder == null)
			{
				this.ShowInfoBox("Add base line and build offset");
                return;
			}

            // TODO: make one export dialog windw
            var paramsWnd = new OffsetParametersWindow();
			if (!paramsWnd.ShowDialog(true, false))
                return;

			string folderPath = this.GetFilePathDlg();
            if (string.IsNullOrEmpty(folderPath))
                return;

            ExportOffsetData(folderPath, paramsWnd.Precision);
            ShowInfoBox("Exported.");
		}
    }
}
