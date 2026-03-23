<div align="center">

<img src="app_icon.ico" alt="App Icon" width="96" />

# Sound Service Broker (ScreenRec)

![C#](https://img.shields.io/badge/C%23-11.0+-blue.svg)
![.NET](https://img.shields.io/badge/.NET-9.0-purple.svg)
![WPF](https://img.shields.io/badge/WPF-UI-blueviolet.svg)
![FFmpeg](https://img.shields.io/badge/FFmpeg-6.0+-green.svg)
![Whisper](https://img.shields.io/badge/Whisper.net-AI-orange.svg)
![LLamaSharp](https://img.shields.io/badge/LLamaSharp-LLM-red.svg)

*A high-performance, stealth-oriented Windows screen recording utility engineered to operate entirely as a lightweight background service with on-device AI transcription.*

[**Why ScreenRec?**](#why-use-sound-service-broker) •
[**Capabilities**](#core-capabilities) •
[**AI Features**](#version-2-ai-features) •
[**Installation**](#system-requirements-and-installation) •
[**Usage**](#configuration-and-usage)

</div>

---

### Why use Sound Service Broker?

* **Absolute Discretion:** Designed for professionals who need zero friction. There are no splash screens, floating control bars, or noisy notifications. The application runs strictly from the system tray under a generic utility name, protecting your privacy during screen shares or presentations.
* **On-Device Artificial Intelligence:** You shouldn't have to upload massive video files to cloud providers just to get a transcript. This utility ships with direct C++ hooks into **Whisper.net** and **LLamaSharp**, generating localized subtitles, `.txt` transcripts, and intelligent bilingual action-item summaries entirely on your CPU.
* **Flawless Output Delivery:** Video files generated are instantly ready for uploading or sharing. FFmpeg processes native AAC audio matrices and flags the `.mp4` packaging with Fast-Start headers for immediate web playback, eliminating the need for post-processing tools like Handbrake.
* **Global Accessibility:** Engineered with low-latency Windows API hooks (HwndSource), allowing you to trigger recordings from within full-screen games, remote desktop sessions, or heavy applications without alt-tabbing.

---

## Technical Overview

Operated entirely via custom global hotkeys, the architecture captures raw desktop frames via `gdigrab` and independent loopback/microphone feeds via `NAudio (WASAPI)`.

### Core Capabilities

*   **Inconspicuous Operation:** The process is registered and displayed as "Sound Service Broker" in the system tray and Task Manager. System notifications regarding capture events use generic text to avoid drawing attention during screen shares.
*   **Low-latency Global Hotkeys:** Registers a system-wide hook (HwndSource) to ensure the configured shortcut (default: Shift + Ctrl + Win + Z) works globally, including full-screen applications.
*   **Dual-Channel Audio:** Captures both system loopback audio and microphone input simultaneously. The application includes an optional microphone software volume booster, and a toggle to disable microphone capture entirely if strict system-audio-only recording is required.
*   **Performance Tracking:** Includes a Developer Monitor console that logs FFmpeg pipeline status. To preserve CPU and disk I/O, statistical per-frame logging is throttled by default unless explicitly needed for debugging.
*   **Long-Duration Reliability:** Contains specific logic to prevent MP4 file corruption on long recordings (e.g., 2+ hours). It forces garbage collection and trims the application working set immediately after muxing to ensure idle memory usage remains negligible (near 20MB).
*   **Web-Optimized Output:** The muxing phase automatically applies the `-movflags +faststart` flag and encodes audio to 192kbps AAC, resulting in videos that can be streamed immediately upon uploading.
*   **Collision-Proof File Saving:** Generates suggested file names using a strict DD.MM.YY_HH.MM.SS_ format.

### Version 2 AI Features

*   **On-Device Transcription:** Integrates Whisper.net natively to execute cross-stream transcription (capturing both microphone and system audio without overlapping corruption). Generates sidecar `.srt` files entirely offline.
*   **LLM-Based Summarization:** Connects seamlessly into LLamaSharp to evaluate generated transcripts and output clean, bilingual (Hinglish/English) summaries to `.txt` files directly using CPU inference.
*   **Dynamic Plugin Routing:** Scans the custom `/plugins/whisper` and `/plugins/llm` folders to dynamically map all discovered `.bin` and `.gguf` AI files. End-users can precisely configure which weights they want mapped at runtime via the settings panel!
*   **Segmented Cancellation Pipelines:** Full execution isolation allows users to natively cancel "Transcription" and/or "Summarization" separately across independent CancellationToken boundaries without corrupting the finalized MP4 recording out of FFmpeg.

## System Requirements and Installation

The application is distributed as a self-contained, portable executable. It does not require a .NET runtime installation.

1.  Download the latest release archive.
2.  Extract the contents to a standard directory.
3.  Ensure `ffmpeg.exe` is present in the exact same directory as `SoundServiceBroker.exe`. This is bundled by default in the official release package.
4.  Run `SoundServiceBroker.exe`.

### Setting up AI Plugins (Optional)

The core screen recording application functions perfectly out-of-the-box without AI models. 
However, to activate the post-processing Transcription and Summarization pipelines, you must provide your own offline weights:

1. **Whisper Transcription:** Download any standard Whisper `ggml` model. We recommend **`ggml-medium.bin`** for accurate Hinglish/English translation, or `ggml-base.bin` for sheer speed. Place the downloaded `.bin` file inside the `plugins/whisper` directory.
2. **LLM Summarization:** Download a CPU-optimized GGUF architecture model. We strongly recommend **`Phi-3-mini-4k-instruct-q4.gguf`** (or a highly-quantized Llama 3 8B model) to strike the perfect balance between prompt comprehension and RAM usage. Place the downloaded `.gguf` file inside the `plugins/llm` directory.
3. Double-click the tray icon to open **Settings**, expand the **Advanced AI Plugins** panel, and explicitly select your loaded models from the dropdown arrays!

### AI System Requirements

Because ScreenRec operates entirely offline to protect privacy, executing AI heavily scales off your local hardware:
* **Storage:** ~1.5GB for `ggml-medium.bin` and ~2.4GB for a Q4 `Phi-3` model.
* **Memory (RAM):** 8GB absolute minimum. **16GB is highly recommended** because LLM summarization utilizes CPU execution to guarantee universal compatibility across graphical architectures.
* **Processor:** Multi-core CPUs (e.g., modern Ryzen or Intel i5/i7) will process the audio waveforms exponentially faster.

## Configuration and Usage

Upon starting, the application runs silently in the system tray. 

**Settings Configuration**
Double-click the tray icon to open the configuration window. Available settings include:
*   Customizing the global shortcut key and modifiers.
*   Video quality presets (High, Medium, Low) and capture framerate (15, 30, 60 FPS).
*   Microphone toggle and volume multiplier.
*   Audio sync calibration slider and 'Start with Windows' toggle.
*   Enabling or disabling Developer logging.

<p align="center">
  <img src="readme/settings.png" alt="Settings Interface" width="500" />
</p>

**Recording Lifecycle**
1.  Press the global hotkey to initiate capture. 
2.  While recording, hovering over the tray icon will display the live elapsed time.
3.  Press the hotkey again (or right-click the tray icon and select "Stop Recording") to stop. A progress window will indicate that the application is actively mixing the audio and video tracks. 
4.  Once processing is complete, a prompt allows you to choose the final file name (auto-focused for immediate typing) and save location.

If "Developer Mode" is enabled in settings, the Monitor Console will appear automatically when recording begins, displaying FFmpeg commands and a throttled activity heartbeat.

<p align="center">
  <img src="readme/dev.png" alt="Developer Console" width="600" />
</p>

## Building from Source

To compile the application manually, the .NET 9.0 SDK is required.

```bash
git clone https://github.com/ajayraho/screenrec.git
cd screenrec/ScreenRecApp

dotnet publish -c Release -r win-x64 --self-contained true -p:PublishReadyToRun=true
```

Ensure `ffmpeg.exe` is placed in the output directory (`bin/Release/net9.0-windows/win-x64/publish/`) alongside the executable before running.

*Note: Desktop capture relies on the gdigrab input device. Recording hardware-encrypted DRM streams (e.g., Netflix via Edge) may result in a black screen due to OS-level HDCP protections.*

---

<div align="center">

Made with ❤️ by Ajit K.

</div>