using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using DynamicData;
using Newtonsoft.Json;
using Rack.CrossSectionUtils.Model;
using Rack.GeoSections.Model;
using Rack.GeoSections.Services;
using Rack.GeoTools;
using Rack.Localization;
using Rack.Navigation;
using Rack.Shared;
using Rack.Shared.Configuration;
using Rack.Shared.MainWindow;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Serilog;
using UnitsNet;
using UnitsNet.Serialization.JsonNet;
using UnitsNet.Units;
using DecorationColumn = Rack.GeoSections.Model.DecorationColumn;

namespace Rack.GeoSections.ViewModels
{
    public sealed class DecorationColumnModeWrapper
    {
        public DecorationColumnModeWrapper(DecorationColumnMode mode, string name)
        {
            Mode = mode;
            Name = name;
        }

        public DecorationColumnMode Mode { get; }
        public string Name { get; }
    }

    public sealed class DecorationColumnWrapper : ReactiveObject
    {
        public DecorationColumnWrapper(DecorationColumn decorationColumn, int order)
        {
            Instance = decorationColumn;
            Order = order;
        }

        public DecorationColumn Instance { get; }

        [Reactive]
        public int Order { get; set; }
    }

    public sealed class EncodingWrapper
    {
        public EncodingWrapper(string header, Encoding encoding)
        {
            Header = header;
            Encoding = encoding;
        }

        public string Header { get; }

        public Encoding Encoding { get; }
    }

    public sealed class SectionInfo
    {
        public string SectionSizeHeader => "Размер основной области разреза:";
        public Length SectionWidth { get; }
        public Length SectionHeight { get; }
        public string FullSectionSizeHeader => "Размер разреза с учётом декоративных элементов:";
        public Length FullSectionWidth { get; }
        public Length FullSectionHeight { get; }
        public string ResolutionCountHeader => "Количество точек дискретизации:";
        public int VerticalPointsCount { get; }
        public int HorizontalPointsCount { get; }

        public SectionInfo(
            Length sectionWidth,
            Length sectionHeight,
            Length fullSectionWidth,
            Length fullSectionHeight,
            int verticalPointsCount,
            int horizontalPointsCount)
        {
            SectionWidth = sectionWidth.ToUnit(LengthUnit.Centimeter);
            SectionHeight = sectionHeight.ToUnit(LengthUnit.Centimeter);
            FullSectionWidth = fullSectionWidth.ToUnit(LengthUnit.Centimeter);
            FullSectionHeight = fullSectionHeight.ToUnit(LengthUnit.Centimeter);
            VerticalPointsCount = verticalPointsCount;
            HorizontalPointsCount = horizontalPointsCount;
        }
    }

    public sealed class ShapeMakerViewModel : ReactiveViewModel
    {
        private readonly History _history;
        private readonly ObservableAsPropertyHelper<bool> _isBusy;

        public ShapeMakerViewModel(
            IDataProviderService dataProviderService,
            IDialogService dialogService,
            IMainWindowService mainWindowService,
            IConfigurationService configurationService,
            ILocalizationService localizationService,
            IScreen hostScreen)
            : base(localizationService, hostScreen)
        {
            GeoSectionsSettings = configurationService.GetConfiguration<GeoSectionsSettings>();
            _history = configurationService.GetConfiguration<History>();
            this.GetIsActivated()
                .Select(isActivated => isActivated
                    ? _history.Files
                    : Observable.Empty<IChangeSet<string, string>>())
                .Switch()
                .Bind(out var history)
                .Subscribe();
            History = history;

            _buildProject = dataProviderService.BuildProject
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, nameof(BuildProject));

            dataProviderService.BuildProject
                .Select(project => project == null
                    ? Observable.Empty<IChangeSet<StructuralMap, string>>()
                    : project.StructuralMapsObservable)
                .Switch()
                .Bind(out var structuralMaps)
                .Subscribe();
            StructuralMaps = structuralMaps;

