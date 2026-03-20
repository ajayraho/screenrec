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
        private Process _ffmpegProcess;
        private string _tempFilePath;
        private string _tempAudioPath;
        private string _tempMicAudioPath;
        private string _finalTempPath;

        private WasapiLoopbackCapture _audioCapture;
        private WaveFileWriter _audioWriter;

        private WasapiCapture _micCapture;
        private WaveFileWriter _micWriter;

        public bool IsRecording { get; private set; }

        public string TempFilePath => _tempFilePath;

        public RecordingService()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "ScreenRecApp");
            if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);
            IsRecording = false;
        }

        public bool StartRecording()
        {
            if (IsRecording) return false;

            string tempDir = Path.Combine(Path.GetTempPath(), "ScreenRecApp");
            if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);

            string id = Guid.NewGuid().ToString();
            _tempFilePath = Path.Combine(tempDir, $"video_{id}.mp4");
            _tempAudioPath = Path.Combine(tempDir, $"sys_{id}.wav");
            _tempMicAudioPath = Path.Combine(tempDir, $"mic_{id}.wav");
            _finalTempPath = Path.Combine(tempDir, $"final_{id}.mp4");

            // START NAUDIO CAPTURE (SYSTEM)
            try
            {
                _audioCapture = new WasapiLoopbackCapture();
                _audioWriter = new WaveFileWriter(_tempAudioPath, _audioCapture.WaveFormat);
                _audioCapture.DataAvailable += (s, a) => { _audioWriter.Write(a.Buffer, 0, a.BytesRecorded); };
                _audioCapture.StartRecording();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "NAudio System Audio Start");
            }

            // START NAUDIO CAPTURE (MIC)
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

            int fps = SettingsManager.Settings.FramesPerSecond;
            string qualityArg = "-preset ultrafast";
            if (SettingsManager.Settings.VideoQuality == "High") qualityArg = "-preset fast -crf 23";
            else if (SettingsManager.Settings.VideoQuality == "Medium") qualityArg = "-preset veryfast -crf 28";
            else qualityArg = "-preset ultrafast -crf 32";

            // START VIDEO RECORDING
            var startInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg.exe",
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
                {
                    App._devWindow.ReportCommand(startInfo.FileName + " " + startInfo.Arguments);
                }

                _ffmpegProcess.ErrorDataReceived += (s, e) => { 
                    if (e.Data != null) 
                    {
                        Logger.Log($"[FFmpeg Video]: {e.Data}"); 
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

        public async Task<string> StopRecording()
        {
            if (!IsRecording) return null;

            try
            {
                // STOP NAUDIO SYSTEM
                try
                {
                    if (_audioCapture != null)
                    {
                        _audioCapture.StopRecording();
                        _audioCapture.Dispose();
                        _audioCapture = null;
                    }
                    if (_audioWriter != null)
                    {
                        _audioWriter.Dispose();
                        _audioWriter = null;
                    }
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

                // STOP VIDEO
                if (_ffmpegProcess != null && !_ffmpegProcess.HasExited)
                {
                    _ffmpegProcess.StandardInput.WriteLine("q");
                    await Task.Run(() => _ffmpegProcess.WaitForExit(5000));
                    
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

                    if (sysExists && micExists)
                    {
                        muxArgs = $"-y -i \"{_tempFilePath}\" -i \"{_tempAudioPath}\" -i \"{_tempMicAudioPath}\" -filter_complex \"[2:a]{volFilter}[mic_boost];[1:a][mic_boost]amix=inputs=2:duration=longest[a]\" -map 0:v -map \"[a]\" -c:v copy -c:a aac \"{_finalTempPath}\"";
                    }
                    else if (sysExists)
                    {
                        muxArgs = $"-y -i \"{_tempFilePath}\" -i \"{_tempAudioPath}\" -c:v copy -c:a aac \"{_finalTempPath}\"";
                    }
                    else if (micExists)
                    {
                        muxArgs = $"-y -i \"{_tempFilePath}\" -i \"{_tempMicAudioPath}\" -filter_complex \"[1:a]{volFilter}[a]\" -map 0:v -map \"[a]\" -c:v copy -c:a aac \"{_finalTempPath}\"";
                    }
                    else
                    {
                        File.Copy(_tempFilePath, _finalTempPath, true);
                    }

                    if (!string.IsNullOrEmpty(muxArgs))
                    {
                        var muxInfo = new ProcessStartInfo
                        {
                            FileName = "ffmpeg.exe",
                            Arguments = muxArgs,
                            UseShellExecute = false,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        };

                        if (App._devWindow != null)
                        {
                            App._devWindow.ReportCommand(muxInfo.FileName + " " + muxInfo.Arguments);
                        }

                        var muxProc = Process.Start(muxInfo);
                        if (muxProc != null)
                        {
                            muxProc.ErrorDataReceived += (s, e) => { if (e.Data != null) Logger.Log($"[FFmpeg MUX]: {e.Data}"); };
                            muxProc.BeginErrorReadLine();
                            await Task.Run(() => muxProc.WaitForExit(15000));
                        }
                    }

                    // CLEANUP TEMPS
                    try { File.Delete(_tempFilePath); } catch { }
                    try { File.Delete(_tempAudioPath); } catch { }
                    try { File.Delete(_tempMicAudioPath); } catch { }

                    Logger.Log("Muxing completed successfully.");
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
    }
}
