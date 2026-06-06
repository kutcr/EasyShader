#nullable disable
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;

namespace EasyShader
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
            UrlBox.Text = AppConfig.Data.ApiUrl;
            KeyBox.Text = AppConfig.Data.ApiKey;
            ModelBox.Text = AppConfig.Data.ModelName;
        }

        private async void Test_Click(object sender, RoutedEventArgs e)
        {
            TestBtn.IsEnabled = false;
            TestStatus.Foreground = System.Windows.Media.Brushes.Orange;
            TestStatus.Text = AppConfig.IsEN ? "Testing Connection..." : "正在验证 API 连接...";

            string url = UrlBox.Text;
            if (!url.EndsWith("/chat/completions"))
            {
                url = url.TrimEnd('/') + (url.Contains("/v1") ? "/chat/completions" : "/v1/chat/completions");
            }
            if (!url.StartsWith("http")) url = "https://" + url;

            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(15);
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {KeyBox.Text}");

                var body = new { model = ModelBox.Text, messages = new[] { new { role = "user", content = "hi, say 'OK'" } }, max_tokens = 5 };
                var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

                var resp = await client.PostAsync(url, content);
                if (resp.IsSuccessStatusCode)
                {
                    TestStatus.Foreground = System.Windows.Media.Brushes.SpringGreen;
                    TestStatus.Text = AppConfig.IsEN ? "✅ Connection Success!" : "✅ 连接成功！API 正常响应。";
                }
                else
                {
                    TestStatus.Foreground = System.Windows.Media.Brushes.Tomato;
                    TestStatus.Text = AppConfig.IsEN ? $"❌ Failed: Status {(int)resp.StatusCode}" : $"❌ 失败: 状态码 {(int)resp.StatusCode}";
                }
            }
            catch
            {
                TestStatus.Foreground = System.Windows.Media.Brushes.Tomato;
                TestStatus.Text = AppConfig.IsEN ? "❌ Error: Cannot reach API." : "❌ 错误: 无法访问接口，请检查地址。";
            }
            finally { TestBtn.IsEnabled = true; }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            AppConfig.Data.ApiUrl = UrlBox.Text;
            AppConfig.Data.ApiKey = KeyBox.Text;
            AppConfig.Data.ModelName = ModelBox.Text;
            AppConfig.Save();

            this.Close();
        }
    }
}