            LoadExcelFileDialog = ReactiveCommand.CreateFromObservable<Unit, string>(_ =>
                {
                    var path = dialogService.ShowOpenFileDialog(
                        "Загрузить данные для построения разреза",
                        new Dictionary<string, string>
                        {
                            {"Excel файлы (*.xls, *.xlsx)", "*.xls;*.xlsx"}
                        });
                    if (string.IsNullOrEmpty(path)) return Observable.Empty<string>();
                    return LoadExcelFile.Execute(path);
                }
            );

            LoadExcelFile = ReactiveCommand.CreateFromObservable<string, string>(path =>
                dataProviderService.LoadFromExcel(path)
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Do(_ =>
                    {
                        _history.Append(path);
                        configurationService.SaveConfiguration(_history);
                    })
                    .Select(_ => path));

            LoadExcelFile.ThrownExceptions
                .Merge(LoadExcelFileDialog.ThrownExceptions)
                .Distinct()
                .Subscribe(exception =>
                {
                    if (!(exception is LoadFromExcelException loadFromExcelException))
                        throw exception;
                    Log.Error(exception, "Error occured while reading Excel file.");
                    var errorMessage = GetErrorMessage(loadFromExcelException);
                    mainWindowService.SendMessage(new Message(
                        errorMessage,
                        MessageType.Error,
                        RepresentationType.BigMessage));
                });

            LoadExcelFile.Subscribe(file =>
            {
                if (string.IsNullOrEmpty(file)) return;
                mainWindowService.SendMessage(
                    new Message($"Данные успешно загружены из файла Excel \"{file}\"."));
            });

            SaveProject = ReactiveCommand.CreateFromObservable(dataProviderService.SaveChanges,
                dataProviderService.BuildProject
                    .Select(project => project == null
                        ? Observable.Return(false)
                        : project.CanBuildSection)
                    .Switch());

            SaveProject.Subscribe(_ =>
                mainWindowService.SendMessage(new Message(
                    "Проект успешно сохранён в файле Excel.")));

            SaveProject.ThrownExceptions.Subscribe(exception =>
            {
                if (exception is IOException ioException)
                {
                    if ((ioException.HResult & 0x0000FFFF) == 32)
                    {
                        mainWindowService.SendMessage(new Message(
                            "Файл открыт в Excel или другой программе. Закройте его и повторите попытку.",
                            MessageType.Error,
                            RepresentationType.BigMessage));
                        return;
                    }
                }

                throw exception;
            });

            GenerateExcelSample = ReactiveCommand.CreateFromTask<Unit, string>(async _ =>
            {
                var answer = await dialogService.ShowYesNoQuestionDialogAsync(
                    "Создание образца",
                    "Добавить в образец данные для примера?");
                if (answer == IDialogService.YesNoAnswer.Cancel)
                    return string.Empty;
                var isFillingMockData = answer == IDialogService.YesNoAnswer.Yes;
                var path = dialogService.ShowSaveFileDialog(
                    "Сохранить образец", "Образец.xlsx",
                    new Dictionary<string, string>
                    {
                        ["Excel файлы (*.xls, *.xlsx)"] = "*.xls;*.xlsx"
                    });
                if (string.IsNullOrEmpty(path)) return string.Empty;
                await dataProviderService.GenerateExcelSample(path, isFillingMockData);

                if (isFillingMockData && BuildProject == null)
                    await LoadExcelFile.Execute(path);

                return path;
            });

            GenerateExcelSample.ThrownExceptions
                .Subscribe(exception =>
                {
                    Log.Error(exception,
                        "Возникло исключение при попытке создания образца файла Excel.");
                    mainWindowService.SendMessage(new Message(
                        "Не удалось сгенерировать образец.", MessageType.Error,
                        RepresentationType.BigMessage));
                });

            GenerateExcelSample.Subscribe(path =>
            {
                if (string.IsNullOrEmpty(path)) return;
                mainWindowService.SendMessage(new Message(
                    $"Образец успешно создан в \"{path}\"."));
            });

