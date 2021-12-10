using FluentValidation;

namespace Rack.GeoSections.Model.Validators
{
    public sealed class WellLabelValidator : AbstractValidator<WellLabel>
    {
        /// <inheritdoc />
        public WellLabelValidator()
        {
            RuleFor(x => x.Text)
                .NotEmpty()
                .WithMessage("Необходимо указать текст надписи.");

            RuleFor(x => x.Well)
                .NotEmpty()
                .WithMessage("Необходимо указать скважину для надписи.");

            RuleFor(x => x.Top)
                .GreaterThan(x => x.Bottom)
                .WithMessage("Верхняя граница надписи не может быть глубже нижней.");

            RuleFor(x => x.Bottom)
                .LessThan(x => x.Top)
                .WithMessage("Нижняя граница надписи не может быть выше верхней.");
        }
    }
}