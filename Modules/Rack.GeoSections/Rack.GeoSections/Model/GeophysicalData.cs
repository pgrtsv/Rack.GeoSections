using System.Runtime.Serialization;
using FluentValidation;
using Rack.GeoSections.Model.Validators;
using UnitsNet;

namespace Rack.GeoSections.Model
{
    /// <summary>
    /// Геофизический замер.
    /// </summary>
    [DataContract]
    public sealed class GeophysicalData
    {
        /// <param name="wellAltitude">Альтитуда скважины, в которой выполнялся замер.</param>
        /// <param name="top">Верхняя относительная глубина интервала.</param>
        /// <param name="bottom">Нижняя относительная глабина интервала.</param>
        /// <param name="value">Значение геофизического параметра в интервале.</param>
        public GeophysicalData(
            Length wellAltitude,
            Length top,
            Length bottom,
            double value)
        {
            WellAltitude = wellAltitude;
            Top = top;
            Bottom = bottom;
            Value = value;
            new GeophysicalTestValidator().ValidateAndThrow(this);
        }

        [DataMember]
        public Length WellAltitude { get; }
        
        /// <summary>
        /// Верхняя глубина интервала, в котором проводился замер.
        /// </summary>
        [DataMember]
        public Length Top { get; }

        /// <summary>
        /// Нижняя глубина интервала, в котором проводился замер.
        /// </summary>
        [DataMember]
        public Length Bottom { get; }

        /// <summary>
        /// Абсолютная верхняя глубина интервала.
        /// </summary>
        public Length AbsoluteTop => WellAltitude - Top;

        /// <summary>
        /// Абсолютная нижняя глубина интервала.
        /// </summary>
        public Length AbsoluteBottom => WellAltitude - Bottom;

        /// <summary>
        /// Значение геофизического параметра в интервале.
        /// </summary>
        [DataMember]
        public double Value { get; }
    }
}