            LoadGridFiles = ReactiveCommand.CreateFromObservable<Unit, Unit>(_ =>
            {
                var files = dialogService.ShowOpenFilesDialog(
                    "Загрузить структурные карты",
                    new Dictionary<string, string>
                    {
                        ["Грид-файлы (*.grd)"] = "*.grd"
                    }).ToArray();
                if (files.Length == 0) return Observable.Empty<Unit>();
                return files.Select(file => LoadGridFile.Execute(file))
                    .OnErrorResumeNext()
                    .Select(_ => Unit.Default);
            });

            LoadGridFile = ReactiveCommand.CreateFromObservable<string, string>(file =>
            {
                return dataProviderService.LoadHorizonSurface(file)
                    .Select(_ => file);
            });

            LoadGridFile.ThrownExceptions.Subscribe(exception =>
            {
                if (!(exception is SurfaceReadFileException readFileException))
                    throw exception;
                Log.Error(exception, "Error occured.");
                var file = (string) readFileException.Data["File"];
                mainWindowService.SendMessage(new Message(
                    $"Не удалось загрузить файл \"{file}\".",
                    MessageType.Error));
            });

            LoadGridFile.Subscribe(file =>
            {
                if (string.IsNullOrEmpty(file)) return;
                mainWindowService.SendMessage(new Message(
                    $"Файл {file} успешно загружен."
                ));
            });

            RemoveHorizonSurface = ReactiveCommand.Create<StructuralMap, Unit>(surface =>
            {
                dataProviderService.RemoveHorizonSurface(surface);
                return Unit.Default;
            });

            ShowGeophysicalDataCommand = ReactiveCommand.Create<Well>(well =>
            {
                dialogService.ShowGeophysicalDataAsync(well);
            });

            GenerateFiles = ReactiveCommand.CreateFromObservable<Unit, string>(_ =>
                {
                    var path = dialogService.ShowOpenDirectoryDialog("Сохранить файлы");
                    if (path == string.Empty) return Observable.Empty<string>();
                    return BuildProject.BuildSection()
                        .Select(result => result.WriteToDirectory(path))
                        .Switch()
                        .Select(_ => path);
                },
                dataProviderService.BuildProject
                    .Select(project => project == null
                        ? Observable.Return(false)
                        : project.CanBuildSection)
                    .Switch());

            GenerateFiles.ThrownExceptions.Subscribe(exception =>
            {
                throw exception;
                //if (!(exception is GenerateFilesException generateFilesException))
                //    throw exception;
                //Log.Error(exception, string.Empty);
                //switch (generateFilesException.Kind)
                //{
                //    case GenerateFilesExceptionKind.EmptyDestination:
                //        _mainWindowService.SendMessage(new Message(
                //            "Не удалось создать файлы разреза, так как не задан путь.",
                //            MessageType.Error));
                //        break;
                //    case GenerateFilesExceptionKind.DestinationFolderNotFound:
                //        _mainWindowService.SendMessage(new Message(
                //            "Не удалось найти заданную директорию.",
                //            MessageType.Error));
                //        break;
                //    case GenerateFilesExceptionKind.UnableToWriteFile:
                //        var message = $"Не удалось записать файл \"{exception.Data["File"]}\".";
                //        switch (exception.InnerException)
                //        {
                //            case IOException ioException:
                //                {
                //                    if ((ioException.HResult & 0x0000FFFF) == 32)
                //                        message += " Файл уже используется другим процессом.";

                //                    break;
                //                }

                //            case UnauthorizedAccessException _:
                //                message += " Запрещён доступ.";
                //                break;
                //        }

                //        _mainWindowService.SendMessage(new Message(
                //            message,
                //            MessageType.Error,
                //            RepresentationType.BigMessage));
                //        break;
                //    default:
                //        throw new ArgumentOutOfRangeException(string.Empty, generateFilesException);
                //}
            });

            GenerateFiles
                .Where(path => !string.IsNullOrEmpty(path))
                .Do(path => mainWindowService.SendMessage(new Message(
                    $"Файлы для построения разреза успешно созданы в \"{path}\".")))
                .Subscribe();

