using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ellipsoidus
{
    public abstract class Builder
    {
        public TimeSpan BuildTime { get; private set; } = TimeSpan.Zero;
        public string Title { get; protected set; }

        public Builder()
        {
            this.Title = this.GetType().Name.Replace("Builder", "");
        }

        public abstract void Build();

        private void BuildWithTimer()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            this.Build();
            sw.Stop();
            this.BuildTime = sw.Elapsed;
        }

        public Task BuildAsync()
        {
            var action = new Action(this.BuildWithTimer);
            return Task.Run(action, CancellationToken.None);
        }


    }
}
