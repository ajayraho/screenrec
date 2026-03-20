using System;
using System.Windows;

namespace ScreenRecApp
{
    public partial class DevWindow : Window
    {
        public DevWindow()
        {
            InitializeComponent();
            Logger.OnLog += ProcessLog;
            
            this.Closed += (s, e) => {
                Logger.OnLog -= ProcessLog;
            };
        }

        private void ProcessLog(string message)
        {
            Dispatcher.BeginInvoke(new Action(() => {
                LogTextBox.AppendText(message + Environment.NewLine);
                LogTextBox.ScrollToEnd();
            }));
        }

        public void ReportCommand(string command)
        {
            Dispatcher.BeginInvoke(new Action(() => {
                LogTextBox.AppendText("======== EXECUTING COMMAND ========\n" + command + "\n===================================\n");
                LogTextBox.ScrollToEnd();
            }));
        }
    }
}
