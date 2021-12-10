using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.Serialization;
using DynamicData;
using DynamicData.Binding;
using FluentValidation;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using Rack.CrossSectionUtils.Extensions;
using Rack.CrossSectionUtils.Model;
using Rack.CrossSectionUtils.Utils;
using Rack.GeoSections.Model.Validators;
using Rack.GeoTools.Extensions;
using Rack.Shared;
using Rack.Shared.FluentValidation;
using ReactiveUI;
using UnitsNet;

namespace Rack.GeoSections.Model
{
    /// <summary>
    /// Агрегат для построения разреза.
    /// </summary>
    [DataContract]
    public sealed class BuildProject : ReactiveObject, INotifyDataErrorInfo
    {
        public BuildProject(
            IEnumerable<Well> wells,
            IEnumerable<Break> breaks,
            BuildSettings settings,
            IEnumerable<DecorationColumn> decorationColumns,
            IEnumerable<StructuralMap> structuralMaps,
            IEnumerable<OilBearingFormation> oilBearingFormations,
            IEnumerable<WellLabel> wellLabels)
        {
            Wells = wells.ToArray();
            Breaks = breaks.ToArray();
            Settings = settings;
            DecorationColumns = decorationColumns.ToArray();
            _structuralMaps.AddOrUpdate(structuralMaps);
            _oilBearingFormations.AddRange(oilBearingFormations);
            _wellLabels.AddRange(wellLabels);

            new BuildProjectArbitraryValidator().ValidateAndThrow(this);

            ProjectChanged = Settings
                .WhenAnyPropertyChanged()
                .Select(_ => Unit.Default);

            ActiveWells = Wells.Select(well => well.WhenValueChanged(x => x.IsEnabled))
                .Merge()
                .Select(_ => GetActiveWells());

            _sectionPath = new BehaviorSubject<LineString>(default);
            Observable.CombineLatest(
                    settings.WhenAnyPropertyChanged(
                            nameof(BuildSettings.Offset),
                            nameof(BuildSettings.IsOffsetScaled),
                            nameof(BuildSettings.HorizontalScale))
                        .StartWith(settings),
                        ActiveWells,
                    (settings, _) => settings)
                .StartWith(settings)
                .Select(_ => GetActiveWells().Count() >= 2 
                    ? CreateSectionPath()
                    : null)
                .Subscribe(_sectionPath);

            _mainAreaHeight = new BehaviorSubject<Length>(Length.Zero);
            Settings.WhenChanged(
                    x => x.VerticalScale,
                    x => x.Top,
                    x => x.Bottom,
                    (_, verticalScale, top, bottom) =>
                        (top - bottom) * verticalScale)
                .Subscribe(_mainAreaHeight);
            _mainAreaWidth = new BehaviorSubject<Length>(Length.Zero);
            Observable.CombineLatest(
                    Settings.WhenValueChanged(y => y.HorizontalScale),
                    SectionPath,
                    (scale, sectionPath) =>
                        sectionPath == null
                            ? Length.Zero
                            : Length.FromMeters(sectionPath.Length) * scale)
                .Subscribe(_mainAreaWidth);

            _sectionHeight = new BehaviorSubject<Length>(Length.Zero);
            Observable.CombineLatest(
                    MainAreaHeight,
                    Settings.WhenValueChanged(x => x.DecorationHeadersHeight),
                    (mainAreaHeight, decorationHeadersHeight) =>
                        mainAreaHeight + decorationHeadersHeight)
                .Subscribe(_sectionHeight);

            _sectionWidth = new BehaviorSubject<Length>(Length.Zero);
            Observable.CombineLatest(
                    MainAreaWidth,
                    DecorationColumns
                        .Select(column => column.WhenValueChanged(x => x.Mode))
                        .Merge()
                        .Select(_ => DecorationColumns.Select(x => x.Mode switch
                        {
                            DecorationColumnMode.None => 0,
                            DecorationColumnMode.Left => 1,
                            DecorationColumnMode.Right => 1,
                            DecorationColumnMode.LeftAndRight => 2,
                            _ => throw new ArgumentOutOfRangeException(nameof(x.Mode), x.Mode, null)
                        }).Sum()),
                    Settings.WhenValueChanged(x => x.DepthColumnMode)
                        .Select(mode => mode switch
                        {
                            DecorationColumnMode.None => 0,
                            DecorationColumnMode.Left => 1,
                            DecorationColumnMode.Right => 1,
                            DecorationColumnMode.LeftAndRight => 2,
                            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
                        }),
                    Settings.WhenValueChanged(x => x.DecorationColumnsWidth),
                    (mainAreaWidth, columnsCount, depthRulersCount, columnWidth) =>
                        mainAreaWidth + (columnsCount + depthRulersCount) * columnWidth)
                .Subscribe(_sectionWidth);

            _verticalPointsCount = new BehaviorSubject<int>(default);
            Observable.CombineLatest(
                    Settings.WhenValueChanged(x => x.VerticalResolution),
                    MainAreaHeight,
                    (verticalResolution, mainAreaHeight) =>
                    {
                        if (verticalResolution <= 0 ||
                            !double.IsFinite(verticalResolution) ||
                            mainAreaHeight <= Length.Zero ||
                            verticalResolution * mainAreaHeight.Centimeters > int.MaxValue)
                            return 0;
                        return Convert.ToInt32(verticalResolution * mainAreaHeight.Centimeters);
                    })
                .Subscribe(_verticalPointsCount);

            _horizontalPointsCount = new BehaviorSubject<int>(default);
            Observable.CombineLatest(
                    Settings.WhenValueChanged(x => x.HorizontalResolution),
                    MainAreaWidth,
                    (resolution, length) =>
                    {
                        if (resolution <= 0 ||
                            !double.IsFinite(resolution) ||
                            length <= Length.Zero ||
                            resolution * length.Centimeters > int.MaxValue)
                            return 0;
                        return Convert.ToInt32(resolution * length.Centimeters);
                    })
                .Subscribe(_horizontalPointsCount);

            Func<IReadOnlyCollection<StructuralMap>, LineString, bool> areStructuralMapsValid =
                (maps, sectionPath) =>
                    maps.Count >= 2 && sectionPath != null && maps.All(map =>
                        map.Envelope.Contains(sectionPath.EnvelopeInternal));

            BreakInfos = Observable.CombineLatest(
                    SectionPath,
                    StructuralMapsObservable.RemoveKey().QueryWhenChanged(),
                    (sectionPath, structuralMaps) =>
                        areStructuralMapsValid.Invoke(structuralMaps, sectionPath)
                            ? Breaks
                                .Select(@break => GetBreakInfo(@break, sectionPath))
                                .ToArray()
                            : Breaks
                                .Select(@break => new BreakInfo(@break, Length.Zero, Length.Zero))
                                .ToArray())
                .ToObservableChangeSet(x => x.Break);

            _validationTemplate = new ReactiveValidationTemplate<BuildProject>(
                new BuildProjectValidator(),
                Observable.Merge(
                        Settings.ErrorsChanges().Select(_ => this),
                        StructuralMapsObservable.Select(_ => this),
                        OilBearingFormationsObservable.Select(_ => this),
                        OilBearingFormationsObservable.QueryWhenChanged(x =>
                                x.Count == 0
                                    ? Observable.Empty<EventPattern<DataErrorsChangedEventArgs>>()
                                    : x.Select(formation => formation.ErrorsChanges())
                                        .Aggregate(Observable.Merge))
                            .Switch()
                            .Select(_ => this),
                        WellLabelsObservable.Select(_ => this),
                        WellLabelsObservable.QueryWhenChanged(x =>
                                x.Count == 0
                                    ? Observable.Empty<EventPattern<DataErrorsChangedEventArgs>>()
                                    : x.Select(label => label.ErrorsChanges())
                                        .Aggregate(Observable.Merge))
                            .Switch()
                            .Select(_ => this),
                        ActiveWells.Select(_ => this))
                    .StartWith(this)
            );

            CanBuildSection = this.ErrorsChanges()
                .Select(_ => !HasErrors)
                .StartWith(!HasErrors);
        }

