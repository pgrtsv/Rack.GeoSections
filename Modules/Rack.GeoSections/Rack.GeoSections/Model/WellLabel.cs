using System;
using System.Collections;
using System.ComponentModel;
using System.Reactive.Linq;
using DynamicData.Binding;
using Rack.GeoSections.Model.Validators;
using Rack.Shared.FluentValidation;
using UnitsNet;

namespace Rack.GeoSections.Model
{
    /// <summary>
    /// Подпись на скважине.
    /// </summary>
    public sealed class WellLabel : INotifyPropertyChanged, INotifyDataErrorInfo
    {
        private readonly ReactiveValidationTemplate<WellLabel> _validationTemplate;

        public WellLabel() =>
            _validationTemplate = new ReactiveValidationTemplate<WellLabel>(
                new WellLabelValidator(),
                this.WhenAnyPropertyChanged().StartWith(this));

        /// <summary>
        /// Название скважины, которую надо подписать.
        /// </summary>
        public string Well { get; set; }

        /// <summary>
        /// Верхняя высота подписи.
        /// </summary>
        public Length Top { get; set; }

        /// <summary>
        /// Нижняя высота подписи.
        /// </summary>
        public Length Bottom { get; set; }

        /// <summary>
        /// Текст подписи.
        /// </summary>
        public string Text { get; set; }

        /// <inheritdoc />
        public event PropertyChangedEventHandler PropertyChanged;

        /// <inheritdoc />
        public IEnumerable GetErrors(string propertyName) => _validationTemplate.GetErrors(propertyName);

        /// <inheritdoc />
        public bool HasErrors => _validationTemplate.HasErrors;

        /// <inheritdoc />
        public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged
        {
            add => _validationTemplate.ErrorsChanged += value;
            remove => _validationTemplate.ErrorsChanged -= value;
        }
    }
}