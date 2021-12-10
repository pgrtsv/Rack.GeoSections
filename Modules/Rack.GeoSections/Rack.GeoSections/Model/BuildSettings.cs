using System;
using System.Collections;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using DynamicData.Binding;
using Rack.CrossSectionUtils.Abstractions.Model;
using Rack.GeoSections.Model.Validators;
using Rack.Shared.Configuration;
using Rack.Shared.FluentValidation;
using UnitsNet;

namespace Rack.GeoSections.Model
{
    /// <summary>
    /// Параметры построения.
    /// </summary>
    [DataContract]
    public sealed class BuildSettings : 
        ICrossSectionBuildSettings,
        IConfiguration, 
        INotifyPropertyChanged, 
        INotifyDataErrorInfo
    {
        private readonly ReactiveValidationTemplate<BuildSettings> _validationTemplate;

        public BuildSettings() =>
            _validationTemplate = new ReactiveValidationTemplate<BuildSettings>(
                new BuildSettingsValidator(),
                this.WhenAnyPropertyChanged());

        /// <summary>
        /// Горизонтальный масштаб.
        /// </summary>
        [DataMember]
        public double HorizontalScale { get; set; } = 1;

        /// <summary>
        /// Вертикальный масштаб.
        /// </summary>
        [DataMember]
        public double VerticalScale { get; set; } = 1;

        /// <summary>
        /// Отступ от краёв разреза.
        /// </summary>
        [DataMember]
        public Length Offset { get; set; }

        [DataMember]
        public bool IsOffsetScaled { get; set; }

        public void OnIsOffsetScaledChanged()
        {
            if (IsOffsetScaled)
                Offset *= HorizontalScale;
            else
                Offset /= HorizontalScale;
        }

        public Length GetScaledOffset() => IsOffsetScaled ? Offset : Offset * HorizontalScale;

        public Length GetUnscaledOffset() => IsOffsetScaled ? Offset / HorizontalScale : Offset;

        /// <summary>
        /// Горизонтальное разрешение (количество точек в 1 см).
        /// </summary>
        [DataMember]
        public int HorizontalResolution { get; set; } = 10;

        /// <summary>
        /// Вертикальное разрешение (количество точек в 1 см).
        /// </summary>
        [DataMember]
        public int VerticalResolution { get; set; } = 10;

        /// <summary>
        /// Верхняя граница разреза (абс. отм.).
        /// </summary>
        [DataMember]
        public Length Top { get; set; }

        /// <summary>
        /// Нижняя граница разреза (абс. отм., м).
        /// </summary>
        [DataMember]
        public Length Bottom { get; set; }

        /// <summary>
        /// Ширина колонок оформления.
        /// </summary>
        [DataMember]
        public Length DecorationColumnsWidth { get; set; } = Length.FromCentimeters(1);

        /// <summary>
        /// Высота заголовков декоративных колонок.
        /// </summary>
        [DataMember]
        public Length DecorationHeadersHeight { get; set; } = Length.FromCentimeters(4);

        [DataMember]
        public string EncodingWebName { get; set; } = Encoding.GetEncoding(1251).WebName;

        public Encoding Encoding
        {
            get => Encoding.GetEncoding(EncodingWebName);
            set => EncodingWebName = value.WebName;
        }

        /// <summary>
        /// Режим отображения шкалы глубины.
        /// </summary>
        [DataMember]
        public CrossSectionUtils.Model.DecorationColumnMode DepthColumnMode { get; set; } = CrossSectionUtils.Model.DecorationColumnMode.LeftAndRight;

        [DataMember]
        public Version Version { get; } = Assembly.GetAssembly(typeof(BuildSettings)).GetName().Version;

        public IEnumerable GetErrors(string propertyName) => _validationTemplate.GetErrors(propertyName);

        public bool HasErrors => _validationTemplate.HasErrors;

        public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged
        {
            add => _validationTemplate.ErrorsChanged += value;
            remove => _validationTemplate.ErrorsChanged -= value;
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}