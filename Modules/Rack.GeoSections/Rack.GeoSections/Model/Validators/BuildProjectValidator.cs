using System.Linq;
using FluentValidation;

namespace Rack.GeoSections.Model.Validators
{
    public sealed class BuildProjectValidator : AbstractValidator<BuildProject>
    {
        /// <inheritdoc />
        public BuildProjectValidator()
        {
            RuleFor(x => x.Settings)
                .SetValidator(new BuildSettingsValidator());
            RuleFor(x => x.ActiveWells)
                .Must((project, _) =>  project.GetActiveWells().Count() >= 2)
                .WithMessage("Количество скважин должно быть не меньше двух.");
            RuleFor(x => x.StructuralMapsObservable)
                .Must((project, _) => project.StructuralMaps.Count() >= 2)
                .WithMessage("Для построения необходимо загрузить не менее двух структурных карт.");
            RuleFor(x => x.SectionPath)
                .Must((project, _) => project.GetSectionPath() != null)
                .WithMessage("Для построения должна быть определена линия разреза.")
                .DependentRules(() =>
                {
                    RuleFor(x => x.StructuralMapsObservable)
                        .Must((project, _) => project.StructuralMaps.All(map =>
                            map.Envelope.Contains(project.GetSectionPath().EnvelopeInternal)))
                        .WithMessage(
                            "Линия разреза должна проходить внутри областей всех структурных карт.");
                });
            RuleFor(x => x.OilBearingFormationsObservable)
                .Must((project, _) => project.OilBearingFormations.All(formation =>
                    !formation.HasErrors))
                .WithMessage("Нефтеносные пласты не должны содержать ошибок.");
            RuleFor(x => x.WellLabelsObservable)
                .Must((project, _) => project.WellLabels.All(label =>
                    !label.HasErrors))
                .WithMessage("Подписи на скважинах не должны содержать ошибок.");
        }
    }
}