using System.Globalization;
using System.Windows.Data;

namespace ParcelWorkflowAddIn;

public sealed class TransactionNumberEqualsConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] is not string rowTransactionNumber || values[1] is not string targetTransactionNumber)
        {
            return false;
        }

        return rowTransactionNumber.Equals(targetTransactionNumber, StringComparison.OrdinalIgnoreCase);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
