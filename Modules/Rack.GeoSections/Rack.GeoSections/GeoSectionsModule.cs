using System.IO;
using System.Reactive.Linq;
using System.Text;
using DryIoc;
using Rack.GeoSections.Services;
using Rack.GeoSections.ViewModels;
using Rack.GeoSections.Views;
using Rack.Localization;
using Rack.Shared;
using Rack.Shared.Configuration;
using Rack.Shared.Help;
using Rack.Shared.Modularity;
using ReactiveUI;

namespace Rack.GeoSections
{
    public sealed class GeoSectionsModule : IModule
    {
        private readonly IContainer _container;
        private readonly IHelpService _helpService;
        private readonly IConfigurationService _configurationService;
        private readonly ApplicationTabs _applicationTabs;
        private readonly ILocalizationService _localizationService;
        private readonly IScreen _hostScreen;
        public const string Name = "Rack.GeoSections";

        public GeoSectionsModule(
            IContainer container,
            IHelpService helpService,
            IConfigurationService configurationService,
            ApplicationTabs applicationTabs,
            ILocalizationService localizationService,
            IScreen hostScreen)
        {
            _container = container;
            _helpService = helpService;
            _configurationService = configurationService;
            _applicationTabs = applicationTabs;
            _localizationService = localizationService;
            _hostScreen = hostScreen;
        }

        string IModule.Name => Name;

        public void RegisterTypes()
        {
            _container.Register<IDataProviderService, DataProviderService>(
                Reuse.Singleton);

            _container.Register<DefaultScalesEditorViewModel>(Reuse.Transient);
            _container.Register<IViewFor<DefaultScalesEditorViewModel>, DefaultScalesEditor>(
                Reuse.Transient);
            _container.Register<GeophysicalViewerViewModel>(Reuse.Transient);
            _container.Register<IViewFor<GeophysicalViewerViewModel>, GeophysicalViewer>(
                Reuse.Transient);
            _container.Register<SectionShapeViewerViewModel>(Reuse.Transient);
            _container.Register<IViewFor<SectionShapeViewerViewModel>, SectionShapeViewer>(
                Reuse.Transient);
            _container.Register<ShapeMakerViewModel>(Reuse.Transient);
            _container.Register<IViewFor<ShapeMakerViewModel>, ShapeMaker>(
                Reuse.Transient);
        }

        public void OnInitialized()
        {
            _helpService.RegisterPage(
                "1. Подготовка данных в Excel",
                File.ReadAllText(@"HelpFiles/Rack.GeoSections.Help.1.md"),
                Name,
                "Русский");
            _helpService.RegisterPage(
                "2. Загрузка данных в Rack",
                File.ReadAllText(@"HelpFiles/Rack.GeoSections.Help.2.md"),
                Name,
                "Русский");
            _helpService.RegisterPage(
                "3. Создание файлов для построения разреза",
                File.ReadAllText(@"HelpFiles/Rack.GeoSections.Help.3.md"),
                Name,
                "Русский");
            _helpService.RegisterPage(
                "4. Построение разреза в GST",
                File.ReadAllText(@"HelpFiles/Rack.GeoSections.Help.4.md"),
                Name,
                "Русский");
            _helpService.RegisterPage(
                "5. Утилита GrdToShpfile",
                File.ReadAllText(@"HelpFiles/Rack.GeoSections.Help.Grd2Shp.md"),
                Name,
                "Русский");
            _helpService.RegisterPage(
                "6. Оформление разреза в ArcMap",
                File.ReadAllText(@"HelpFiles/Rack.GeoSections.Help.5.md"),
                Name,
                "Русский");

            _configurationService
                .RegisterDefaultConfiguration(() => new GeoSectionsSettings());
            _configurationService
                .RegisterDefaultConfiguration(() => new History());

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            _applicationTabs.Add(new ReactiveMenuTab(
                _localizationService.CurrentLanguage.Select(_ => 
                    _localizationService.GetLocalization(Name)[Name]),
                ReactiveCommand.Create(() =>
                {
                    _hostScreen.Router.NavigateAndReset
                        .Execute(_container.Resolve<ShapeMakerViewModel>());
                })));
        }
    }
}