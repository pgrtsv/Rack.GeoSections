using System.Collections;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using ImTools;
using NetTopologySuite.Geometries;
using Rack.GeoSections.Model;
using Polygon = System.Windows.Shapes.Polygon;

namespace Rack.GeoSections.Infrastructure.Controls
{
    public class SectionViewerControl : Canvas
    {
        public static readonly DependencyProperty SectionShapeProperty = DependencyProperty.Register(
            "SectionShape", typeof(LineString), typeof(SectionViewerControl),
            new PropertyMetadata(SectionShapePropertyChangedCallback));

        public static readonly DependencyProperty WellsProperty = DependencyProperty.Register(
            "Wells", typeof(IEnumerable), typeof(SectionViewerControl),
            new PropertyMetadata(WellsPropertyChangedCallback));

        public static readonly DependencyProperty DecorationColumnsProperty = DependencyProperty.Register(
            "DecorationColumns", typeof(IEnumerable), typeof(SectionViewerControl),
            new PropertyMetadata(DecorationColumnsPropertyChangedCallback));

        public static readonly DependencyProperty BuildSettingsProperty = DependencyProperty.Register(
            "BuildSettings", typeof(BuildSettings), typeof(SectionViewerControl),
            new PropertyMetadata(BuildSettingsPropertyChangedCallback));

        private const double PxInCm = 96 / 2.54;

        public LineString SectionShape
        {
            get => (LineString) GetValue(SectionShapeProperty);
            set => SetValue(SectionShapeProperty, value);
        }

        public IEnumerable Wells
        {
            get => (IEnumerable) GetValue(WellsProperty);
            set => SetValue(WellsProperty, value);
        }

        public IEnumerable DecorationColumns
        {
            get => (IEnumerable) GetValue(DecorationColumnsProperty);
            set => SetValue(DecorationColumnsProperty, value);
        }

        public BuildSettings BuildSettings
        {
            get => (BuildSettings) GetValue(BuildSettingsProperty);
            set => SetValue(BuildSettingsProperty, value);
        }

        private bool CanDraw =>
            BuildSettings != null && Wells != null && DecorationColumns != null && SectionShape != null;

        private static void SectionShapePropertyChangedCallback(DependencyObject d,
            DependencyPropertyChangedEventArgs e)
        {
            var viewer = (SectionViewerControl) d;
            if (viewer.CanDraw)
                viewer.Draw();
        }

        private static void BuildSettingsPropertyChangedCallback(DependencyObject d,
            DependencyPropertyChangedEventArgs e)
        {
            var viewer = (SectionViewerControl) d;
            if (viewer.CanDraw)
                viewer.Draw();
        }

        private static void WellsPropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var viewer = (SectionViewerControl) d;
            if (e.OldValue is INotifyCollectionChanged oldObservableCollection)
                CollectionChangedEventManager.RemoveHandler(oldObservableCollection,
                    viewer.OnWellsCollectionChanged);
            if (e.NewValue is INotifyCollectionChanged newObservableCollection)
                CollectionChangedEventManager.AddHandler(newObservableCollection,
                    viewer.OnWellsCollectionChanged);
            if (viewer.CanDraw)
                viewer.Draw();
        }

        private void OnWellsCollectionChanged(object sender, NotifyCollectionChangedEventArgs args)
        {
            if (CanDraw)
                Draw();
        }


        private static void DecorationColumnsPropertyChangedCallback(DependencyObject d,
            DependencyPropertyChangedEventArgs e)
        {
            var viewer = (SectionViewerControl) d;
            if (e.OldValue is INotifyCollectionChanged oldObservableCollection)
                CollectionChangedEventManager.RemoveHandler(oldObservableCollection,
                    viewer.OnDecorationColumnsCollectionChanged);
            if (e.NewValue is INotifyCollectionChanged newObservableCollection)
                CollectionChangedEventManager.AddHandler(newObservableCollection,
                    viewer.OnDecorationColumnsCollectionChanged);
            if (viewer.CanDraw)
                viewer.Draw();
        }

        private void OnDecorationColumnsCollectionChanged(object sender, NotifyCollectionChangedEventArgs args)
        {
            if (CanDraw)
                Draw();
        }

        private void Draw()
        {
            Children.Clear();
            var contentAreaWidth = (SectionShape.Length + BuildSettings.Offset.Meters * 2) 
                                   * BuildSettings.HorizontalScale * PxInCm;
            var contentAreaHeight = 100;
            var contentArea = new Rectangle
            {
                Fill = Brushes.AliceBlue,
                Width = contentAreaWidth,
                Height = contentAreaHeight
            };
            Children.Add(contentArea);
            var isFirstWell = true;
            var distance = 0.0;
            foreach (var well in Wells.Cast<Well>())
            {
                var wellView = new Polygon
                {
                    Points = PointCollection.Parse("0, 17.32, 10, 0, 20, 17.32"),
                    Fill = Brushes.Black
                };
                if (isFirstWell)
                {
                    distance = BuildSettings.Offset.Meters;
                    isFirstWell = false;
                }
                else
                {
                    var wellIndex = SectionShape.Coordinates.IndexOf(well.Point.Coordinate);
                    var distanceToPreviousPoint = well.Point.Coordinate.Distance(SectionShape.Coordinates[wellIndex - 1]);
                    distance += distanceToPreviousPoint;
                }

                SetLeft(wellView, distance * BuildSettings.HorizontalScale * PxInCm - 10);
                Children.Add(wellView);
            }
        }
    }
}