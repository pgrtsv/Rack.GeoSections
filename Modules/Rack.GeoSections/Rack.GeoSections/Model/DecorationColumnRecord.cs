using System;
using System.Collections;
using System.ComponentModel;
using System.Runtime.Serialization;
using DynamicData.Binding;
using Rack.CrossSectionUtils.Abstractions.Model;
using Rack.GeoSections.Model.Validators;
using Rack.Shared.FluentValidation;
using UnitsNet;

namespace Rack.GeoSections.Model
{
    /// <summary>
    /// Запись в колонке для оформления разреза.
    /// </summary>
    [DataContract]
    public sealed class DecorationColumnRecord :
        IDecorationColumnRecord,
        INotifyPropertyChanged,
        INotifyDataErrorInfo
    {
        private readonly ReactiveValidationTemplate<DecorationColumnRecord> _validationTemplate;

        public DecorationColumnRecord() =>
            _validationTemplate = new ReactiveValidationTemplate<DecorationColumnRecord>(
                new DecorationColumnRecordValidator(),
                this.WhenAnyPropertyChanged());

        /// <summary>
        /// Текст записи.
        /// </summary>
        [DataMember]
        public string Text { get; set; }

        [DataMember]
        public Length LeftBottom { get; set; }

        [DataMember]
        public Length LeftTop { get; set; }

        [DataMember]
        public Length RightBottom { get; set; }

        [DataMember]
        public Length RightTop { get; set; }

        public IEnumerable GetErrors(string propertyName) =>
            _validationTemplate.GetErrors(propertyName);

        public bool HasErrors => _validationTemplate.HasErrors;

        public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged
        {
            add => _validationTemplate.ErrorsChanged += value;
            remove => _validationTemplate.ErrorsChanged -= value;
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}