            ShowSectionLine = ReactiveCommand.CreateFromTask(() =>
                    dialogService.ShowSectionShapeAsync(dataProviderService.BuildProject),
                dataProviderService.BuildProject
                    .Select(project => project == null
                        ? Observable.Return(false)
                        : project.SectionPath.Select(path => path != null))
                    .Switch());

            _isBusy = Observable.CombineLatest(
                    LoadGridFile.IsExecuting,
                    LoadExcelFile.IsExecuting,
                    GenerateFiles.IsExecuting,
                    GenerateExcelSample.IsExecuting,
                    dataProviderService.IsFree.Select(x => !x),
                    (a1, a2, a3, a4, a5) => a1 || a2 || a3 || a4 || a5)
                .ToProperty(this, nameof(IsBusy));

            SetHorizontalScale = ReactiveCommand.Create<double>(
                x => { BuildProject.Settings.HorizontalScale = x; },
                this.WhenAnyValue(
                    x => x.IsBusy,
                    x => x.BuildProject,
                    (isBusy, project) => !isBusy && project != null));

            SetVerticalScale = ReactiveCommand.Create<double>(
                x => { BuildProject.Settings.VerticalScale = x; },
                this.WhenAnyValue(
                    x => x.IsBusy,
                    x => x.BuildProject,
                    (isBusy, project) => !isBusy && project != null));

            EditDefaultScales =
                ReactiveCommand.CreateFromTask(dialogService
                    .ShowDefaultScalesEditorDialogAsync);

            dataProviderService.BuildProject.Select(project => project == null
                    ? Observable.Empty<IChangeSet<OilBearingFormation>>()
                    : project.OilBearingFormationsObservable)
                .Switch()
                .Bind(out var oilBearingFormations)
                .Subscribe();
            OilBearingFormations = oilBearingFormations;

            AddOilBearingFormation = ReactiveCommand.Create(() =>
                BuildProject.AddOilBearingFormation(new OilBearingFormation()));

            RemoveOilBearingFormation = ReactiveCommand.Create<OilBearingFormation>(formation =>
                BuildProject.RemoveOilBearingFormation(formation));

            dataProviderService.BuildProject
                .Select(x => x == null
                    ? Observable.Empty<IChangeSet<WellLabel>>()
                    : x.WellLabelsObservable)
                .Switch()
                .Bind(out var wellLabels)
                .Subscribe();
            WellLabels = wellLabels;

            AddWellLabel = ReactiveCommand.Create(() =>
                BuildProject.AddWellLabel(new WellLabel()));

            RemoveWellLabel = ReactiveCommand.Create<WellLabel>(wellLabel =>
                BuildProject.RemoveWellLabel(wellLabel));

            SerializeProject = ReactiveCommand.CreateFromTask(async () =>
            {
                var path = dialogService.ShowSaveFileDialog("Сохранить проект",
                    "sample_project.json",
                    new Dictionary<string, string> {["JSON"] = ".json"});
                if (string.IsNullOrEmpty(path)) return;
                File.WriteAllText(path, JsonConvert.SerializeObject(BuildProject,
                    new JsonSerializerSettings
                    {
                        Converters = new List<JsonConverter>
                        {
                            new UnitsNetIQuantityJsonConverter(),
                            new NetTopologySuite.IO.Converters.CoordinateConverter(),
                            new NetTopologySuite.IO.Converters.GeometryConverter()
                        }
                    }));
            });


            _sectionInfo = dataProviderService.BuildProject
                .Select(buildProject => buildProject == null
                    ? Observable.Return(new SectionInfo(
                        Length.Zero,
                        Length.Zero,
                        Length.Zero,
                        Length.Zero,
                        0,
                        0))
                    : Observable.CombineLatest(
                        buildProject.MainAreaWidth,
                        buildProject.MainAreaHeight,
                        buildProject.SectionWidth,
                        buildProject.SectionHeight,
                        buildProject.HorizontalPointsCount,
                        buildProject.VerticalPointsCount,
                        (mainAreaWidth
                            , mainAreaHeight,
                            sectionWidth,
                            sectionHeight,
                            horizontalPointsCount,
                            verticalPointsCount) => new SectionInfo(
                            mainAreaWidth,
                            mainAreaHeight,
                            sectionWidth,
                            sectionHeight,
                            verticalPointsCount,
                            horizontalPointsCount)))
                .Switch()
                .ToProperty(this, nameof(SectionInfo));

