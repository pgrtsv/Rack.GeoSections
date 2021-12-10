using System;
using System.Reactive;
using System.Reactive.Concurrency;
using Rack.GeoSections.Model;
using Rack.GeoTools;

namespace Rack.GeoSections.Services
{
    public sealed class LoadFromExcelException : Exception
    {
        public abstract class ExceptionKind
        {
            public abstract T Match<T>(
                Func<IOError, T> ioError,
                Func<ParseError, T> parseError);

            public sealed class IOError : ExceptionKind
            {
                public enum ErrorKind
                {
                    FileNotFound,
                    IOError
                }

                public IOError(string path, ErrorKind kind)
                {
                    Path = path;
                    Kind = kind;
                }

                public string Path { get; }

                public ErrorKind Kind { get; }

                /// <inheritdoc />
                public override T Match<T>(Func<IOError, T> ioError,
                    Func<ParseError, T> parseError) =>
                    ioError.Invoke(this);
            }

            public abstract class ParseError : ExceptionKind
            {
                /// <inheritdoc />
                protected ParseError(string path, string worksheet)
                {
                    Path = path;
                    Worksheet = worksheet;
                }

                public abstract T Match<T>(
                    Func<RequiredSheetNotFound, T> requriedSheetNotFound,
                    Func<WrongCellType, T> wrongCellType,
                    Func<ColumnHeaderNotFound, T> columnHeaderNotFound,
                    Func<EmptyWells, T> emptyWells,
                    Func<WellWithoutGeophysicalData, T> wellWithoutGeophysicalData,
                    Func<BreakIsNotFullySpecified, T> breakIsNotFullySpecified, 
                    Func<WellNotFound, T> wellNotFound);

                public string Path { get; }

                public string Worksheet { get; }

                /// <inheritdoc />
                public sealed override T Match<T>(Func<IOError, T> ioError,
                    Func<ParseError, T> parseError) =>
                    parseError.Invoke(this);

                public sealed class RequiredSheetNotFound : ParseError
                {
                    /// <inheritdoc />
                    public RequiredSheetNotFound(string path, string worksheet)
                        : base(path, worksheet)
                    {
                    }

                    /// <inheritdoc />
                    public override T Match<T>(
                        Func<RequiredSheetNotFound, T> requriedSheetNotFound,
                        Func<WrongCellType, T> wrongCellType,
                        Func<ColumnHeaderNotFound, T> columnHeaderNotFound,
                        Func<EmptyWells, T> emptyWells,
                        Func<WellWithoutGeophysicalData, T> wellWithoutGeophysicalData,
                        Func<BreakIsNotFullySpecified, T> breakIsNotFullySpecified,
                        Func<WellNotFound, T> wellNotFound) =>
                        requriedSheetNotFound.Invoke(this);
                }

                public sealed class WrongCellType : ParseError
                {
                    public enum CellType
                    {
                        Boolean,
                        Numeric,
                        Length,
                        ColumnMode,
                        Encoding
                    }

                    /// <summary>
                    /// Адрес ячейки с ошибкой.
                    /// </summary>
                    public string Address { get; }

                    /// <summary>
                    /// Значение в ячейке с ошибкой.
                    /// </summary>
                    public string Value { get; }

                    /// <summary>
                    /// Ожидаемый тип значения в ячейке.
                    /// </summary>
                    public CellType ExpectedCellType { get; }

                    /// <inheritdoc />
                    public WrongCellType(string path,
                        string worksheet,
                        string address,
                        string value,
                        CellType expectedCellType)
                        : base(path, worksheet)
                    {
                        Address = address;
                        Value = value;
                        ExpectedCellType = expectedCellType;
                    }

                    /// <inheritdoc />
                    public override T Match<T>(Func<RequiredSheetNotFound, T> requriedSheetNotFound,
                        Func<WrongCellType, T> wrongCellType,
                        Func<ColumnHeaderNotFound, T> columnHeaderNotFound,
                        Func<EmptyWells, T> emptyWells,
                        Func<WellWithoutGeophysicalData, T> wellWithoutGeophysicalData,
                        Func<BreakIsNotFullySpecified, T> breakIsNotFullySpecified,
                        Func<WellNotFound, T> wellNotFound) =>
                        wrongCellType.Invoke(this);
                }

                public sealed class ColumnHeaderNotFound : ParseError
                {
                    /// <inheritdoc />
                    public ColumnHeaderNotFound(string header, string path, string worksheet)
                        : base(path, worksheet) => Header = header;

                    /// <summary>
                    /// Искомый заголовок колонки.
                    /// </summary>
                    public string Header { get; }

                    /// <inheritdoc />
                    public override T Match<T>(
                        Func<RequiredSheetNotFound, T> requriedSheetNotFound,
                        Func<WrongCellType, T> wrongCellType,
                        Func<ColumnHeaderNotFound, T> columnHeaderNotFound,
                        Func<EmptyWells, T> emptyWells,
                        Func<WellWithoutGeophysicalData, T> wellWithoutGeophysicalData,
                        Func<BreakIsNotFullySpecified, T> breakIsNotFullySpecified,
                        Func<WellNotFound, T> wellNotFound) =>
                        columnHeaderNotFound.Invoke(this);
                }

                public sealed class EmptyWells : ParseError
                {
                    /// <inheritdoc />
                    public override T Match<T>(Func<RequiredSheetNotFound, T> requriedSheetNotFound,
                        Func<WrongCellType, T> wrongCellType,
                        Func<ColumnHeaderNotFound, T> columnHeaderNotFound,
                        Func<EmptyWells, T> emptyWells,
                        Func<WellWithoutGeophysicalData, T> wellWithoutGeophysicalData,
                        Func<BreakIsNotFullySpecified, T> breakIsNotFullySpecified,
                        Func<WellNotFound, T> wellNotFound) =>
                        emptyWells.Invoke(this);

