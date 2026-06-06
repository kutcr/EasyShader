#nullable disable
#pragma warning disable CA1416 
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Text.RegularExpressions;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Media.Imaging;

namespace EasyShader
{
    public partial class MainWindow : Window
    {
        private List<string> _results = new List<string>();
        private CancellationTokenSource _cts;
        private bool _isGenerating = false;
        private string _attachedBase64 = null;
        private string _attachedMimeType = null;

        private SettingsWindow _settingsWin;
        private RefineWindow _refineWin;
        private GalleryWindow _galleryWin;
        private ChatWindow _chatWin;

        public MainWindow()
        {
            InitializeComponent();
            if (!Directory.Exists("Thumbnails")) Directory.CreateDirectory("Thumbnails");
            AppConfig.Load();
            UpdateStatus(AppConfig.GetString("s_Ready"));
        }

        private void UpdateStatus(string msg)
        {
            Dispatcher.Invoke(() => StatusText.Text = msg);
        }

        private void OpenOrActivate<T>(ref T winRef, Func<T> creator) where T : Window
        {
            if (winRef == null || !winRef.IsLoaded)
            {
                winRef = creator();
                winRef.Owner = this;
                winRef.Show();
            }
            else
            {
                if (winRef.WindowState == WindowState.Minimized) winRef.WindowState = WindowState.Normal;
                winRef.Activate();
            }
        }

        private void Lang_Click(object sender, RoutedEventArgs e)
        {
            AppConfig.Data.Language = AppConfig.IsEN ? "zh" : "en";
            AppConfig.Save();
            UpdateStatus(AppConfig.GetString("s_Ready"));
        }

        private void Settings_Click(object sender, RoutedEventArgs e) => OpenOrActivate(ref _settingsWin, () => new SettingsWindow());
        private void OpenGallery_Click(object sender, RoutedEventArgs e) => OpenOrActivate(ref _galleryWin, () => new GalleryWindow());
        private void OpenChat_Click(object sender, RoutedEventArgs e) => OpenOrActivate(ref _chatWin, () => new ChatWindow());

        private void OpenRefine_Click(object sender, RoutedEventArgs e)
        {
            OpenOrActivate(ref _refineWin, () => new RefineWindow(PromptInput.Text, (res) => {
                PromptInput.Text = res;
                this.Activate();
            }));
        }

        private void Attach_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new Microsoft.Win32.OpenFileDialog { Filter = "媒体文件|*.png;*.jpg;*.jpeg;*.bmp;*.mp4;*.webm" };
            if (ofd.ShowDialog() == true) LoadMediaFile(ofd.FileName);
        }

        private void PromptInput_PreviewDragOver(object sender, DragEventArgs e)
        {
            e.Handled = true;
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        }

