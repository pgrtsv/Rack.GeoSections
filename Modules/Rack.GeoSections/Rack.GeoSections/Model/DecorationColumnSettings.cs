using System.Collections.Generic;
using Rack.CrossSectionUtils.Abstractions.Model;

namespace Rack.GeoSections.Model
{
    public sealed class DecorationColumnSettings : IDecorationColumnsSettings<
        DepthRulerColumn,
        DecorationColumn,
        DecorationColumnRecord>
    {
        public DecorationColumnSettings(
            DepthRulerColumn ruler, 
            IReadOnlyCollection<DecorationColumn> decorationColumnsWithRecords)
        {
            Ruler = ruler;
            DecorationColumnsWithRecords = decorationColumnsWithRecords;
        }

        /// <inheritdoc />
        public DepthRulerColumn Ruler { get; }

        /// <inheritdoc />
        public IReadOnlyCollection<DecorationColumn> DecorationColumnsWithRecords { get; }
    }
}