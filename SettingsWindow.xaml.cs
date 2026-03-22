using System;
using System.IO;
using System.Windows;
using NAudio.Wave;
using NAudio.CoreAudioApi;

namespace ScreenRecApp
{
    public partial class SettingsWindow : Window
    {
        private System.Windows.Threading.DispatcherTimer _timer;

        private uint _tempModifiers;
        private uint _tempVirtualKey;
        private string _tempDisplayText;

        public SettingsWindow()
        {
            InitializeComponent();
            SavePathBox.Text = SettingsManager.Settings.SavePath;
            DevModeCheck.IsChecked = SettingsManager.Settings.DeveloperMode;
            StartupCheck.IsChecked = App.IsStartupEnabled();
            
            _tempModifiers = SettingsManager.Settings.HotkeyModifiers;
            _tempVirtualKey = SettingsManager.Settings.HotkeyVirtualKey;
            _tempDisplayText = SettingsManager.Settings.HotkeyDisplayText;
            _tempModifiers = SettingsManager.Settings.HotkeyModifiers;

            ChkModWin.IsChecked = (_tempModifiers & 8) == 8;
            ChkModShift.IsChecked = (_tempModifiers & 4) == 4;
            ChkModCtrl.IsChecked = (_tempModifiers & 2) == 2;
            ChkModAlt.IsChecked = (_tempModifiers & 1) == 1;

            string[] parts = _tempDisplayText.Split('+');
            HotkeyCharBox.Text = parts[parts.Length - 1].Trim();
            
            TimerMinutesBox.Text = SettingsManager.Settings.NotificationTimerMinutes.ToString();
            
            // Map Quality
            foreach (System.Windows.Controls.ComboBoxItem item in QualityCombo.Items) {
                if (item.Content.ToString() == SettingsManager.Settings.VideoQuality) {
                    QualityCombo.SelectedItem = item; break;
                }
            }
            if (QualityCombo.SelectedItem == null) QualityCombo.SelectedIndex = 0;

            // Map FPS
            foreach (System.Windows.Controls.ComboBoxItem item in FpsCombo.Items) {
                if (item.Tag?.ToString() == SettingsManager.Settings.FramesPerSecond.ToString()) {
                    FpsCombo.SelectedItem = item; break;
                }
            }
            if (FpsCombo.SelectedItem == null) FpsCombo.SelectedIndex = 1;

            MicBoostSlider.Value = SettingsManager.Settings.MicVolumeBoost;
            CaptureMicCheck.IsChecked = SettingsManager.Settings.CaptureMicAudio;
            AudioSyncSlider.Value = SettingsManager.Settings.AudioSyncOffsetMs;
            AudioSyncText.Text = $"{SettingsManager.Settings.AudioSyncOffsetMs} ms";

            // Map Transcription Language
            foreach (System.Windows.Controls.ComboBoxItem item in LangCombo.Items) {
                if (item.Tag?.ToString() == SettingsManager.Settings.TranscriptionLanguage) {
                    LangCombo.SelectedItem = item; break;
                }
            }
            if (LangCombo.SelectedItem == null) LangCombo.SelectedIndex = 0;

            _timer = new System.Windows.Threading.DispatcherTimer();
            _timer.Interval = System.TimeSpan.FromMilliseconds(500);
            _timer.Tick += (s, e) => UpdateStatus();
            _timer.Start();
            UpdateStatus();

            CheckWhisperPlugin();
            CheckLlmPlugin();

            this.Closed += SettingsWindow_Closed;
        }

        private void SettingsWindow_Closed(object sender, EventArgs e)
        {
            _timer.Stop();
        }

        private void Header_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
                this.DragMove();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void AudioSyncSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (AudioSyncText != null)
                AudioSyncText.Text = $"{(int)e.NewValue} ms";
        }

