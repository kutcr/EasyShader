#nullable disable
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Linq;

namespace EasyShader
{
    public class ConfigData
    {
        public string ApiUrl { get; set; } = "https://api.deepseek.com/chat/completions";
        public string ApiKey { get; set; } = "";
        public string ModelName { get; set; } = "deepseek-v4-flash";
        public string Language { get; set; } = "zh";
    }

    public static class AppConfig
    {
        public static ConfigData Data { get; set; } = new ConfigData();
        private static readonly string ConfigPath = "config.json";
        private static ResourceDictionary _currentDict = new ResourceDictionary();

        public static void Load()
        {
            if (File.Exists(ConfigPath))
            {
                try { Data = JsonSerializer.Deserialize<ConfigData>(File.ReadAllText(ConfigPath)) ?? new ConfigData(); } catch { }
            }
            ApplyLanguage();
        }

        public static void Save()
        {
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(Data));
            ApplyLanguage();
        }

        public static bool SupportsVision()
        {
            if (string.IsNullOrEmpty(Data.ModelName)) return false;
            string m = Data.ModelName.ToLower();
            string[] visionKeywords = { "vision", "gpt-4o", "gpt-4-turbo", "claude-3", "gemini", "vl", "flash", "kimi" };
            return visionKeywords.Any(k => m.Contains(k));
        }

        public static bool IsEN => Data.Language == "en";
        public static string GetString(string key) => _currentDict.Contains(key) ? _currentDict[key].ToString() : key;

        public static string GetValidApiUrl()
        {
            string url = Data.ApiUrl?.Trim() ?? "";
            if (string.IsNullOrEmpty(url)) return "";
            if (!url.EndsWith("/chat/completions")) url = url.TrimEnd('/') + (url.Contains("/v1") ? "/chat/completions" : "/v1/chat/completions");
            if (!url.StartsWith("http")) url = "https://" + url;
            return url;
        }

        public static void ApplyLanguage()
        {
            _currentDict.Clear();
            if (IsEN)
            {
                _currentDict.Add("t_RefineBtn", "✨ Prompt Assistant"); _currentDict.Add("t_ChatBtn", "💬 Free Chat");
                _currentDict.Add("t_Prompt", "Prompt"); _currentDict.Add("t_SettingsHeader", "GENERATION SETTINGS");
                _currentDict.Add("t_Parallel", "Parallel Variants"); _currentDict.Add("t_Temp", "Temperature");
                _currentDict.Add("t_Tokens", "Max Tokens"); _currentDict.Add("t_Clear", "Clear");
                _currentDict.Add("t_Export", "Export"); _currentDict.Add("t_Build", "Generate");
                _currentDict.Add("t_Stop", "Stop"); _currentDict.Add("t_Variants", "Variants: ");
                _currentDict.Add("t_Compile", "⚡ Compile & Apply"); _currentDict.Add("t_Fav", "★ Favorite");
                _currentDict.Add("t_Gallery", "📁 Gallery"); _currentDict.Add("t_SetTitle", "⚙️ Settings");
                _currentDict.Add("t_ApiUrl", "API URL"); _currentDict.Add("t_ApiKey", "API Key");
                _currentDict.Add("t_Model", "Model Name"); _currentDict.Add("t_Save", "Save Settings");
                _currentDict.Add("t_Test", "Test Connection"); _currentDict.Add("t_RefineTitle", "Prompt Assistant");
                _currentDict.Add("t_ChatTitle", "Free Chat"); _currentDict.Add("t_Send", "Send");
                _currentDict.Add("t_Apply", "Apply"); _currentDict.Add("t_TipTitle", "💡 Parameter Strategy");
                _currentDict.Add("t_Tip1", "1. Simple 2D: Temp 0.2, Tokens 2048.");
                _currentDict.Add("t_Tip2", "2. Masterpieces: Temp 0.5-0.7, Tokens 8192.");
                _currentDict.Add("s_Ready", "Ready."); _currentDict.Add("s_Aborted", "Aborted.");
                _currentDict.Add("s_NoKey", "Missing Configuration!"); _currentDict.Add("s_Generating", "Generating ");
                _currentDict.Add("s_Finished", "Finished."); _currentDict.Add("s_Success", "Success!");
                _currentDict.Add("s_Error", "Compile Error"); _currentDict.Add("s_ConfirmClear", "Clear all?");
                _currentDict.Add("s_Saved", "✅ Saved"); _currentDict.Add("s_SaveFailed", "Failed");
                _currentDict.Add("s_NoVision", "Model might not support images. Continue?");
                _currentDict.Add("s_RefineGreeting", "Hello! I am your Prompt Assistant.");
                _currentDict.Add("s_ChatGreeting", "Hello! Ask me anything.");
            }
            else
            {
                _currentDict.Add("t_RefineBtn", "✨ 提示词优化助手"); _currentDict.Add("t_ChatBtn", "💬 大模型自由对话");
                _currentDict.Add("t_Prompt", "提示词"); _currentDict.Add("t_SettingsHeader", "生成配置");
                _currentDict.Add("t_Parallel", "并行方案数量"); _currentDict.Add("t_Temp", "发散度");
                _currentDict.Add("t_Tokens", "最大长度"); _currentDict.Add("t_Clear", "清空");
                _currentDict.Add("t_Export", "导出"); _currentDict.Add("t_Build", "开始生成");
                _currentDict.Add("t_Stop", "停止生成"); _currentDict.Add("t_Variants", "方案对比: ");
                _currentDict.Add("t_Compile", "⚡ 编译并应用"); _currentDict.Add("t_Fav", "★ 收藏");
                _currentDict.Add("t_Gallery", "📁 收藏夹"); _currentDict.Add("t_SetTitle", "⚙️ 大模型配置");
                _currentDict.Add("t_ApiUrl", "接口地址"); _currentDict.Add("t_ApiKey", "接口密钥");
                _currentDict.Add("t_Model", "模型名称"); _currentDict.Add("t_Save", "保存配置");
                _currentDict.Add("t_Test", "测试连接"); _currentDict.Add("t_RefineTitle", "提示词优化助手");
                _currentDict.Add("t_ChatTitle", "大模型自由对话"); _currentDict.Add("t_Send", "发送想法");
                _currentDict.Add("t_Apply", "应用蓝图方案"); _currentDict.Add("t_TipTitle", "💡 参数使用策略");
                _currentDict.Add("t_Tip1", "1. 简单2D：发散度 0.2，长度 2048。");
                _currentDict.Add("t_Tip2", "2. 艺术大作：发散度 0.5-0.7，长度 8192。");
                _currentDict.Add("s_Ready", "准备就绪"); _currentDict.Add("s_Aborted", "已中止。");
                _currentDict.Add("s_NoKey", "配置不完整！"); _currentDict.Add("s_Generating", "正在生成 ");
                _currentDict.Add("s_Finished", "全部完成"); _currentDict.Add("s_Success", "渲染成功！");
                _currentDict.Add("s_Error", "显卡报错"); _currentDict.Add("s_ConfirmClear", "确定清空？");
                _currentDict.Add("s_Saved", "✅ 已收藏"); _currentDict.Add("s_SaveFailed", "失败");
                _currentDict.Add("s_NoVision", "模型可能不支持视觉，是否继续？");
                _currentDict.Add("s_RefineGreeting", "你好！我是你的提示词优化助手。");
                _currentDict.Add("s_ChatGreeting", "你好！请随时向我提问。");
            }
            Application.Current.Resources.MergedDictionaries.Clear();
            Application.Current.Resources.MergedDictionaries.Add(_currentDict);
        }
    }
}