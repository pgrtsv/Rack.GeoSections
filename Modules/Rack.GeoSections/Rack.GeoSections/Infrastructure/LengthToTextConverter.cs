using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;
using UnitsNet;
using UnitsNet.Units;

namespace Rack.GeoSections.Infrastructure
{
    public sealed class LengthToTextConverter : MarkupExtension, IValueConverter
    {
        private static readonly Lazy<LengthToTextConverter> Instance =
            new Lazy<LengthToTextConverter>(() => new LengthToTextConverter());

        /// <inheritdoc />
        public override object ProvideValue(IServiceProvider serviceProvider) => Instance.Value;

        /// <inheritdoc />
        public object Convert(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture) =>
            ((Length) value).ToString(culture);

        /// <inheritdoc />
        public object ConvertBack(object value, Type targetType, object parameter,
            CultureInfo culture)
        {
            var text = (string) value;
            var defaultUnit = (LengthUnit) parameter;
            if (string.IsNullOrEmpty(text))
                return Length.Zero.ToUnit(defaultUnit);
            if (Length.TryParse(text, out var length))
                return length;
            if (double.TryParse(text, out var doubleValue))
                return new Length(doubleValue, defaultUnit);
            return Length.Zero.ToUnit(defaultUnit);
        }
    }
}