using System;
using System.IO;
using System.Threading.Tasks;
using System.Text;
using LLama;
using LLama.Common;

namespace ScreenRecApp
{
    public static class SummarizationHelper
    {
        public static async Task GenerateSummaryAsync(string mp4Path, string transcript, System.Threading.CancellationToken ct = default)
        {
            if (!SettingsManager.Settings.GenerateSummary || string.IsNullOrWhiteSpace(transcript) || ct.IsCancellationRequested) return;

            string pluginsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins", "llm");
            if (!Directory.Exists(pluginsDir)) return;

            string[] models = Directory.GetFiles(pluginsDir, "*.gguf");
            if (models.Length == 0) return;

            string modelPath = models[0];

            try
            {
                Logger.Log($"Starting AI Summarization with model: {Path.GetFileName(modelPath)}");

                string prompt = $"<|user|>\nWritten in a mix of Hindi, Hinglish, and English, this dialogue contains expressions of happiness and casual remarks.\n\nYour task:\n1. Interpret the meaning beyond literal translations.\n2. Translate the entire dialogue into clear, formal English.\n3. Identify key points, decisions, and action items.\n4. Provide a summarized version with bullet points.\n\nDialogue:\n{transcript}\n<|end|>\n<|assistant|>\n";

                var parameters = new ModelParams(modelPath)
                {
                    ContextSize = 4096,
                    GpuLayerCount = 0 // CPU inference only to ensure universal compatibility
                };

                using var model = LLamaWeights.LoadFromFile(parameters);
                using var context = model.CreateContext(parameters);
                var executor = new InstructExecutor(context);

                var inferenceParams = new InferenceParams()
                {
                    MaxTokens = 1024,
                    AntiPrompts = new[] { "<|end|>", "<|user|>", "<|assistant|>", "User:", "==" }
                };

                string summaryPath = mp4Path.Replace(".mp4", "_summary.txt");
                using var writer = new StreamWriter(summaryPath, false, Encoding.UTF8);

                await foreach (var text in executor.InferAsync(prompt, inferenceParams, ct))
                {
                    if (ct.IsCancellationRequested) break;
                    await writer.WriteAsync(text);
                }

                Logger.Log($"Summary saved to {Path.GetFileName(summaryPath)}");
            }
            catch (OperationCanceledException)
            {
                Logger.Log("Summarization was cancelled.");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "LLM Summarization");
            }
        }
    }
}
