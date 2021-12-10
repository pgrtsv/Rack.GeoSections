using System.Linq;
using System.Reactive.Disposables;
using Rack.GeoSections.ViewModels;
using ReactiveUI;

namespace Rack.GeoSections.Views
{
    public partial class SectionShapeViewer : ReactiveUserControl<SectionShapeViewerViewModel>
    {
        public SectionShapeViewer()
        {
            InitializeComponent();

            this.WhenActivated(cleanUp =>
            {
                this.OneWayBind(
                    ViewModel,
                    x => x.Layers,
                    x => x.MapCanvas.Layers)
                    .DisposeWith(cleanUp);

                this.Bind(
                        ViewModel,
                        x => x.X,
                        x => x.MapCanvas.X)
                    .DisposeWith(cleanUp);

                this.Bind(
                    ViewModel,
                    x => x.Y,
                    x => x.MapCanvas.Y)
                    .DisposeWith(cleanUp);

                this.OneWayBind(
                        ViewModel,
                        x => x.Coordinates,
                        x => x.CoordinatesTextBlock.Text)
                    .DisposeWith(cleanUp);

            });
        }
    }
}
