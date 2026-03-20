using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using Application = System.Windows.Application;

namespace ScreenRecApp
{
    public partial class App : Application
    {
        private NotifyIcon _notifyIcon;
        private GlobalHotkeyService _hotkeyService;
        public static RecordingService CurrentRecordingService { get; private set; }
        private System.Windows.Forms.Timer _notificationTimer;
        public static DevWindow _devWindow;

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            Logger.Log("Application started.");
            SettingsManager.Load();

            CurrentRecordingService = new RecordingService();

            _notifyIcon = new NotifyIcon();
            try
            {
                _notifyIcon.Icon = Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location);
            }
            catch { _notifyIcon.Icon = SystemIcons.Information; }
            _notifyIcon.Visible = true;
            _notifyIcon.Text = "Runtime Broker";

            var contextMenu = new System.Windows.Forms.ContextMenuStrip();
            contextMenu.Items.Add("Settings", null, (s, ev) => new SettingsWindow().Show());
            contextMenu.Items.Add("Exit", null, (s, ev) => Shutdown());
            _notifyIcon.ContextMenuStrip = contextMenu;
            _notifyIcon.DoubleClick += (s, ev) => new SettingsWindow().Show();

            try
            {
                _hotkeyService = new GlobalHotkeyService(SettingsManager.Settings.HotkeyModifiers, SettingsManager.Settings.HotkeyVirtualKey);
                _hotkeyService.HotkeyPressed += OnHotkeyPressed;
                Logger.Log("Global hotkey registered.");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Hotkey Registration");
            }

            _notificationTimer = new System.Windows.Forms.Timer();
            _notificationTimer.Interval = 30 * 60 * 1000; // 30 mins
            _notificationTimer.Tick += OnNotificationTimerTick;
        }

        public static void UpdateGlobalHotkey()
        {
            var app = (App)Current;
            if (app._hotkeyService != null)
            {
                app._hotkeyService.Register(SettingsManager.Settings.HotkeyModifiers, SettingsManager.Settings.HotkeyVirtualKey);
                Logger.Log($"Global hotkey updated to: {SettingsManager.Settings.HotkeyDisplayText}");
            }
        }

        private async void OnHotkeyPressed(object sender, EventArgs e)
        {
            try
            {
                Logger.Log($"Hotkey pressed. Processing... Current Recording State: {CurrentRecordingService?.IsRecording}");
                if (CurrentRecordingService.IsRecording)
                {
                    // Stop Recording
                    _notificationTimer.Stop();

                    LoaderWindow loader = new LoaderWindow();
                    loader.Show();

                    string tempFilePath = await CurrentRecordingService.StopRecording();
                    Logger.Log($"Stopped recording. Temp path: {tempFilePath}");
                    _notifyIcon.Text = "Runtime Broker";

                    loader.Close();

                    // Ensure file exists
                    if (!string.IsNullOrEmpty(tempFilePath) && File.Exists(tempFilePath))
                    {
                        PromptWindow prompt = new PromptWindow();
                        bool? result = prompt.ShowDialog();
                        
                        if (result == true)
                        {
                            string finalName = string.IsNullOrEmpty(prompt.ResultName) ? "untitled" : prompt.ResultName;
                            string finalTimestamp = string.IsNullOrEmpty(prompt.ResultTimestamp) ? DateTime.Now.ToString("dd.MM.yy_HH.mm_") : prompt.ResultTimestamp;

                            string targetDir = SettingsManager.Settings.SavePath;
                            if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

                            string targetFile = Path.Combine(targetDir, finalTimestamp + finalName + ".mp4");

                            try
                            {
                                File.Move(tempFilePath, targetFile, true);
                                Logger.Log($"File successfully saved to {targetFile}");
                            }
                            catch (Exception ex)
                            {
                                Logger.LogError(ex, "File Move");
                            }
                        }
                        else
                        {
                            try 
                            { 
                                File.Delete(tempFilePath); 
                                Logger.Log("User cancelled save prompt. Temp file deleted."); 
                            } 
                            catch { }
                        }
                    }
                    else
                    {
                        Logger.Log("Temp file not found after recording stopped.");
                    }
                }
                else
                {
                    // Start Recording
                    Logger.Log("Attempting to start recording...");
                    
                    if (SettingsManager.Settings.DeveloperMode && _devWindow == null)
                    {
                        _devWindow = new DevWindow();
                        _devWindow.Closed += (s, ev) => _devWindow = null;
                        _devWindow.Show();
                    }

                    bool started = CurrentRecordingService.StartRecording();
                    if (started)
                    {
                        Logger.Log("Recording started successfully.");
                        _notifyIcon.Text = "Runtime Broker";
                        _notificationTimer.Interval = SettingsManager.Settings.NotificationTimerMinutes * 60 * 1000;
                        _notificationTimer.Start();
                    }
                    else
                    {
                        Logger.Log("Failed to start recording! Check ffmpeg.");
                        System.Windows.MessageBox.Show("Failed to start recording! Is ffmpeg installed?", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "OnHotkeyPressed Critical Unhandled");
            }
        }

        private void OnNotificationTimerTick(object sender, EventArgs e)
        {
            _notificationTimer.Stop(); // Ensure only once per recording session
            _notifyIcon.ShowBalloonTip(3000, "Runtime Broker", " ", ToolTipIcon.None);
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            if (CurrentRecordingService.IsRecording)
                CurrentRecordingService.StopRecording().Wait();

            _hotkeyService?.Dispose();
            _notifyIcon?.Dispose();
            _notificationTimer?.Dispose();
        }
    }
}