                    /// <inheritdoc />
                    public EmptyWells(string path, string worksheet) : base(path, worksheet)
                    {
                    }
                }

                public sealed class WellWithoutGeophysicalData : ParseError
                {
                    /// <inheritdoc />
                    public WellWithoutGeophysicalData(string path, string worksheet, string well) :
                        base(path, worksheet) => Well = well;

                    public string Well { get; }

                    /// <inheritdoc />
                    public override T Match<T>(
                        Func<RequiredSheetNotFound, T> requriedSheetNotFound,
                        Func<WrongCellType, T> wrongCellType,
                        Func<ColumnHeaderNotFound, T> columnHeaderNotFound,
                        Func<EmptyWells, T> emptyWells,
                        Func<WellWithoutGeophysicalData, T> wellWithoutGeophysicalData,
                        Func<BreakIsNotFullySpecified, T> breakIsNotFullySpecified,
                        Func<WellNotFound, T> wellNotFound) =>
                        wellWithoutGeophysicalData.Invoke(this);
                }

                public sealed class BreakIsNotFullySpecified : ParseError
                {
                    public string WellNotFound { get; }

                    public int Row { get; }

                    /// <inheritdoc />
                    public override T Match<T>(
                        Func<RequiredSheetNotFound, T> requriedSheetNotFound,
                        Func<WrongCellType, T> wrongCellType,
                        Func<ColumnHeaderNotFound, T> columnHeaderNotFound,
                        Func<EmptyWells, T> emptyWells,
                        Func<WellWithoutGeophysicalData, T> wellWithoutGeophysicalData,
                        Func<BreakIsNotFullySpecified, T> breakIsNotFullySpecified,
                        Func<WellNotFound, T> wellNotFound) =>
                        breakIsNotFullySpecified.Invoke(this);

                    /// <inheritdoc />
                    public BreakIsNotFullySpecified(
                        string path,
                        string worksheet,
                        string wellNotFound,
                        int row)
                        : base(path, worksheet)
                    {
                        WellNotFound = wellNotFound;
                        Row = row;
                    }
                }

                public sealed class WellNotFound : ParseError
                {
                    public string WellName { get; }
                    public string Address { get; }

                    /// <inheritdoc />
                    public WellNotFound(
                        string path, 
                        string worksheet, 
                        string wellName,
                        string address)
                        : base(path, worksheet)
                    {
                        WellName = wellName;
                        Address = address;
                    }

                    /// <inheritdoc />
                    public override T Match<T>(Func<RequiredSheetNotFound, T> requriedSheetNotFound,
                        Func<WrongCellType, T> wrongCellType,
                        Func<ColumnHeaderNotFound, T> columnHeaderNotFound,
                        Func<EmptyWells, T> emptyWells,
                        Func<WellWithoutGeophysicalData, T> wellWithoutGeophysicalData,
                        Func<BreakIsNotFullySpecified, T> breakIsNotFullySpecified,
                        Func<WellNotFound, T> wellNotFound) =>
                        wellNotFound.Invoke(this);
                }
            }
        }

        public ExceptionKind Kind { get; }

        public LoadFromExcelException(ExceptionKind kind)
            : base(kind.ToString()) =>
            Kind = kind;

        public LoadFromExcelException(ExceptionKind kind, Exception innerException)
            : base(kind.ToString(), innerException) =>
            Kind = kind;
    }

    public interface IDataProviderService
    {
        IObservable<BuildProject> BuildProject { get; }

        /// <summary>
        /// true, если сервис не занят.
        /// </summary>
        IObservable<bool> IsFree { get; }

        /// <summary>
        /// Путь к файлу Excel, из которого загружены данные. string.Empty, если файл не загружен.
        /// </summary>
        IObservable<string> Path { get; }

        /// <summary>
        /// Дата и время перезагрузки файла.
        /// </summary>
        IObservable<string> FileReload { get; }

        /// <summary>
        /// Исключения, возникающие при попытке перезагрузки файла.
        /// </summary>
        IObservable<LoadFromExcelException> FileReloadException { get; }

        /// <summary>
        /// true, если отслеживаются изменения в файле Excel (по умолчанию после загрузки).
        /// Если при перезагрузке файла при изменении возникает ошибка, отслеживание прекращается
        /// и возвращается false.
        /// </summary>
        IObservable<bool> IsTrackingExcelFile { get; }

        /// <summary>
        /// Загружает проект из файла Excel в фоне. В случае удачной загрузки начинает отслеживание
        /// изменений в файле и перезагружает его.
        /// </summary>
        /// <param name="path">Путь к файлу с данными.</param>
        /// <exception cref="LoadFromExcelException"></exception>
        IObservable<Unit> LoadFromExcel(string path);

        IObservable<Unit> SaveChanges();

        /// <summary>
        /// Создаёт образец файла Excel с данными.
        /// </summary>
        /// <param name="fileName">Путь к файлу-образцу.</param>
        /// <param name="fillMockData">Если true, листы будут заполнены данными для примера.</param>
        IObservable<Unit> GenerateExcelSample(
            string fileName,
            bool fillMockData = false);

        /// <summary>
        /// Загружает поверхность горизонта из грид-файла.
        /// </summary>
        /// <param name="fileName">Путь к грид-файлу.</param>
        /// <exception cref="SurfaceReadFileException">File: string - путь к файлу.</exception>
        IObservable<Unit> LoadHorizonSurface(string fileName);

        /// <summary>
        /// Удаляет загруженную поверхность горизонта.
        /// </summary>
        /// <param name="structuralMap"></param>
        void RemoveHorizonSurface(StructuralMap structuralMap);
    }
}