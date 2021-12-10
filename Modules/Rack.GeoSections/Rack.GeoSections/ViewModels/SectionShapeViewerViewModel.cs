using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using NetTopologySuite.Geometries;
using Rack.GeoSections.Model;
using Rack.GeoTools;
using Rack.Localization;
using Rack.Navigation;
using Rack.Shared;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace Rack.GeoSections.ViewModels
{
    public sealed class DrawableWell : ILabeledSpatial
    {
        public DrawableWell(Well well)
        {
            Well = well;
            Description = well.Name;
            Geometry = well.Point;
        }

        public Well Well { get; }
        public string Description { get; }
        public Geometry Geometry { get; }
    }

    public sealed class SectionShape : ISpatial
    {
        public SectionShape(LineString sectionShape) => Geometry = sectionShape;
        public Geometry Geometry { get; }
    }

    public sealed class SectionShapeViewerViewModel : ReactiveViewModel, IDialogViewModel
    {
        private readonly ObservableAsPropertyHelper<string> _coordinates;
        private readonly BehaviorSubject<BuildProject> _buildProject;
        private IDisposable _localCleanUp = Disposable.Empty;

        public SectionShapeViewerViewModel(
            ILocalizationService localizationService,
            IScreen hostScreen)
            : base(localizationService, hostScreen)
        {
            _buildProject = new BehaviorSubject<BuildProject>(null);
            _coordinates = this
                .WhenAnyValue(x => x.X,
                    x => x.Y,
                    (x, y) => $"X:{x:F}; Y:{y:F}")
                .ToProperty(this, nameof(Coordinates));

            _layers = _buildProject.Select(project => project == null
                    ? Observable.Empty<(BuildProject, LineString)>()
                    : project.SectionPath.Select(sectionPath => (project, sectionPath)))
                .Switch()
                .Select(x =>
                {
                    var (project, sectionPath) = x;
                    return new IDrawableLayer[]
                    {
                        new DrawableLayer<DrawableWell>("Скважины", new Style
                        {
                            IsLabeled = true,
                            ActiveFillColor = new Color(0, 0, 0, 255),
                            MainFillColor = new Color(0, 0, 0, 255),
                            StrokeThickness = 5,
                            FontBackgroundColor = new Color(255, 255, 255, 100)
                        }, project.GetActiveWells().Select(well => new DrawableWell(well)).ToArray()),
                        new DrawableLayer<SectionShape>("Линия разреза", new Style
                        {
                            ActiveFillColor = new Color(0, 0, 0, 255),
                            MainFillColor = new Color(0, 0, 0, 255),
                            StrokeColor = new Color(0, 0, 0, 255),
                            StrokeThickness = 2,
                        }, new[] {new SectionShape(sectionPath)})
                    };
                })
                .ToProperty(this, nameof(Layers));
        }

        public IReadOnlyCollection<IDrawableLayer> Layers => _layers.Value;
        private readonly ObservableAsPropertyHelper<IDrawableLayer[]> _layers;

        [Reactive]
        public double X { get; set; }

        [Reactive]
        public double Y { get; set; }

        public string Coordinates => _coordinates.Value;

        public override IEnumerable<string> LocalizationKeys { get; } =
            new[] {GeoSectionsModule.Name};

        public void OnDialogOpened(IReadOnlyDictionary<string, object> parameters)
        {
            var projectObservable = (IObservable<BuildProject>) parameters[nameof(BuildProject)];
            _localCleanUp = projectObservable.Subscribe(_buildProject);
        }

        IReadOnlyDictionary<string, object> IDialogViewModel.OnDialogClosed()
        {
            _localCleanUp.Dispose();
            _buildProject.Dispose();
            _coordinates.Dispose();
            return new Dictionary<string, object>();
        }

        public string Title { get; } = "Линия разреза";
        public bool CanClose { get; } = true;
        public event Action<IReadOnlyDictionary<string, object>> RequestClose;
    }
}