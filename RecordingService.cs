using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using NAudio.Wave;
using NAudio.CoreAudioApi;

namespace ScreenRecApp
{
    public class RecordingService
    {
        // Custom provider that plays microscopic, mathematically non-zero analog hiss (-100dB).
        // It keeps WASAPI awake EXACTLY like a SilenceProvider, keeping system audio perfectly synchronized,
        // but completely eliminates the 0x00 absolute-zero bug that crashes Whisper's FFT decoders.
        private class WhiteNoiseProvider : IWaveProvider
        {
            private readonly WaveFormat _waveFormat;
            private readonly Random _random = new Random();

            public WhiteNoiseProvider(WaveFormat waveFormat) { _waveFormat = waveFormat; }
            public WaveFormat WaveFormat => _waveFormat;

            public int Read(byte[] buffer, int offset, int count)
            {
                if (_waveFormat.Encoding == WaveFormatEncoding.IeeeFloat)
                {
                    for (int i = 0; i < count; i += 4)
                    {
                        // Tiny floating point noise around 1e-6 (approx -100dB)
                        float noise = (float)((_random.NextDouble() * 2 - 1) * 0.00001);
                        byte[] bytes = BitConverter.GetBytes(noise);
                        Buffer.BlockCopy(bytes, 0, buffer, offset + i, 4);
                    }
                }
                else
                {
                    for (int i = 0; i < count; i += 2)
                    {
                        short noise = (short)_random.Next(-1, 2);
                        buffer[offset + i] = (byte)(noise & 0xFF);
                        buffer[offset + i + 1] = (byte)((noise >> 8) & 0xFF);
                    }
                }
                return count;
            }
        }
        private Process _ffmpegProcess;
        private string _tempFilePath;
        private string _tempAudioPath;
        private string _tempMicAudioPath;
        private string _finalTempPath;

        private WasapiLoopbackCapture _audioCapture;
        private WaveFileWriter _audioWriter;

        private WasapiCapture _micCapture;
        private WaveFileWriter _micWriter;

        // Hack to keep system audio awake so WasapiLoopbackCapture doesn't skip silence gaps
        private WasapiOut _silenceOut;

        public bool IsRecording { get; private set; }

        public string TempFilePath => _tempFilePath;

        // Sync tracking: NAudio starts immediately, FFmpeg takes 1-3s to init.
        // We measure the exact moment FFmpeg actually starts capturing frames
        // and trim that delta off the audio at mux time.
        private DateTime _audioStartTime;
        private DateTime _ffmpegFirstFrameTime;
        private bool _firstFrameReceived;

        public static string GetFFmpegPath()
        {
            // 1. Try App Directory
            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            string localPath = Path.Combine(exeDir, "ffmpeg.exe");
            if (File.Exists(localPath)) return localPath;

            // 2. Try Current Directory
            string cwd = Environment.CurrentDirectory;
            string cwdPath = Path.Combine(cwd, "ffmpeg.exe");
            if (File.Exists(cwdPath)) return cwdPath;

            // 3. System PATH fallback
            return "ffmpeg.exe";
        }

