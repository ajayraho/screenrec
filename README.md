# 🎙️ Sound Service Broker (ScreenRecApp)

A **stealth-focused, highly optimized hardware-accelerated screen & audio recorder** built with C#, WPF, and FFmpeg. 

It runs invisibly in your Windows System Tray (disguised as "Sound Service Broker" to avoid raising suspicion) and operates entirely via a hidden Global Hotkey.

---

## ✨ Features

- **🥷 Stealth Operation**: Appears in the system tray as "Sound Service Broker" with an innocuous system icon. Windows notifications never mention recording.
- **⚡ Instant Global Hotkeys**: Uses raw, low-level Windows API hooks (HwndSource) to ensure your hotkey (`Ctrl + Shift + Z` by default) works instantly from anywhere—even inside fullscreen games.
- **🎧 Dual Audio Capture**: Uses NAudio (WASAPI) to perfectly capture both **System Audio** and your **Microphone** simultaneously, automatically mixing them into the final video.
- **🎙️ Mic Boost**: Custom audio filter built-in with volume multiplier for quiet microphones.
- **🚀 Ultra-Optimized Resource Usage**: 
  - Prevents FFmpeg log-spam to keep disk I/O and CPU usage near 0% during background running.
  - Aggressive `SetProcessWorkingSetSize` memory compaction to forcefully free RAM the moment a recording finishes.
- **⏱️ Built for Long Recordings**: Can comfortably capture 3+ hour meetings with custom graceful shutdown timeouts that prevent the `.mp4` from corrupting.
- **💻 Premium Frameless UI**: A beautiful, custom-designed dark mode interface for the Settings menu and Developer Console.
- **🌐 Web-Ready Video**: Automatically applies `-movflags +faststart` and encodes audio to `192k AAC` so resulting videos stream instantly on Discord or Google Drive without downloading first!

---

## 🛠️ Requirements & Installation

This is a **100% portable program** built as a self-contained release. **No installer or .NET runtime is required.**

1. Download the latest Release `.zip` file from the repository.
2. Extract it into any folder on your PC.
3. The release **already contains** the main `SoundServiceBroker.exe` program and the required `ffmpeg.exe` file bundled together.
4. Simply double-click `SoundServiceBroker.exe` to run it instantly.

---

## 🎮 How to Use

1. **Launch it:** It will start silently. You will see a small, generic "Information" icon in your Windows system tray.
2. **Settings Menu:** Double-click the tray icon (or right-click → Settings) to configure:
   - Your secret global hotkey.
   - Video Quality (`High / Medium / Low`).
   - Frames Per Second (FPS).
   - Mic boost.
   - Developer logging.
3. **Start Recording:** Press your global hotkey. It will instantly start recording in the background. A stealthy "reminder" balloon will occasionally pop up just to let you know the system is alive.
4. **Stop Recording:** Press your global hotkey again. You'll briefly see an indeterminate progress bar while the core system muxes your audio and video. 
5. **Save:** A prompt will ask you to name the file (pre-populated with a second-precision timestamp to prevent collisions).

---

## 👨‍💻 Developer Mode

If you're troubleshooting or curious about the FFmpeg pipelines:
- Open Settings and enable **Developer Mode**.
- The next time you press your shortcut, the **Broker Monitor** window will appear. 
- It tracks FFmpeg pipeline status, live commands, and frame health at exactly 1 ping per second to prevent UI choking.

---

## 🏗️ Build from Source

To compile this project yourself, you will need the [.NET 9.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/9.0) installed.

```bash
# Clone the repository
git clone https://github.com/ajayraho/screenrec.git
cd screenrec/ScreenRecApp

# Publish as a self-contained ReadyToRun executable
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishReadyToRun=true
```

Then drop `ffmpeg.exe` next to the compiled `.exe` inside `bin/Release/net9.0-windows/win-x64/publish/`.

---

*Note: This tool uses `gdigrab` to capture the desktop directly, meaning it bypasses most simple capture blocks but may still appear as a black screen when recording DRM-protected content (like Netflix/Hulu apps) due to hardware-level HDCP.*

---

Made with ❤️ by Ajit K.