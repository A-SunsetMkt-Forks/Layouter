using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows;

namespace Layouter.Utility
{
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool invert = parameter != null && parameter.ToString() == "Invert";
            bool result = value is bool boolValue && boolValue;
            return invert ? (result ? Visibility.Collapsed : Visibility.Visible) : (result ? Visibility.Visible : Visibility.Collapsed);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool invert = parameter != null && parameter.ToString() == "Invert";
            bool result = value is Visibility visibility && visibility == Visibility.Visible;
            return invert ? !result : result;
        }
    }

    public class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool boolValue && !boolValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool boolValue && !boolValue;
        }
    }
    
    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool invert = parameter != null && parameter.ToString() == "Invert";
            bool isNull = value == null;
            return invert ? (isNull ? Visibility.Visible : Visibility.Collapsed) : (isNull ? Visibility.Collapsed : Visibility.Visible);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    
    public class PointToMarginConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Point point)
            {
                return new Thickness(point.X, point.Y, 0, 0);
            }
            return new Thickness(0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
