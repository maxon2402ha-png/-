using System.Windows;
using System.Windows.Media;

namespace КР_Ханников.Helpers
{
    public static class ThemeManager
    {
        public static bool IsDarkTheme { get; private set; } = false;

        public static void ApplyTheme(bool isDark)
        {
            IsDarkTheme = isDark;
            var dict = Application.Current.Resources;

            if (isDark)
            {
                                dict["Brush.Background"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#111827"));                 dict["Brush.Surface"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1F2937"));                    dict["Brush.SurfaceAlt"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#374151")); 
                dict["Brush.TextPrimary"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F9FAFB"));                 dict["Brush.TextSecondary"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9CA3AF")); 
                dict["Brush.Border"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#374151"));                 }
            else
            {
                                dict["Brush.Background"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F3F4F6"));
                dict["Brush.Surface"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF"));
                dict["Brush.SurfaceAlt"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F9FAFB"));

                dict["Brush.TextPrimary"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#111827"));
                dict["Brush.TextSecondary"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280"));

                dict["Brush.Border"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E5E7EB"));
            }

                        dict["Brush.TextMain"] = dict["Brush.TextPrimary"];
            dict["Brush.TextLight"] = dict["Brush.TextSecondary"];
        }
    }
}