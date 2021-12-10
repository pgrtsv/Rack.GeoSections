using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.Serialization;
using DynamicData.Binding;
using Rack.CrossSectionUtils.Abstractions.Model;
using Rack.CrossSectionUtils.Model;
using Rack.CrossSectionUtils.Validation.Messages;
using Rack.CrossSectionUtils.Validation.Validators;
using Rack.GeoSections.Model.Validators;
using Rack.Shared.FluentValidation;

namespace Rack.GeoSections.Model
{
    /// <summary>
    /// Колонка для оформления разреза.
    /// </summary>
    [DataContract]
    public sealed class DecorationColumn : IDecorationColumnWithRecords<DecorationColumnRecord>,
        INotifyPropertyChanged, INotifyDataErrorInfo
    {
        private readonly ReactiveValidationTemplate<DecorationColumn> _validationTemplate;

        public DecorationColumn() =>
            _validationTemplate = new ReactiveValidationTemplate<DecorationColumn>(
                new DecorationColumnWithRecordsValidator<DecorationColumn, DecorationColumnRecord>(
                    new DecorationColumnWithRecordsValidationMessages(),
                    new DecorationColumnValidator(),
                    new DecorationColumnRecordValidator<DecorationColumnRecord>(
                        new DecorationColumnRecordValidationMessages())),
                this.WhenAnyPropertyChanged());

        /// <inheritdoc />
        [DataMember]
        public string Header { get; set; }

        /// <summary>
        /// Режим отрисовки колонки.
        /// </summary>
        [DataMember]
        public DecorationColumnMode Mode { get; set; }

        [DataMember]
        public ObservableCollection<DecorationColumnRecord> Records { get; set; } =
            new ObservableCollection<DecorationColumnRecord>();

        IReadOnlyCollection<DecorationColumnRecord>
            IDecorationColumnWithRecords<DecorationColumnRecord>.Records => Records;

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