        private readonly ReactiveValidationTemplate<BuildProject> _validationTemplate;

        /// <summary>
        /// Скважины, участвующие или потенциально участвующие в построении.
        /// </summary>
        [DataMember]
        public IReadOnlyCollection<Well> Wells { get; private set; }

        public IObservable<IEnumerable<Well>> ActiveWells { get; }

        public IEnumerable<Well> GetActiveWells() => Wells.Where(x => x.IsEnabled);

        /// <summary>
        /// Происходит, когда проект меняется.
        /// </summary>
        public IObservable<Unit> ProjectChanged { get; }

        /// <summary>
        /// Разбивки.
        /// </summary>
        [DataMember]
        public IReadOnlyCollection<Break> Breaks { get; }

        /// <summary>
        /// Параметры построения.
        /// </summary>
        [DataMember]
        public BuildSettings Settings { get; }

        /// <summary>
        /// Колонки оформления.
        /// </summary>
        [DataMember]
        public IReadOnlyCollection<DecorationColumn> DecorationColumns { get; }

        private readonly SourceCache<StructuralMap, string> _structuralMaps =
            new SourceCache<StructuralMap, string>(x => x.Path);

        [DataMember]
        public IEnumerable<StructuralMap> StructuralMaps => _structuralMaps.Items;

