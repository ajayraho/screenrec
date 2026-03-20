using System;
using System.Windows;
using System.Windows.Input;

namespace ScreenRecApp
{
    public partial class DevWindow : Window
    {
        private int _lineCount = 0;

        public DevWindow()
        {
            InitializeComponent();

            // Snap to bottom-right of screen
            var screen = System.Windows.SystemParameters.WorkArea;
            this.Left = screen.Right - this.Width - 20;
            this.Top = screen.Bottom - this.Height - 20;

            Logger.OnLog += ProcessLog;
            this.Closed += (s, e) => Logger.OnLog -= ProcessLog;
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            LogTextBox.Clear();
            LastCommandText.Text = "—";
            _lineCount = 0;
            LineCountText.Text = "0 lines";
        }

        private void ProcessLog(string message)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _lineCount++;
                LogTextBox.AppendText(message + Environment.NewLine);
                LogTextBox.ScrollToEnd();
                LineCountText.Text = $"{_lineCount} lines";
            }));
        }

        public void ReportCommand(string command)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                // Show truncated command in the header bar
                string displayCmd = command.Length > 80 ? command[..80] + "…" : command;
                LastCommandText.Text = displayCmd;

                // Also log it fully in the text area
                LogTextBox.AppendText($"\n▶ {command}\n");
                LogTextBox.ScrollToEnd();
            }));
        }

        public void SetRecordingState(bool isRecording)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                StatusDot.Fill = isRecording
                    ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(243, 139, 168)) // red
                    : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(166, 227, 161)); // green
                StatusLabel.Text = isRecording ? " — Recording" : " — Idle";
            }));
        }
    }
}
