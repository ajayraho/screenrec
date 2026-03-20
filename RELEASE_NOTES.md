# 🚀 ScreenRecApp (alias Runtime Broker) - v1.0.0 Release

High-performance, stealth-optimized screen recording suite for Windows.

## ✨ Key Features

### 👻 Stealth & Privacy (Ghost Protocol)
- **Assembly Spoofing**: The application runs under an alias `RuntimeBroker.exe` with Microsoft-signed metadata to remain inconspicuous in the Task Manager.
- **Generic Notifications**: System-style balloon tips appear from "Runtime Broker" with empty body text to avoid detection during screen shares.
- **Premium Minimalist UI**: Frameless, modern, dark-themed interface with custom draggable headers and rounded borders.

### 🎥 High-Performance Video
- **Dynamic Framerate**: Choose between 15, 30, or 60 FPS for ultra-smooth captures.
- **Quality Presets**: High, Medium, and Low encoding settings to balance file size and visual fidelity.
- **Real-time Processing**: Fast H.264 encoding via FFmpeg.

### 🎙️ Advanced Audio Suite (Teams-Ready)
- **Dual Stream Mixing**: Simultaneously captures both your system audio (Teams/Zoom participants) and your own microphone.
- **Mic Volume Boost**: Amplify low-sensitivity microphones natively from 1.0x to 10.0x during the muxing process.
- **Live Mic Tester**: Visual real-time sound-level meter in settings to verify input before recording.

### ⚙️ User Experience & Workflow
- **Robust Hotkey Management**: Redesigned checkbox-based modifier selection (`Win`, `Ctrl`, `Shift`, `Alt`) to prevent OS-level keyboard hook conflicts.
- **Intelligent File Saving**: Timestamped automatic naming with a history-aware filename suggestion box.
- **Standby Loader**: A minimal, subtle processing window shows progress when rendering finished recordings.
- **Auto-Cleanup**: Automatically deletes massive temporary raw files if a recording is cancelled.

## 📦 How to Run
1. This is a **Self-Contained** application. You do NOT need the .NET runtime installed.
2. Ensure `ffmpeg.exe` is in your system PATH or in the same directory as the executable.
3. Launch `RuntimeBroker.exe` and use your configured hotkey (Default: `Win + Alt + S`) to start/stop!

---
*Built with ❤️ for professional stealth recording.*
