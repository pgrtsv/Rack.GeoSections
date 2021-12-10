using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using ClosedXML.Excel;
using NetTopologySuite.Geometries;
using Newtonsoft.Json;
using Rack.CrossSectionUtils.Extensions;
using Rack.CrossSectionUtils.Model;
using Rack.GeoSections.Model;
using Rack.GeoTools;
using ReactiveUI;
using UnitsNet;
using UnitsNet.Serialization.JsonNet;
using UnitsNet.Units;
using DecorationColumn = Rack.GeoSections.Model.DecorationColumn;
using DecorationColumnRecord = Rack.GeoSections.Model.DecorationColumnRecord;

namespace Rack.GeoSections.Services
{
    public sealed class DataProviderService : IDataProviderService, IDisposable
    {
        private readonly BehaviorSubject<BuildProject> _buildProject;
        private readonly BehaviorSubject<string> _excelPath;
        private readonly BehaviorSubject<bool> _isTrackingFile;
        private readonly BehaviorSubject<bool> _isFree;
        private readonly Subject<string> _fileReload;
        private readonly Subject<LoadFromExcelException> _fileReloadException;
        private readonly CompositeDisposable _cleanUp;
        private CompositeDisposable _localCleanUp = new CompositeDisposable();

        public DataProviderService()
        {
            _cleanUp = new CompositeDisposable();
            _buildProject = new BehaviorSubject<BuildProject>(null)
                .DisposeWith(_cleanUp);
            _excelPath = new BehaviorSubject<string>(string.Empty)
                .DisposeWith(_cleanUp);
            _isTrackingFile = new BehaviorSubject<bool>(false)
                .DisposeWith(_cleanUp);
            _isFree = new BehaviorSubject<bool>(true)
                .DisposeWith(_cleanUp);
            _fileReload = new Subject<string>()
                .DisposeWith(_cleanUp);
            _fileReloadException = new Subject<LoadFromExcelException>()
                .DisposeWith(_cleanUp);
        }

        /// <inheritdoc />
        public IObservable<BuildProject> BuildProject => _buildProject;

        /// <inheritdoc />
        public IObservable<string> Path => _excelPath;

        /// <inheritdoc />
        public IObservable<string> FileReload => _fileReload;

        /// <inheritdoc />
        public IObservable<LoadFromExcelException> FileReloadException => _fileReloadException;

        /// <inheritdoc />
        public IObservable<bool> IsFree => _isFree;

        /// <inheritdoc />
        public IObservable<bool> IsTrackingExcelFile => _isTrackingFile;

        /// <inheritdoc />
        public IObservable<Unit> LoadFromExcel(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new LoadFromExcelException(
                    new LoadFromExcelException.ExceptionKind.IOError(
                        path,
                        LoadFromExcelException.ExceptionKind.IOError.ErrorKind.FileNotFound));

            return LoadFromExcelImpl(path)
                .Do(_ =>
                {
                    _excelPath.OnNext(path);
                    _localCleanUp.Dispose();
                    _localCleanUp = new CompositeDisposable();
                    var excelFileWatcher = new FileSystemWatcher(
                            System.IO.Path.GetDirectoryName(path),
                            System.IO.Path.GetFileName(path))
                        .DisposeWith(_localCleanUp);
                    Observable.FromEventPattern<RenamedEventHandler, RenamedEventArgs>(
                            x => excelFileWatcher.Renamed += x,
                            x => excelFileWatcher.Renamed -= x)
                        .Throttle(TimeSpan.FromMilliseconds(50))
                        .Select(_ =>
                        {
                            try
                            {
                                LoadFromExcelImpl(path).Wait();
                                return path;
                            }
                            catch (LoadFromExcelException exc)
                            {
                                _isTrackingFile.OnNext(false);
                                _fileReloadException.OnNext(exc);
                                return string.Empty;
                            }
                        })
                        .Where(x => x != string.Empty)
                        .Subscribe(_fileReload)
                        .DisposeWith(_localCleanUp);
                    excelFileWatcher.EnableRaisingEvents = true;
                });
        }

        private void StartBlockingOperation()
        {
            if (!_isFree.Value)
                throw new InvalidOperationException("Only one blocking operation can be started.");
            _isFree.OnNext(false);
        }

        private void FinishBlockingOperation() => _isFree.OnNext(true);

