# ScreenRecApp (Sound Service Broker) - Consolidated Release Notes

## Version 2.0 -> AI Intelligence & Synchronization Overhaul

This major version introduces state-of-the-art on-device AI integration and completely rewrites the audio acoustic synchronization engine to support extreme edge cases.

### Advanced AI Integrations
- **LLM-Based Summarization:** Integrated the LLamaSharp framework to generate intelligent summaries of your transcriptions. This operates entirely on the CPU to guarantee cross-device compatibility without requiring dedicated GPUs. The system is engineered to gracefully understand bilingual Hinglish/English dialogues.
- **Dual-Stream Whisper Transcription:** Rewrote `TranscriptionHelper` to isolate and transcribe the microphone and system audio independently through 16kHz resampled streams. This prevents overlapping audio tracks from destructively interfering with Whisper's token evaluations, seamlessly stitching them together chronologically.
- **Whisper 'No Speech' Block Override:** Disabled Whisper.net's native heuristic that automatically drops entire 30-second processing windows if they contain "insufficient speech" (`WithNoSpeechThreshold(100f)`). This fixes a critical flaw where user speech scattered lightly across long periods of silence was erroneously discarded.
- **Whisper Hallucination Filtering:** Added aggressive regex filtering to strip natively hallucinated tokens like `[non-english speech]`, `[music]`, and trailing recursive noise strings completely off the final `.srt` and `.txt` records, alongside rigorous exact-trailing duplicate trimming to prevent infinite repetition loops.
- **Intelligent Dual-Cancellation Pipelines:** Converted the post-processing loading GUI to expose discrete "Cancel Transcription" and "Cancel Summarization" hooks, allowing the raw `.mp4` video to finalize instantly if you bypass AI steps.

### User Interface & Experience
- **Real-Time Bouncing Loading UI:** Rebuilt the `LoaderWindow` to render a native bouncing progress bar while tying an exact `XX%` numerical percentage readout directly to the underlying C++ Whisper execution loops in real-time.
- **Precision Plugin Configuration:** Expanded the Settings menu with an internal `Expander` drop-down for AI plugins. The system now automatically scans the `plugins/whisper` and `plugins/llm` root folders, rendering dynamic combo boxes so you can pinpoint the exact `.gguf` and `.bin` models your specific computer thrives with.
- **Glassmorphism 'About' Integration:** Injected a beautiful, corner-radiused Glassmorphism-themed "About" screen directly mapped to a new item in the system-tray context menu.

### Architecture & Stability
- **Global Acoustic Synchronization Fix:** Rebuilt the NAudio `WasapiLoopbackCapture` baseline to actively inject a `WhiteNoiseProvider` continuous loop at an inaudible volume. This completely overrides the Windows Audio Session API behavior which goes to "sleep" during absolute acoustic silence, which previously corrupted FFmpeg frame timings!
- **FFmpeg Null-Padding Alignments:** Rearchitected the background demuxing/mixing command using `apad` filters mapped against `duration=first` logic (`-filter_complex "amix=inputs=2:duration=first:dropout_transition=2:normalize=0"`). This guarantees system audio dynamically pads out perfectly to match microphone duration lengths, even if a user mutes their computer audio entirely mid-recording.
- **Concurrent Session Isolation:** Overhauled the `RecordingService` internal state paths by strictly scoping `_tempFilePath` variables locally per operation. You can now press the Record hotkey to start capturing immediately while the previous background AI job is still writing out your `.srt` files without them dangerously colliding!

---

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
- **Frame-Perfect Audio/Video Sync:** Re-engineered the synchronization pipeline to calculate precise timestamp deltas between the first video frame and the audio stream. Added a manual "Audio Sync" offset slider in settings for fine-tuning.
- **Dynamic Volume Adjustment:** Features an internal multiplier filter for mixing quiet microphones alongside loud desktop playback streams.

### End-User Features
- **Key Binding:** Removed static shortcut logic in favor of an interactive key listener menu. The global hook utilizes lightweight window sourcing (HwndSource) to prevent blocking conflicts with other key loggers and games.
- **File Naming Logic:** The default naming convention now utilizes a rigid `DD.MM.YY_HH.MM.SS_` fallback to eliminate multi-session overwriting.
- **Auto-Focused Naming:** The filename input box is automatically selected upon finishing a recording, allowing absolute immediate typing.
- **Tray Controls & Startup:** Added a dynamic right-click "Stop Recording" context menu to the tray icon and a native "Start with Windows" registry toggle. Hovering over the tray icon while recording now displays a live elapsed timer.