        /// <summary>
        /// Структурные карты.
        /// </summary>
        public IObservable<IChangeSet<StructuralMap, string>> StructuralMapsObservable =>
            _structuralMaps.Connect();
        
        private readonly SourceList<OilBearingFormation> _oilBearingFormations =
            new SourceList<OilBearingFormation>();

        [DataMember]
        public IEnumerable<OilBearingFormation> OilBearingFormations => 
            _oilBearingFormations.Items;

        public IObservable<IChangeSet<OilBearingFormation>> OilBearingFormationsObservable =>
            _oilBearingFormations.Connect();

        private readonly SourceList<WellLabel> _wellLabels = new SourceList<WellLabel>();

        [DataMember]
        public IEnumerable<WellLabel> WellLabels => _wellLabels.Items;

        public IObservable<IChangeSet<WellLabel>> WellLabelsObservable => _wellLabels.Connect();

        /// <summary>
        /// Линия разреза. Потребитель сам управляет частотой расчёта линии.
        /// </summary>
        public IObservable<LineString> SectionPath => _sectionPath;

        private readonly BehaviorSubject<LineString> _sectionPath;

        public LineString GetSectionPath() => _sectionPath.Value;

        /// <summary>
        /// Количество точек дискретизации в разрезе по вертикали.
        /// </summary>
        public IObservable<int> VerticalPointsCount => _verticalPointsCount;

        private readonly BehaviorSubject<int> _verticalPointsCount;

        /// <summary>
        /// Количество точек дискретизации в разрезе по горизонтали.
        /// </summary>
        public IObservable<int> HorizontalPointsCount => _horizontalPointsCount;

        private readonly BehaviorSubject<int> _horizontalPointsCount;

        /// <summary>
        /// Высота главной области разреза. 
        /// </summary>
        public IObservable<Length> MainAreaHeight => _mainAreaHeight;

        private readonly BehaviorSubject<Length> _mainAreaHeight;

        /// <summary>
        /// Ширина главной области разреза. 
        /// </summary>
        public IObservable<Length> MainAreaWidth => _mainAreaWidth;

        private readonly BehaviorSubject<Length> _mainAreaWidth;

        /// <summary>
        /// Высота разреза с учётом колонок оформления.
        /// </summary>
        public IObservable<Length> SectionHeight => _sectionHeight;

        private readonly BehaviorSubject<Length> _sectionHeight;

        /// <summary>
        /// Ширина разреза с учётом колонок оформления.
        /// </summary>
        public IObservable<Length> SectionWidth => _sectionWidth;

        private readonly BehaviorSubject<Length> _sectionWidth;

        public IObservable<IChangeSet<BreakInfo, Break>> BreakInfos { get; }

        /// <summary>
        /// true, когда можно построить разрез.
        /// </summary>
        public IObservable<bool> CanBuildSection { get; }

