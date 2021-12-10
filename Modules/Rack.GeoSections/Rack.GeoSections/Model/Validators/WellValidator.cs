using System;
using FluentValidation;
using UnitsNet;

namespace Rack.GeoSections.Model.Validators
{
    public sealed class WellValidator: AbstractValidator<Well>
    {
        public static Lazy<WellValidator> Instance { get; } = new Lazy<WellValidator>(() => new WellValidator());

        public WellValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty()
                .WithMessage("Необходимо указать название скважины.");
            RuleFor(x => x.Bottom)
                .GreaterThan(Length.Zero)
                .WithMessage("Значение забоя должно быть положителньым числом.");

        }
    }
}