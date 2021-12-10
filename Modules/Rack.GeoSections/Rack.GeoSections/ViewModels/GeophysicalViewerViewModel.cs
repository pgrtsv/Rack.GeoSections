using System;
using System.Collections.Generic;
using Rack.GeoSections.Model;
using Rack.Localization;
using Rack.Navigation;
using Rack.Shared;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace Rack.GeoSections.ViewModels
{
    public sealed class GeophysicalViewerViewModel : ReactiveViewModel, IDialogViewModel
    {
        public GeophysicalViewerViewModel(
            ILocalizationService localizationService, 
            IScreen hostScreen) 
            : base(localizationService, hostScreen)
        {
        }

        [Reactive]
        public IEnumerable<GeophysicalData> GeophysicalTests { get; private set; }

        public override IEnumerable<string> LocalizationKeys { get; } = new[] {GeoSectionsModule.Name};

        public bool CanClose { get; } = true;
        public event Action<IReadOnlyDictionary<string, object>> RequestClose;

        public void OnDialogOpened(IReadOnlyDictionary<string, object> parameters)
        {
            var well = (Well) parameters["Well"];
            GeophysicalTests = well.GeophysicalData;
            Title = $"Геофизика для скважины {well.Name}";
        }

        public IReadOnlyDictionary<string, object> OnDialogClosed() => new Dictionary<string, object>();

        [Reactive]
        public string Title { get; private set; }
    }
}