        public IObservable<BuildResult> BuildSection() => Observable.Start(() =>
        {
            var horizontalOffset = GetLeftColumnsWidth();
            var sectionPath = _sectionPath.Value;
            var mainAreaWidth = _mainAreaWidth.Value;
            var mainAreaHeight = _mainAreaHeight.Value;
            var horizontalStep = Settings.GetHorizontalStep();
            var verticalStep = Settings.GetVerticalStep();

            var breakLines = GetBreaksLineStrings(
                sectionPath,
                horizontalStep,
                horizontalOffset);
            var wellLabelWidth = Length.FromCentimeters(0.6);

            return new BuildResult(
                this,
                new Dictionary<Well, GeophysicalDataDiscreteValue[]>(GetActiveWells()
                    .Select((well, i) =>
                    {
                        var x = Settings.Offset == Length.Zero
                            ? i == 0
                                ? Length.Zero
                                : Length.FromMeters(
                                    new LineString(sectionPath.Coordinates.Take(i + 1).ToArray())
                                        .Length)
                            : Length.FromMeters(
                                new LineString(sectionPath.Coordinates.Take(i + 2).ToArray())
                                    .Length);
                        var y = Settings.Bottom;
                        var discreteValues = new List<GeophysicalDataDiscreteValue>();
                        while (y < Settings.Top)
                        {
                            var data = well.GeophysicalData.FirstOrDefault(x =>
                                x.AbsoluteBottom <= y && x.AbsoluteTop >= y);
                            discreteValues.Add(new GeophysicalDataDiscreteValue(
                                x,
                                y,
                                x.TransformToCrossSectionXAxis(Settings, horizontalOffset),
                                y.TransformToCrossSectionYAxis(Settings),
                                data?.Value ?? 0));
                            y += verticalStep;
                        }

                        return new KeyValuePair<Well, GeophysicalDataDiscreteValue[]>(well,
                            discreteValues.ToArray());
                    })),
                new Feature(CreateRectangle(
                        new Coordinate(horizontalOffset.Centimeters, mainAreaHeight.Centimeters),
                        new Coordinate(horizontalOffset.Centimeters + mainAreaWidth.Centimeters,
                            0)),
                    new AttributesTable()),
                GetActiveWells().Select((well, i) =>
                {
                    var x = Settings.Offset == Length.Zero
                        ? i == 0
                            ? Length.Zero
                            : Length.FromMeters(
                                new LineString(sectionPath.Coordinates.Take(i + 1).ToArray())
                                    .Length)
                        : Length.FromMeters(
                            new LineString(sectionPath.Coordinates.Take(i + 2).ToArray()).Length);
                    x = x * Settings.HorizontalScale + horizontalOffset;
                    return new Feature(
                        CreateWellLineString(
                            x,
                            mainAreaHeight + Length.FromCentimeters(0.6),
                            Length.FromCentimeters(0.6)),
                        new AttributesTable
                        {
                            {"Well", well.Name},
                            {"Altitude", well.Altitude.Meters},
                            {"Bottom", well.Bottom.Meters}
                        });
                }).ToArray(),
                _structuralMaps.Items
                    .Zip(_structuralMaps.Items
                            .ToCrossSectionRepresentation(sectionPath, Settings, horizontalOffset),
                        (surface, line) =>
                            new Feature(line, new AttributesTable {{"Name", surface.Name}}))
                    .ToArray(),
                GetBreaksFeatures(breakLines, sectionPath, horizontalStep),
                GetActiveWells().Select(well => new Feature(well.Point,
                    new AttributesTable {{"Name", well.Name}})).ToArray(),
                new Feature(sectionPath, new AttributesTable()),
                GetActiveAreas(breakLines, horizontalStep),
                Settings.CreateDecorationColumns(
                        new DecorationColumnSettings(
                            new DepthRulerColumn {Mode = Settings.DepthColumnMode},
                            DecorationColumns),
                        mainAreaWidth)
                    .ToArray(),
                OilBearingFormations.Select(formation => new Feature(new Polygon(
                        new LinearRing(
                            breakLines[formation.TopBreak].Coordinates
                                .Union(breakLines[formation.BottomBreak].Coordinates
                                    .Reverse())
                                .Append(breakLines[formation.TopBreak].Coordinates.First())
                                .ToArray())
                    ), new AttributesTable()))
                    .ToArray(),
                WellLabels
                    .Where(label => label.Top > Settings.Bottom &&
                                    label.Bottom < Settings.Top)
                    .Select(label =>
                    {
                        var well = Wells.GetByName(label.Well);
                        var x = Length.FromMeters(new LineString(sectionPath
                                    .Coordinates[
                                        ..(sectionPath.Coordinates
                                            .IndexOf(well.Point.Coordinate) + 1)])
                                .Length)
                            .TransformToCrossSectionXAxis(Settings, horizontalOffset);
                        var top = label.Top.TransformToCrossSectionYAxis(Settings);
                        var bottom = label.Bottom.TransformToCrossSectionYAxis(Settings);

                        return new Feature(CreateRectangle(
                                new Coordinate((x - wellLabelWidth / 2).Centimeters,
                                    top.Centimeters),
                                new Coordinate((x + wellLabelWidth / 2).Centimeters,
                                    bottom.Centimeters)),
                            new AttributesTable
                            {
                                {"Скважина", label.Well},
                                {"Текст", label.Text}
                            });
                    })
                    .ToArray(),
                Settings.Top < Length.Zero
                    ? null
                    : new Feature(new LineString(new[]
                    {
                        new Coordinate(
                            horizontalOffset.Centimeters,
                            Length.Zero.TransformToCrossSectionYAxis(Settings).Centimeters),
                        new Coordinate(
                            (horizontalOffset + mainAreaWidth).Centimeters,
                            Length.Zero.TransformToCrossSectionYAxis(Settings).Centimeters),
                    }), new AttributesTable())
            );
        });

        public void AddStructuralMap(StructuralMap map) => _structuralMaps.AddOrUpdate(map);

        public void RemoveStructuralMap(StructuralMap map) => _structuralMaps.Remove(map);

        public void AddOilBearingFormation(OilBearingFormation formation) =>
            _oilBearingFormations.Add(formation);

        public void RemoveOilBearingFormation(OilBearingFormation formation) =>
            _oilBearingFormations.Remove(formation);

        public void AddWellLabel(WellLabel wellLabel) =>
            _wellLabels.Add(wellLabel);

