using System;
using System.Collections.Generic;
using System.Reactive;
using Rack.Localization;
using Rack.Navigation;
using Rack.Shared;
using Rack.Shared.Configuration;
using ReactiveUI;

namespace Rack.GeoSections.ViewModels
{
    public sealed class DefaultScalesEditorViewModel : ReactiveViewModel, IDialogViewModel
    {
        public DefaultScalesEditorViewModel(
            IConfigurationService configurationService,
            ILocalizationService localizationService,
            IScreen hostScreen)
            : base(localizationService, hostScreen)
        {
            Settings = configurationService.GetConfiguration<GeoSectionsSettings>().Clone();
            Save = ReactiveCommand.Create(() =>
            {
                configurationService.GetConfiguration<GeoSectionsSettings>().Map(Settings);
                configurationService.SaveConfiguration(Settings);
            });
        }

        public ReactiveCommand<Unit, Unit> Save { get; }

        public GeoSectionsSettings Settings { get; }

        public override IEnumerable<string> LocalizationKeys { get; } = new[] {GeoSectionsModule.Name};

        public void OnDialogOpened(IReadOnlyDictionary<string, object> parameters)
        {
        }

        IReadOnlyDictionary<string, object> IDialogViewModel.OnDialogClosed() =>
            new Dictionary<string, object>();

        public string Title { get; } = "Изменение масштабов по умолчанию";
        public bool CanClose { get; } = true;

        public event Action<IReadOnlyDictionary<string, object>> RequestClose;
    }
}