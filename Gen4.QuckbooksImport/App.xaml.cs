using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using System.IO;

namespace Gen4.QuckbooksImport
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void Application_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            string ErrorLogFile = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "\\ErrorLog.txt";

            StreamWriter swOriginal = new StreamWriter(ErrorLogFile, true);
            TextWriter sw = TextWriter.Synchronized(swOriginal);
            sw.Write("\r\n\r\n" + "Unhandled Exception: " + e.Exception.ToString());
            sw.Close();
            sw = null;

            e.Handled = true;
        }
    }

}
