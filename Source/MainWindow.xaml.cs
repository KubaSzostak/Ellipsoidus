using Ellipsoidus.Properties;
using Ellipsoidus.Windows;
using Esri;
using Esri.ArcGISRuntime.Controls;
using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Layers;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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
using System.Windows.Media;
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
		private System.Timers.Timer hideInfoTimer = new System.Timers.Timer(9000.0);

		private Polygon BufferOutput = null;
		private GeodesicOffsetBuilder OffsetBuilder = null;
        private MedianLineBuilder MedianLineBuilder = null;


		private Symbols srcGeoSymb = Symbols.Blue3;
		private Symbols srcPrjSymb = Symbols.Blue2;

        private string ExeDir = null;
        private Settings Settings = Settings.Default;
        private LineLineIntersectionPanel lineLineIntersectionPanel;
        private LineLengthAzPanel lineLengthAzPanel;

		public MainWindow()
        {
            InitNumberFormat();
			this.InitializeComponent();
            

            Ellipsoidus.Presenter.MapView = this.MapView;
            Ellipsoidus.Presenter.ShowInfoBox = this.ShowInfoBox;
            Ellipsoidus.Presenter.HideInfoBox = this.HideInfoBox;

            ExeDir = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
            if (string.IsNullOrEmpty(Settings.LoadBaseLineFile))
                Settings.LoadBaseLineFile = Path.Combine(ExeDir, "SampleData", "de_base_line.txt");

			this.MapView.Map.SpatialReference = SpatialReference.Create(4326);

			this.MeasurementsLayer.AddLineLabelling(Symbols.Black2, "Value");
			this.SourceLineGeodesicLayer.AddPointLabelling(this.srcGeoSymb, "Id");

			this.OffsetResultsLayer.AddLineLabelling(Symbols.Magenta2, "Value");
            this.OffsetResultsLayer.AddPointLabelling(Symbols.Red1, "Id");

            this.CuttingLineLayer.AddPointLabelling(Symbols.Gray1, "Id");
            this.GeodesicAreaLayer.AddPointLabelling(Symbols.Orange1, "Id");

            this.MapView.Cursor = Presenter.MapCursor;

			this.hideInfoTimer.Elapsed += delegate(object s, ElapsedEventArgs e)
			{
				base.Dispatcher.Invoke<Visibility>(() => this.infoBox.Visibility = Visibility.Collapsed);
			};

			this.HideInfoBox();
			this.progressBox.Visibility = Visibility.Collapsed;
			this.progressBar.IsIndeterminate = true;


            lineLineIntersectionPanel = new LineLineIntersectionPanel(this.MapView, this.MeasurementsLayer);
            lineLengthAzPanel = new LineLengthAzPanel(this.MapView, this.MeasurementsLayer);
		}

        private void InitNumberFormat()
        {
            var clt = new CultureInfo(Thread.CurrentThread.CurrentCulture.LCID);
            clt.NumberFormat.NumberDecimalSeparator = ".";

            Thread.CurrentThread.CurrentCulture = clt;
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

		public async Task StartBuilder(Builder builder)
		{
            this.HideInfoBox();
            this.progressBar.IsIndeterminate = true; // start animation from beginning

			this.progressBox.Visibility = Visibility.Visible;
			this.statusInfo.Content = "Building " + builder.Title + "...";
            this.menuBar.IsEnabled = false;

            try
            {
			    await builder.BuildAsync();
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

            this.ShowInfoBox(builder.Title + " built in " + builder.BuildTime.TotalSeconds.ToString("0") + " sec.");
        }

		public static void DoEvents()
		{
            Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new Action(delegate { }));
		}



		private async void MyMapView_LayerLoaded(object sender, LayerLoadedEventArgs e)
		{
			if (e.LoadError != null)
			{
                if (e.Layer == this.BasemapLayer)
                {
                    await LoadCountriesShapefile();
                }
                else
                {
                    var errMsg = string.Format("Error while loading layer '{0}': \r\n - {1}", e.Layer.ID, e.LoadError.Message);
                    if (e.LoadError.InnerException != null)
                    {
                        errMsg += "\r\n - " + e.LoadError.InnerException.Message;
                    }
                    MessageBox.Show(errMsg, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
					Application.Current.Shutdown();
                }
			}
		}

        private async Task LoadCountriesShapefile()
        {
            var shpPath = Path.Combine(ExeDir, "ExportTemplate", "shp", "countries.shp");
            var shpData = await ShapefileTable.OpenAsync(shpPath);
            var shpLayer = new FeatureLayer(shpData) 
            { 
                ID="Countries",
                DisplayName="Countries",
                Opacity=0.3
            };
            this.MapView.Map.Layers.Insert(0, shpLayer);
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
							this.wgsCoordsBlock.Content = "ETRS89/WGS84: " + ConvertCoordinate.ToDegreesMinutesSeconds(mapPoint, 1);
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
            if (Ellipsoidus.Presenter.BaseLine1 == null)
			{
				this.ShowInfoBox("Add source line");
			}
			else
			{
                var paramsWnd = new OffsetParametersWindow();
				if (paramsWnd.ShowDialog(true))
				{
					this.AddBuffer(paramsWnd.Distance, paramsWnd.MaxDeviation);
				}
			}
		}

		private void offsetButton_Click(object sender, RoutedEventArgs e)
		{
            if (Ellipsoidus.Presenter.BaseLine1 == null)
			{
				this.ShowInfoBox("Add source line");
			}
			else
			{
                var paramsWnd = new OffsetParametersWindow();
				if (paramsWnd.ShowDialog(false))
				{
					this.AddOffset(paramsWnd.Distance);
				}
			}
        }

        private async void GenerateMedianLine_Click(object sender, RoutedEventArgs e)
        {
            if ((Presenter.BaseLine1 == null) || (Presenter.BaseLine2 == null))
            {
                this.ShowInfoBox("Add baselines first.");
                return;
            }

            this.OffsetCuttedLinesLayer.Graphics.Clear();
            this.OffsetResultsLayer.Graphics.Clear();
            this.OffsetSourceAuxiliaryLinesLayer.Graphics.Clear();
            this.DensifyLayer.Graphics.Clear();

            this.MedianLineBuilder = new MedianLineBuilder(Presenter.BaseLine1, Presenter.BaseLine2, Presenter.CuttingLine);
            await this.StartBuilder(this.MedianLineBuilder);    

            var symbols = Symbols.Gray1;
            foreach (var sax in this.MedianLineBuilder.ConstructionLines)
            {
                this.OffsetSourceAuxiliaryLinesLayer.Add(sax, symbols.Line);
                this.OffsetSourceAuxiliaryLinesLayer.Add(sax.MidPoint, symbols.Point);
            }

            

            symbols = Symbols.Red2;
            //this.OffsetResultsLayer.Add(this.MedianLineBuilder.MedianLine, symbols.Line);
            //this.OffsetResultsLayer.AddPoints(this.MedianLineBuilder.MedianLine.Vertices, symbols.Point);
        }

        private void clearToolsResultsButton_Click(object sender, RoutedEventArgs e)
		{
			this.ClearToolsResults();
		}



		private async void addLine_Click(object sender, RoutedEventArgs e)
		{
            var points = await Presenter.PickLineAsync();
			this.AddBaseLine1(points, "PickOnMap");
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
            var startPoint = await Presenter.PickPointAsync();
            var endPoint = await Presenter.PickPointAsync();

            var offsetParametersWindow = new OffsetParametersWindow();
			if (offsetParametersWindow.ShowDialog(true))
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
            var mapPoint = await Presenter.PickPointAsync();

            var graphic = new Graphic();
			graphic.Geometry = mapPoint;
			graphic.Symbol = gray.Point;

			this.MeasurementsLayer.Graphics.Add(graphic);


            var mapPoint2 = await Presenter.PickPointAsync();
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
            if (Ellipsoidus.Presenter.BaseLine1 == null)
			{
				this.ShowInfoBox("Add source line");
			}
			else
			{
                var point = await Presenter.PickPointAsync();
                var dist = Ellipsoidus.Presenter.BaseLine1.GeodesicDistTo(point);
				this.ShowInfoBox(Utils.RoundDist(dist));
			}
		}

        private void lineLineIntersection_Click(object sender, RoutedEventArgs e)
        {
            lineLineIntersectionPanel.ResetPresenter();
            lineLineIntersectionPanel.UpdateLayer();
            this.sideBar.Show(lineLineIntersectionPanel, "Line-line intersection");
        }

        private void lineLengthAz_Click(object sender, RoutedEventArgs e)
        {
            lineLengthAzPanel.ResetPresenter();
            lineLengthAzPanel.UpdateLayer();
            this.sideBar.Show(lineLengthAzPanel, "Line length and azimuth");
        }


        
        private void clearMeasurements_Click(object sender, RoutedEventArgs e)
		{
			this.MeasurementsLayer.Graphics.Clear();
            this.GeodesicAreaLayer.Graphics.Clear();
            Ellipsoidus.Presenter.GeodesicArea.Clear();

        }

		private void clearTests_Click(object sender, RoutedEventArgs e)
		{
			this.TestsLayer.Graphics.Clear();
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

		private void AddBaseLine1(List<GeodesicMapPoint> points, string origin)
		{
			this.ClearToolsResults();
			this.SourceLineGeodesicLayer.Graphics.Clear();
			this.SourceLineProjectedLayer.Graphics.Clear();
            this.DensifyLayer.Graphics.Clear();

            var geoLn = GeodesicPolyline.Create(points.Cast<MapPoint>().ToList());
            geoLn.UpdateOrigin(origin);
            this.AddProjectedSourceLine(geoLn);

            Presenter.BaseLine1 = geoLn;

            this.ClearCuttingLine();
			this.mnuGeodesicOffset.IsEnabled = true;
            this.mnuCuttingLine.IsEnabled = true;
            this.mnuLoadBaseLine2.IsEnabled = true;

            this.OffsetBuilder = null;
        }

        private void AddBaseLine2(List<GeodesicMapPoint> points, string origin)
        {
            var geoLn = GeodesicPolyline.Create(points.Cast<MapPoint>().ToList());
            geoLn.UpdateOrigin(origin);
            this.AddProjectedSourceLine(geoLn);

            Presenter.BaseLine2 = geoLn;
        }

        private void AddProjectedSourceLine(GeodesicPolyline geoLn)
        {
            var projectedLine = new Polyline(geoLn.Vertices);
			this.SourceLineProjectedLayer.Add(projectedLine, this.srcPrjSymb.DashLine);
			this.SourceLineProjectedLayer.AddRange(geoLn.Vertices, this.srcPrjSymb.Point);

            this.SourceLineGeodesicLayer.Add(geoLn, this.srcGeoSymb.Line);
			this.SourceLineGeodesicLayer.AddPoints(geoLn.Vertices, this.srcGeoSymb.Point);
        }

        private Polygon GetEsriBuffer(double dist, double precision)
        {
            if (Ellipsoidus.Presenter.BaseLine1 == null)
                return null;

            var srcLn = new Polyline(Ellipsoidus.Presenter.BaseLine1.Vertices);
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

        Graphic _errorPointGraphic = null;

		private async void AddOffset(double dist)
		{
            if (Ellipsoidus.Presenter.BaseLine1 != null)
			{
                
                this.OffsetCuttedLinesLayer.Graphics.Clear();
                this.OffsetResultsLayer.Graphics.Clear();
                this.OffsetSourceAuxiliaryLinesLayer.Graphics.Clear();
                this.DensifyLayer.Graphics.Clear();
                if (_errorPointGraphic!=null)
                {
                    SourceLineGeodesicLayer.Graphics.Remove(_errorPointGraphic);
                    _errorPointGraphic = null;
                }
                

                this.OffsetBuilder = new GeodesicOffsetBuilder(Ellipsoidus.Presenter.BaseLine1, dist, Ellipsoidus.Presenter.CuttingLine);
				await this.StartBuilder(this.OffsetBuilder);

                if (this.OffsetBuilder.SegmentErrorPoint != null)
                {
                    OffsetBuilder.SegmentErrorPoint.Id += " # ERROR";
                    _errorPointGraphic = SourceLineGeodesicLayer.Add(OffsetBuilder.SegmentErrorPoint, Symbols.Black3.Point);
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
        
        private List<GeodesicMapPoint> LoadPointsFromFileDialog()
        {
            var dlg = new OpenFileDialog();
            dlg.Filter = "Text files|*.txt;*.nxy";
            dlg.InitialDirectory = Path.GetDirectoryName(this.Settings.LoadBaseLineFile);
            //dlg.FileName = this.Settings.LoadBaseLineFile;

            if (dlg.ShowDialog() != true)
                return null;

            this.Settings.LoadBaseLineFile = dlg.FileName;

            return TextFile.LoadPoints(dlg.FileName); 
        }

        private bool SaveTextFileDialog(string text)
        {
            var dlg = new SaveFileDialog();
            dlg.Filter = "Text files|*.txt";
            dlg.InitialDirectory = Path.GetDirectoryName(this.Settings.LoadBaseLineFile);
            //dlg.FileName = this.Settings.LoadBaseLineFile;

            if (dlg.ShowDialog() != true)
                return false;

            System.IO.File.WriteAllText(dlg.FileName, text, Encoding.UTF8);
            this.ShowInfoBox("Saved to \r\n '" + dlg.FileName);
            return true;
        }

        private async void LoadBaseLine1FromFile_Click(object sender, RoutedEventArgs e)
        {
            var points = LoadPointsFromFileDialog();
            if (points == null)
                return;

            AddBaseLine1(points, "KeyedIn");
            await MapView.SetViewAsync(Ellipsoidus.Presenter.BaseLine1.Extent.Expand(2.0));
        }

        private async void LoadBaseLine2FromFile_Click(object sender, RoutedEventArgs e)
        {
            var points = LoadPointsFromFileDialog();
            if (points == null)
                return;

            AddBaseLine2(points, "KeyedIn");
            await MapView.SetViewAsync(Ellipsoidus.Presenter.BaseLine2.Extent.Expand(2.0));
        }

        private void BaseLineSave_Click(object sender, RoutedEventArgs e)
        {
            if (Ellipsoidus.Presenter.BaseLine1 == null)
			{
				this.ShowInfoBox("Add baseline");
                return;
			}
            
            var exportOpts = new ExportOptionsWindow(false, false);
            if (!exportOpts.ShowDialog(Path.GetDirectoryName(this.Settings.LoadBaseLineFile)))
                return;

            var fn = Path.Combine(exportOpts.FolderPath, Path.GetFileNameWithoutExtension(this.Settings.LoadBaseLineFile));

            ShapeFile.SaveLine(Ellipsoidus.Presenter.BaseLine1.Vertices, fn + ".shp");

            ShapeFile.SavePoints(Ellipsoidus.Presenter.BaseLine1.Vertices, fn + "_points.shp", -1);
            TextFile.SavePoints(Ellipsoidus.Presenter.BaseLine1.Vertices, fn + "_points.txt", -1);

            var geodesicPoints = ShapeFile.SaveLineDensify(Ellipsoidus.Presenter.BaseLine1.Lines, fn + "_geodesic.shp");
            TextFile.SavePoints(geodesicPoints, fn + "_geodesic.txt", -1);
            
            ShowInfoBox("Exported to " + fn + ".shp");
        }
        
        private void UpdateDensifyPoints(IEnumerable<MapPoint> points)
        {
            /*
             * For testing only
             */ 
            this.DensifyLayer.Dispatcher.Invoke(() =>
            {
                this.DensifyLayer.Graphics.Clear();

                var dln = new Polyline(points);
                var dpts = new Multipoint(points);

                this.DensifyLayer.Add(dln, Symbols.Black2.DashLine);
                this.DensifyLayer.Add(dpts, Symbols.Black2.Point);
            });
             /**/
        }

		private async void offsetButtonSave_Click(object sender, RoutedEventArgs e)
		{
			if (this.OffsetBuilder == null)
			{
				this.ShowInfoBox("Add base line and build offset");
                return;
			}

            var exportOpts = new ExportOptionsWindow(true, false);
            if (!exportOpts.ShowDialog(this.Settings.ExportOutputDir))
                return;

            this.Settings.ExportOutputDir = exportOpts.FolderPath;

            var esriBuffer = this.GetEsriBuffer(this.OffsetBuilder.BufferDist, exportOpts.MaxDeviation);
            var exportOffsetBuilder = new OffsetDataExportBuilder(this.OffsetBuilder, esriBuffer, exportOpts.FolderPath, exportOpts.MaxDeviation, exportOpts.FirstPointNo);
            await this.StartBuilder(exportOffsetBuilder);
            //ExportOffsetData(folderPath, paramsWnd.Precision);

            // MaxDeviation changed, so DensifyPoints changed to
            var densiyfyPts = this.OffsetBuilder.OffsetSegments.GetGeodesicDensifyPoints(exportOpts.MaxDeviation);
            UpdateDensifyPoints(densiyfyPts);
        }

        private async void addCuttingLine_Click(object sender, RoutedEventArgs e)
        {
            var points = await Presenter.PickLineAsync();
            this.AddCuttingLine(points, "PickOnMap");
        }

        private async void loadCuttingLineFromFile_Click(object sender, RoutedEventArgs e)
        {
            var points = LoadPointsFromFileDialog();
            if (points == null)
                return;

            AddCuttingLine(points, "KeyedIn");
            await MapView.SetViewAsync(Ellipsoidus.Presenter.CuttingLine.Extent.Expand(2.0));
        }

        private void AddCuttingLine(List<GeodesicMapPoint> points, string origin)
        {
            if (Ellipsoidus.Presenter.BaseLine1 == null)
                return;

            var mapPoints = points.Cast<MapPoint>().ToList();

            Ellipsoidus.Presenter.CuttingLine = GeodesicPolyline.Create(mapPoints);



            this.CuttingLineLayer.Graphics.Clear();
            foreach (var ln in Ellipsoidus.Presenter.CuttingLine.Lines)
            {
                ln.UpdateOrigin(origin);
            }

            //this.CuttingLineLayer.Add(CuttingPolygon, Symbols.Gray1.Fill);
            this.CuttingLineLayer.Add(Ellipsoidus.Presenter.CuttingLine, Symbols.Gray3.DotLine);
            this.CuttingLineLayer.AddPoints(points, Symbols.Gray1.Point);
        }

        private void ClearCuttingLine()
        {
            this.CuttingLineLayer.Graphics.Clear();
            Ellipsoidus.Presenter.CuttingLine = null;
        }

        private void clearCuttingLine_Click(object sender, RoutedEventArgs e)
        {
            ClearCuttingLine();
        }
        

        private async void distToBaseLineFile_Click(object sender, RoutedEventArgs e)
        {
            if (Ellipsoidus.Presenter.BaseLine1 == null)
            {
                this.ShowInfoBox("First add base line.");
                return;
            }
            var points = LoadPointsFromFileDialog();
            if (points == null)
                return;

            var outDir = Path.ChangeExtension(this.Settings.LoadBaseLineFile, null) + "_dist_raport" + Path.DirectorySeparatorChar;
            Directory.CreateDirectory(outDir);
            var fn = outDir + Path.GetFileName(this.Settings.LoadBaseLineFile);

            var distRaport = new DistToBaseLineRaportBuilder(points, Presenter.BaseLine1, Presenter.BaseLine2, fn);
            await this.StartBuilder(distRaport);
            this.ShowInfoBox("Saved to " + fn);
        }

        public static RenderTargetBitmap GetMapImage(MapView view)
        {
            Size size = new Size(view.ActualWidth, view.ActualHeight);
            if (size.IsEmpty)
                return null;

            RenderTargetBitmap result = new RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32);

            DrawingVisual drawingvisual = new DrawingVisual();
            using (DrawingContext context = drawingvisual.RenderOpen())
            {
                context.DrawRectangle(new VisualBrush(view), null, new Rect(new Point(), size));
                context.Close();
            }

            result.Render(drawingvisual);
            return result;
        }

        public void SaveMapToStream(Stream stm)
        {
            var src = GetMapImage(this.MapView);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(src));
            encoder.Save(stm);
        }

        /// <summary> 
        /// Copies a UI element to the clipboard as an image, and as text.
        /// </summary> 
        /// <param name="element">The element to copy.</param> 
        public static void CopyUIElementToClipboard(FrameworkElement element)
        {
            // Based on http://elegantcode.com/2010/12/13/wpfcopy-uielement-to-clipboard-as-multiple-formats/

            //data object to hold our different formats representing the element
            DataObject dataObject = new DataObject();
            //lets start with the text representation 
            //to make is easy we will just assume the object set as the DataContext has the ToString method overrideen and we use that as the text
            var text = element.ToString();
            if (element.DataContext != null)
                text = element.DataContext.ToString();
            dataObject.SetData(DataFormats.Text, text, true);

            //now lets do the image representation 
            double width = element.ActualWidth;
            double height = element.ActualHeight;
            RenderTargetBitmap bmpCopied = new RenderTargetBitmap((int)Math.Round(width), (int)Math.Round(height), 96, 96, PixelFormats.Default);
            DrawingVisual dv = new DrawingVisual();
            using (DrawingContext dc = dv.RenderOpen())
            {
                VisualBrush vb = new VisualBrush(element);
                dc.DrawRectangle(vb, null, new Rect(new Point(), new Size(width, height)));
            }
            bmpCopied.Render(dv);
            dataObject.SetData(DataFormats.Bitmap, bmpCopied, true);

            //now place our object in the clipboard 
            Clipboard.SetDataObject(dataObject, true);
        }

        private void copyToClipboard_Click(object sender, RoutedEventArgs e)
        {
            CopyUIElementToClipboard(this.MapView);
            ShowInfoBox("Map copied to clipboard.");
        }

        private void saveMapToFile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog();
            dlg.Filter = "PNG files|*.png";
            if (dlg.ShowDialog() != true)
                return;
            using (var stm = dlg.OpenFile())
            {
                SaveMapToStream(stm);
            }
        }



        private async void addAreaPointsCW_Click(object sender, RoutedEventArgs e)
        {
            var points = LoadPointsFromFileDialog();
            if (points == null)
                return;

            await AddGeodesicAreaPoints(points, Settings.LoadBaseLineFile, "KeyedIn");
        }

        private async void addAreaPointsACW_Click(object sender, RoutedEventArgs e)
        {
            var points = LoadPointsFromFileDialog();
            if (points == null)
                return;

            points.Reverse();
            await AddGeodesicAreaPoints(points, Settings.LoadBaseLineFile, "KeyedIn");

        }

        private void saveArea_Click(object sender, RoutedEventArgs e)
        {
            if (!Presenter.GeodesicArea.HasData)
            {
                ShowInfoBox("Nothing to save - add area border points.");
                return;
            }

            var fileText = Presenter.GeodesicArea.GetInfoText();

            fileText += "\r\n\r\n" + "Point coordinates source:";
            foreach (var fp in Presenter.GeodesicArea.SourceFilePath)
            {
                fileText += "\r\n" + fp;
            }

            fileText += "\r\n\r\n" + "Calculated by Ellipsoidus";

            SaveTextFileDialog(fileText);
        }

        private async Task AddGeodesicAreaPoints(List<GeodesicMapPoint> points, string srcFilePath, string origin)
        {

            Presenter.GeodesicArea.AddPoints(points, srcFilePath, "KeyedIn");

            this.GeodesicAreaLayer.Graphics.Clear();
            this.GeodesicAreaLayer.Add(Ellipsoidus.Presenter.GeodesicArea.Polygon, Symbols.Orange2.Fill);
            this.GeodesicAreaLayer.AddPoints(Presenter.GeodesicArea.Points, Symbols.Orange1.Point);

            await MapView.SetViewAsync(Ellipsoidus.Presenter.GeodesicArea.Polygon.Extent.Expand(2.0));


            this.ShowInfoBox(Presenter.GeodesicArea.GetInfoText());
        }
    }
}
