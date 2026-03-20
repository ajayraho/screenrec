# ScreenRecApp (Sound Service Broker) - Consolidated Release Notes

## Version 1.0 -> Final Release Build

This document outlines the complete transition of ScreenRecApp into a silent, robust, and highly-optimized system tray recording utility, branded for stealth as "Sound Service Broker".

### Application Architecture and Stability Updates
- **Single-File Execution Overhaul:** Moved away from single-file extraction models that caused blocking and loading delays. The release is now bundled as a standard portable directory containing the executable and `ffmpeg.exe`, reducing startup time to zero.
- **Single-Instance Mutex Enforcement:** Implemented a system-wide Mutex. Attempting to open the application twice now cleanly aborts without leaving background zombie processes. 
- **Graceful Thread Handling:** Initializing audio devices (WASAPI) has been moved off the UI thread and encapsulated in asynchronous tasks. This prevents the application from showing a "Not Responding" state when hardware drivers are slow to initialize.
- **Robust Shutdown Modes:** Transitioned the application shutdown mode to explicit triggers preventing accidental closures when interacting with settings.

### Stealth and UI Redesign
- **Application Renaming:** The core assembly and executable are now named `SoundServiceBroker.exe` to blend seamlessly into the Windows task manager.
- **Notification Adjustments:** All systemic balloon tooltips have had arbitrary body text removed, and only pulse a generic "Sound Service Broker" alert during active capture.
- **Premium Frameless Interfaces:** Implemented custom drag-and-drop borders, dark theme elements, and pixel-snapping across the Setting and Developer windows instead of default Windows borders.
- **Status Indicators:** Integrated a dynamic connection/status dot within the developer window that tracks the application's active capture state.

### FFmpeg and Muxing Optimizations
- **Extended Grace Periods:** Muxing timeouts have been fundamentally increased. The software will wait up to two hours for large files to mix, and provides FFmpeg 60 seconds to commit the MP4 "moov atom" upon exit, ensuring multi-hour recordings never corrupt on save.
- **Log Spam Remediation:** FFmpeg's verbose statistical outputs have been heavily throttled. Frame-level logging is disabled; a single ping replaces thousands of rows to drastically reduce disk I/O and processor usage down to 0% idle capacity.
- **Dynamic Encoding Parameters:** The muxer outputs have been updated to encode 192k bitrate AAC and utilize the `-movflags +faststart` flag, explicitly preparing generated files for immediate web streaming without post-processing.
- **Absolute Cleanup:** Implemented aggressive garbage collection and working set trim routines `SetProcessWorkingSetSize` from the Windows graphical kernel. The host immediately drops back to minimal background RAM levels after heavy muxing operations.

### Audio Pipeline Enhancements
- **Discretionary Microphone Capture:** A setting has been introduced allowing users to explicitly toggle microphone capture on or off independently from system playback.
- **Live Output Tester:** Included a live audio-level meter inside the settings screen to verify microphone capture devices prior to recording.
- **Dynamic Volume Adjustment:** Features an internal multiplier filter for mixing quiet microphones alongside loud desktop playback streams.

### End-User Features
- **Key Binding:** Removed static shortcut logic in favor of an interactive key listener menu. The global hook utilizes lightweight window sourcing (HwndSource) to prevent blocking conflicts with other key loggers and games.
- **File Naming Logic:** The default naming convention now utilizes a rigid `DD.MM.YY_HH.MM.SS_` fallback to eliminate multi-session overwriting.
