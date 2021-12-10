using System;
using FluentValidation;
using UnitsNet;

namespace Rack.GeoSections.Model.Validators
{
    public sealed class BuildSettingsValidator: AbstractValidator<BuildSettings>
    {
        public static Lazy<BuildSettingsValidator> Instance { get; } = new Lazy<BuildSettingsValidator>(() => new BuildSettingsValidator());

        public BuildSettingsValidator()
        {
            RuleFor(x => x.HorizontalScale)
                .GreaterThan(0)
                .WithMessage("Масштаб не может быть отрицательным числом или нулём.");
            RuleFor(x => x.VerticalScale)
                .GreaterThan(0)
                .WithMessage("Масштаб не может быть отрицательным числом или нулём.");
            RuleFor(x => x.HorizontalScale)
                .Must(double.IsFinite)
                .WithMessage("Масштаб должен быть конечным числом.");
            RuleFor(x => x.VerticalScale)
                .Must(double.IsFinite)
                .WithMessage("Масштаб должен быть конечным числом.");
            RuleFor(x => x.Offset)
                .GreaterThanOrEqualTo(Length.Zero)
                .WithMessage("Расстояние отступа не может быть отрицательным числом.");
            RuleFor(x => x.HorizontalResolution)
                .GreaterThan(0)
                .WithMessage("Разрешение не может быть отрицательным числом или нулём.");
            RuleFor(x => x.VerticalResolution)
                .GreaterThan(0)
                .WithMessage("Разрешение не может быть отрицательным числом или нулём.");
            RuleFor(x => x.Top)
                .GreaterThan(x => x.Bottom)
                .WithMessage("Верхняя граница разреза не может быть ниже нижней.");
            RuleFor(x => x.Bottom)
                .LessThan(x => x.Top)
                .WithMessage("Нижняя граница разреза не может быть выше верхней.");
            RuleFor(x => x.DecorationColumnsWidth)
                .GreaterThanOrEqualTo(Length.FromMillimeters(1))
                .WithMessage("Ширина колонок оформления должна быть не меньше 1 мм.");
            RuleFor(x => x.DecorationHeadersHeight)
                .GreaterThanOrEqualTo(Length.FromMillimeters(1))
                .WithMessage("Высота заголовков колонок оформления должна быть не меньше 1 мм.");
        }
    }
}