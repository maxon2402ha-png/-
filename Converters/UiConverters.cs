using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using КР_Ханников.Core;

namespace КР_Ханников.Converters
{
    // =========================================================
    // Конвертеры цветов и статусов
    // =========================================================

    [ValueConversion(typeof(string), typeof(Brush))]
    public class StatusToColorConverter : IValueConverter
    {
        public static StatusToColorConverter Instance { get; } = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var status = value as string ?? string.Empty;

            return status switch
            {
                Constants.TicketStatus.Open => new SolidColorBrush(Color.FromRgb(249, 115, 22)),      // Orange
                Constants.TicketStatus.InProgress => new SolidColorBrush(Color.FromRgb(59, 130, 246)),// Blue
                Constants.TicketStatus.Resolved => new SolidColorBrush(Color.FromRgb(34, 197, 94)),   // Green
                Constants.TicketStatus.Closed => new SolidColorBrush(Color.FromRgb(107, 114, 128)),   // Gray
                _ => new SolidColorBrush(Color.FromRgb(156, 163, 175))                                // Default Gray
            };
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => Binding.DoNothing;
    }

    [ValueConversion(typeof(string), typeof(Brush))]
    public class RoleToColorConverter : IValueConverter
    {
        public static RoleToColorConverter Instance { get; } = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var role = value as string ?? string.Empty;

            return role switch
            {
                Constants.UserRoles.Admin => new SolidColorBrush(Color.FromRgb(220, 38, 38)),   // Red
                Constants.UserRoles.Support => new SolidColorBrush(Color.FromRgb(37, 99, 235)), // Blue
                Constants.UserRoles.Client => new SolidColorBrush(Color.FromRgb(16, 185, 129)), // Emerald
                _ => new SolidColorBrush(Color.FromRgb(107, 114, 128))                          // Gray
            };
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => Binding.DoNothing;
    }

    // =========================================================
    // Конвертеры видимости и логики
    // =========================================================

    [ValueConversion(typeof(object), typeof(Visibility))]
    public class NullToVisibilityConverter : IValueConverter
    {
        public static NullToVisibilityConverter Instance { get; } = new();
        public bool Invert { get; set; } = false;

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            bool isNull = value is null;
            if (value is string s) isNull = string.IsNullOrWhiteSpace(s);
            if (Invert) isNull = !isNull;
            return isNull ? Visibility.Collapsed : Visibility.Visible;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => Binding.DoNothing;
    }

    [ValueConversion(typeof(bool), typeof(Visibility))]
    public class BoolToVisibilityConverter : IValueConverter
    {
        public static BoolToVisibilityConverter Instance { get; } = new();
        public bool Invert { get; set; } = false;

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var flag = (value as bool?) ?? false;
            if (Invert) flag = !flag;
            return flag ? Visibility.Visible : Visibility.Collapsed;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => Binding.DoNothing;
    }

    [ValueConversion(typeof(bool), typeof(double))]
    public class BoolToOpacityConverter : IValueConverter
    {
        public static BoolToOpacityConverter Instance { get; } = new();
        public double TrueOpacity { get; set; } = 1.0;
        public double FalseOpacity { get; set; } = 0.4;
        public bool Invert { get; set; } = false;

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var flag = (value as bool?) ?? false;
            if (Invert) flag = !flag;

            double t = TrueOpacity;
            double f = FalseOpacity;

            if (parameter is string p && !string.IsNullOrWhiteSpace(p))
            {
                var parts = p.Split(';');
                if (parts.Length >= 1) double.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out t);
                if (parts.Length >= 2) double.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out f);
            }

            return flag ? t : f;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => Binding.DoNothing;
    }

    [ValueConversion(typeof(bool), typeof(bool))]
    public class InverseBoolConverter : IValueConverter
    {
        public static InverseBoolConverter Instance { get; } = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var flag = (value as bool?) ?? false;
            return !flag;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => Convert(value, targetType, parameter, culture);
    }

    [ValueConversion(typeof(string), typeof(Visibility))]
    public class RoleToVisibilityConverter : IValueConverter
    {
        public static RoleToVisibilityConverter Instance { get; } = new();
        public bool Invert { get; set; } = false;

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var role = value as string ?? string.Empty;
            var param = parameter as string ?? string.Empty;

            if (string.IsNullOrWhiteSpace(param))
                return Visibility.Collapsed;

            var allowedRoles = param
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(r => r.Trim())
                .ToArray();

            bool isAllowed = allowedRoles.Contains(role, StringComparer.OrdinalIgnoreCase);
            if (Invert) isAllowed = !isAllowed;

            return isAllowed ? Visibility.Visible : Visibility.Collapsed;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => Binding.DoNothing;
    }

    [ValueConversion(typeof(string), typeof(ImageSource))]
    public class PathToImageConverter : IValueConverter
    {
        public static PathToImageConverter Instance { get; } = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var path = value as string;

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return null;
            }

            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(path);
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch
            {
                return null;
            }
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => Binding.DoNothing;
    }

    // =========================================================
    // Математические конвертеры для KPI и SLA
    // =========================================================

    public class LessThanConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value != null && parameter != null)
                return System.Convert.ToDouble(value) < System.Convert.ToDouble(parameter);
            return false;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    public class GreaterThanConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value != null && parameter != null)
                return System.Convert.ToDouble(value) > System.Convert.ToDouble(parameter);
            return false;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}