using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ScreenRecApp
{
    public class AppSettings
    {
        public string SavePath { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "screenrec");
        public List<string> History { get; set; } = new List<string>();
        public bool DeveloperMode { get; set; } = false;
        
        // Default: Shift (4) | Ctrl (2) | Win (8) = 14, Key Z (0x5A)
        public uint HotkeyModifiers { get; set; } = 0x000E;
        public uint HotkeyVirtualKey { get; set; } = 0x5A;
        public string HotkeyDisplayText { get; set; } = "Shift + Ctrl + Win + Z";
        
        // Recording Options
        public int NotificationTimerMinutes { get; set; } = 30;
        public string VideoQuality { get; set; } = "High"; // High, Medium, Low
        public int FramesPerSecond { get; set; } = 30;
        
        public double MicVolumeBoost { get; set; } = 1.0; // 1.0 = 100%
        public bool CaptureMicAudio { get; set; } = true;
        public int AudioSyncOffsetMs { get; set; } = -900; // ms to trim from audio head. Positive if audio is early.
        // Transcription Options
        public bool GenerateSubtitles { get; set; } = false;
        public bool GenerateTranscript { get; set; } = false;
        public bool GenerateSummary { get; set; } = false;
        public string TranscriptionLanguage { get; set; } = "auto";
        public string WhisperModelFile { get; set; } = ""; // Empty = auto-pick first .bin
        public string LlmModelFile { get; set; } = "";     // Empty = auto-pick first .gguf
    }

    public static class SettingsManager
    {
        private static readonly string SettingsFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ScreenRecApp", "settings.json");

        public static AppSettings Settings { get; private set; } = new AppSettings();

        public static void Load()
        {
            if (File.Exists(SettingsFilePath))
            {
                try
                {
                    string json = File.ReadAllText(SettingsFilePath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    if (settings != null)
                        Settings = settings;
                }
                catch (Exception)
                {
                    // Ignore errors, use defaults
                }
            }
        }

        public static void Save()
        {
            try
            {
                var directory = Path.GetDirectoryName(SettingsFilePath);
                if (!Directory.Exists(directory) && directory != null)
                    Directory.CreateDirectory(directory);

                string json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsFilePath, json);
            }
            catch (Exception)
            {
                // Ignore errors
            }
        }
    }
}
