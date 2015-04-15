using Ellipsoidus.Windows;
using Esri;
using Esri.ArcGISRuntime;
using Esri.ArcGISRuntime.Controls;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Layers;
using Microsoft.Win32;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Threading;

namespace Ellipsoidus
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            try
            {
                ArcGISRuntimeEnvironment.ClientId = "bjOeU5M29vt4t0QR";
                ArcGISRuntimeEnvironment.Initialize();
                Application.Current.Dispatcher.UnhandledException += new DispatcherUnhandledExceptionEventHandler(this.Dispatcher_UnhandledException);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "ArcGIS Runtime initialization failed.");
                base.Shutdown();
            }
        }


        private void Dispatcher_UnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            string msg = e.Exception.Message;
            if (e.Exception.InnerException != null)
            {
                msg = msg + "\r\n" + e.Exception.InnerException.Message;
            }
            MessageBox.Show(msg, "ERROR", MessageBoxButton.OK, MessageBoxImage.Hand);
            e.Handled = true;
        }
    }
}
