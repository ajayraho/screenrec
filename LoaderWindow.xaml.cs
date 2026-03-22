using System;
using System.Windows;
using System.Windows.Input;

namespace ScreenRecApp
{
    public partial class LoaderWindow : Window
    {
        public event Action CancelAIRequested;

        public LoaderWindow()
        {
            InitializeComponent();
        }

        public void UpdateStatus(string status)
        {
            Dispatcher.Invoke(() => StatusText.Text = status);
        }

        public void ShowCancelAction(bool show)
        {
            Dispatcher.Invoke(() => CancelActionText.Visibility = show ? Visibility.Visible : Visibility.Collapsed);
        }

        private void CancelAction_Click(object sender, MouseButtonEventArgs e)
        {
            CancelActionText.Text = "Cancelling...";
            CancelActionText.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#6c7086"));
            CancelActionText.Cursor = System.Windows.Input.Cursors.Arrow;
            CancelAIRequested?.Invoke();
        }
    }
}
