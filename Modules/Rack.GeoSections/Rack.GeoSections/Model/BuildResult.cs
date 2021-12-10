using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using NetTopologySuite.Features;
using Rack.GeoTools.Shapefiles;

namespace Rack.GeoSections.Model
{
    /// <summary>
    /// Результат построения разреза.
    /// </summary>
    public sealed class BuildResult
    {
        public BuildResult(
            BuildProject project,
            IReadOnlyDictionary<Well, GeophysicalDataDiscreteValue[]> geophysicalData,
            Feature sectionArea,
            IReadOnlyCollection<Feature> wells,
            IReadOnlyCollection<Feature> structuralMaps,
            IReadOnlyCollection<Feature> breaks,
            IReadOnlyCollection<Feature> wellPoints,
            Feature sectionPath,
            IReadOnlyCollection<Feature> activeAreas,
            IReadOnlyCollection<Feature> decorationColumns,
            IReadOnlyCollection<Feature> oilBearingFormations,
            IReadOnlyCollection<Feature> wellLabels,
            Feature zeroMark)
        {
            Project = project;
            Wells = wells;
            DecorationColumns = decorationColumns;
            SectionArea = sectionArea;
            StructuralMaps = structuralMaps;
            Breaks = breaks;
            ActiveAreas = activeAreas;
            OilBearingFormations = oilBearingFormations;
            WellPoints = wellPoints;
            SectionPath = sectionPath;
            ZeroMark = zeroMark;
            WellLabels = wellLabels;
            GeophysicalData = geophysicalData;
        }

        public BuildProject Project { get; }
        public IReadOnlyCollection<Feature> Wells { get; }
        public IReadOnlyCollection<Feature> DecorationColumns { get; }
        public Feature SectionArea { get; }
        public IReadOnlyCollection<Feature> StructuralMaps { get; }

        public IReadOnlyCollection<Feature> Breaks { get; }

        public IReadOnlyCollection<Feature> ActiveAreas { get; }
        public IReadOnlyCollection<Feature> OilBearingFormations { get; }
        public IReadOnlyCollection<Feature> WellPoints { get; }

        public IReadOnlyDictionary<Well, GeophysicalDataDiscreteValue[]> GeophysicalData { get; }
        public Feature SectionPath { get; }
        public Feature ZeroMark { get; }
        public IReadOnlyCollection<Feature> WellLabels { get; }

        private Dictionary<string, IReadOnlyCollection<Feature>> GetFeatures()
        {
            var dictionary = new Dictionary<string, IReadOnlyCollection<Feature>>
            {
                ["Скважины"] = Wells,
                ["Колонки"] = DecorationColumns,
                ["Область"] = new[] {SectionArea},
                ["Структурные карты"] = StructuralMaps,
                ["Разбивки"] = Breaks,
                ["Обрезка"] = ActiveAreas,
                ["Координаты скважин"] = WellPoints,
                ["Линия разреза"] = new[] {SectionPath},
            };
            if (ZeroMark != null)
                dictionary.Add("Нулевая отметка", new[] {ZeroMark});
            if (WellLabels.Any())
                dictionary.Add("Подписи скважин", WellLabels);
            if (OilBearingFormations.Any())
                dictionary.Add("Нефтеносные пласты", OilBearingFormations);
            return dictionary;
        }

        public IObservable<Unit> WriteToDirectory(string directory) => Observable.Start(() =>
        {
            WriteGeophysicalTableToTsv(Path.Combine(directory, "ГИС.tsv"));
            foreach (var (fileName, features) in GetFeatures())
                features.WriteToShapefile(Path.Combine(directory, fileName),
                    Project.Settings.Encoding);
        });

        private void WriteGeophysicalTableToTsv(string path)
        {
            using var writer = File.CreateText(path);
            writer.WriteLine("Номер скважины\tl(м)\tl(масштаб, см)\th(м)\th(масштаб, см)\tPS");
            foreach (var (well, geophysicalData) in GeophysicalData)
            foreach (var discreteValue in geophysicalData)
                writer.WriteLine(string.Join('\t',
                    well.Name,
                    discreteValue.X.Meters,
                    discreteValue.ScaledX.Centimeters,
                    discreteValue.Y.Meters,
                    discreteValue.ScaledY.Centimeters,
                    discreteValue.Value));
        }
    }
}