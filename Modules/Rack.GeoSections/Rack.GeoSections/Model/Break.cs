using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using UnitsNet;

namespace Rack.GeoSections.Model
{
    /// <summary>
    /// Разбивка для построения разреза.
    /// </summary>
    [DataContract]
    public sealed class Break
    {
        public Break() {}

        public Break(IEnumerable<KeyValuePair<string, Length>> values, string name,
            IReadOnlyCollection<Well> wells)
        {
            _values = new Dictionary<string, Length>(values);
            _absoluteValues = Values.ToDictionary(
                x => x.Key,
                x => wells.GetByName(x.Key).Altitude - x.Value);
            Name = name;
        }

        /// <summary>
        /// Уникальное название разбивки.
        /// </summary>
        [DataMember]
        public string Name { get; private set; }

        [DataMember]
        private readonly Dictionary<string, Length> _values;

        /// <summary>
        /// Глубины разбивок в скважинах. Ключом является название скважиины.
        /// </summary>
        public IReadOnlyDictionary<string, Length> Values => _values;

        [DataMember]
        private readonly Dictionary<string, Length> _absoluteValues;

        public IReadOnlyDictionary<string, Length> AbsoluteValues => _absoluteValues;

        public Length GetAbsoluteValue(Well well) => well.Altitude - Values[well.Name];

        public Length this[string wellName] => Values
            .First(x =>
                x.Key.Equals(wellName, StringComparison.OrdinalIgnoreCase))
            .Value;
    }
}