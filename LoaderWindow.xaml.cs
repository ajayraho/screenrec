using System;
using System.Windows;
using System.Windows.Input;

namespace ScreenRecApp
{
    public partial class LoaderWindow : Window
    {
        public event Action CancelTranscriptionRequested;
        public event Action CancelSummarizationRequested;

        public bool IsTranscriptionCancelled { get; private set; }
        public bool IsSummarizationCancelled { get; private set; }

        public LoaderWindow()
        {
            InitializeComponent();
        }

        public void UpdateStatus(string status)
        {
            Dispatcher.Invoke(() =>
            {
                StatusText.Text = status;
                MainProgress.IsIndeterminate = true;
                ProgressText.Text = "";
            });
        }

        public void UpdateProgress(int percent)
        {
            Dispatcher.Invoke(() =>
            {
                ProgressText.Text = $"{percent}%";
            });
        }

        public void ShowCancelOptions(bool showTranscription, bool showSummarization)
        {
            Dispatcher.Invoke(() =>
            {
                CancelTranscriptionText.Visibility = showTranscription && !IsTranscriptionCancelled ? Visibility.Visible : Visibility.Collapsed;
                CancelSummarizationText.Visibility = showSummarization && !IsSummarizationCancelled ? Visibility.Visible : Visibility.Collapsed;
            });
        }

        private void CancelTranscription_Click(object sender, MouseButtonEventArgs e)
        {
            if (IsTranscriptionCancelled) return;
            IsTranscriptionCancelled = true;
            CancelTranscriptionText.Text = "Cancelling transcription...";
            CancelTranscriptionText.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#6c7086"));
            CancelTranscriptionText.Cursor = System.Windows.Input.Cursors.Arrow;
            CancelTranscriptionRequested?.Invoke();
            
            // Cancelling transcription forces summarization to be cancelled too, as it depends on it.
            if (!IsSummarizationCancelled && CancelSummarizationText.Visibility == Visibility.Visible)
            {
                CancelSummarization_Click(null, null);
            }
        }

        private void CancelSummarization_Click(object sender, MouseButtonEventArgs e)
        {
            if (IsSummarizationCancelled) return;
            IsSummarizationCancelled = true;
            CancelSummarizationText.Text = "Cancelling summarization...";
            CancelSummarizationText.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#6c7086"));
            CancelSummarizationText.Cursor = System.Windows.Input.Cursors.Arrow;
            CancelSummarizationRequested?.Invoke();
        }
    }
}
