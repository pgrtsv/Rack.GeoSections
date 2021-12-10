using FluentValidation;

namespace Rack.GeoSections.Model.Validators
{
    public class BuildProjectArbitraryValidator : AbstractValidator<BuildProject>
    {
        /// <inheritdoc />
        public BuildProjectArbitraryValidator()
        {
            RuleFor(x => x.Settings).NotEmpty();
            RuleFor(x => x.Wells).NotEmpty()
                .DependentRules(() =>
                    RuleFor(x => x.Wells)
                        .Transform(x => x.Count)
                        .GreaterThanOrEqualTo(2));
        }
    }
}