        private IObservable<Unit> LoadFromExcelImpl(string path)
        {
            StartBlockingOperation();
            return Observable.Start(() =>
            {
                XLWorkbook workbook;
                try
                {
                    using var reader = File.Open(path, FileMode.Open, FileAccess.Read,
                        FileShare.ReadWrite);
                    workbook = new XLWorkbook(reader);
                }
                catch (IOException ioException)
                {
                    throw new LoadFromExcelException(
                        new LoadFromExcelException.ExceptionKind.IOError(
                            path, LoadFromExcelException.ExceptionKind.IOError.ErrorKind.IOError),
                        ioException);
                }

                var project = LoadProject(workbook, path);

                workbook.Dispose();

                return project;
            }, RxApp.TaskpoolScheduler)
                .ObserveOnDispatcher()
                .Do(project =>
                {
                    _buildProject.OnNext(project);
                    FinishBlockingOperation();
                }, exception => FinishBlockingOperation())
                .Select(_ => Unit.Default);
        }

        private BuildProject LoadProject(XLWorkbook workbook, string path)
        {
            var wells = LoadWells(workbook, path);
            var breaks = LoadBreaks(workbook, path, wells);
            var settings = LoadSettings(workbook, path);
            return new BuildProject(
                wells,
                breaks,
                settings,
                LoadDecorationColumns(workbook, path),
                LoadStructuralMaps(workbook, path),
                LoadOilBearingFormations(workbook, path, breaks),
                LoadWellLabels(workbook, path, wells)
            );
        }

        private void LoadGeophysicalData(XLWorkbook workbook, string path,
            IReadOnlyCollection<WellDto> wells)
        {
            if (!workbook.TryGetWorksheet(
                SheetNames.GeophysicalData.SheetName,
                out var sheet))
                throw new LoadFromExcelException(
                    new LoadFromExcelException.ExceptionKind.ParseError.RequiredSheetNotFound(
                        path,
                        SheetNames.GeophysicalData.SheetName));
            var header = sheet.FirstRowUsed();
            var wellNameIndex = header.GetRequriedColumnIndex(SheetNames.GeophysicalData.WellName, path);
            var roofIndex = header.GetRequriedColumnIndex(SheetNames.GeophysicalData.Roof, path);
            var soleIndex = header.GetRequriedColumnIndex(SheetNames.GeophysicalData.Sole, path);
            var psIndex = header.GetRequriedColumnIndex(SheetNames.GeophysicalData.Value, path);
            foreach (var row in sheet.RowsUsed(x => !x.Equals(header)))
            {
                var wellName = row.Cell(wellNameIndex).GetString();
                var well = wells.FirstOrDefault(x =>
                    x.Name.Equals(wellName, StringComparison.Ordinal));
                if (well == null)
                    continue;
                var geophysicalData = new GeophysicalData(
                    well.Altitude,
                    row.Cell(roofIndex).GetRequiredLength(path, LengthUnit.Meter),
                    row.Cell(soleIndex).GetRequiredLength(path, LengthUnit.Meter),
                    row.Cell(psIndex).GetRequiredDouble(path));
                well.GeophysicalData.Add(geophysicalData);
            }
        }

