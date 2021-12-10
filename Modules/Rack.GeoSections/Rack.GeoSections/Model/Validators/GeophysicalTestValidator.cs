using System;
using FluentValidation;
using UnitsNet;

namespace Rack.GeoSections.Model.Validators
{
    public sealed class GeophysicalTestValidator : AbstractValidator<GeophysicalData>
    {
        public GeophysicalTestValidator()
        {
            RuleFor(x => x.Top)
                .GreaterThanOrEqualTo(Length.Zero);
            RuleFor(x => x.Bottom)
                .GreaterThanOrEqualTo(Length.Zero);
            RuleFor(x => x.Top)
                .LessThan(x => x.Bottom);
        }
    }
}