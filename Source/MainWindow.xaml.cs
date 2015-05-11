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
using System.Threading;
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
		private System.Timers.Timer hideInfoTimer = new System.Timers.Timer(9000.0);

		private GeodesicPolyline BaseLine = null;
		private Polygon BufferOutput = null;
		private GeodesicOffsetBuilder OffsetBuilder = null;

        private GeodesicPolyline CuttingLine;

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
            this.OffsetResultsLayer.AddPointLabelling(Symbols.Red1, "Id");

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
            this.menuBar.IsEnabled = false;

            try
            {
			    await task;
            }
            catch (Exception ex)
            {
                var msg = ex.Message;
                if (ex.InnerException != null)
                    msg += "\r\n" + ex.InnerException.Message;
                MessageBox.Show(msg, "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
            }
			this.progressBox.Visibility = Visibility.Collapsed;
			this.statusInfo.Content = "";
            this.menuBar.IsEnabled = true;
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
                    this.ShowInfoBox(errMsg);
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
			if (this.BaseLine == null)
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
			if (this.BaseLine == null)
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


        public async Task<List<GeodesicMapPoint>> PickLineAsync()
        {
            this.ShowInfoBox("Double click to end line");
            this.MapView.Cursor = Cursors.Cross;
            var geometry = await this.MapView.Editor.RequestShapeAsync(DrawShape.Polyline, Symbols.Gray1.DashLine, null);
            this.MapView.Cursor = MainWindow.MapCursor;

            var points = (geometry as Polyline).GetPoints();
            var list = new List<GeodesicMapPoint>();

            int num = 1;
            foreach (var pt in points)
            {
                var idPt = pt.Cast();
                idPt.UpdateOrigin("PickOnMap");
                idPt.Id = num++.ToString();
                list.Add(idPt);
            }

            this.HideInfoBox();
            return list;
        }

		private async void addLine_Click(object sender, RoutedEventArgs e)
		{
            var points = await PickLineAsync();
			this.AddBaseLine(points, "PickOnMap");
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
			if (this.BaseLine == null)
			{
				this.ShowInfoBox("Add source line");
			}
			else
			{
                var point = await this.PickPoint();
                var dist = this.BaseLine.GeodesicDistTo(point);
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

		private void AddBaseLine(List<GeodesicMapPoint> points, string origin)
		{
			this.ClearToolsResults();
			this.SourceLineGeodesicLayer.Graphics.Clear();
			this.SourceLineProjectedLayer.Graphics.Clear();
            this.DensifyLayer.Graphics.Clear();

            var projectedLine = new Polyline(points);
            this.BaseLine = GeodesicPolyline.Create(points.Cast<MapPoint>().ToList());
            foreach (var ln in BaseLine.Lines)
            {
                ln.UpdateOrigin(origin);
            }

			this.SourceLineProjectedLayer.Add(projectedLine, this.srcPrjSymb.DashLine);
			this.SourceLineProjectedLayer.AddRange(points, this.srcPrjSymb.Point);
			this.SourceLineGeodesicLayer.Add(this.BaseLine, this.srcGeoSymb.Line);
			this.SourceLineGeodesicLayer.AddPoints(points, this.srcGeoSymb.Point);

            this.ClearCuttingLine();
			this.mnuGeodesicOffset.IsEnabled = true;
            this.mnuCuttingLine.IsEnabled = true;

            this.OffsetBuilder = null;
		}

        private Polygon GetEsriBuffer(double dist, double precision)
        {
            if (this.BaseLine == null)
                return null;

            var srcLn = new Polyline(this.BaseLine.Vertices);
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
			if (this.BaseLine != null)
			{
                
                this.OffsetCuttedLinesLayer.Graphics.Clear();
                this.OffsetResultsLayer.Graphics.Clear();
                this.OffsetSourceAuxiliaryLinesLayer.Graphics.Clear();

				this.OffsetBuilder = new GeodesicOffsetBuilder(this.BaseLine, dist, this.CuttingLine);
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

		private string GetFolderPathDlg()
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

        private List<GeodesicMapPoint> LoadPointsFromFileDialog()
        {
            var dlg = new OpenFileDialog();
            dlg.Filter = "Text files|*.txt;*.nxy";
            dlg.InitialDirectory = Path.GetDirectoryName(this.Settings.LoadBaseLineFile);
            //dlg.FileName = this.Settings.LoadBaseLineFile;

            if (dlg.ShowDialog() != true)
                return null;

            this.Settings.LoadBaseLineFile = dlg.FileName;

            return Utils.LoadFromFile(dlg.FileName); 
        }

        private async void loadFromFile_Click(object sender, RoutedEventArgs e)
        {
            var points = LoadPointsFromFileDialog();
            if (points == null)
                return;

            AddBaseLine(points, "KeyedIn");
            await MapView.SetViewAsync(this.BaseLine.Extent.Expand(2.0));
        }

        private void baseLineSave_Click(object sender, RoutedEventArgs e)
        {
			if (this.BaseLine == null)
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

            ShapeFile.SaveLine(this.BaseLine.Vertices, fn + ".shp");

            ShapeFile.SavePoints(this.BaseLine.Vertices, fn + "-points.shp");
            Utils.SaveToFile(this.BaseLine.Vertices, fn + "-points.txt", 0.1);

            ShapeFile.SaveLineDensify(this.BaseLine.Lines, fn + "-geodesic.shp");


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
        
        private void UpdateDensifyPoints(IEnumerable<MapPoint> points)
        {
            this.DensifyLayer.Dispatcher.Invoke(() =>
            {
                this.DensifyLayer.Graphics.Clear();

                var dln = new Polyline(points);
                var dpts = new Multipoint(points);

                this.DensifyLayer.Add(dln, Symbols.Black2.DashLine);
                this.DensifyLayer.Add(dpts, Symbols.Black2.Point);
            });
        }

        private void ExportOffsetData(string destDir, double maxDev)
        {
            var densiyfyPts = this.OffsetBuilder.OffsetSegments.GetGeodesicDensifyPoints(maxDev);
            UpdateDensifyPoints(densiyfyPts);

            var shpDir = Path.Combine(destDir, "shp") + Path.DirectorySeparatorChar;
            Directory.CreateDirectory(shpDir);

            var txtDir = Path.Combine(destDir, "txt") + Path.DirectorySeparatorChar;
            Directory.CreateDirectory(txtDir);

            var points = ShapeFile.SaveLineCombo(this.OffsetBuilder.OffsetSegments, shpDir + "offset.shp", maxDev);
            Utils.SaveToFile(points, txtDir + "offset-points.txt", maxDev);
            Utils.SaveToFile(this.OffsetBuilder.OffsetSegments.GetVertices(), txtDir + "offset-vertices.txt", maxDev);

            ShapeFile.SaveLineCombo(this.OffsetBuilder.ReferenceLine.Lines, shpDir + "base-line.shp", maxDev);
            Utils.SaveToFile(this.BaseLine.Vertices, txtDir + "base-line.txt", maxDev);

            var esriBuff = GetEsriBuffer(this.OffsetBuilder.BufferDist, maxDev);
            ShapeFile.SaveEsriBuff(esriBuff.GetPoints().ToList(), shpDir + "esri-buff.shp", maxDev);


            var srcMxd = Path.Combine(ExeDir, "ExportTemplate", "ellipsoidus.mxd");
            var destMxd = Path.Combine(destDir, "ellipsoidus.mxd");
            if (!File.Exists(destMxd))
                File.Copy(srcMxd, destMxd);

            CopyWordShp(shpDir);
        }

        public Task ExportOffsetDataAsync(string destDir, double maxDev)
        {
            var action = new Action(() => { ExportOffsetData(destDir, maxDev); } );
            return Task.Run(action, CancellationToken.None);
        }

		private async void offsetButtonSave_Click(object sender, RoutedEventArgs e)
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

			string folderPath = this.GetFolderPathDlg();
            if (string.IsNullOrEmpty(folderPath))
                return;

            await  this.StartProgress(ExportOffsetDataAsync(folderPath, paramsWnd.Precision), "Exporting offset data...");
            //ExportOffsetData(folderPath, paramsWnd.Precision);
            ShowInfoBox("Exported.");
		}

        private async void addCuttingLine_Click(object sender, RoutedEventArgs e)
        {
            var points = await PickLineAsync();
            this.AddCuttingLine(points, "PickOnMap");
        }

        private async void loadCuttingLineFromFile_Click(object sender, RoutedEventArgs e)
        {
            var points = LoadPointsFromFileDialog();
            if (points == null)
                return;

            AddCuttingLine(points, "KeyedIn");
            await MapView.SetViewAsync(this.CuttingLine.Extent.Expand(2.0));
        }

        private void AddCuttingLine(List<GeodesicMapPoint> points, string origin)
        {
            if (this.BaseLine == null)
                return;

            var mapPoints = points.Cast<MapPoint>().ToList();

            this.CuttingLine = GeodesicPolyline.Create(mapPoints);



            this.CuttingLineLayer.Graphics.Clear();
            foreach (var ln in CuttingLine.Lines)
            {
                ln.UpdateOrigin(origin);
            }

            //this.CuttingLineLayer.Add(CuttingPolygon, Symbols.Gray1.Fill);
            this.CuttingLineLayer.Add(CuttingLine, Symbols.Gray3.DotLine);
            this.CuttingLineLayer.AddPoints(points, Symbols.Gray1.Point);
        }

        private void ClearCuttingLine()
        {
            this.CuttingLineLayer.Graphics.Clear();
            this.CuttingLine = null;
        }

        private void clearCuttingLine_Click(object sender, RoutedEventArgs e)
        {
            ClearCuttingLine();
        }

        private void GenerateDistToBaseLineRaport(IEnumerable<GeodesicMapPoint> points, string fileName)
        {
            var devList = new List<double>();
            double devDist = 22224.0;

            var rap = new RaportText();
            var fnShp = Path.ChangeExtension(fileName, null);

            using (var shp = ShapeFile.NewLineShapefile())
            {
                foreach (var pt in points)
                {

                    var near = BaseLine.NearestCoordinate(pt);
                    var gln = GeodesicLineSegment.Create(pt, near.Point);
                    var dev = near.Distance - devDist;

                    rap.AddLineInfo(gln);
                    rap.Add("Deviation from "+devDist.ToString()+":");
                    var lnDevText = dev.ToString("0.000").PadLeft(15);
                    rap.Add(lnDevText);

                    devList.Add(dev);

                    rap.AddLn();


                    shp.AddLine(pt, near.Point, dev, near.Distance);
                }

                shp.SaveAs(fnShp+".shp", true);
            }

            rap.AddLn();
            rap.Add("   # SUMMARY # ");
            rap.Add("Max deviation: " + devList.Max().ToString("0.000"));
            rap.Add("Min deviation: " + devList.Min().ToString("0.000"));

            ShapeFile.SavePoints(points, fnShp + "-points.shp");

            rap.SaveToFile(fileName);


        }
        
        private Task GenerateDistToBaseLineRaportAsync(IEnumerable<GeodesicMapPoint> points, string fileName)
        {
            var action = new Action(() => { GenerateDistToBaseLineRaport(points, fileName); });
            return Task.Run(action, CancellationToken.None);
        }

        private async void distToBaseLineFile_Click(object sender, RoutedEventArgs e)
        {
            if (this.BaseLine == null)
            {
                this.ShowInfoBox("First add base line.");
                return;
            }
            var points = LoadPointsFromFileDialog();
            if (points == null)
                return;

            var fn = Path.ChangeExtension(this.Settings.LoadBaseLineFile, null) + "-dist-raport.txt";
            await this.StartProgress(GenerateDistToBaseLineRaportAsync(points, fn), "Calculating...");
            this.ShowInfoBox("Saved to " + fn);
        }
    }
}
