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

        private WasapiCapture _testMicCapture;
        private bool _isTestingMic = false;

        public SettingsWindow()
        {
            InitializeComponent();
            SavePathBox.Text = SettingsManager.Settings.SavePath;
            DevModeCheck.IsChecked = SettingsManager.Settings.DeveloperMode;
            
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

            _timer = new System.Windows.Threading.DispatcherTimer();
            _timer.Interval = System.TimeSpan.FromMilliseconds(500);
            _timer.Tick += (s, e) => UpdateStatus();
            _timer.Start();
            UpdateStatus();

            this.Closed += SettingsWindow_Closed;
        }

        private void SettingsWindow_Closed(object sender, EventArgs e)
        {
            if (_isTestingMic)
            {
                try { _testMicCapture?.StopRecording(); _testMicCapture?.Dispose(); } catch { }
            }
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

        private void TestMic_Click(object sender, RoutedEventArgs e)
        {
            if (_isTestingMic)
            {
                try { _testMicCapture?.StopRecording(); _testMicCapture?.Dispose(); } catch { }
                TestMicButton.Content = "Test Mic";
                MicLevelBar.Value = 0;
                _isTestingMic = false;
            }
            else
            {
                try
                {
                    _testMicCapture = new WasapiCapture();
                    _testMicCapture.DataAvailable += (s, a) =>
                    {
                        float max = 0;
                        for (int index = 0; index < a.BytesRecorded; index += 4)
                        {
                            if (index + 4 <= a.Buffer.Length)
                            {
                                float sample = BitConverter.ToSingle(a.Buffer, index);
                                if (sample < 0) sample = -sample;
                                if (sample > max) max = sample;
                            }
                        }
                        
                        float visualMax = max * (float)MicBoostSlider.Value;
                        if (visualMax > 1f) visualMax = 1f;

                        Dispatcher.InvokeAsync(() => { MicLevelBar.Value = visualMax; });
                    };
                    _testMicCapture.StartRecording();
                    TestMicButton.Content = "Stop Mic";
                    _isTestingMic = true;
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show("Could not start microphone test: " + ex.Message);
                }
            }
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
            SettingsManager.Settings.HotkeyModifiers = currentMods;
            SettingsManager.Settings.HotkeyVirtualKey = _tempVirtualKey;
            SettingsManager.Settings.HotkeyDisplayText = displayText;
            
            SettingsManager.Settings.MicVolumeBoost = Math.Round(MicBoostSlider.Value, 1);
            SettingsManager.Settings.CaptureMicAudio = CaptureMicCheck.IsChecked == true;

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