        public void RemoveWellLabel(WellLabel wellLabel) =>
            _wellLabels.Remove(wellLabel);

        private LineString CreateSectionPath()
        {
            var unscaledOffset = Settings.GetUnscaledOffset();
            if (unscaledOffset == Length.Zero)
                return new LineString(GetActiveWells().Select(x => x.Point.Coordinate).ToArray());

            var firstWell = GetActiveWells().First();
            var secondWell = GetActiveWells().Skip(1).First();
            var firstOffsetPoint = firstWell.Point.Coordinate.GetOffsetCoordinate(
                secondWell.Point.Coordinate,
                unscaledOffset.Meters);

            var lastWell = GetActiveWells().Last();
            var preLastWell = GetActiveWells().SkipLast(1).Last();
            var lastOffsetPoint = lastWell.Point.Coordinate.GetOffsetCoordinate(
                preLastWell.Point.Coordinate,
                unscaledOffset.Meters);

            return new LineString(GetActiveWells().Select(x => x.Point.Coordinate)
                .Prepend(firstOffsetPoint)
                .Append(lastOffsetPoint)
                .ToArray());
        }

        /// <summary>
        /// Возвращает полилинию, обозначающую скважину.
        /// </summary>
        /// <param name="x">Расстояние от левого края разреза до скважины.</param>
        /// <param name="upperY">Верхняя точка отрисовки линии.</param>
        /// <param name="lowerY">Нижняя точка отрисовки линии.</param>
        /// <returns></returns>
        private LineString CreateWellLineString(Length x, Length upperY, Length lowerY)
        {
            if (upperY < lowerY) throw new ArgumentException();
            return new LineString(new[]
            {
                new Coordinate(x.Centimeters, lowerY.Centimeters),
                new Coordinate(x.Centimeters - 0.2, lowerY.Centimeters),
                new Coordinate(x.Centimeters + 0.2, lowerY.Centimeters),
                new Coordinate(x.Centimeters, lowerY.Centimeters),
                new Coordinate(x.Centimeters, lowerY.Centimeters + 0.5),
                new Coordinate(x.Centimeters + 0.3, lowerY.Centimeters + 0.5),
                new Coordinate(x.Centimeters - 0.3, lowerY.Centimeters + 0.75),
                new Coordinate(x.Centimeters, lowerY.Centimeters + 0.75),
                new Coordinate(x.Centimeters, upperY.Centimeters)
            });
        }


        private BreakInfo GetBreakInfo(
            Break @break,
            LineString sectionPath)
        {
            var (topHorizonSurface, bottomHorizonSurface) =
                GetClosestStructuralMaps(@break);
            var firstWell = GetActiveWells().First();
            var lastWell = GetActiveWells().Last();
            if (Settings.Offset == Length.Zero)
                return new BreakInfo(@break, @break.GetAbsoluteValue(firstWell),
                    @break.GetAbsoluteValue(lastWell));
            var firstOffsetCoordinate = sectionPath.Coordinates.First();
            var lastOffsetCoordinate = sectionPath.Coordinates.Last();
            return new BreakInfo(@break,
                GetBreakDepth(
                    topHorizonSurface.GetZ(firstOffsetCoordinate),
                    bottomHorizonSurface.GetZ(firstOffsetCoordinate),
                    topHorizonSurface.GetZ(firstWell.Point.Coordinate),
                    bottomHorizonSurface.GetZ(firstWell.Point.Coordinate),
                    @break.GetAbsoluteValue(firstWell)),
                GetBreakDepth(
                    topHorizonSurface.GetZ(lastOffsetCoordinate),
                    bottomHorizonSurface.GetZ(lastOffsetCoordinate),
                    topHorizonSurface.GetZ(lastWell.Point.Coordinate),
                    bottomHorizonSurface.GetZ(lastWell.Point.Coordinate),
                    @break.GetAbsoluteValue(lastWell)));
        }

