using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using NetTopologySuite.Geometries;
using Rack.GeoTools;
using Rack.GeoTools.Abstractions.Model;
using UnitsNet;

namespace Rack.GeoSections.Model
{
    /// <summary>
    /// Структурная карта.
    /// </summary>
    [DataContract]
    public sealed class StructuralMap : ISurface
    {
        public StructuralMap(string path, Surface sourceSurface)
        {
            Path = path;
            Name = System.IO.Path.GetFileNameWithoutExtension(path);
            SourceSurface = sourceSurface;
        }

        /// <summary>
        /// Поверхность.
        /// </summary>
        [DataMember]
        public Surface SourceSurface { get; }

        /// <summary>
        /// Название файла, из которого загружена карта.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Путь к файлу, из которого загружена карта.
        /// </summary>
        [DataMember]
        public string Path { get; }

        public IEnumerable<Length> Values => SourceSurface.Values.Select(
            x => Length.FromMeters(-Math.Abs(x)));

        /// <inheritdoc />
        IReadOnlyCollection<double> ISurface.Values => SourceSurface.Values;

        /// <inheritdoc />
        public IReadOnlyCollection<double> Coefficients => SourceSurface.Coefficients;

        public Length GetZ(Coordinate coordinate) => Length.FromMeters(
            -Math.Abs(SourceSurface.GetZ(coordinate.X, coordinate.Y)));

        /// <inheritdoc />
        double ISurface.GetZ(double x, double y) => GetZ(new Coordinate(x, y)).Meters;

        /// <inheritdoc />
        public int XCount => SourceSurface.XCount;

        /// <inheritdoc />
        public int YCount => SourceSurface.YCount;

        public Length XStep => Length.FromMeters(SourceSurface.XStep);

        /// <inheritdoc />
        double ISurface.XStep => XStep.Meters;

        public Length YStep => Length.FromMeters(SourceSurface.YStep);

        /// <inheritdoc />
        double ISurface.YStep => YStep.Meters;

        /// <inheritdoc />
        public Envelope Envelope => SourceSurface.Envelope;

        public Length ZMin => Length.FromMeters(-Math.Abs(SourceSurface.ZMin));
        double ISurface.ZMin => ZMin.Meters;

        public Length ZMax => Length.FromMeters(-Math.Abs(SourceSurface.ZMax));
        double ISurface.ZMax => ZMax.Meters;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        /// <exception cref="SurfaceReadFileException"></exception>
        public static StructuralMap FromGridFile(string filePath) =>
            new StructuralMap(filePath,
                Surface.FromGridFile(filePath));

        public override string ToString() => Name;
    }
}