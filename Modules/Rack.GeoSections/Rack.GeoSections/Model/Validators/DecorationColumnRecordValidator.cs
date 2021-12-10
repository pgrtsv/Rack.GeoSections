using FluentValidation;

namespace Rack.GeoSections.Model.Validators
{
    public sealed class DecorationColumnRecordValidator : AbstractValidator<DecorationColumnRecord>
    {
        public static readonly DecorationColumnRecordValidator Instance = new DecorationColumnRecordValidator();

        public DecorationColumnRecordValidator()
        {
            RuleFor(x => x.LeftBottom)
                .LessThan(x => x.LeftTop)
                .WithMessage("Нижняя граница не может быть выше верхней.");
            RuleFor(x => x.LeftTop)
                .GreaterThan(x => x.LeftBottom)
                .WithMessage("Верхняя граница не может быть ниже нижней.");
            RuleFor(x => x.RightBottom)
                .LessThan(x => x.RightTop)
                .WithMessage("Нижняя граница не может быть выше верхней.");
            RuleFor(x => x.RightTop)
                .GreaterThan(x => x.RightBottom)
                .WithMessage("Верхняя граница не может быть ниже нижней.");
        }
    }
}