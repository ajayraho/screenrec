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

        private static System.Threading.Mutex? _mutex = null;
        private SettingsWindow? _settingsWindow = null;

        private void OpenSettings()
        {
            if (_settingsWindow != null)
            {
                _settingsWindow.Activate();
                _settingsWindow.WindowState = WindowState.Normal;
                return;
            }
            _settingsWindow = new SettingsWindow();
            _settingsWindow.Closed += (s, ev) => _settingsWindow = null;
            _settingsWindow.Show();
        }
 
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            const string appName = "ScreenRecApp_SingleInstance_Mutex";
            bool createdNew = false;
            _mutex = new System.Threading.Mutex(false, appName);
            try
            {
                createdNew = _mutex.WaitOne(0);
            }
            catch (System.Threading.AbandonedMutexException)
            {
                // Previous instance was force-killed — we inherit the mutex and proceed
                createdNew = true;
            }

            if (!createdNew)
            {
                System.Windows.MessageBox.Show("Another instance of the recorder is already running.", "Already Running", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }

            Logger.Log("Application started.");
            SettingsManager.Load();

            CurrentRecordingService = new RecordingService();

            _notifyIcon = new NotifyIcon();
            _notifyIcon.Icon = SystemIcons.Information; // Default icon
            try
            {
                // Attempt to load the application's actual window/file icon safely
                _notifyIcon.Icon = Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location);
            }
            catch { }
            
            _notifyIcon.Visible = true;
            _notifyIcon.Text = "Sound Service Broker";

            var contextMenu = new System.Windows.Forms.ContextMenuStrip();
            contextMenu.Items.Add("Settings", null, (s, ev) => OpenSettings());
            contextMenu.Items.Add("Exit", null, (s, ev) => Shutdown());
            _notifyIcon.ContextMenuStrip = contextMenu;
            _notifyIcon.DoubleClick += (s, ev) => OpenSettings();

            Logger.Log("Starting Hotkey Registration...");
            try
            {
                _hotkeyService = new GlobalHotkeyService(SettingsManager.Settings.HotkeyModifiers, SettingsManager.Settings.HotkeyVirtualKey);
                _hotkeyService.HotkeyPressed += OnHotkeyPressed;
                Logger.Log("Global hotkey registered successfully.");
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
                    _devWindow?.SetRecordingState(false);
                    _notifyIcon.Text = "Sound Service Broker";

                    loader.Close();

                    // Ensure file exists
                    if (!string.IsNullOrEmpty(tempFilePath) && File.Exists(tempFilePath))
                    {
                        PromptWindow prompt = new PromptWindow();
                        bool? result = prompt.ShowDialog();
                        
                        if (result == true)
                        {
                            string finalName = string.IsNullOrEmpty(prompt.ResultName) ? "untitled" : prompt.ResultName;
                            string finalTimestamp = string.IsNullOrEmpty(prompt.ResultTimestamp) ? DateTime.Now.ToString("ddMMyy_HHmmss_") : prompt.ResultTimestamp;

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
                    // Start Recording - run on background thread so audio device 
                    // init (WasapiCapture) never blocks the UI thread
                    Logger.Log("Attempting to start recording...");
                    
                    if (SettingsManager.Settings.DeveloperMode && _devWindow == null)
                    {
                        _devWindow = new DevWindow();
                        _devWindow.Closed += (s, ev) => _devWindow = null;
                        _devWindow.Show();
                    }

                    bool started = await Task.Run(() => CurrentRecordingService.StartRecording());

                    if (started)
                    {
                        Logger.Log("Recording started successfully.");
                        _devWindow?.SetRecordingState(true);
                        _notifyIcon.Text = "Sound Service Broker";
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
            _notifyIcon.ShowBalloonTip(3000, "Sound Service Broker", " ", ToolTipIcon.None);
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            if (CurrentRecordingService.IsRecording)
                CurrentRecordingService.StopRecording().Wait();

            _hotkeyService?.Dispose();
            _notifyIcon?.Dispose();
            _notificationTimer?.Dispose();

            try { _mutex?.ReleaseMutex(); _mutex?.Dispose(); } catch { }
        }
    }
}
