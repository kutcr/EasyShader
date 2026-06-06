#nullable disable
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace EasyShader
{
    public partial class RefineWindow : Window
    {
        public string ResultPrompt { get; set; } = "";
        private System.Collections.Generic.List<object> _chatHistory = new System.Collections.Generic.List<object>();
        private Action<string> _applyCallback;

        public RefineWindow(string current, Action<string> onApply)
        {
            InitializeComponent();
            InputBox.Text = current;
            _applyCallback = onApply;

            string sys = @"You are a professional Shader Concept Artist. 
Your task is to transform simple user ideas into vivid, descriptive, and high-quality English prompts for HLSL generation.
FOCUS ON:
1. Cinematic lighting (volumetric, neon, glow).
2. Organic textures (fractal, liquid, noise-based).
3. Rhythmic motion (pulsing, flowing, swirling).
4. Color harmony (complementary colors, gradients).

RULES:
- Output ONLY a single paragraph of rich English description.
- DO NOT use bullet points.
- DO NOT mention HLSL variables, code, or math formulas.
- DO NOT use Chinese.";

            _chatHistory.Add(new { role = "system", content = sys });
            ChatBox.Text = "你好！我是你的提示词优化助手。";
        }

        private void InputBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers != ModifierKeys.Shift)
            {
                e.Handled = true; Send_Click(null, null);
            }
        }

        private async void Send_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(InputBox.Text)) return;
            string msg = InputBox.Text;
            string you = AppConfig.IsEN ? "You: " : "你: ";
            string ai = AppConfig.IsEN ? "Assistant: " : "助手: ";
            ChatBox.Text += $"\n\n{you}{msg}\n{ai}";
            _chatHistory.Add(new { role = "user", content = msg });
            InputBox.Text = "";
            Button btn = sender as Button; if (btn != null) btn.IsEnabled = false;

            try
            {
                StringBuilder sb = new StringBuilder();
                var body = new { model = AppConfig.Data.ModelName, messages = _chatHistory, stream = true, temperature = 0.7 };

                await Task.Run(async () => {
                    using var client = new HttpClient();
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {AppConfig.Data.ApiKey}");
                    var req = new HttpRequestMessage(HttpMethod.Post, AppConfig.GetValidApiUrl()) { Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") };
                    using var resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
                    using var sr = new StreamReader(await resp.Content.ReadAsStreamAsync());
                    while (!sr.EndOfStream)
                    {
                        var l = await sr.ReadLineAsync();
                        if (l != null && l.StartsWith("data: ") && l != "data: [DONE]")
                        {
                            using var j = JsonDocument.Parse(l.Substring(6));
                            if (j.RootElement.GetProperty("choices")[0].GetProperty("delta").TryGetProperty("content", out var c))
                            {
                                string chunk = c.GetString() ?? "";
                                Dispatcher.Invoke(() => {
                                    sb.Append(chunk); ChatBox.Text += chunk; ChatBox.ScrollToEnd();
                                });
                            }
                        }
                    }
                });

                _chatHistory.Add(new { role = "assistant", content = sb.ToString() });
                InputBox.Text = sb.ToString().Trim();
            }
            catch { ChatBox.Text += "\n[Service Error]"; }
            finally { if (btn != null) btn.IsEnabled = true; }
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            _applyCallback?.Invoke(InputBox.Text);

            if (this.Owner != null)
            {
                this.Owner.Activate();
                this.Owner.Focus();
            }
            this.Close();
        }
    }
}