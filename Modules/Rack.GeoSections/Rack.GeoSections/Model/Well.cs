using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using FluentValidation;
using NetTopologySuite.Geometries;
using Rack.GeoSections.Model.Validators;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using UnitsNet;

namespace Rack.GeoSections.Model
{
    /// <summary>
    /// Скважина.
    /// </summary>
    [DataContract]
    public sealed class Well : ReactiveObject
    {
        public Well(
            string name,
            Length altitude,
            Point point,
            Length bottom,
            IEnumerable<GeophysicalData> geophysicalData,
            bool isEnabled)
        {
            Name = name;
            Altitude = altitude;
            Point = point;
            Bottom = bottom;
            GeophysicalData = geophysicalData.ToArray();
            IsEnabled = isEnabled;
            new WellValidator().ValidateAndThrow(this);
        }

        /// <summary>
        /// Название скважины (должно быть уникальным).
        /// </summary>
        [DataMember]
        public string Name { get; }

        /// <summary>
        /// Значение альтитуды.
        /// </summary>
        [DataMember]
        public Length Altitude { get; }

        /// <summary>
        /// Точка-координата скважины.
        /// </summary>
        [DataMember]
        public Point Point { get; }

        /// <summary>
        /// Глубина забоя.
        /// </summary>
        [DataMember]
        public Length Bottom { get; }

        /// <summary>
        /// true, если скважина участвует в построении разреза.
        /// </summary>
        [DataMember, Reactive]
        public bool IsEnabled { get; set; }

        /// <summary>
        /// Геофизические замеры, проведённые в скважине.
        /// </summary>
        [DataMember]
        public IReadOnlyCollection<GeophysicalData> GeophysicalData { get; }

        public override string ToString() => Name;
    }

    public static class WellsEx
    {
        public static Well GetByName(this IEnumerable<Well> wells, string wellName) =>
            wells.First(well => wellName.Equals(well.Name, StringComparison.Ordinal));
    }
}