            dataProviderService.BuildProject
                .Select(project => project == null
                    ? Observable.Empty<IChangeSet<BreakInfo, Break>>()
                    : project.BreakInfos)
                .Switch()
                .ObserveOnDispatcher()
                .Bind(out var breakInfos)
                .Subscribe();
            BreakInfos = breakInfos;

            OpenHelp = ReactiveCommand.CreateFromTask(dialogService.ShowHelpAsync);

            this.WhenActivated(cleanUp =>
            {
                dataProviderService.FileReload
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Subscribe(path => mainWindowService.SendMessage(
                        new Message(
                            $"Файл \"{path}\" перезагружен.")))
                    .DisposeWith(cleanUp);

                Observable.CombineLatest(
                        dataProviderService.Path,
                        dataProviderService.FileReloadException,
                        (path, exception) =>
                            $"При перезагрузке файла \"{path}\" произошла ошибка. " +
                            GetErrorMessage(exception))
                    .Subscribe(errorMessage => mainWindowService.SendMessage(new Message(
                        errorMessage,
                        MessageType.Error)))
                    .DisposeWith(cleanUp);

                dataProviderService.Path
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Subscribe(path => mainWindowService.ChangeHeader(
                        path == string.Empty
                            ? Localization["Rack.GeoSections"]
                            : Path.GetFileName(path)))
                    .DisposeWith(cleanUp);
            });
        }

        private string ConvertCellType(
            LoadFromExcelException.ExceptionKind.ParseError.WrongCellType.CellType cellType) =>
            cellType switch
            {
                LoadFromExcelException.ExceptionKind.ParseError.WrongCellType.CellType
                    .Boolean => "флага (ИСТИНА/ЛОЖЬ)",
                LoadFromExcelException.ExceptionKind.ParseError.WrongCellType.CellType
                    .Numeric => "числа",
                LoadFromExcelException.ExceptionKind.ParseError.WrongCellType.CellType.Length =>
                "расстояния",
                LoadFromExcelException.ExceptionKind.ParseError.WrongCellType.CellType
                    .ColumnMode =>
                "режима отображения",
                LoadFromExcelException.ExceptionKind.ParseError.WrongCellType.CellType
                    .Encoding =>
                "кодировки",
                _ => throw new ArgumentOutOfRangeException(nameof(cellType), cellType,
                    null)
            };

        private string GetErrorMessage(LoadFromExcelException exception) =>
            exception.Kind.Match(
                ioError => ioError.Kind switch
                {
                    LoadFromExcelException.ExceptionKind.IOError.ErrorKind.FileNotFound =>
                    $"Файл \"{ioError.Path}\" не найден, он был перемещён или удалён.",
                    LoadFromExcelException.ExceptionKind.IOError.ErrorKind.IOError =>
                    $"При чтении файла \"{ioError.Path}\" произошла ошибка.",
                    _ => throw new ArgumentOutOfRangeException()
                },
                parseError => parseError.Match(
                    sheetNotFound => $"Не найден лист \"{sheetNotFound.Worksheet}\".",
                    wrongCellType =>
                        $"На листе \"{wrongCellType.Worksheet}\" в ячейке {wrongCellType.Address} вместо {ConvertCellType(wrongCellType.ExpectedCellType)} содержится значение \"{wrongCellType.Value}\".",
                    columnHeaderNotFound =>
                        $"На листе \"{columnHeaderNotFound.Worksheet}\" не найдена колонка \"{columnHeaderNotFound.Header}\".",
                    emptyWells =>
                        "Указано меньше двух скважин, что недостаточно для построения разреза.",
                    wellWithoutGeophysicalData =>
                        $"Для скважины \"{wellWithoutGeophysicalData.Well}\" не указаны данные ГИС.",
                    breakIsNotFullySpecified =>
                        $"В разбивке на строке \"{breakIsNotFullySpecified.Row}\" не указана глубина в скважине \"{breakIsNotFullySpecified.WellNotFound}\".",
                    wellNotFound =>
                        $"На листе {wellNotFound.Worksheet} в ячейке {wellNotFound.Address} обнаружена ссылка на несуществующую скважину ({wellNotFound.WellName}).")
            );


