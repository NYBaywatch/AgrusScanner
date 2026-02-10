using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Data;
using AgrusScanner.Models;

namespace AgrusScanner.Converters;

public class PortListConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ObservableCollection<PortResult> ports && ports.Count > 0)
            return string.Join(", ", ports.Select(p => $"{p.Port}/{p.ServiceName}"));
        return "-";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
