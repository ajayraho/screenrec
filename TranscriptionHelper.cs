using System;
using System.IO;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Whisper.net;
using Whisper.net.Ggml;

namespace ScreenRecApp
{
    public class TaggedSegment
    {
        public SegmentData Segment { get; set; }
        public string Source { get; set; }
    }

    public static class TranscriptionHelper
    {
        public static async Task<string> GenerateTranscriptionsAsync(string mp4Path, string micWavPath = null, string sysWavPath = null, LoaderWindow loader = null, System.Threading.CancellationToken ct = default)
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
            
            // Collect audio sources to transcribe
            var sources = new List<(string Name, string Path)>();
            if (!string.IsNullOrEmpty(micWavPath) && File.Exists(micWavPath))
                sources.Add(("Mic", micWavPath));
            if (!string.IsNullOrEmpty(sysWavPath) && File.Exists(sysWavPath))
                sources.Add(("System", sysWavPath));

            // Fallback: extract from video if no raw audio available
            if (sources.Count == 0)
                sources.Add(("Mixed", mp4Path));

            var tempWavPathsToClean = new List<string>();

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

                Logger.Log("[Transcription] Transcribing sources sequentially...");

                var taggedSegments = new List<TaggedSegment>();

                // Whisper emits these special tokens when it refuses to transcribe a segment.
                // We must filter them out entirely — they are NOT real transcript content.
                var hallucinationTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "[video playback]", "[non-english speech]", "[blank_audio]",
                    "[music]", "[applause]", "[laughter]", "[noise]",
                    "(non-english speech)", "(music)", "(applause)"
                };

                foreach (var source in sources)
                {
                    // Create a brand new Processor instance for EVERY source file!
                    // If you reuse the same processor, Whisper completely hallucinates on the second file
                    // because it tries to use the acoustic context from the first file.
                    using var processor = builder.Build();

                    string tempWav = mp4Path.Replace(".mp4", $"_{source.Name}_whisper.wav");
                    tempWavPathsToClean.Add(tempWav);

                    Logger.Log($"[Transcription] Resampling {source.Name} audio '{Path.GetFileName(source.Path)}' → 16kHz mono PCM");

                    // Resample to exact 16kHz mono PCM
                    using (var process = new Process())
                    {
                        process.StartInfo.FileName = RecordingService.GetFFmpegPath();
                        // Added -af dynaudnorm to dynamically boost quiet system/mic audio to standard loudness.
                        // Loopback capture depends on Windows volume sliders, so it's often too quiet for Whisper.
                        process.StartInfo.Arguments = $"-y -i \"{source.Path}\" -vn -ar 16000 -ac 1 -c:a pcm_s16le -af \"dynaudnorm=p=0.9:m=100:s=5:g=5\" \"{tempWav}\"";
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

                    if (!File.Exists(tempWav)) continue;

                    Logger.Log($"[Transcription] Transcribing {source.Name} audio...");

                    using (var fileStream = File.OpenRead(tempWav))
                    {
                        string lastAddedText = "";
                        await foreach (var segment in processor.ProcessAsync(fileStream, ct))
                        {
                            string rawText = segment.Text.Trim();
                            if (string.IsNullOrWhiteSpace(rawText)) continue;

                            // Drop Whisper hallucination placeholder tokens
                            if (hallucinationTokens.Contains(rawText)) continue;
                            if (rawText.StartsWith("[") && rawText.EndsWith("]")) continue;
                            if (rawText.StartsWith("(") && rawText.EndsWith(")")) continue;

                            // Drop exact/trailing duplicates
                            if (rawText == lastAddedText) continue;
                            if (rawText.Length > 15 && lastAddedText.EndsWith(rawText)) continue;

                            taggedSegments.Add(new TaggedSegment { Segment = segment, Source = source.Name });
                            lastAddedText = rawText;
                        }
                    }
                }

                // Chronological merge of all segments from both mic and system audio
                var sortedSegments = taggedSegments.OrderBy(s => s.Segment.Start).ToList();
                var fullText = new StringBuilder();
                foreach (var ts in sortedSegments)
                {
                    fullText.AppendLine($"{ts.Segment.Text.Trim()}");
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
                    for (int i = 0; i < sortedSegments.Count; i++)
                    {
                        var ts = sortedSegments[i];
                        writer.WriteLine(i + 1);
                        writer.WriteLine($"{FormatTime(ts.Segment.Start)} --> {FormatTime(ts.Segment.End)}");
                        writer.WriteLine($"{ts.Segment.Text.Trim()}");
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
                // Delete ALL temporary downsampled Whisper WAVs
                foreach (string tmpPath in tempWavPathsToClean)
                {
                    try { File.Delete(tmpPath); } catch { }
                }
            }
            
            return resultText;
        }

        private static string FormatTime(TimeSpan time)
        {
            return $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00},{time.Milliseconds:000}";
        }
    }
}
