using Esri.ArcGISRuntime.Layers;
using System;
using System.Threading;
using System.Threading.Tasks;
namespace Ellipsoidus
{
    public abstract class TestBase
    {
        public GraphicsLayer Layer = new GraphicsLayer();
        public readonly RaportText Raport = new RaportText();
        private static double km = 1000.0;
        protected double[] TestDistances = new double[]
		{
			0.1 * TestBase.km,
			0.5 * TestBase.km,
			1.0 * TestBase.km,
			2.0 * TestBase.km,
			5.0 * TestBase.km,
			10.0 * TestBase.km,
			20.0 * TestBase.km,
			50.0 * TestBase.km,
			100.0 * TestBase.km,
			200.0 * TestBase.km,
			500.0 * TestBase.km,
			1000.0 * TestBase.km,
			2000.0 * TestBase.km
		};
        public TestBase()
        {
        }
        public void ShowRaport()
        {
            if (this.Raport.Count > 0)
            {
                this.Raport.Insert(0, "");
                this.Raport.Insert(0, "### " + base.GetType().Name);
                Utils.ShowNotepad(this.Raport);
            }
        }
        public abstract void RunTest();
        public Task RunTestAsync()
        {
            return Task.Run(new Action(this.RunTest), CancellationToken.None);
        }
    }
}
