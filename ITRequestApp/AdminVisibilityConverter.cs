using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.IO;

namespace ITRequestApp
{
    public class AdminVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string role = value as string;
            string logMessage = $"AdminVisibilityConverter: role = {role ?? "null"}";
            Log(logMessage);
            bool isAdmin = role == "Admin";
            string visibilityResult = isAdmin ? "Visible" : "Collapsed";
            Log($"AdminVisibilityConverter: isAdmin = {isAdmin}, returning Visibility.{visibilityResult}");
            return isAdmin ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        private void Log(string message)
        {
            try
            {
                string logPath = "app_log.txt";
                string logMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}: {message}\n";
                File.AppendAllText(logPath, logMessage);
            }
            catch
            {
                // Ignore logging errors
            }
        }
    }
}