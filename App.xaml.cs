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
        private System.Windows.Forms.Timer _trayTooltipTimer;  // updates tray text while recording
        private DateTime _recordingStartedAt;
        public static DevWindow _devWindow;

        private static System.Threading.Mutex? _mutex = null;
        private SettingsWindow? _settingsWindow = null;

        private ToolStripMenuItem _stopRecordingMenuItem;

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
            _stopRecordingMenuItem = new ToolStripMenuItem("Stop Recording", null, async (s, ev) => await TriggerStopRecording());
            _stopRecordingMenuItem.Visible = false; // hidden until recording starts
            contextMenu.Items.Add(_stopRecordingMenuItem);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add("Settings", null, (s, ev) => OpenSettings());
            contextMenu.Items.Add("About", null, (s, ev) => { new AboutWindow().ShowDialog(); });
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
            _notificationTimer.Interval = 30 * 60 * 1000;
            _notificationTimer.Tick += OnNotificationTimerTick;

            // Tooltip updater — fires every second while recording to show elapsed time
            _trayTooltipTimer = new System.Windows.Forms.Timer();
            _trayTooltipTimer.Interval = 1000;
            _trayTooltipTimer.Tick += (s, ev) =>
            {
                if (CurrentRecordingService?.IsRecording == true)
                {
                    var elapsed = DateTime.UtcNow - _recordingStartedAt;
                    string ts = elapsed.ToString(@"hh\:mm\:ss");
                    // NotifyIcon.Text has a 63-char limit on Windows
                    string text = $"Recording active — {ts}";
                    if (text.Length > 63) text = text[..63];
                    _notifyIcon.Text = text;
                }
            };
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

        // ── Startup with Windows ──────────────────────────────────────────────
        private const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string AppRegName = "SoundServiceBroker";

        public static bool IsStartupEnabled()
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey, false);
            return key?.GetValue(AppRegName) != null;
        }

        public static void SetStartupEnabled(bool enable)
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey, true);
            if (key == null) return;
            if (enable)
            {
                string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location
                    .Replace(".dll", ".exe"); // handles self-contained publish path
                key.SetValue(AppRegName, $"\"{exePath}\"");
                Logger.Log("Startup with Windows: enabled.");
            }
            else
            {
                key.DeleteValue(AppRegName, false);
                Logger.Log("Startup with Windows: disabled.");
            }
        }

        // Shared stop logic — used by both the hotkey and the tray Stop button
        private async Task TriggerStopRecording()
        {
            if (!CurrentRecordingService.IsRecording) return;

            _notificationTimer.Stop();
            _trayTooltipTimer.Stop();
            _notifyIcon.Text = "Saving in progress...";
            _stopRecordingMenuItem.Visible = false;

            LoaderWindow loader = new LoaderWindow();
            loader.Show();

            string tempFilePath = await CurrentRecordingService.StopRecording(loader);
            Logger.Log($"Stopped recording. Temp path: {tempFilePath}");
            _devWindow?.SetRecordingState(false);
            _notifyIcon.Text = "Sound Service Broker";

            loader.Close();

            if (!string.IsNullOrEmpty(tempFilePath) && File.Exists(tempFilePath))
            {
                PromptWindow prompt = new PromptWindow();
                bool? result = prompt.ShowDialog();

                if (result == true)
                {
                    string finalName = string.IsNullOrEmpty(prompt.ResultName) ? "untitled" : prompt.ResultName;
                    string finalTimestamp = string.IsNullOrEmpty(prompt.ResultTimestamp) ? DateTime.Now.ToString("dd.MM.yy_HH.mm.ss_") : prompt.ResultTimestamp;

                    string targetDir = SettingsManager.Settings.SavePath;
                    if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

                    string targetFile = Path.Combine(targetDir, finalTimestamp + finalName + ".mp4");
                    try
                    {
                        File.Move(tempFilePath, targetFile, true);
                        Logger.Log($"File successfully saved to {targetFile}");

                        string tempSrt = tempFilePath.Replace(".mp4", ".srt");
                        if (File.Exists(tempSrt)) File.Move(tempSrt, targetFile.Replace(".mp4", ".srt"), true);

                        string tempTxt = tempFilePath.Replace(".mp4", "_transcript.txt");
                        if (File.Exists(tempTxt)) File.Move(tempTxt, targetFile.Replace(".mp4", "_transcript.txt"), true);

                        string tempSum = tempFilePath.Replace(".mp4", "_summary.txt");
                        if (File.Exists(tempSum)) File.Move(tempSum, targetFile.Replace(".mp4", "_summary.txt"), true);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "File Move");
                    }
                }
                else
                {
                    try { File.Delete(tempFilePath); Logger.Log("User cancelled save. Temp file deleted."); } catch { }
                    try { File.Delete(tempFilePath.Replace(".mp4", ".srt")); } catch { }
                    try { File.Delete(tempFilePath.Replace(".mp4", "_transcript.txt")); } catch { }
                    try { File.Delete(tempFilePath.Replace(".mp4", "_summary.txt")); } catch { }
                }
            }
            else
            {
                Logger.Log("Temp file not found after recording stopped.");
            }
        }

        private async void OnHotkeyPressed(object sender, EventArgs e)
        {
            try
            {
                Logger.Log($"Hotkey pressed. Processing... Current Recording State: {CurrentRecordingService?.IsRecording}");
                if (CurrentRecordingService.IsRecording)
                {
                    await TriggerStopRecording();
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
                        _recordingStartedAt = DateTime.UtcNow;
                        _trayTooltipTimer.Start();
                        _stopRecordingMenuItem.Visible = true;
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
            _trayTooltipTimer?.Dispose();

            try { _mutex?.ReleaseMutex(); _mutex?.Dispose(); } catch { }
        }
    }
}