        private void MicBoostSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (MicBoostText != null)
            {
                MicBoostText.Text = Math.Round(e.NewValue, 1).ToString("0.0") + "x";
            }
        }

        private void HotkeyCharBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            e.Handled = true;
            var key = (e.Key == System.Windows.Input.Key.System ? e.SystemKey : e.Key);
            
            if (key == System.Windows.Input.Key.LeftShift || key == System.Windows.Input.Key.RightShift ||
                key == System.Windows.Input.Key.LeftCtrl || key == System.Windows.Input.Key.RightCtrl ||
                key == System.Windows.Input.Key.LeftAlt || key == System.Windows.Input.Key.RightAlt ||
                key == System.Windows.Input.Key.LWin || key == System.Windows.Input.Key.RWin)
            {
                return;
            }

            _tempVirtualKey = (uint)System.Windows.Input.KeyInterop.VirtualKeyFromKey(key);
            HotkeyCharBox.Text = key.ToString();
        }

        private void UpdateStatus()
        {
            if (App.CurrentRecordingService != null && App.CurrentRecordingService.IsRecording)
            {
                StatusText.Text = "Status: 🔴 REC (Recording)";
                StatusText.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#f38ba8"));
            }
            else
            {
                StatusText.Text = "Status: Idle";
                StatusText.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#a6e3a1"));
            }
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.SelectedPath = SavePathBox.Text;
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    SavePathBox.Text = dialog.SelectedPath;
                }
            }
        }

        private void CheckWhisperPlugin()
        {
            string pluginsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins", "whisper");
            bool pluginFound = false;
            string foundModelInfo = "";

            if (Directory.Exists(pluginsDir))
            {
                var files = Directory.GetFiles(pluginsDir, "*.bin");
                if (files.Length > 0)
                {
                    pluginFound = true;
                    foundModelInfo = Path.GetFileName(files[0]);
                }
            }

            if (pluginFound)
            {
                TranscriptionStatusText.Text = $"🟢 Plugin Detected ({foundModelInfo})";
                TranscriptionStatusText.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#a6e3a1"));
                DownloadPluginBtn.Visibility = Visibility.Collapsed;
                GenerateSubtitlesCheck.IsEnabled = true;
                GenerateTranscriptCheck.IsEnabled = true;
                LangCombo.IsEnabled = true;

                GenerateSubtitlesCheck.IsChecked = SettingsManager.Settings.GenerateSubtitles;
                GenerateTranscriptCheck.IsChecked = SettingsManager.Settings.GenerateTranscript;
            }
            else
            {
                TranscriptionStatusText.Text = "🔴 Plugin Missing";
                TranscriptionStatusText.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#f38ba8"));
                DownloadPluginBtn.Visibility = Visibility.Visible;
                GenerateSubtitlesCheck.IsEnabled = false;
                GenerateTranscriptCheck.IsEnabled = false;
                LangCombo.IsEnabled = false;

                GenerateSubtitlesCheck.IsChecked = false;
                GenerateTranscriptCheck.IsChecked = false;
            }
        }

        private void DownloadPlugin_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.MessageBox.Show(
                "To enable AI Transcriptions:\n\n1. Download a ggml model (e.g. ggml-medium.bin) from huggingface (ggerganov/whisper.cpp).\n2. Create a folder named 'plugins\\whisper' next to this executable.\n3. Place the .bin file inside.", 
                "Download Whisper Plugin", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://huggingface.co/ggerganov/whisper.cpp/tree/main") { UseShellExecute = true });
        }

        private void CheckLlmPlugin()
        {
            string pluginsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins", "llm");
            bool pluginFound = false;
            string foundModelInfo = "";

            if (Directory.Exists(pluginsDir))
            {
                var files = Directory.GetFiles(pluginsDir, "*.gguf");
                if (files.Length > 0)
                {
                    pluginFound = true;
                    foundModelInfo = Path.GetFileName(files[0]);
                }
            }

            if (pluginFound)
            {
                LlmStatusText.Text = $"🟢 Plugin Detected ({foundModelInfo})";
                LlmStatusText.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#a6e3a1"));
                DownloadLlmBtn.Visibility = Visibility.Collapsed;
                GenerateSummaryCheck.IsEnabled = true;

                GenerateSummaryCheck.IsChecked = SettingsManager.Settings.GenerateSummary;
            }
            else
            {
                LlmStatusText.Text = "🔴 Plugin Missing";
                LlmStatusText.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#f38ba8"));
                DownloadLlmBtn.Visibility = Visibility.Visible;
                GenerateSummaryCheck.IsEnabled = false;

                GenerateSummaryCheck.IsChecked = false;
            }
        }

        private void DownloadLlm_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.MessageBox.Show(
                "To enable AI Summarization:\n\n1. Download a GGUF model (e.g. Phi-3-mini-4k-instruct-q4.gguf) from huggingface.\n2. Create a folder named 'plugins\\llm' next to this executable.\n3. Place the .gguf file inside.", 
                "Download Summarizer Plugin", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://huggingface.co/microsoft/Phi-3-mini-4k-instruct-gguf/tree/main") { UseShellExecute = true });
        }

        private void RefreshPlugin_Click(object sender, RoutedEventArgs e)
        {
            CheckWhisperPlugin();
            CheckLlmPlugin();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            uint currentMods = 0;
            string displayText = "";

            if (ChkModWin.IsChecked == true) { currentMods |= 8; displayText += "Win + "; }
            if (ChkModCtrl.IsChecked == true) { currentMods |= 2; displayText += "Ctrl + "; }
            if (ChkModShift.IsChecked == true) { currentMods |= 4; displayText += "Shift + "; }
            if (ChkModAlt.IsChecked == true) { currentMods |= 1; displayText += "Alt + "; }
            
            displayText += HotkeyCharBox.Text;

            SettingsManager.Settings.SavePath = SavePathBox.Text;
            SettingsManager.Settings.DeveloperMode = DevModeCheck.IsChecked == true;
            App.SetStartupEnabled(StartupCheck.IsChecked == true);
            SettingsManager.Settings.HotkeyModifiers = currentMods;
            SettingsManager.Settings.HotkeyVirtualKey = _tempVirtualKey;
            SettingsManager.Settings.HotkeyDisplayText = displayText;
            
            SettingsManager.Settings.MicVolumeBoost = Math.Round(MicBoostSlider.Value, 1);
            SettingsManager.Settings.CaptureMicAudio = CaptureMicCheck.IsChecked == true;
            SettingsManager.Settings.AudioSyncOffsetMs = (int)AudioSyncSlider.Value;
            
            SettingsManager.Settings.GenerateSubtitles = GenerateSubtitlesCheck.IsChecked == true;
            SettingsManager.Settings.GenerateTranscript = GenerateTranscriptCheck.IsChecked == true;
            SettingsManager.Settings.GenerateSummary = GenerateSummaryCheck.IsChecked == true;

            if (LangCombo.SelectedItem is System.Windows.Controls.ComboBoxItem lItem && lItem.Tag != null)
                SettingsManager.Settings.TranscriptionLanguage = lItem.Tag.ToString()!;

            if (int.TryParse(TimerMinutesBox.Text, out int mins) && mins > 0)
                SettingsManager.Settings.NotificationTimerMinutes = mins;

            if (QualityCombo.SelectedItem is System.Windows.Controls.ComboBoxItem qItem)
                SettingsManager.Settings.VideoQuality = qItem.Content.ToString();

            if (FpsCombo.SelectedItem is System.Windows.Controls.ComboBoxItem fItem && int.TryParse(fItem.Tag?.ToString(), out int fps))
                SettingsManager.Settings.FramesPerSecond = fps;

            SettingsManager.Save();
            
            App.UpdateGlobalHotkey();
            
            Close();
        }
    }
}
