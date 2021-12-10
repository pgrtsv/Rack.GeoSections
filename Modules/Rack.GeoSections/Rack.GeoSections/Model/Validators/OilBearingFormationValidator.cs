using System.Linq;
using FluentValidation;
using UnitsNet;
using UnitsNet.Units;

namespace Rack.GeoSections.Model.Validators
{
    public sealed class OilBearingFormationValidator : AbstractValidator<OilBearingFormation>
    {
        public OilBearingFormationValidator()
        {
            RuleFor(x => x.TopBreak)
                .NotNull()
                .WithMessage("Необходимо указать верхннюю разбивку");
            RuleFor(x => x.BottomBreak)
                .NotNull()
                .WithMessage("Необходимо указать нижнюю разбивку");
            RuleFor(x => x.TopBreak)
                .Must((formation, x) =>
                    x.Values.Values.Average(LengthUnit.Meter) < formation.BottomBreak.Values.Values.Average(LengthUnit.Meter))
                .When(formation => formation.TopBreak != null && formation.BottomBreak != null)
                .WithMessage("Верхняя разбивка должна быть выше, чем нижняя.");
            RuleFor(x => x.BottomBreak)
                .Must((formation, x) =>
                    x.Values.Values.Average(LengthUnit.Meter) > formation.TopBreak.Values.Values.Average(LengthUnit.Meter))
                .When(formation => formation.TopBreak != null && formation.BottomBreak != null)
                .WithMessage("Нижняя разбивка должна быть ниже, чем верхняя.");
        }
    }
}