        public GeoSectionsSettings GeoSectionsSettings { get; }

        public BuildProject BuildProject => _buildProject.Value;
        private readonly ObservableAsPropertyHelper<BuildProject> _buildProject;
        private readonly ObservableAsPropertyHelper<SectionInfo> _sectionInfo;

        public ReadOnlyObservableCollection<BreakInfo> BreakInfos { get; }
        public ReadOnlyObservableCollection<StructuralMap> StructuralMaps { get; }
        public ReadOnlyObservableCollection<OilBearingFormation> OilBearingFormations { get; }
        public ReadOnlyObservableCollection<WellLabel> WellLabels { get; }
        public ReadOnlyObservableCollection<string> History { get; }

        public DecorationColumnModeWrapper[] AvailableModes { get; } =
        {
            new DecorationColumnModeWrapper(DecorationColumnMode.None, "Скрыть"),
            new DecorationColumnModeWrapper(DecorationColumnMode.Left, "Только слева"),
            new DecorationColumnModeWrapper(DecorationColumnMode.Right, "Только справа"),
            new DecorationColumnModeWrapper(DecorationColumnMode.LeftAndRight, "Слева и справа")
        };

        public EncodingWrapper[] AvailableEncodings { get; } =
        {
            new EncodingWrapper("UTF-8 (для версий ArcMap не раньше 10.5)", Encoding.UTF8),
            new EncodingWrapper("Windows-1251 (для старых версий ArcMap)",
                Encoding.GetEncoding(1251))
        };

        public bool IsBusy => _isBusy.Value;
        public SectionInfo SectionInfo => _sectionInfo.Value;

        public override IEnumerable<string> LocalizationKeys { get; } =
            new[] {GeoSectionsModule.Name};

        #region Команды

        public ReactiveCommand<Unit, string> LoadExcelFileDialog { get; }
        public ReactiveCommand<string, string> LoadExcelFile { get; }
        public ReactiveCommand<Unit, Unit> LoadGridFiles { get; }
        public ReactiveCommand<string, string> LoadGridFile { get; }
        public ReactiveCommand<Unit, Unit> SaveProject { get; }
        public ReactiveCommand<Unit, string> GenerateFiles { get; }
        public ReactiveCommand<Unit, Unit> ShowSectionLine { get; }

        public ReactiveCommand<Well, Unit> ShowGeophysicalDataCommand { get; }

        /// <summary>
        /// Устанавливает горизонтальный масштаб у <see cref="BuildSettings" />.
        /// Команда используется для задания стандартных значений масштаба из UI.
        /// </summary>

        public ReactiveCommand<double, Unit> SetHorizontalScale { get; }

        /// <summary>
        /// Устанавливает вертикальный масштаб у <see cref="BuildSettings" />.
        /// Команда используется для задания стандартных значений масштаба из UI.
        /// </summary>
        public ReactiveCommand<double, Unit> SetVerticalScale { get; }

        public ReactiveCommand<Unit, Unit> EditDefaultScales { get; }

        public ReactiveCommand<StructuralMap, Unit> RemoveHorizonSurface { get; }

        public ReactiveCommand<Unit, string> GenerateExcelSample { get; }

        public ReactiveCommand<Unit, Unit> OpenHelp { get; }

        public ReactiveCommand<Unit, Unit> AddOilBearingFormation { get; }

        public ReactiveCommand<OilBearingFormation, Unit> RemoveOilBearingFormation { get; }

        public ReactiveCommand<Unit, Unit> AddWellLabel { get; }

        public ReactiveCommand<WellLabel, Unit> RemoveWellLabel { get; }

        public ReactiveCommand<Unit, Unit> SerializeProject { get; }

        #endregion
    }
}