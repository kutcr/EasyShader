#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace EasyShader
{
    public class FavoriteItem { public string Time { get; set; } public string Prompt { get; set; } public string Code { get; set; } public string ThumbnailPath { get; set; } public System.Windows.Media.ImageSource ThumbnailSource { get; set; } public string RawLine { get; set; } }

    public partial class GalleryWindow : Window
    {
        public GalleryWindow() { InitializeComponent(); LoadFavorites(); }

        private void LoadFavorites()
        {
            var items = new List<FavoriteItem>();
            if (File.Exists("favs.db"))
            {
                var lines = File.ReadAllLines("favs.db").Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
                foreach (var line in lines)
                {
                    var p = line.Split('|');
                    if (p.Length >= 3)
                    {
                        var item = new FavoriteItem { Time = p[0], Prompt = p[1], Code = p[2].Replace("[N]", "\n"), ThumbnailPath = p.Length > 3 ? p[3] : "", RawLine = line };
                        if (!string.IsNullOrEmpty(item.ThumbnailPath) && File.Exists(item.ThumbnailPath))
                        {
                            BitmapImage bmp = new BitmapImage(); bmp.BeginInit(); bmp.CacheOption = BitmapCacheOption.OnLoad; bmp.UriSource = new Uri(item.ThumbnailPath); bmp.EndInit(); bmp.Freeze(); item.ThumbnailSource = bmp;
                        }
                        items.Add(item);
                    }
                }
            }
            GalleryList.ItemsSource = null; GalleryList.ItemsSource = items;
        }

        private async void Copy_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as System.Windows.Controls.Button;
            var item = btn.DataContext as FavoriteItem;
            if (item != null)
            {
                Clipboard.SetText(item.Code);
                string old = btn.Content.ToString(); btn.Content = "✅ 已复制";
                await Task.Delay(1000); btn.Content = old;
            }
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            var item = (sender as System.Windows.Controls.Button).DataContext as FavoriteItem;
            if (item == null) return;
            if (MessageBox.Show("确定要删除这条收藏吗？", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                var lines = File.ReadAllLines("favs.db").ToList();
                lines.Remove(item.RawLine);
                File.WriteAllLines("favs.db", lines);
                if (File.Exists(item.ThumbnailPath)) try { File.Delete(item.ThumbnailPath); } catch { }
                LoadFavorites();
            }
        }
    }
}