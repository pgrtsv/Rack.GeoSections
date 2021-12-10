using System;
using System.Collections;
using System.ComponentModel;
using System.Reactive.Linq;
using System.Runtime.Serialization;
using DynamicData.Binding;
using FluentValidation;
using Rack.GeoSections.Model.Validators;
using Rack.Shared.FluentValidation;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace Rack.GeoSections.Model
{
    /// <summary>
    /// Нефтеносный пласт.
    /// </summary>
    [DataContract]
    public sealed class OilBearingFormation : ReactiveObject, INotifyDataErrorInfo
    {
        private readonly ReactiveValidationTemplate<OilBearingFormation> _validationTemplate;

        public OilBearingFormation(Break topBreak, Break bottomBreak) : this()
        {
            TopBreak = topBreak;
            BottomBreak = bottomBreak;
        }

        public OilBearingFormation() =>
            _validationTemplate = new ReactiveValidationTemplate<OilBearingFormation>(
                new OilBearingFormationValidator(), 
                this.WhenAnyPropertyChanged().StartWith(this));

        [DataMember, Reactive]
        public Break TopBreak { get; set; }

        [DataMember, Reactive]
        public Break BottomBreak { get; set; }

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