        private void PromptInput_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0) LoadMediaFile(files[0]);
            }
        }

        private void PromptInput_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control && Clipboard.ContainsImage())
            {
                var source = Clipboard.GetImage();
                if (source != null)
                {
                    using (var ms = new MemoryStream())
                    {
                        var enc = new JpegBitmapEncoder();
                        enc.Frames.Add(BitmapFrame.Create(source));
                        enc.Save(ms);
                        byte[] data = ms.ToArray();
                        _attachedBase64 = Convert.ToBase64String(data);
                        _attachedMimeType = "image/jpeg";
                        ShowAttachment("Clipboard_Img.jpg", data);
                        e.Handled = true;
                    }
                }
            }
        }

        private void LoadMediaFile(string path)
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                _attachedBase64 = Convert.ToBase64String(bytes);
                string ext = Path.GetExtension(path).ToLower();
                _attachedMimeType = ext switch { ".mp4" => "video/mp4", ".png" => "image/png", _ => "image/jpeg" };
                ShowAttachment(Path.GetFileName(path), bytes);
            }
            catch { }
        }

        private void ShowAttachment(string name, byte[] data)
        {
            AttachmentName.Text = name;
            if (_attachedMimeType.StartsWith("image/"))
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.StreamSource = new MemoryStream(data);
                bitmap.EndInit();
                ImgPreview.Source = bitmap;
            }
            else { ImgPreview.Source = null; }
            AttachmentBar.Visibility = Visibility.Visible;
        }

        private void RemoveAttachment_Click(object sender, RoutedEventArgs e)
        {
            _attachedBase64 = null; AttachmentBar.Visibility = Visibility.Collapsed;
        }

        private async void ActionBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_isGenerating) { _cts?.Cancel(); return; }

            if (string.IsNullOrWhiteSpace(AppConfig.Data.ApiKey) || string.IsNullOrWhiteSpace(AppConfig.Data.ModelName))
            {
                MessageBox.Show(AppConfig.GetString("s_NoKey"), "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                Settings_Click(null, null);
                return;
            }

            if (!string.IsNullOrEmpty(_attachedBase64) && !AppConfig.SupportsVision())
            {
                var res = MessageBox.Show(AppConfig.GetString("s_NoVision"), "Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (res == MessageBoxResult.No) return;
            }

            if (string.IsNullOrWhiteSpace(PromptInput.Text) && string.IsNullOrEmpty(_attachedBase64)) return;

            string safePrompt = PromptInput.Text;
            int safeCount = (int)ParallelSlider.Value;
            double safeTemp = TempSlider.Value;
            int safeTokens = (int)TokensSlider.Value;

            _isGenerating = true;
            ActionBtn.SetResourceReference(ContentProperty, "t_Stop");
            ActionBtn.Style = (Style)FindResource("UEDangerButton");
            _cts = new CancellationTokenSource();
            _results.Clear();
            ResetVariantButtons();

            try
            {
                for (int i = 0; i < safeCount; i++)
                {
                    if (_cts.IsCancellationRequested) break;
                    UpdateStatus(AppConfig.GetString("s_Generating") + $"{i + 1}/{safeCount}...");
                    StringBuilder sb = new StringBuilder();

                    await Task.Run(async () => {
                        try
                        {
                            await RequestStream(safePrompt, safeTemp, safeTokens, (c) => Dispatcher.Invoke(() => {
                                sb.Append(c); CodeOutput.Text = sb.ToString(); CodeOutput.ScrollToEnd();
                            }), _cts.Token);
                        }
                        catch (Exception ex)
                        {
                            Dispatcher.Invoke(() => {
                                StatusText.Text = "API Error";
                                sb.Clear();
                                sb.Append("/* === API Request Failed ===\n");
                                sb.Append(ex.Message);
                                sb.Append("\n===================== */\n");
                                CodeOutput.Text = sb.ToString();
                                _cts.Cancel();
                            });
                        }
                    });

                    if (_cts.IsCancellationRequested) break;

                    if (!CodeOutput.Text.Contains("API Request Failed"))
                    {
                        string finalCode = SanitizeFinalCode(sb.ToString());
                        _results.Add(finalCode);
                        ShowVariantButton(i + 1, safeCount);
                        SwitchTo(i);
                    }
                }
            }
            finally
            {
                _isGenerating = false;
                ActionBtn.SetResourceReference(ContentProperty, "t_Build");
                ActionBtn.Style = (Style)FindResource("UEPrimaryButton");
                if (!_cts.IsCancellationRequested && !CodeOutput.Text.Contains("API Request Failed"))
                    UpdateStatus(AppConfig.GetString("s_Finished"));
            }
        }

        private async Task RequestStream(string p, double temp, int maxTokens, Action<string> cb, CancellationToken token)
        {
            string sys = "Act as HLSL Expert. Output PURE HLSL code. No Chinese. Constants: register(b0). Entry: main.";

            object userContent;
            if (string.IsNullOrEmpty(_attachedBase64))
            {
                userContent = p;
            }
            else
            {
                var contents = new List<object> { new { type = "text", text = p } };
                contents.Add(new { type = "image_url", image_url = new { url = $"data:{_attachedMimeType};base64,{_attachedBase64}" } });
                userContent = contents.ToArray();
            }

            var body = new
            {
                model = AppConfig.Data.ModelName,
                messages = new object[] { new { role = "system", content = sys }, new { role = "user", content = userContent } },
                stream = true,
                temperature = temp,
                max_tokens = maxTokens
            };

            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {AppConfig.Data.ApiKey}");
            var req = new HttpRequestMessage(HttpMethod.Post, AppConfig.GetValidApiUrl())
            {
                Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
            };

            using var resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, token);
            if (!resp.IsSuccessStatusCode)
            {
                string errorBody = await resp.Content.ReadAsStringAsync();
                throw new Exception($"HTTP Status: {(int)resp.StatusCode}\nDetails: {errorBody}");
            }

            using var stream = await resp.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);
            while (!reader.EndOfStream)
            {
                if (token.IsCancellationRequested) break;
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: ")) continue;
                string json = line.Substring(6).Trim(); if (json == "[DONE]") break;
                try
                {
                    using var doc = JsonDocument.Parse(json);
                    cb(doc.RootElement.GetProperty("choices")[0].GetProperty("delta").GetProperty("content").GetString());
                }
                catch { }
            }
        }

        private string SanitizeFinalCode(string raw)
        {
            var match = Regex.Match(raw, @"```(?:hlsl)?\s*(.*?)\s*```", RegexOptions.Singleline);
            string code = match.Success ? match.Groups[1].Value : raw;
            code = code.Replace("```hlsl", "").Replace("```", "").Trim();
            code = Regex.Replace(code, @"[\u4e00-\u9fa5]+", "");
            if (!code.Contains("cbuffer"))
            {
                code = "cbuffer Constants : register(b0) {\n    float iTime;\n    float iResolutionX;\n    float iResolutionY;\n    float padding;\n};\n\n" + code;
            }
            if (code.Contains("{") && !code.EndsWith("}")) code += "\n}";
            return code;
        }

        private void ManualCompile_Click(object sender, RoutedEventArgs e)
        {
            string code = SanitizeFinalCode(CodeOutput.Text);
            CodeOutput.Text = code;
            ApplyShader(code);
        }

        private void SwitchTo(int i)
        {
            if (i < _results.Count)
            {
                CodeOutput.Text = _results[i];
                ApplyShader(_results[i]);
            }
        }

        private void ApplyShader(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) { ShaderViewer.CompileAndSetShader(""); return; }
            string res = ShaderViewer.CompileAndSetShader(code);
            if (res == "Success")
            {
                if (!_isGenerating) UpdateStatus(AppConfig.GetString("s_Success"));
                ShaderViewer.Visibility = Visibility.Visible; ErrorOverlay.Visibility = Visibility.Collapsed;
            }
            else
            {
                UpdateStatus(AppConfig.GetString("s_Error"));
                ShaderViewer.Visibility = Visibility.Collapsed; ErrorOverlay.Visibility = Visibility.Visible; ErrorOverlayText.Text = res;
                CodeOutput.Text = "/* --- Error --- \n" + res + "\n--- */\n\n" + code;
            }
        }

        private void SwitchResult_Click(object s, RoutedEventArgs e) { if (s is Control ctrl) SwitchTo(int.Parse(ctrl.Tag.ToString())); }

        private void ResetVariantButtons()
        {
            ResultSwitcher.Visibility = Visibility.Collapsed;
            VarBtn1.Visibility = Visibility.Collapsed; VarBtn2.Visibility = Visibility.Collapsed;
            VarBtn3.Visibility = Visibility.Collapsed; VarBtn4.Visibility = Visibility.Collapsed;
            VarBtn5.Visibility = Visibility.Collapsed; VarBtn1.IsChecked = true;
        }

        private void ShowVariantButton(int num, int total)
        {
            if (total <= 1) return; ResultSwitcher.Visibility = Visibility.Visible;
            if (num == 1) VarBtn1.Visibility = Visibility.Visible;
            else if (num == 2) VarBtn2.Visibility = Visibility.Visible;
            else if (num == 3) VarBtn3.Visibility = Visibility.Visible;
            else if (num == 4) VarBtn4.Visibility = Visibility.Visible;
            else if (num == 5) VarBtn5.Visibility = Visibility.Visible;
        }

        private void NewShader_Click(object s, RoutedEventArgs e)
        {
            if (MessageBox.Show(AppConfig.GetString("s_ConfirmClear"), "Reset", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                PromptInput.Text = ""; CodeOutput.Text = ""; UpdateStatus(AppConfig.GetString("s_Ready"));
                ResetVariantButtons(); _results.Clear(); ShaderViewer.CompileAndSetShader("");
                ShaderViewer.Visibility = Visibility.Visible; ErrorOverlay.Visibility = Visibility.Collapsed; RemoveAttachment_Click(null, null);
            }
        }

        private void Export_Click(object s, RoutedEventArgs e)
        {
            var sfd = new Microsoft.Win32.SaveFileDialog { Filter = "HLSL|*.hlsl" };
            if (sfd.ShowDialog() == true) File.WriteAllText(sfd.FileName, CodeOutput.Text);
        }

        private async void SaveFavorite_Click(object s, RoutedEventArgs e)
        {
            var btn = s as Button; if (string.IsNullOrEmpty(CodeOutput.Text) || btn == null || ErrorOverlay.Visibility == Visibility.Visible) return;
            string id = DateTime.Now.ToString("yyyyMMddHHmmss"); string thumbPath = Path.GetFullPath($"Thumbnails/{id}.jpg");
            try
            {
                var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(this); var pt = ShaderViewer.PointToScreen(new System.Windows.Point(0, 0));
                int w = (int)(ShaderViewer.ActualWidth * dpi.DpiScaleX); int h = (int)(ShaderViewer.ActualHeight * dpi.DpiScaleY);
                using (Bitmap bmp = new Bitmap(w, h)) { using (Graphics g = Graphics.FromImage(bmp)) { g.CopyFromScreen((int)pt.X, (int)pt.Y, 0, 0, new System.Drawing.Size(w, h)); } bmp.Save(thumbPath, ImageFormat.Jpeg); }
                File.AppendAllText("favs.db", $"{DateTime.Now:MM-dd HH:mm}|{PromptInput.Text.Replace("|", " ")}|{CodeOutput.Text.Replace("\n", "[N]")}|{thumbPath}\n");
                btn.Content = AppConfig.GetString("s_Saved"); btn.Background = System.Windows.Media.Brushes.Transparent;
                await Task.Delay(1500); btn.SetResourceReference(ContentProperty, "t_Fav");
            }
            catch { UpdateStatus(AppConfig.GetString("s_SaveFailed")); }
        }
    }
}