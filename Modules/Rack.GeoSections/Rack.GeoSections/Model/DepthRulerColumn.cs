using System.ComponentModel;
using Rack.CrossSectionUtils.Abstractions.Model;
using Rack.CrossSectionUtils.Model;

namespace Rack.GeoSections.Model
{
    public sealed class DepthRulerColumn : IDecorationColumn, INotifyPropertyChanged
    {
        /// <inheritdoc />
        public string Header { get; } = "АБС. ОТМ., м";

        /// <inheritdoc />
        public DecorationColumnMode Mode { get; set; }

        /// <inheritdoc />
        public event PropertyChangedEventHandler PropertyChanged;
    }
}