        private Feature[] GetBreaksFeatures(
            Dictionary<Break, LineString> breaks,
            LineString sectionPath,
            Length horizontalStep)
        {
            var features = breaks.Select(x =>
            {
                var @break = x.Key;
                var line = x.Value;
                var (closestTopStructuralMap, closestBottomStructuralMap) =
                    GetClosestStructuralMaps(@break);
                var topAverage = Length.Zero;
                var bottomAverage = Length.Zero;
                var count = 0;
                for (int i = 0; i < sectionPath.Count - 1; i++)
                {
                    var sectionPathPassed = // Длина пройденного пути (м).
                        i == 0
                            ? Length.Zero
                            : Length.FromMeters(
                                new LineString(sectionPath.Coordinates.Take(i + 1).ToArray())
                                    .Length);

                    var currentSectionPathCoordinate = sectionPath[i];

                    while (Length.FromMeters(
                               currentSectionPathCoordinate.Distance(sectionPath[i + 1])) >
                           horizontalStep)
                    {
                        topAverage += closestTopStructuralMap
                            .GetZ(currentSectionPathCoordinate)
                            .TransformToCrossSectionYAxis(Settings);
                        bottomAverage += closestBottomStructuralMap
                            .GetZ(currentSectionPathCoordinate)
                            .TransformToCrossSectionYAxis(Settings);
                        count++;
                        currentSectionPathCoordinate = currentSectionPathCoordinate
                            .GetOffsetCoordinate(sectionPath[i + 1], horizontalStep.Meters,
                                true);
                        sectionPathPassed += horizontalStep;
                    }
                }

                topAverage /= count;
                bottomAverage /= count;

                return new Feature(line,
                    new AttributesTable
                    {
                        {
                            "A", (bottomAverage -
                                  Length.FromMeters(line.Coordinates.Select(x => x.Y)
                                      .Average())) /
                                 (bottomAverage - topAverage)
                        },
                        {"Type", "Middle"}
                    });
            }).ToArray();
            features.Select(x =>
                    (feature: x, maxCoordinate: x.Geometry.Coordinates.Max(y => y.Y)))
                .Aggregate((x, y) => x.maxCoordinate > y.maxCoordinate ? x : y)
                .feature
                .Attributes["Type"] = "Top";
            features.Select(x =>
                    (feature: x, minCoordinate: x.Geometry.Coordinates.Min(y => y.Y)))
                .Aggregate((x, y) => x.minCoordinate < y.minCoordinate ? x : y)
                .feature
                .Attributes["Type"] = "Bottom";
            return features;
        }

        private (StructuralMap, StructuralMap) GetClosestStructuralMaps(Break @break)
        {
            var structuralMapsWithMinMaxValues = _structuralMaps.Items
                .Select(map => (map, minY: map.Values.Min(), maxY: map.Values.Max()))
                .ToArray();
            var averageBreakValue = Length.FromMeters(@break.AbsoluteValues
                .Values
                .Average(length => length.Meters));

            var mapsAbove = structuralMapsWithMinMaxValues
                .Where(x => x.maxY > averageBreakValue)
                .ToArray();
            if (mapsAbove.Length == 0)
                mapsAbove = structuralMapsWithMinMaxValues;

            var closestMapAbove = mapsAbove
                .Aggregate((x, y) => x.maxY < y.maxY ? x : y)
                .map;

            var mapsBelow = structuralMapsWithMinMaxValues
                .Where(x => x.minY < averageBreakValue && x.map != closestMapAbove)
                .ToArray();
            if (mapsBelow.Length == 0)
                mapsBelow = structuralMapsWithMinMaxValues;

            var closestMapBelow = mapsBelow
                .Where(x => x.map != closestMapAbove)
                .Aggregate((x, y) => x.maxY > y.maxY ? x : y)
                .map;

            return (closestMapAbove, closestMapBelow);
        }

        private Dictionary<Break, LineString> GetBreaksLineStrings(
            LineString sectionPath,
            Length horizontalStep,
            Length leftColumnsWidth) => new Dictionary<Break, LineString>(
            Breaks.Select(@break => new
                KeyValuePair<Break, LineString>(
                    @break,
                    GetBreakLineString(
                        @break,
                        sectionPath,
                        horizontalStep,
                        leftColumnsWidth))));

        private Feature[] GetActiveAreas(Dictionary<Break, LineString> breakLines,
            Length horizontalStep)
        {
            var topBreakLine = breakLines
                .Aggregate((x, y) =>
                    x.Value.Coordinates.First().Y > y.Value.Coordinates.First().Y ? x : y)
                .Value;
            var bottomBreakLine = breakLines
                .Aggregate((x, y) =>
                    x.Value.Coordinates.First().Y < y.Value.Coordinates.First().Y ? x : y)
                .Value;
            return new[]
            {
                new Feature(new Polygon(new LinearRing(
                        topBreakLine.Coordinates
                            .Union(bottomBreakLine.Coordinates.Reverse())
                            .Append(topBreakLine.Coordinates.First())
                            .ToArray())),
                    new AttributesTable()),
            };
        }

