using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Utility;

namespace EtlMonitor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MyTraceListener MyTraceListener;

        public MainWindow()
        {
            InitializeComponent();
            this.MyTraceListener = new MyTraceListener();
            this.MyTraceListener.MyTraceListenerOnWriteLine += (o, e) =>
              {
                  _txtStatus.Dispatcher.BeginInvoke(
                      new Action(() =>
                      {
                          _txtStatus.AppendText(e.MessageLine);
                          _txtStatus.ScrollToEnd();
                      }));
              };
            this.Loaded += MainWindow_Loaded;
            this.Closed += (o, e) =>
              {
                  MyTraceListener.Dispose();
              };
        }
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                Trace.Write("Loaded");
            }
            catch (Exception ex)
            {
                this.Content = ex.ToString();
            }
        }
    }
}
