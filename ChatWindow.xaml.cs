#nullable disable
using System;
using System.Collections.Generic;
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
    public partial class ChatWindow : Window
    {
        private List<object> _chatHistory = new List<object>();

        public ChatWindow()
        {
            InitializeComponent();
            ChatBox.Text = AppConfig.GetString("s_ChatGreeting");
            _chatHistory.Add(new { role = "system", content = "You are a helpful AI assistant." });
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
            string ai = AppConfig.IsEN ? "AI: " : "助手: ";
            ChatBox.Text += $"\n\n{you}{msg}\n{ai}";
            _chatHistory.Add(new { role = "user", content = msg }); InputBox.Text = "";
            Button btn = sender as Button; if (btn != null) btn.IsEnabled = false;

            try
            {
                StringBuilder sb = new StringBuilder();
                var body = new { model = AppConfig.Data.ModelName, messages = _chatHistory, stream = true };
                await Task.Run(async () => {
                    using var client = new HttpClient(); client.DefaultRequestHeaders.Add("Authorization", $"Bearer {AppConfig.Data.ApiKey}");
                    var req = new HttpRequestMessage(HttpMethod.Post, AppConfig.GetValidApiUrl()) { Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") };
                    using var resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
                    resp.EnsureSuccessStatusCode();
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
                                Dispatcher.Invoke(() => { sb.Append(chunk); ChatBox.Text += chunk; ChatBox.ScrollToEnd(); });
                            }
                        }
                    }
                });
                _chatHistory.Add(new { role = "assistant", content = sb.ToString() });
            }
            catch { ChatBox.Text += "\n[Error]"; }
            finally { if (btn != null) btn.IsEnabled = true; }
        }
    }
}