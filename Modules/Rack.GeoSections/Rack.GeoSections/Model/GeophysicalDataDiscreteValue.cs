using System.Runtime.Serialization;
using UnitsNet;

namespace Rack.GeoSections.Model
{
    /// <summary>
    /// Дискретное значение геофизического параметра в точке.
    /// </summary>
    [DataContract]
    public sealed class GeophysicalDataDiscreteValue
    {
        [DataMember]
        public Length X { get; }

        [DataMember]
        public Length Y { get; }

        [DataMember]
        public Length ScaledX { get; }

        [DataMember]
        public Length ScaledY { get; }

        [DataMember]
        public double Value { get; }

        public GeophysicalDataDiscreteValue(
            Length x, Length y, Length scaledX, Length scaledY, double value)
        {
            X = x;
            Y = y;
            ScaledX = scaledX;
            ScaledY = scaledY;
            Value = value;
        }
    }
}