        private LineString GetBreakLineString(
            Break @break,
            LineString sectionPath,
            Length horizontalStep,
            Length leftColumnsWidth)
        {
            var (topStructuralMap, bottomStructuralMap) =
                GetClosestStructuralMaps(@break);
            var breakCoordinates = sectionPath.Coordinates.SkipLast(1)
                .Zip(sectionPath.Coordinates.Skip(1),
                    (leftSectionPathCoordinate, rightSectionPathCoordinate) =>
                    {
                        var sectionDistance =
                            leftSectionPathCoordinate.Distance(rightSectionPathCoordinate);

                        var leftWell = GetActiveWells().FirstOrDefault(x =>
                            x.Point.Coordinate.Equals(leftSectionPathCoordinate));
                        var rightWell = GetActiveWells().FirstOrDefault(x =>
                            x.Point.Coordinate.Equals(rightSectionPathCoordinate));

                        var pathPassed = leftWell == null
                            ? Length.Zero
                            : Length.FromMeters(
                                new LineString(sectionPath.Coordinates[..(
                                        sectionPath.Coordinates.IndexOf(leftSectionPathCoordinate) +
                                        1)])
                                    .Length);

                        if (leftWell is null && rightWell is null)
                            throw new InvalidOperationException();
                        if ((leftWell is null || rightWell is null) &&
                            Settings.Offset == Length.Zero)
                            throw new InvalidOperationException();

                        var breakCoordinates = new List<Coordinate>();

                        if (leftWell is null)
                        {
                            var rightWellBreakDepth = @break.GetAbsoluteValue(rightWell);
                            var currentSectionPathCoordinate = leftSectionPathCoordinate;
                            while (Length.FromMeters(currentSectionPathCoordinate
                                .Distance(rightSectionPathCoordinate)) >= horizontalStep)
                            {
                                breakCoordinates.Add(new Coordinate(
                                    pathPassed
                                        .TransformToCrossSectionXAxis(Settings, leftColumnsWidth)
                                        .Centimeters,
                                    GetBreakDepth(
                                            topStructuralMap.GetZ(
                                                currentSectionPathCoordinate),
                                            bottomStructuralMap.GetZ(
                                                currentSectionPathCoordinate),
                                            topStructuralMap.GetZ(rightSectionPathCoordinate),
                                            bottomStructuralMap.GetZ(
                                                rightSectionPathCoordinate),
                                            rightWellBreakDepth)
                                        .TransformToCrossSectionYAxis(Settings)
                                        .Centimeters));
                                currentSectionPathCoordinate = currentSectionPathCoordinate
                                    .GetOffsetCoordinate(rightSectionPathCoordinate,
                                        horizontalStep.Meters, true);
                                pathPassed += horizontalStep;
                            }

                            return breakCoordinates;
                        }
                        else if (rightWell is null)
                        {
                            var leftWellBreakDepth = @break.GetAbsoluteValue(leftWell);
                            var currentSectionPathCoordinate = leftSectionPathCoordinate;
                            while (Length.FromMeters(currentSectionPathCoordinate
                                .Distance(rightSectionPathCoordinate)) >= horizontalStep)
                            {
                                breakCoordinates.Add(new Coordinate(
                                    pathPassed
                                        .TransformToCrossSectionXAxis(Settings, leftColumnsWidth)
                                        .Centimeters,
                                    GetBreakDepth(
                                            topStructuralMap.GetZ(currentSectionPathCoordinate),
                                            bottomStructuralMap.GetZ(
                                                currentSectionPathCoordinate),
                                            topStructuralMap.GetZ(
                                                leftSectionPathCoordinate),
                                            bottomStructuralMap.GetZ(
                                                leftSectionPathCoordinate),
                                            leftWellBreakDepth)
                                        .TransformToCrossSectionYAxis(Settings)
                                        .Centimeters));
                                currentSectionPathCoordinate = currentSectionPathCoordinate
                                    .GetOffsetCoordinate(rightSectionPathCoordinate,
                                        horizontalStep.Meters, true);
                                pathPassed += horizontalStep;
                            }

                            var sectionPathLength = Length.FromMeters(sectionPath.Length);
                            breakCoordinates.Add(new Coordinate(
                                sectionPathLength
                                    .TransformToCrossSectionXAxis(Settings, leftColumnsWidth)
                                    .Centimeters,
                                GetBreakDepth(
                                        topStructuralMap.GetZ(rightSectionPathCoordinate),
                                        bottomStructuralMap.GetZ(rightSectionPathCoordinate),
                                        topStructuralMap.GetZ(leftSectionPathCoordinate),
                                        bottomStructuralMap.GetZ(leftSectionPathCoordinate),
                                        leftWellBreakDepth)
                                    .TransformToCrossSectionYAxis(Settings)
                                    .Centimeters));

                            return breakCoordinates;
                        }
                        else
                        {
                            var leftWellBreakDepth = @break.GetAbsoluteValue(leftWell);
                            var rightWellBreakDepth = @break.GetAbsoluteValue(rightWell);
                            var currentSectionPathCoordinate = leftSectionPathCoordinate;

                            while (Length.FromMeters(currentSectionPathCoordinate
                                .Distance(rightSectionPathCoordinate)) >= horizontalStep)
                            {
                                breakCoordinates.Add(new Coordinate(
                                    pathPassed
                                        .TransformToCrossSectionXAxis(Settings, leftColumnsWidth)
                                        .Centimeters,
                                    GetBreakDepth(
                                            topStructuralMap.GetZ(leftSectionPathCoordinate),
                                            bottomStructuralMap.GetZ(
                                                leftSectionPathCoordinate),
                                            leftWellBreakDepth,
                                            topStructuralMap.GetZ(
                                                currentSectionPathCoordinate),
                                            bottomStructuralMap.GetZ(
                                                currentSectionPathCoordinate),
                                            topStructuralMap.GetZ(rightSectionPathCoordinate),
                                            bottomStructuralMap.GetZ(
                                                rightSectionPathCoordinate),
                                            rightWellBreakDepth,
                                            leftSectionPathCoordinate.Distance(
                                                currentSectionPathCoordinate) /
                                            sectionDistance)
                                        .TransformToCrossSectionYAxis(Settings)
                                        .Centimeters));
                                currentSectionPathCoordinate = currentSectionPathCoordinate
                                    .GetOffsetCoordinate(rightSectionPathCoordinate,
                                        horizontalStep.Meters, true);
                                pathPassed += horizontalStep;
                            }

                            return breakCoordinates;
                        }
                    })
                .Aggregate(Enumerable.Empty<Coordinate>(), (x, y) => x.Union(y))
                .ToArray();
            return new LineString(breakCoordinates);
        }

