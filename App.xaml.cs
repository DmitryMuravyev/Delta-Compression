using System.Configuration;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace DeltaComp
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
    }


    // Value converter used to change the color of the icon (System.Windows.Shapes.Path)
    // in cases when the control is enabled or disabled. 
    [ValueConversion(typeof(Boolean?), typeof(SolidColorBrush))]
    public class EnabledToColorConverter : IValueConverter
    {
        public EnabledToColorConverter()
        {
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (System.Convert.ToBoolean(value))
            {
                return new SolidColorBrush(Colors.Black);
            }
            return new SolidColorBrush(Colors.LightGray);
        }


        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
   
}
