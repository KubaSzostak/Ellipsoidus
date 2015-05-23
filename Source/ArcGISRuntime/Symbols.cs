using Esri.ArcGISRuntime.Layers;
using Esri.ArcGISRuntime.Symbology;

namespace Esri
{
    public class Symbols
    {
        public static Symbols Black1 = new Symbols(System.Windows.Media.Colors.Black, 1.0);
        public static Symbols Black2 = new Symbols(System.Windows.Media.Colors.Black, 2.0);
        public static Symbols Black3 = new Symbols(System.Windows.Media.Colors.Black, 3.0);

        public static Symbols Gray1 = new Symbols(System.Windows.Media.Colors.Gray, 1.0);
        public static Symbols Gray2 = new Symbols(System.Windows.Media.Colors.Gray, 2.0);
        public static Symbols Gray3 = new Symbols(System.Windows.Media.Colors.Gray, 3.0);

        public static Symbols Blue1 = new Symbols(System.Windows.Media.Colors.Blue, 1.0);
        public static Symbols Blue2 = new Symbols(System.Windows.Media.Colors.Blue, 2.0);
        public static Symbols Blue3 = new Symbols(System.Windows.Media.Colors.Blue, 3.0);

        public static Symbols Red1 = new Symbols(System.Windows.Media.Colors.Red, 1.0);
        public static Symbols Red2 = new Symbols(System.Windows.Media.Colors.Red, 2.0);
        public static Symbols Red3 = new Symbols(System.Windows.Media.Colors.Red, 3.0);

        public static Symbols Magenta1 = new Symbols(System.Windows.Media.Colors.Magenta, 1.0);
        public static Symbols Magenta2 = new Symbols(System.Windows.Media.Colors.Magenta, 2.0);
        public static Symbols Magenta3 = new Symbols(System.Windows.Media.Colors.Magenta, 3.0);

        public static Symbols Maroon1 = new Symbols(System.Windows.Media.Colors.Maroon, 1.0);
        public static Symbols Maroon2 = new Symbols(System.Windows.Media.Colors.Maroon, 2.0);
        public static Symbols Maroon3 = new Symbols(System.Windows.Media.Colors.Maroon, 3.0);

        public static Symbols Orange1 = new Symbols(System.Windows.Media.Colors.Orange, 1.0);
        public static Symbols Orange2 = new Symbols(System.Windows.Media.Colors.Orange, 2.0);
        public static Symbols Orange3 = new Symbols(System.Windows.Media.Colors.Orange, 3.0);

        public static Symbols Green1 = new Symbols(System.Windows.Media.Colors.Green, 1.0);
        public static Symbols Green2 = new Symbols(System.Windows.Media.Colors.Green, 2.0);
        public static Symbols Green3 = new Symbols(System.Windows.Media.Colors.Green, 3.0);

        public SimpleMarkerSymbol Point;
        public SimpleMarkerSymbol Cross;
        public SimpleMarkerSymbol Diamond;
        public SimpleMarkerSymbol Square;
        public SimpleMarkerSymbol Triangle;
        public SimpleMarkerSymbol X;
        public SimpleLineSymbol Line;
        public SimpleLineSymbol DotLine;
        public SimpleLineSymbol DashLine;
        public SimpleFillSymbol Fill;
        public TextSymbol Text;

        public Symbols(System.Windows.Media.Color color, double width)
        {
            double ptSize = width * 4.0;
            if (ptSize < 6.0)
                ptSize = 6.0;

            this.Point = new SimpleMarkerSymbol { Color = color, Size = ptSize, Style = SimpleMarkerStyle.Circle };
            this.Cross = new SimpleMarkerSymbol { Color = color, Size = ptSize, Style = SimpleMarkerStyle.Cross };
            this.Diamond = new SimpleMarkerSymbol { Color = color, Size = ptSize, Style = SimpleMarkerStyle.Diamond };
            this.Square = new SimpleMarkerSymbol { Color = color, Size = ptSize, Style = SimpleMarkerStyle.Square };
            this.Triangle = new SimpleMarkerSymbol { Color = color, Size = ptSize, Style = SimpleMarkerStyle.Triangle };
            this.X = new SimpleMarkerSymbol { Color = color, Size = ptSize, Style = SimpleMarkerStyle.X };

            this.Line = new SimpleLineSymbol { Color = color, Style = SimpleLineStyle.Solid, Width = width };
            this.DotLine = new SimpleLineSymbol { Color = color, Style = SimpleLineStyle.Dot, Width = width };
            this.DashLine = new SimpleLineSymbol { Color = color, Style = SimpleLineStyle.Dash, Width = width };

            var fillCollor = System.Windows.Media.Color.FromArgb(44, color.R, color.G, color.B);
            this.Fill = new SimpleFillSymbol { Color = fillCollor, Style = SimpleFillStyle.Solid, Outline = this.Line };

            SymbolFont font = new SymbolFont("Tahoma", 9.0 + width, SymbolFontStyle.Normal, SymbolTextDecoration.None, SymbolFontWeight.Bold);
            this.Text = new TextSymbol { Color = color, Font = font, BorderLineColor = System.Windows.Media.Colors.White, BorderLineSize = 1.0 };
        }

        private static int nextSymbolIndex = -1;
        private static Symbols[] nextSymbols = new Symbols[]
		{
			Symbols.Black2,
			Symbols.Gray2,
			Symbols.Blue2,
			Symbols.Red2,
			Symbols.Magenta2,
			Symbols.Orange2
		};
        public static Symbols NextSymbol()
        {
            Symbols.nextSymbolIndex++;
            if (Symbols.nextSymbolIndex >= Symbols.nextSymbols.Length)
            {
                Symbols.nextSymbolIndex = 0;
            }
            return Symbols.nextSymbols[Symbols.nextSymbolIndex];
        }

        public AttributeLabelClass GetLabelClass(string attrName, LabelPlacement placement)
        {
            return new AttributeLabelClass
            {
                Symbol = this.Text,
                TextExpression = "[" + attrName + "]",
                LabelPlacement = placement
            };
        }
    }
}