        /// <summary>
        /// Возвращает суммарную ширину колонок слева от разреза в см.
        /// </summary>
        private Length GetLeftColumnsWidth() =>
            DecorationColumns
                .Select(x => x.Mode)
                .Append(Settings.DepthColumnMode)
                .Count(DecorationColumnModeEx.HasLeft) *
            Settings.DecorationColumnsWidth;

        /// <summary>
        /// Возвращает значение глубины для линии разбивки.
        /// </summary>
        /// <param name="top1">Глубина границы над искомой точкой.</param>
        /// <param name="bottom1">Глубина границы под искомой точкой.</param>
        /// <param name="top2">Глубина границы над ближайшим известным значением.</param>
        /// <param name="bottom2">Глубина границы под ближайшим известным значением.</param>
        /// <param name="h2">Известная глубина линии разбивки в ближайшей скважине.</param>
        private Length GetBreakDepth(
            Length top1,
            Length bottom1,
            Length top2,
            Length bottom2,
            Length h2) =>
            bottom1 - (bottom2 - h2) * (bottom1 - top1) / (bottom2 - top2);

        /// <summary>
        /// Возвращает значение глубины для линии разбивки.
        /// </summary>
        /// <param name="top1">Глубина границы над искомой точкой.</param>
        /// <param name="bottom1">Глубина границы под искомой точкой.</param>
        /// <param name="top2">Глубина границы над ближайшим известным значением.</param>
        /// <param name="bottom2">Глубина границы под ближайшим известным значением.</param>
        /// <param name="h2">Известная глубина линии разбивки в ближайшей скважине.</param>
        private Length GetBreakDepth(
            Length top0,
            Length bottom0,
            Length h0,
            Length top1,
            Length bottom1,
            Length top2,
            Length bottom2,
            Length h2,
            double coefficient) =>
            (1 - coefficient) * (bottom1 - (bottom0 - h0) * (bottom1 - top1) / (bottom0 - top0))
            + coefficient * (bottom1 - (bottom2 - h2) * (bottom1 - top1) / (bottom2 - top2));

        /// <summary>
        /// Возвращает прямоугольный полигон.
        /// </summary>
        /// <param name="startCoordinate">Координаты верхнего левого угла.</param>
        /// <param name="endCoordinate">Координаты правого нижнего угла.</param>
        private Polygon CreateRectangle(Coordinate startCoordinate, Coordinate endCoordinate) =>
            new Polygon(new LinearRing(new[]
            {
                startCoordinate,
                new Coordinate(startCoordinate.X, endCoordinate.Y),
                endCoordinate,
                new Coordinate(endCoordinate.X, startCoordinate.Y),
                startCoordinate
            }));

        /// <inheritdoc />
        public IEnumerable GetErrors(string propertyName) =>
            _validationTemplate.GetErrors(propertyName);

        /// <inheritdoc />
        public bool HasErrors => _validationTemplate.HasErrors;

        /// <inheritdoc />
        public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged
        {
            add => _validationTemplate.ErrorsChanged += value;
            remove => _validationTemplate.ErrorsChanged -= value;
        }
    }
}