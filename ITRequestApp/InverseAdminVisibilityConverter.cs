using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ITRequestApp
{
    public class InverseAdminVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string role = value as string;
            bool isAdmin = role == "Admin";
            return isAdmin ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}