        public RecordingService()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "ScreenRecApp");
            if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);
            IsRecording = false;
        }

        public bool StartRecording()
        {
            if (IsRecording) return false;

            // Kill any leftover ffmpeg from a previous failed/crashed session
            if (_ffmpegProcess != null)
            {
                try { if (!_ffmpegProcess.HasExited) _ffmpegProcess.Kill(); } catch { }
                _ffmpegProcess.Dispose();
                _ffmpegProcess = null;
            }

            string tempDir = Path.Combine(Path.GetTempPath(), "ScreenRecApp");
            if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);

            string id = Guid.NewGuid().ToString();
            _tempFilePath = Path.Combine(tempDir, $"video_{id}.mp4");
            _tempAudioPath = Path.Combine(tempDir, $"sys_{id}.wav");
            _tempMicAudioPath = Path.Combine(tempDir, $"mic_{id}.wav");
            _finalTempPath = Path.Combine(tempDir, $"final_{id}.mp4");

            _firstFrameReceived = false;

            // START NAUDIO IMMEDIATELY — audio begins capturing right away.
            // We record the exact timestamp so we can trim the head at mux time.
            try
            {
                _audioCapture = new WasapiLoopbackCapture();
                _audioWriter = new WaveFileWriter(_tempAudioPath, _audioCapture.WaveFormat);
                _audioCapture.DataAvailable += (s, a) => { _audioWriter?.Write(a.Buffer, 0, a.BytesRecorded); };
                _audioCapture.StartRecording();
                _audioStartTime = DateTime.UtcNow;

                // REAL FIX FOR WASAPI JITTER/SILENCE COMPRESSION:
                // WasapiLoopbackCapture completely stops firing events when no audio plays.
                // By forcing the system to play a silent stream in the background via WasapiOut,
                // the Windows audio engine never sleeps. DataAvailable fires continuously in real-time,
                // padding sys.wav with perfectly synced real-time zero bytes naturally.
                try
                {
                    _silenceOut = new WasapiOut(NAudio.CoreAudioApi.AudioClientShareMode.Shared, 100);
                    _silenceOut.Init(new WhiteNoiseProvider(_audioCapture.WaveFormat));
                    _silenceOut.Play();
                }
                catch (Exception silenceEx)
                {
                    Logger.LogError(silenceEx, "Failed to start silence provider");
                }
            }
            catch (Exception ex)
            {
                _audioStartTime = DateTime.UtcNow;
                Logger.LogError(ex, "NAudio System Audio Start");
            }

            if (SettingsManager.Settings.CaptureMicAudio)
            {
                try
                {
                    _micCapture = new WasapiCapture();
                    _micWriter = new WaveFileWriter(_tempMicAudioPath, _micCapture.WaveFormat);
                    _micCapture.DataAvailable += (s, a) => { _micWriter.Write(a.Buffer, 0, a.BytesRecorded); };
                    _micCapture.StartRecording();
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "NAudio Mic Audio Start");
                }
            }

            // Log audio start time for dev mode visibility
            if (SettingsManager.Settings.DeveloperMode)
                Logger.Log($"[Sync] Audio capture started at {_audioStartTime:HH:mm:ss.fff} UTC");

            int fps = SettingsManager.Settings.FramesPerSecond;
            string qualityArg = "-preset ultrafast";
            if (SettingsManager.Settings.VideoQuality == "High") qualityArg = "-preset fast -crf 23";
            else if (SettingsManager.Settings.VideoQuality == "Medium") qualityArg = "-preset veryfast -crf 28";
            else qualityArg = "-preset ultrafast -crf 32";

            var startInfo = new ProcessStartInfo
            {
                FileName = GetFFmpegPath(),
                Arguments = $"-y -f gdigrab -framerate {fps} -i desktop -c:v libx264 {qualityArg} -pix_fmt yuv420p \"{_tempFilePath}\"",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            try
            {
                _ffmpegProcess = new Process { StartInfo = startInfo };
                _ffmpegProcess.EnableRaisingEvents = true;

                if (App._devWindow != null)
                    App._devWindow.ReportCommand(startInfo.FileName + " " + startInfo.Arguments);

                int frameLogCounter = 0;
                _ffmpegProcess.ErrorDataReceived += (s, e) => 
                { 
                    if (!string.IsNullOrWhiteSpace(e.Data)) 
                    {
                        // FFmpeg on Windows uses \r to overwrite the progress line in-place.
                        // ErrorDataReceived splits on \n but the data still has a leading \r.
                        // We must trim it before checking the content — this was the root bug.
                        string line = e.Data.TrimStart('\r', ' ');

                        // Capture the timestamp of the very first frame from gdigrab.
                        if (!_firstFrameReceived && (line.StartsWith("frame=") || line.StartsWith("size=")))
                        {
                            _ffmpegFirstFrameTime = DateTime.UtcNow;
                            _firstFrameReceived = true;
                            double lag = (_ffmpegFirstFrameTime - _audioStartTime).TotalSeconds;
                            Logger.Log($"[Sync] Video capture started at {_ffmpegFirstFrameTime:HH:mm:ss.fff} UTC");
                            Logger.Log($"[Sync] Audio lead: {lag:F3}s  --  this will be trimmed at mux.");
                        }

                        if (!line.StartsWith("frame=") && !line.StartsWith("size="))
                        {
                            if (SettingsManager.Settings.DeveloperMode)
                                Logger.Log($"[FFmpeg]: {line}"); 
                        }
                        else if (SettingsManager.Settings.DeveloperMode)
                        {
                            frameLogCounter++;
                            if (frameLogCounter % 30 == 0)
                                Logger.Log("Recording in progress...");
                        }
                    }
                };

                _ffmpegProcess.Start();
                _ffmpegProcess.BeginErrorReadLine();
                IsRecording = true;
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "FFmpeg Start");
                return false;
            }
        }

        public async Task<string> StopRecording(LoaderWindow loader = null)
        {
            if (!IsRecording) return null;

            using var cts = new System.Threading.CancellationTokenSource();
            if (loader != null)
            {
                loader.UpdateStatus("Optimizing video...");
                loader.CancelAIRequested += () => {
                    try { cts.Cancel(); } catch {}
                };
            }

            try
            {
                // STOP NAUDIO SYSTEM
                try
                {
                    if (_silenceOut != null)
                    {
                        try { _silenceOut.Stop(); _silenceOut.Dispose(); } catch { }
                        _silenceOut = null;
                    }

                    if (_audioCapture != null)
                    {
                        _audioCapture.StopRecording();
                        _audioCapture.Dispose();
                        _audioCapture = null;
                    }
                    _audioWriter?.Dispose();
                    _audioWriter = null;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "NAudio System Stop");
                }

                // STOP NAUDIO MIC
                try
                {
                    if (_micCapture != null)
                    {
                        _micCapture.StopRecording();
                        _micCapture.Dispose();
                        _micCapture = null;
                    }
                    if (_micWriter != null)
                    {
                        _micWriter.Dispose();
                        _micWriter = null;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "NAudio Mic Stop");
                }

                if (_ffmpegProcess != null && !_ffmpegProcess.HasExited)
                {
                    _ffmpegProcess.StandardInput.WriteLine("q");
                    
                    // CRITICAL FOR LONG VIDEOS: For a 2 hour recording, FFmpeg takes 10+ seconds 
                    // just to write the "moov atom" (MP4 video index) to the end of the file. 
                    // If we kill it too early, the 5GB MP4 file is instantly corrupted forever.
                    await Task.Run(() => _ffmpegProcess.WaitForExit(60000)); // wait up to 60 sec
                    
                    if (!_ffmpegProcess.HasExited)
                    {
                        _ffmpegProcess.Kill();
                    }
                }
                
                IsRecording = false;

                // MUX AUDIO(S) WITH VIDEO
                if (File.Exists(_tempFilePath))
                {
                    Logger.Log("Muxing video and audio...");
                    
                    bool sysExists = File.Exists(_tempAudioPath);
                    bool micExists = File.Exists(_tempMicAudioPath);
                    
                    double boost = SettingsManager.Settings.MicVolumeBoost;
                    string volFilter = boost != 1.0 ? $"volume={boost.ToString(System.Globalization.CultureInfo.InvariantCulture)}" : "volume=1.0";
                    string muxArgs = "";

                // SYNC COMPUTATION:
                // Auto-detected: delta between NAudio start and FFmpeg first frame.
                // Manual: user-configured offset from Settings (positive = audio was early).
                double autoTrimSeconds = 0;
                if (_firstFrameReceived && _ffmpegFirstFrameTime > _audioStartTime)
                    autoTrimSeconds = (_ffmpegFirstFrameTime - _audioStartTime).TotalSeconds;

                double manualTrimSeconds = SettingsManager.Settings.AudioSyncOffsetMs / 1000.0;
                double totalTrimSeconds = autoTrimSeconds + manualTrimSeconds;

                Logger.Log($"[Sync] Auto trim: {autoTrimSeconds:F3}s | Manual offset: {manualTrimSeconds:F3}s | Applied: {totalTrimSeconds:F3}s");

                // Positive total = audio started too early → trim audio head with -ss
                // Negative total = audio started too late → delay audio with -itsoffset 
                string audioTrim = "";
                if (totalTrimSeconds > 0.02)
                    audioTrim = $"-ss {totalTrimSeconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)} ";
                else if (totalTrimSeconds < -0.02)
                    audioTrim = $"-itsoffset {Math.Abs(totalTrimSeconds).ToString("F3", System.Globalization.CultureInfo.InvariantCulture)} ";

                    if (sysExists && micExists)
                    {
                        // apad extends sys.wav with silence so it is never shorter than mic.
                        // mic_boost is passed FIRST to amix → duration=first uses mic's length.
                        // normalize=0 keeps mic at full volume even when sys audio is silent.
                        muxArgs = $"-y -i \"{_tempFilePath}\" {audioTrim}-i \"{_tempAudioPath}\" {audioTrim}-i \"{_tempMicAudioPath}\" -filter_complex \"[1:a]apad[sys_full];[2:a]{volFilter}[mic_boost];[mic_boost][sys_full]amix=inputs=2:duration=first:normalize=0[a]\" -map 0:v -map \"[a]\" -c:v copy -c:a aac -b:a 192k -movflags +faststart \"{_finalTempPath}\"";
                    }
                    else if (sysExists)
                    {
                        muxArgs = $"-y -i \"{_tempFilePath}\" {audioTrim}-i \"{_tempAudioPath}\" -c:v copy -c:a aac -b:a 192k -movflags +faststart \"{_finalTempPath}\"";
                    }
                    else if (micExists)
                    {
                        muxArgs = $"-y -i \"{_tempFilePath}\" {audioTrim}-i \"{_tempMicAudioPath}\" -filter_complex \"[1:a]{volFilter}[a]\" -map 0:v -map \"[a]\" -c:v copy -c:a aac -b:a 192k -movflags +faststart \"{_finalTempPath}\"";
                    }
                    else
                    {
                        File.Copy(_tempFilePath, _finalTempPath, true);
                    }


                    if (!string.IsNullOrEmpty(muxArgs))
                    {
                        var muxInfo = new ProcessStartInfo
                        {
                            FileName = GetFFmpegPath(),
                            Arguments = muxArgs,
                            UseShellExecute = false,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        };

                        if (App._devWindow != null)
                        {
                            App._devWindow.ReportCommand(muxInfo.FileName + " " + muxInfo.Arguments);
                        }

                        using (var muxProc = Process.Start(muxInfo))
                        {
                            if (muxProc != null)
                            {
                                muxProc.ErrorDataReceived += (s, e) => { 
                                    if (!string.IsNullOrWhiteSpace(e.Data) && SettingsManager.Settings.DeveloperMode && !e.Data.StartsWith("size=")) 
                                        Logger.Log($"[FFmpeg MUX]: {e.Data}"); 
                                };
                                muxProc.BeginErrorReadLine();
                                
                                // Muxing a 2+ hour recording containing 5-7 GB of video and audio
                                // can take several minutes. Never time this out early. Wait up to 2 hours.
                                await Task.Run(() => muxProc.WaitForExit(7200000));
                                
                                if (!muxProc.HasExited) { try { muxProc.Kill(); } catch { } }
                                muxProc.CancelErrorRead();
                            }
                        }
                    }

                    // CLEANUP VIDEO/MIC TEMPS — but preserve raw audio WAVs until AFTER transcription
                    // because we feed the clean unmixed mic WAV straight to Whisper instead of 
                    // re-extracting from the degraded amix MP4 (which causes the 'only system audio transcribed' bug).
                    string savedMicPath  = micExists  ? _tempMicAudioPath : null;
                    string savedSysPath  = sysExists  ? _tempAudioPath    : null;
                    try { File.Delete(_tempFilePath); } catch { }

                    Logger.Log("Muxing completed successfully.");
                    
                    if (loader != null && !cts.IsCancellationRequested)
                    {
                        loader.UpdateStatus("Generating transcript...");
                        loader.ShowCancelAction(true);
                    }

                    string transcriptText = await TranscriptionHelper.GenerateTranscriptionsAsync(_finalTempPath, savedMicPath, savedSysPath, loader, cts.Token);
                    
                    if (loader != null && !cts.IsCancellationRequested && !string.IsNullOrWhiteSpace(transcriptText))
                    {
                        loader.UpdateStatus("Generating summary...");
                    }

                    await SummarizationHelper.GenerateSummaryAsync(_finalTempPath, transcriptText, cts.Token);

                    if (loader != null) 
                    {
                        loader.ShowCancelAction(false);
                        loader.UpdateStatus("Wrapping up...");
                    }

                    // Now safe to delete raw audio temps
                    try { File.Delete(_tempAudioPath); } catch { }
                    try { File.Delete(_tempMicAudioPath); } catch { }

                    CompactMemory(); // Aggressively free RAM after heavy recording session
                    return _finalTempPath;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Recording Stop/Muxing Error");
            }
            finally
            {
                if (_ffmpegProcess != null) 
                {
                    if (!_ffmpegProcess.HasExited) try { _ffmpegProcess.Kill(); } catch { }
                    _ffmpegProcess.Dispose();
                    _ffmpegProcess = null;
                }
                IsRecording = false;
            }

            return null; // Return null if muxing failed completely
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool SetProcessWorkingSetSize(IntPtr process, UIntPtr minimumWorkingSetSize, UIntPtr maximumWorkingSetSize);

        private void CompactMemory()
        {
            try
            {
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
                GC.WaitForPendingFinalizers();
                GC.Collect();
                SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle, (UIntPtr)0xFFFFFFFF, (UIntPtr)0xFFFFFFFF);
                Logger.Log("Memory compacted successfully.");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Memory Compaction");
            }
        }
    }
}
