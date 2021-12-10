using System.Reactive.Disposables;
using Rack.GeoSections.ViewModels;
using ReactiveUI;

namespace Rack.GeoSections.Views
{
    /// <summary>
    /// Interaction logic for GeophysicalViewer.xaml
    /// </summary>
    public partial class GeophysicalViewer : ReactiveUserControl<GeophysicalViewerViewModel>
    {
        public GeophysicalViewer()
        {
            InitializeComponent();

            this.WhenActivated(cleanUp =>
            {
                this.OneWayBind(
                    ViewModel,
                    x => x.GeophysicalTests,
                    x => x.GeophysicalTestsDataGrid.ItemsSource)
                    .DisposeWith(cleanUp);
            });
        }
    }
}
