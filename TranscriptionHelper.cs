using System;
using System.IO;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text;
using System.Collections.Generic;
using Whisper.net;
using Whisper.net.Ggml;

namespace ScreenRecApp
{
    public static class TranscriptionHelper
    {
        public static async Task<string> GenerateTranscriptionsAsync(string mp4Path, string rawWavPath = null, LoaderWindow loader = null, System.Threading.CancellationToken ct = default)
        {
            if (!SettingsManager.Settings.GenerateSubtitles && !SettingsManager.Settings.GenerateTranscript && !SettingsManager.Settings.GenerateSummary)
                return "";

            if (ct.IsCancellationRequested) return "";

            string pluginsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins", "whisper");
            if (!Directory.Exists(pluginsDir)) return "";

            string[] models = Directory.GetFiles(pluginsDir, "*.bin");
            if (models.Length == 0) return "";
            
            string modelPath = models[0];

            Logger.Log($"Starting AI Transcription with model: {Path.GetFileName(modelPath)}");
            
            string ffmpegPath = RecordingService.GetFFmpegPath();
            // Always produce a clean 16kHz/mono/PCM-16 temp WAV for Whisper.
            // Whisper.net's WaveParser rejects anything that isn't exactly this format.
            string whisperWavPath = mp4Path.Replace(".mp4", "_whisper.wav");

            // Source priority: raw mic WAV → raw sys WAV → extract from final MP4
            string sourceForWhisper = (!string.IsNullOrEmpty(rawWavPath) && File.Exists(rawWavPath))
                ? rawWavPath
                : mp4Path;

            Logger.Log($"[Transcription] resampling '{Path.GetFileName(sourceForWhisper)}' → 16kHz mono PCM");

            using (var process = new Process())
            {
                process.StartInfo.FileName = ffmpegPath;
                // -vn skips video (safe for both WAV and MP4 sources)
                process.StartInfo.Arguments = $"-y -i \"{sourceForWhisper}\" -vn -ar 16000 -ac 1 -c:a pcm_s16le \"{whisperWavPath}\"";
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.UseShellExecute = false;
                process.Start();
                try {
                    await process.WaitForExitAsync(ct);
                } catch (OperationCanceledException) {
                    if (!process.HasExited) process.Kill();
                    return "";
                }
            }

            string wavPath = whisperWavPath;

            if (!File.Exists(wavPath))
            {
                Logger.Log("Transcription Error: Failed to extract WAV.");
                return "";
            }

            string resultText = "";
            try
            {
                // 2. Initialize Whisper
                using var whisperFactory = WhisperFactory.FromPath(modelPath);

                string targetLang = SettingsManager.Settings.TranscriptionLanguage;

                // CRITICAL: Whisper.net's .WithLanguage() does NOT accept "auto" as a valid token.
                // Passing "auto" likely resolves to an undefined/wrong language internally.
                // True auto-detection = simply don't call .WithLanguage() at all.
                var builder = whisperFactory.CreateBuilder();
                if (!string.IsNullOrEmpty(targetLang) && targetLang != "auto")
                {
                    builder = builder.WithLanguage(targetLang);
                    Logger.Log($"[Transcription] language locked to '{targetLang}'");
                }
                else
                {
                    Logger.Log("[Transcription] language auto-detection enabled (no constraint)");
                }

                // A short mixed-script prompt biases the token decoder toward Hindi+English phoneme space.
                // This is the correct use of .WithPrompt() — minimal, factual, not a sentence.
                // Critically, it prevents the [NON-ENGLISH SPEECH] suppression token from firing,
                // because the model now "expects" to hear Hindi alongside English content.
                builder = builder.WithPrompt("Hindi English");

                Logger.Log("[Transcription] in progress...");
                using var processor = builder.Build();

                var segments = new List<SegmentData>();
                var fullText = new StringBuilder();

                // Whisper emits these special tokens when it refuses to transcribe a segment.
                // We must filter them out entirely — they are NOT real transcript content.
                var hallucinationTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "[video playback]", "[non-english speech]", "[blank_audio]",
                    "[music]", "[applause]", "[laughter]", "[noise]",
                    "(non-english speech)", "(music)", "(applause)"
                };

                using (var fileStream = File.OpenRead(wavPath))
                {
                    string lastAddedText = "";
                    await foreach (var segment in processor.ProcessAsync(fileStream, ct))
                    {
                        string rawText = segment.Text.Trim();
                        if (string.IsNullOrWhiteSpace(rawText)) continue;

                        // Drop Whisper hallucination placeholder tokens
                        if (hallucinationTokens.Contains(rawText)) continue;
                        // Also drop tokens wrapped in brackets/parens dynamically
                        if (rawText.StartsWith("[") && rawText.EndsWith("]")) continue;
                        if (rawText.StartsWith("(") && rawText.EndsWith(")")) continue;

                        // Drop exact/trailing duplicates (silence loop defence)
                        if (rawText == lastAddedText) continue;
                        if (rawText.Length > 15 && lastAddedText.EndsWith(rawText)) continue;

                        segments.Add(segment);
                        fullText.AppendLine(rawText);
                        lastAddedText = rawText;
                    }
                }
                
                resultText = fullText.ToString();

                // 3. Write Output Files
                if (SettingsManager.Settings.GenerateTranscript)
                {
                    string txtPath = mp4Path.Replace(".mp4", "_transcript.txt");
                    using var writer = new StreamWriter(txtPath, false, Encoding.UTF8);
                    writer.Write(resultText);
                    Logger.Log($"Transcript saved to {Path.GetFileName(txtPath)}");
                }

                if (SettingsManager.Settings.GenerateSubtitles)
                {
                    string srtPath = mp4Path.Replace(".mp4", ".srt");
                    using var writer = new StreamWriter(srtPath, false, Encoding.UTF8);
                    for (int i = 0; i < segments.Count; i++)
                    {
                        var s = segments[i];
                        writer.WriteLine(i + 1);
                        writer.WriteLine($"{FormatTime(s.Start)} --> {FormatTime(s.End)}");
                        writer.WriteLine(s.Text.Trim());
                        writer.WriteLine();
                    }
                    Logger.Log($"Subtitles saved to {Path.GetFileName(srtPath)}");
                }
            }
            catch (OperationCanceledException)
            {
                Logger.Log("Transcription was cancelled.");
                return "";
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Whisper Transcription");
            }
            finally
            {
                // Always delete the resampled temp WAV we created for Whisper
                try { File.Delete(wavPath); } catch { }
            }
            
            return resultText;
        }

        private static string FormatTime(TimeSpan time)
        {
            return $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00},{time.Milliseconds:000}";
        }
    }
}