        private Well[] LoadWells(XLWorkbook workbook, string path)
        {
            if (!workbook.TryGetWorksheet(SheetNames.Wells.SheetName, out var sheet))
                throw new LoadFromExcelException(
                    new LoadFromExcelException.ExceptionKind.ParseError.RequiredSheetNotFound(
                        path,
                        SheetNames.Wells.SheetName));
            var headerRow = sheet.FirstRowUsed();
            var wellNameColumnIndex = headerRow.GetRequriedColumnIndex(SheetNames.Wells.Name, path);
            var altitudeColumnIndex = headerRow.GetRequriedColumnIndex(SheetNames.Wells.Altitude, path);
            var xColumnIndex = headerRow.GetRequriedColumnIndex(SheetNames.Wells.X, path);
            var yColumnIndex = headerRow.GetRequriedColumnIndex(SheetNames.Wells.Y, path);
            var bottomColumnIndex = headerRow.GetRequriedColumnIndex(SheetNames.Wells.Bottom, path);
            var isWellEnabledColumnIndex = headerRow.GetCellColumnIndex(cell =>
                SheetNames.Wells.IsEnabled.Equals(cell.GetString(), StringComparison.OrdinalIgnoreCase));
            var wells = sheet.RowsUsed(x => !x.Equals(headerRow))
                .Select(row => new WellDto
                {
                    Name = row.Cell(wellNameColumnIndex).GetString(),
                    Altitude = row.Cell(altitudeColumnIndex).GetRequiredLength(path, LengthUnit.Meter),
                    Coordinate = new Coordinate(
                        row.Cell(xColumnIndex).GetRequiredDouble(path),
                        row.Cell(yColumnIndex).GetRequiredDouble(path)),
                    Bottom = row.Cell(bottomColumnIndex).GetRequiredLength(path, LengthUnit.Meter),
                    IsEnabled = isWellEnabledColumnIndex < 0 ||
                                row.Cell(isWellEnabledColumnIndex).GetRequiredBoolean(path)
                })
                .ToList();
            LoadGeophysicalData(workbook, path, wells);

            return wells
                .Select(x => new Well(
                    x.Name,
                    x.Altitude,
                    new Point(x.Coordinate),
                    x.Bottom,
                    x.GeophysicalData.Count > 0
                        ? x.GeophysicalData
                        : throw new LoadFromExcelException(
                            new LoadFromExcelException.ExceptionKind.ParseError.
                                WellWithoutGeophysicalData(
                                    path,
                                    SheetNames.Wells.SheetName,
                                    x.Name)),
                    x.IsEnabled))
                .ToArray();
        }

        private Break[] LoadBreaks(XLWorkbook workbook, string path,
            IReadOnlyCollection<Well> wells)
        {
            if (!workbook.TryGetWorksheet(SheetNames.Breaks.SheetName, out var sheet))
                throw new LoadFromExcelException(
                    new LoadFromExcelException.ExceptionKind.ParseError.RequiredSheetNotFound(
                        path, SheetNames.Breaks.SheetName));
            var wellsRow = sheet.RowsUsed().First();
            var index = 1;
            return sheet.RowsUsed(row => !row.Equals(wellsRow))
                .Select(row =>
                {
                    var valuesInWells = row.CellsUsed()
                        .Select(cell =>
                        {
                            var well = wells.GetByName(
                                    wellsRow.Cell(cell.WorksheetColumn().ColumnNumber())
                                        .GetString());
                            return new KeyValuePair<string, Length>(well.Name, cell.GetRequiredLength(path, LengthUnit.Meter));
                        })
                        .Where(x => x.Key != null)
                        .ToArray();

                    foreach (var well in wells)
                        if (!valuesInWells.Select(x => x.Key)
                            .Contains(well.Name))
                            throw new LoadFromExcelException(
                                new LoadFromExcelException.ExceptionKind.ParseError.
                                    BreakIsNotFullySpecified(
                                        path, SheetNames.Breaks.SheetName, well.Name,
                                        row.RowNumber()));

                    return new Break(valuesInWells, index++.ToString(), wells);
                })
                .ToArray();
        }

        private BuildSettings LoadSettings(XLWorkbook workbook, string path)
        {
            if (!workbook.TryGetWorksheet(SheetNames.Settings.SheetName, out var sheet))
                return new BuildSettings();
            var rowIndex = 1;
            var settings = new BuildSettings
            {
                VerticalScale = ScaleConvert.ConvertFrom(sheet.Cell(rowIndex++, 2).GetString()),
                HorizontalScale = ScaleConvert.ConvertFrom(sheet.Cell(rowIndex++, 2).GetString()),
                VerticalResolution = sheet.Cell(rowIndex++, 2).GetRequiredInt(path),
                HorizontalResolution = sheet.Cell(rowIndex++, 2).GetRequiredInt(path),
                Top = sheet.Cell(rowIndex++, 2).GetRequiredLength(path, LengthUnit.Meter),
                Bottom = sheet.Cell(rowIndex++, 2).GetRequiredLength(path, LengthUnit.Meter),
                IsOffsetScaled = sheet.Cell(rowIndex, 4).GetRequiredBoolean(path),
            };
            settings.Offset = sheet.Cell(rowIndex++, 2)
                .GetRequiredLength(path, settings.IsOffsetScaled
                    ? LengthUnit.Centimeter
                    : LengthUnit.Meter);
            settings.DecorationColumnsWidth = sheet.Cell(rowIndex++, 2)
                .GetRequiredLength(path, LengthUnit.Centimeter);
            settings.DecorationHeadersHeight = sheet.Cell(rowIndex++, 2)
                .GetRequiredLength(path, LengthUnit.Centimeter);
            settings.DepthColumnMode = sheet.Cell(rowIndex++, 2)
                .GetRequiredColumnMode(path);
            settings.Encoding = sheet.Cell(rowIndex++, 2)
                .GetRequiredEncoding(path);
            return settings;
        }

