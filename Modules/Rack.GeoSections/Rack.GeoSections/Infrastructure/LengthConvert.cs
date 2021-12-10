using UnitsNet;
using UnitsNet.Units;

namespace Rack.GeoSections.Infrastructure
{
    public static class LengthConvert
    {
        public static Length FromString(string text, LengthUnit defaultUnit)
        {
            if (Length.TryParse(text, out var length))
                return length;
            if (double.TryParse(text, out var value))
                return new Length(value, defaultUnit);
            return Length.Zero;
        }
    }
}