        private IEnumerable<DecorationColumn> LoadDecorationColumns(XLWorkbook workbook,
            string path)
        {
            var sheet = GetRequiredWorksheet(workbook, path,
                SheetNames.DecorationColumns.SheetName);
            DecorationColumn lastDecorationColumn = null;
            var headerRow = sheet.RowsUsed().First();
            foreach (var row in sheet.RowsUsed(x => !x.Equals(headerRow)))
            {
                var header = row.Cell(1).GetString();
                var isModeSpecified = !row.Cell(7).IsEmpty();
                var mode = isModeSpecified
                    ? row.Cell(7).GetRequiredColumnMode(path)
                    : DecorationColumnMode.LeftAndRight;
                if (string.IsNullOrWhiteSpace(header))
                {
                    if (lastDecorationColumn == null)
                    {
                        lastDecorationColumn = new DecorationColumn
                        {
                            Header = string.Empty,
                            Records = new ObservableCollection<DecorationColumnRecord>(),
                            Mode = mode
                        };
                        yield return lastDecorationColumn;
                    }
                }
                else
                {
                    if (lastDecorationColumn == null ||
                        !lastDecorationColumn.Header.Equals(header,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        lastDecorationColumn = new DecorationColumn
                        {
                            Header = header,
                            Records = new ObservableCollection<DecorationColumnRecord>(),
                            Mode = mode
                        };
                        yield return lastDecorationColumn;
                    }
                }

                var record = new DecorationColumnRecord
                {
                    Text = row.Cell(2).GetString(),
                    LeftTop = row.Cell(3).GetOptionalLength(path, LengthUnit.Meter),
                    LeftBottom = row.Cell(4).GetOptionalLength(path, LengthUnit.Meter),
                    RightTop = row.Cell(5).GetOptionalLength(path, LengthUnit.Meter),
                    RightBottom = row.Cell(6).GetOptionalLength(path, LengthUnit.Meter)
                };

                if (record.LeftBottom == default && record.LeftTop == default)
                {
                    if (record.RightBottom != default || record.RightTop != default)
                    {
                        record.LeftBottom = record.RightBottom;
                        record.LeftTop = record.RightTop;
                        if (!isModeSpecified)
                            lastDecorationColumn.Mode = lastDecorationColumn.Mode.AddRight();
                    }
                }
                else
                {
                    lastDecorationColumn.Mode = lastDecorationColumn.Mode.AddLeft();
                    if (record.RightBottom == default && record.RightTop == default)
                    {
                        record.RightBottom = record.LeftBottom;
                        record.RightTop = record.LeftTop;
                    }
                    else
                    {
                        if (!isModeSpecified)
                            lastDecorationColumn.Mode = lastDecorationColumn.Mode.AddRight();
                    }
                }

                lastDecorationColumn.Records.Add(record);
            }
        }

        private IEnumerable<StructuralMap> LoadStructuralMaps(XLWorkbook workbook, string path)
        {
            if (!workbook.TryGetWorksheet(SheetNames.StructuralMaps.SheetName,
                out var sheet))
                return Enumerable.Empty<StructuralMap>();
            return sheet.FirstColumnUsed().CellsUsed()
                .Select(cell => cell.GetString())
                .Where(File.Exists)
                .Select(StructuralMap.FromGridFile);
        }

        private IEnumerable<OilBearingFormation> LoadOilBearingFormations(
            XLWorkbook workbook,
            string path,
            IReadOnlyCollection<Break> breaks)
        {
            if (!workbook.TryGetWorksheet(SheetNames.OilBearingFormations.SheetName, out var sheet))
                return Enumerable.Empty<OilBearingFormation>();
            return sheet.RowsUsed()
                .Skip(1)
                .Select(row => new OilBearingFormation(
                    breaks.First(x => x.Name == row.Cell(1).GetString()),
                    breaks.First(x => x.Name == row.Cell(2).GetString())));
        }

        private IEnumerable<WellLabel> LoadWellLabels(
            XLWorkbook workbook,
            string path,
            IReadOnlyCollection<Well> wells)
        {
            if (!workbook.TryGetWorksheet(SheetNames.WellLabels.SheetName, out var sheet))
                return Enumerable.Empty<WellLabel>();
            return sheet.RowsUsed().Skip(1).Select(row =>
            {
                var wellName = row.Cell(1).GetString();
                var well = wells.FirstOrDefault(x => x.Name == wellName);
                if (well == null)
                    throw new LoadFromExcelException(
                        new LoadFromExcelException.ExceptionKind.ParseError.WellNotFound(
                            path,
                            SheetNames.WellLabels.SheetName,
                            wellName,
                            row.Cell(1).Address.ToString()));
                var top = row.Cell(2).GetRequiredLength(path, LengthUnit.Meter);
                var bottom = row.Cell(3).GetRequiredLength(path, LengthUnit.Meter);
                var text = row.Cell(4).GetString();
                return new WellLabel
                {
                    Well = well.Name,
                    Top = top,
                    Bottom = bottom,
                    Text = text
                };
            });
        }

        private static XLColor RequiredSheetColor = XLColor.Yellow;

        private static XLColor OptionalSheetColor = XLColor.Green;

        private static XLColor TechnicalSheetColor = XLColor.CoolGrey;

        private XLWorkbook SaveProject(BuildProject project)
        {
            var workbook = new XLWorkbook();
            FillWellsSheet(CreateWellsSheet(workbook, true), project.Wells, true);
            FillGeophysicalDataSheet(CreateGeophysicalDataSheet(workbook), project.Wells);
            FillBreaksSheet(CreateBreaksSheet(workbook), project);
            FillDecorationColumnsSheet(CreateDecorationColumnsSheet(workbook),
                project.DecorationColumns);
            FillSettingsSheet(CreateSettingsSheet(workbook), project.Settings);
            if (project.OilBearingFormations.Any())
                FillOilBearingFormationsSheet(CreateOilBearingFormationsSheet(workbook),
                    project.OilBearingFormations);
            if (project.WellLabels.Any())
                FillWellLabelsSheet(CreateWellLabelsSheet(workbook), project.WellLabels);
            if (project.StructuralMaps.Any())
                FillStructuralMapsSheet(CreateStructuralMapsSheet(workbook),
                    project.StructuralMaps);
            foreach (var sheet in workbook.Worksheets)
            foreach (var column in sheet.Columns())
                column.AdjustToContents();
            return workbook;
        }

        private void FillWellsSheet(IXLWorksheet sheet, IEnumerable<Well> wells,
            bool isOptionalDataIncluded)
        {
            var row = 2;
            foreach (var well in wells)
            {
                var column = 1;
                if (isOptionalDataIncluded)
                    sheet.Cell(row, column++).SetValue(well.IsEnabled);
                sheet.Cell(row, column++).SetValue(well.Name);
                sheet.Cell(row, column++).SetCustomValue(well.Altitude);
                sheet.Cell(row, column++).SetValue(well.Point.X);
                sheet.Cell(row, column++).SetValue(well.Point.Y);
                sheet.Cell(row, column++).SetCustomValue(well.Bottom);
                row++;
            }
        }

        private IXLWorksheet CreateWellsSheet(
            XLWorkbook workbook,
            bool isOptionalDataIncluded)
        {
            var sheet = workbook.AddWorksheet(SheetNames.Wells.SheetName);
            sheet.SetTabColor(RequiredSheetColor);
            var i = 1;
            if (isOptionalDataIncluded)
                sheet.Cell(1, i++).SetHeaderValue(SheetNames.Wells.IsEnabled);
            sheet.Cell(1, i++).SetHeaderValue(SheetNames.Wells.Name);
            sheet.Cell(1, i++).SetHeaderValue(SheetNames.Wells.Altitude);
            sheet.Cell(1, i++).SetHeaderValue(SheetNames.Wells.X);
            sheet.Cell(1, i++).SetHeaderValue(SheetNames.Wells.Y);
            sheet.Cell(1, i++).SetHeaderValue(SheetNames.Wells.Bottom);
            return sheet;
        }

        private void FillGeophysicalDataSheet(IXLWorksheet sheet, IEnumerable<Well> wells)
        {
            var row = 2;
            foreach (var (well, data) in wells.SelectMany(
                x => x.GeophysicalData,
                (well, data) => (well, data)))
            {
                var column = 1;
                sheet.Cell(row, column++).SetValue(well.Name);
                sheet.Cell(row, column++).SetCustomValue(data.Top);
                sheet.Cell(row, column++).SetCustomValue(data.Bottom);
                sheet.Cell(row, column++).SetValue(data.Value);
                row++;
            }
        }

        private IXLWorksheet CreateGeophysicalDataSheet(XLWorkbook workbook)
        {
            var sheet = workbook.AddWorksheet(SheetNames.GeophysicalData.SheetName);
            sheet.SetTabColor(RequiredSheetColor);
            var i = 1;
            sheet.Cell(1, i++).SetHeaderValue(SheetNames.GeophysicalData.WellName);
            sheet.Cell(1, i++).SetHeaderValue(SheetNames.GeophysicalData.Roof);
            sheet.Cell(1, i++).SetHeaderValue(SheetNames.GeophysicalData.Sole);
            sheet.Cell(1, i++).SetHeaderValue(SheetNames.GeophysicalData.Value);
            return sheet;
        }

        private void FillDecorationColumnsSheet(
            IXLWorksheet sheet,
            IEnumerable<DecorationColumn> decorationColumns)
        {
            var row = 2;
            foreach (var column in decorationColumns)
            {
                sheet.Cell(row, 1).SetValue(column.Header);
                sheet.Cell(row, 7).SetCustomValue(column.Mode);
                foreach (var record in column.Records)
                {
                    var i = 2;
                    sheet.Cell(row, i++).SetValue(record.Text);
                    sheet.Cell(row, i++).SetCustomValue(record.LeftTop);
                    sheet.Cell(row, i++).SetCustomValue(record.LeftBottom);
                    sheet.Cell(row, i++).SetCustomValue(record.RightTop);
                    sheet.Cell(row, i++).SetCustomValue(record.RightBottom);
                    row++;
                }
            }
        }

        private IXLWorksheet CreateDecorationColumnsSheet(XLWorkbook workbook)
        {
            var sheet = workbook.AddWorksheet(SheetNames.DecorationColumns.SheetName);
            sheet.SetTabColor(OptionalSheetColor);
            var i = 1;
            sheet.Cell(1, i++).SetHeaderValue(SheetNames.DecorationColumns.Header);
            sheet.Cell(1, i++).SetHeaderValue(SheetNames.DecorationColumns.Text);
            sheet.Cell(1, i++).SetHeaderValue(SheetNames.DecorationColumns.TopLeft);
            sheet.Cell(1, i++).SetHeaderValue(SheetNames.DecorationColumns.BottomLeft);
            sheet.Cell(1, i++).SetHeaderValue(SheetNames.DecorationColumns.TopRight);
            sheet.Cell(1, i++).SetHeaderValue(SheetNames.DecorationColumns.BottomRight);
            sheet.Cell(1, i++).SetHeaderValue(SheetNames.DecorationColumns.Mode);
            return sheet;
        }

        private void FillBreaksSheet(IXLWorksheet sheet, BuildProject project)
        {
            var column = 1;
            foreach (var well in project.Wells)
            {
                sheet.Cell(1, column++).SetHeaderValue(well.Name);
            }

            var row = 2;
            foreach (var @break in project.Breaks)
            {
                column = 1;
                foreach (var well in project.Wells)
                    sheet.Cell(row, column++).SetCustomValue(@break[well.Name]);
                row++;
            }
        }

        private IXLWorksheet CreateBreaksSheet(XLWorkbook workbook)
        {
            var sheet = workbook.AddWorksheet(SheetNames.Breaks.SheetName);
            sheet.SetTabColor(OptionalSheetColor);
            return sheet;
        }

        private void FillSettingsSheet(IXLWorksheet sheet, BuildSettings settings)
        {
            var row = 1;
            sheet.Cell(row++, 2).SetValue(ScaleConvert.Convert(settings.VerticalScale));
            sheet.Cell(row++, 2).SetValue(ScaleConvert.Convert(settings.HorizontalScale));
            sheet.Cell(row++, 2).SetValue(settings.VerticalResolution);
            sheet.Cell(row++, 2).SetValue(settings.HorizontalResolution);
            sheet.Cell(row++, 2).SetCustomValue(settings.Top);
            sheet.Cell(row++, 2).SetCustomValue(settings.Bottom);
            sheet.Cell(row++, 2).SetCustomValue(settings.Offset);
            sheet.Cell(row - 1, 4).SetValue(settings.IsOffsetScaled);
            sheet.Cell(row++, 2).SetCustomValue(settings.DecorationColumnsWidth);
            sheet.Cell(row++, 2).SetCustomValue(settings.DecorationHeadersHeight);
            sheet.Cell(row++, 2).SetCustomValue(settings.DepthColumnMode);
            sheet.Cell(row++, 2).SetCustomValue(settings.Encoding);
        }

        private IXLWorksheet CreateSettingsSheet(XLWorkbook workbook)
        {
            var sheet = workbook.AddWorksheet(SheetNames.Settings.SheetName);
            sheet.SetTabColor(TechnicalSheetColor);
            var row = 1;
            sheet.Cell(row++, 1).SetHeaderValue(SheetNames.Settings.VerticalScale);
            sheet.Cell(row++, 1).SetHeaderValue(SheetNames.Settings.HorizontalScale);
            sheet.Cell(row++, 1).SetHeaderValue(SheetNames.Settings.VerticalResolution);
            sheet.Cell(row++, 1).SetHeaderValue(SheetNames.Settings.HorizontalResolution);
            sheet.Cell(row++, 1).SetHeaderValue(SheetNames.Settings.Top);
            sheet.Cell(row++, 1).SetHeaderValue(SheetNames.Settings.Bottom);
            sheet.Cell(row++, 1).SetHeaderValue(SheetNames.Settings.Offset);
            sheet.Cell(row - 1, 3).SetHeaderValue(SheetNames.Settings.IsOffsetScaled);
            sheet.Cell(row++, 1).SetHeaderValue(SheetNames.Settings.DecorationColumnsWidth);
            sheet.Cell(row++, 1).SetHeaderValue(SheetNames.Settings.DecorationHeadersHeight);
            sheet.Cell(row++, 1).SetHeaderValue(SheetNames.Settings.DepthColumnMode);
            sheet.Cell(row++, 1).SetHeaderValue(SheetNames.Settings.Encoding);
            return sheet;
        }

        private void FillOilBearingFormationsSheet(IXLWorksheet sheet,
            IEnumerable<OilBearingFormation> formations)
        {
            var row = 2;
            foreach (var formation in formations)
            {
                var column = 1;
                sheet.Cell(row, column++).SetValue(formation.TopBreak.Name);
                sheet.Cell(row, column++).SetValue(formation.BottomBreak.Name);
                row++;
            }
        }

        private IXLWorksheet CreateOilBearingFormationsSheet(XLWorkbook workbook)
        {
            var sheet = workbook.AddWorksheet(SheetNames.OilBearingFormations.SheetName);
            sheet.SetTabColor(TechnicalSheetColor);
            var column = 1;
            sheet.Cell(1, column++).SetHeaderValue(SheetNames.OilBearingFormations.TopBreak);
            sheet.Cell(1, column++).SetHeaderValue(SheetNames.OilBearingFormations.BottomBreak);
            return sheet;
        }

        private void FillWellLabelsSheet(IXLWorksheet sheet, IEnumerable<WellLabel> wellLabels)
        {
            var row = 2;
            foreach (var label in wellLabels)
            {
                var column = 1;
                sheet.Cell(row, column++).SetValue(label.Well);
                sheet.Cell(row, column++).SetCustomValue(label.Top);
                sheet.Cell(row, column++).SetCustomValue(label.Bottom);
                sheet.Cell(row, column++).SetValue(label.Text);
                row++;
            }
        }

        private IXLWorksheet CreateWellLabelsSheet(XLWorkbook workbook)
        {
            var sheet = workbook.AddWorksheet(SheetNames.WellLabels.SheetName);
            sheet.SetTabColor(TechnicalSheetColor);
            var column = 1;
            sheet.Cell(1, column++).SetHeaderValue(SheetNames.WellLabels.Well);
            sheet.Cell(1, column++).SetHeaderValue(SheetNames.WellLabels.Top);
            sheet.Cell(1, column++).SetHeaderValue(SheetNames.WellLabels.Bottom);
            sheet.Cell(1, column++).SetHeaderValue(SheetNames.WellLabels.Text);
            return sheet;
        }

        private void FillStructuralMapsSheet(IXLWorksheet sheet, IEnumerable<StructuralMap> maps)
        {
            var row = 1;
            foreach (var structuralMap in maps)
                sheet.Cell(row++, 1).SetValue(structuralMap.Path);
        }

        private IXLWorksheet CreateStructuralMapsSheet(XLWorkbook workbook)
        {
            var sheet = workbook.AddWorksheet(SheetNames.StructuralMaps.SheetName);
            sheet.SetTabColor(TechnicalSheetColor);
            return sheet;
        }

        public IObservable<Unit> GenerateExcelSample(
            string fileName,
            bool fillMockData = false) => Observable.Start(() =>
        {
            using var workbook = fillMockData
                ? CreateMockSampleWorkbook(System.IO.Path.GetDirectoryName(fileName))
                : CreateEmptySampleWorkbook();
            using var writer = File.Create(fileName);
            workbook.SaveAs(writer);
        });

        private XLWorkbook CreateEmptySampleWorkbook()
        {
            var workbook = new XLWorkbook();
            CreateWellsSheet(workbook, false);
            CreateGeophysicalDataSheet(workbook);
            CreateDecorationColumnsSheet(workbook);
            CreateBreaksSheet(workbook);
            foreach (var sheet in workbook.Worksheets)
            foreach (var column in sheet.Columns())
                column.AdjustToContents();
            return workbook;
        }

        private XLWorkbook CreateMockSampleWorkbook(string path)
        {
            var buildProject = JsonConvert.DeserializeObject<BuildProject>(
                File.ReadAllText("Resources/sample_project.json"),
                new JsonSerializerSettings
                {
                    Converters = new List<JsonConverter>
                    {
                        new UnitsNetIQuantityJsonConverter(),
                        new NetTopologySuite.IO.Converters.CoordinateConverter(),
                        new NetTopologySuite.IO.Converters.GeometryConverter()
                    }
                });

            foreach (var structuralMap in buildProject.StructuralMaps.ToArray())
            {
                var mapPath = System.IO.Path.Combine(path, structuralMap.Name);
                mapPath = System.IO.Path.ChangeExtension(mapPath, ".grd");
                structuralMap.SourceSurface.ToGridFile(mapPath);
                buildProject.RemoveStructuralMap(structuralMap);
                buildProject.AddStructuralMap(StructuralMap.FromGridFile(mapPath));
            }

            return SaveProject(buildProject);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <exception cref="IOException"></exception>
        public IObservable<Unit> SaveChanges() => Observable.Start(() =>
        {
            SaveProject(_buildProject.Value).SaveAs(_excelPath.Value);
        });

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        /// <exception cref="SurfaceReadFileException"></exception>
        public IObservable<Unit> LoadHorizonSurface(string fileName)
        {
            StartBlockingOperation();
            return Observable.Start(() => StructuralMap.FromGridFile(fileName))
                .ObserveOnDispatcher()
                .Do(structuralMap =>
                {
                    _buildProject.Value.AddStructuralMap(structuralMap);
                    FinishBlockingOperation();
                }, exception => FinishBlockingOperation())
                .Select(_ => Unit.Default);
        }

        public void RemoveHorizonSurface(StructuralMap structuralMap) =>
            _buildProject.Value.RemoveStructuralMap(structuralMap);

        public void Dispose() => _cleanUp.Dispose();

        /// <param name="workbook">Книга Excel.</param>
        /// <param name="path">Путь к <see cref="workbook"/>.</param>
        /// <param name="sheetName">Заголовок искомого листа.</param>
        /// <exception cref="LoadFromExcelException"></exception>
        private IXLWorksheet GetRequiredWorksheet(
            XLWorkbook workbook,
            string path,
            string sheetName)
        {
            if (!workbook.TryGetWorksheet(sheetName, out var sheet))
                throw new LoadFromExcelException(
                    new LoadFromExcelException.ExceptionKind.ParseError.RequiredSheetNotFound(
                        path,
                        sheetName));
            return sheet;
        }

        private sealed class WellDto
        {
            public string Name { get; set; }

            public Length Altitude { get; set; }

            public Length Bottom { get; set; }

            public Coordinate Coordinate { get; set; }

            public List<GeophysicalData> GeophysicalData { get; } = new List<GeophysicalData>();

            public bool IsEnabled { get; set; }
        }
    }
}