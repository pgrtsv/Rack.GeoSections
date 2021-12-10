using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using DynamicData;
using DynamicData.Binding;
using MahApps.Metro.Controls;
using Microsoft.Xaml.Behaviors;
using Rack.GeoSections.ViewModels;
using Rack.GeoTools;
using Rack.Wpf.Behaviors;
using Rack.Wpf.Reactive;
using ReactiveUI;
using UnitsNet;
using UnitsNet.Units;
using DataGridTextColumn = MaterialDesignThemes.Wpf.DataGridTextColumn;
using Style = System.Windows.Style;

namespace Rack.GeoSections.Views
{
    public partial class ShapeMaker : ReactiveUserControl<ShapeMakerViewModel>
    {
        public ShapeMaker()
        {
            InitializeComponent();

            Interaction.GetBehaviors(GenerateFilesButton)
                .Add(new UpdateOnExecuteCommandBehavior
                {
                    EditingRoot = EditingRoot
                });

            foreach (var textBox in this.FindChildren<TextBox>())
            {
                var behaviors = Interaction.GetBehaviors(textBox);
                behaviors.Add(new FocusNextOnEnterBehavior());
                behaviors.Add(new TextBoxSelectAllOnFocusBehavior());
            }

            this.WhenActivated(cleanUp =>
            {
                var binding = new BindingHelper<ShapeMaker, ShapeMakerViewModel>(
                    this,
                    cleanUp);

                #region Attaching behaviors

                binding
                    .AttachBehaviors(
                        x => x.GenerateFilesButton,
                        new UpdateOnExecuteCommandBehavior
                        {
                            EditingRoot = EditingRoot
                        })
                    .AttachBehaviors<TextBox>(
                        () => new FocusNextOnEnterBehavior(),
                        () => new TextBoxSelectAllOnFocusBehavior())
                    .AttachBehaviors<DataGrid>(
                        () => new MouseWheelScrollBehavior())
                    ;

                #endregion

                #region ProgressBar bindings

                binding
                    .OneWayBind(
                        x => x.IsBusy,
                        x => x.ProgressBar.Visibility)
                    ;

                #endregion

                binding
                    .Do(() =>
                    {
                        ViewModel.WhenAnyValue(x => x.BuildProject)
                            .Subscribe(project =>
                            {
                                StartWorkingDockPanel.Visibility = project == null
                                    ? Visibility.Visible
                                    : Visibility.Collapsed;
                                WorkingAreaDockPanel.Visibility = project == null
                                    ? Visibility.Collapsed
                                    : Visibility.Visible;
                            })
                            .DisposeWith(cleanUp);
                    })
                    ;

                #region StartWorkingDockPanel bindings

                binding
                    .Do(() =>
                    {
                        HistoryListBox.ItemsSource = ViewModel.History;
                        HistoryListBox.Events().SelectionChanged
                            .Select(_ => (string) HistoryListBox.SelectedItem)
                            .Where(x => x != null)
                            .InvokeCommand(ViewModel.LoadExcelFile)
                            .DisposeWith(cleanUp);
                    })
                    .BindCommand(
                        x => x.LoadExcelFileDialog,
                        x => x.LoadExcelHyperlink)
                    .BindCommand(
                        x => x.GenerateExcelSample,
                        x => x.CreateSampleHyperlink)
                    .BindCommand(
                        x => x.OpenHelp,
                        x => x.OpenHelpHyperlink)
                    ;

                #endregion

                #region EditingRoot bindings

                #region ToolBarTray bindings

                binding
                    .BindCommand(
                        x => x.GenerateExcelSample,
                        x => x.GenerateExcelSampleButton)
                    .BindCommand(
                        x => x.LoadExcelFileDialog,
                        x => x.LoadExcelFileButton)
                    .BindCommand(
                        x => x.GenerateFiles,
                        x => x.GenerateFilesButton)
                    .BindCommand(
                        x => x.SaveProject,
                        x => x.SaveProjectButton)
                    .BindCommand(
                        x => x.SerializeProject,
                        x => x.SerializeProjectButton)
                    .Do(() =>
                    {
                        var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
                        if (environment != "Development")
                            SerializeProjectButton.Visibility = Visibility.Collapsed;
                    })
                    ;

                #endregion

                #region Данные

                binding
                    .OneWayBind(
                        x => x.BuildProject.Wells,
                        x => x.WellsDataGrid.ItemsSource)
                    .BindCommand(
                        x => x.ShowSectionLine,
                        x => x.ShowSectionLineButton)
                    .OneWayBind(
                        x => x.StructuralMaps,
                        x => x.StructuralMapsDataGrid.ItemsSource)
                    .BindCommand(
                        x => x.LoadGridFiles,
                        x => x.LoadGridFilesButton)
                    .OneWayBind(
                        x => x.BreakInfos,
                        x => x.BreaksDataGrid.ItemsSource)
                    .Do(() =>
                    {
                        ViewModel.WhenAnyValue(x => x.BuildProject)
                            .Where(project => project != null)
                            .Subscribe(project =>
                            {
                                BreaksDataGrid.Columns.Clear();
                                BreaksDataGrid.Columns.Add(new DataGridTextColumn
                                {
                                    ElementStyle =
                                        (Style) FindResource(
                                            "MaterialDesignDataGridTextColumnStyle"),
                                    Header = "№",
                                    Binding = new Binding("Break.Name") {Mode = BindingMode.OneTime}
                                });
                                foreach (var well in project.Wells)
                                    BreaksDataGrid.Columns.Add(new DataGridTextColumn
                                    {
                                        ElementStyle =
                                            (Style) FindResource(
                                                "MaterialDesignDataGridTextColumnStyle"),
                                        Header = well.Name,
                                        Binding = new Binding($"Break[{well.Name}]")
                                            {Mode = BindingMode.OneTime}
                                    });
                                BreaksDataGrid.Columns.Add(new DataGridTextColumn
                                {
                                    ElementStyle =
                                        (Style)FindResource(
                                            "MaterialDesignDataGridTextColumnStyle"),
                                    Header = "Крайние глубины",
                                    Binding = new Binding("Depths") {Mode = BindingMode.OneTime}
                                });
                            })
                            .DisposeWith(cleanUp);
                    })
                    ;

                #endregion

                #region Параметры построения

                binding
                    .BindCommand(
                        x => x.SetVerticalScale,
                        x => x.SetVerticalScaleButton1,
                        x => x.GeoSectionsSettings.DefaultVerticalScale1)
                    .OneWayBind(
                        x => x.GeoSectionsSettings.DefaultVerticalScale1,
                        x => x.SetVerticalScaleButton1.Content,
                        ScaleConvert.Convert)
                    .BindCommand(
                        x => x.SetVerticalScale,
                        x => x.SetVerticalScaleButton2,
                        x => x.GeoSectionsSettings.DefaultVerticalScale2)
                    .OneWayBind(
                        x => x.GeoSectionsSettings.DefaultVerticalScale2,
                        x => x.SetVerticalScaleButton2.Content,
                        ScaleConvert.Convert)
                    .BindCommand(
                        x => x.SetVerticalScale,
                        x => x.SetVerticalScaleButton3,
                        x => x.GeoSectionsSettings.DefaultVerticalScale3)
                    .OneWayBind(
                        x => x.GeoSectionsSettings.DefaultVerticalScale3,
                        x => x.SetVerticalScaleButton3.Content,
                        ScaleConvert.Convert)
                    .Bind(
                        x => x.BuildProject.Settings.VerticalScale,
                        x => x.VerticalScaleTextBox.Text,
                        VerticalScaleTextBox.Events().LostKeyboardFocus,
                        ScaleConvert.Convert,
                        ScaleConvert.ConvertFrom)
                    .BindValidationError(
                        x => x.BuildProject.Settings,
                        x => x.VerticalScale,
                        x => x.VerticalScaleTextBox)
                    .BindCommand(
                        x => x.SetHorizontalScale,
                        x => x.SetHorizontalScaleButton1,
                        x => x.GeoSectionsSettings.DefaultHorizontalScale1)
                    .OneWayBind(
                        x => x.GeoSectionsSettings.DefaultHorizontalScale1,
                        x => x.SetHorizontalScaleButton1.Content,
                        ScaleConvert.Convert)
                    .BindCommand(
                        x => x.SetHorizontalScale,
                        x => x.SetHorizontalScaleButton2,
                        x => x.GeoSectionsSettings.DefaultHorizontalScale2)
                    .OneWayBind(
                        x => x.GeoSectionsSettings.DefaultHorizontalScale2,
                        x => x.SetHorizontalScaleButton2.Content,
                        ScaleConvert.Convert)
                    .BindCommand(
                        x => x.SetHorizontalScale,
                        x => x.SetHorizontalScaleButton3,
                        x => x.GeoSectionsSettings.DefaultHorizontalScale3)
                    .OneWayBind(
                        x => x.GeoSectionsSettings.DefaultHorizontalScale3,
                        x => x.SetHorizontalScaleButton3.Content,
                        ScaleConvert.Convert)
                    .Bind(
                        x => x.BuildProject.Settings.HorizontalScale,
                        x => x.HorizontalScaleTextBox.Text,
                        HorizontalScaleTextBox.Events().LostKeyboardFocus,
                        ScaleConvert.Convert,
                        ScaleConvert.ConvertFrom)
                    .BindValidationError(
                        x => x.BuildProject.Settings,
                        x => x.HorizontalScale,
                        x => x.HorizontalScaleTextBox)
                    .Bind(
                        x => x.BuildProject.Settings.VerticalResolution,
                        x => x.VerticalResolutionTextBox.Text)
                    .BindValidationError(
                        x => x.BuildProject.Settings,
                        x => x.VerticalResolution,
                        x => x.VerticalResolutionTextBox)
                    .Bind(
                        x => x.BuildProject.Settings.HorizontalResolution,
                        x => x.HorizontalResolutionTextBox.Text)
                    .BindValidationError(
                        x => x.BuildProject.Settings,
                        x => x.HorizontalResolution,
                        x => x.HorizontalResolutionTextBox)
                    .BindLength(
                        x => x.BuildProject.Settings.Top,
                        x => x.TopTextBox.Text,
                        TopTextBox.Events().LostKeyboardFocus)
                    .BindValidationError(
                        x => x.BuildProject.Settings,
                        x => x.Top,
                        x => x.TopTextBox)
                    .BindLength(
                        x => x.BuildProject.Settings.Bottom,
                        x => x.BottomTextBox.Text,
                        BottomTextBox.Events().LostKeyboardFocus)
                    .BindValidationError(
                        x => x.BuildProject.Settings,
                        x => x.Bottom,
                        x => x.BottomTextBox)
                    .BindLength(
                        x => x.BuildProject.Settings.Offset,
                        x => x.OffsetTextBox.Text,
                        OffsetTextBox.Events().LostKeyboardFocus,
                        () => ViewModel.BuildProject.Settings.IsOffsetScaled 
                            ? LengthUnit.Centimeter
                            : LengthUnit.Meter)
                    .BindValidationError(
                        x => x.BuildProject.Settings,
                        x => x.Offset,
                        x => x.OffsetTextBox)
                    .Bind(
                        x => x.BuildProject.Settings.IsOffsetScaled,
                        x => x.IsOffsetScaledToggleButton.IsChecked)
                    .BindLength(
                        x => x.BuildProject.Settings.DecorationColumnsWidth,
                        x => x.DecorationColumnsWidthTextBox.Text,
                        DecorationColumnsWidthTextBox.Events().LostKeyboardFocus,
                        LengthUnit.Centimeter)
                    .BindValidationError(
                        x => x.BuildProject.Settings,
                        x => x.DecorationColumnsWidth,
                        x => x.DecorationColumnsWidthTextBox)
                    .BindLength(
                        x => x.BuildProject.Settings.DecorationHeadersHeight,
                        x => x.DecorationHeadersHeightTextBox.Text,
                        DecorationHeadersHeightTextBox.Events().LostKeyboardFocus,
                        LengthUnit.Centimeter)
                    .BindValidationError(
                        x => x.BuildProject.Settings,
                        x => x.DecorationHeadersHeight,
                        x => x.DecorationHeadersHeightTextBox)
                    .Do(() =>
                    {
                        DepthColumnModeComboBox.DisplayMemberPath =
                            nameof(DecorationColumnModeWrapper.Name);
                        DepthColumnModeComboBox.SelectedValuePath =
                            nameof(DecorationColumnModeWrapper.Mode);
                        DepthColumnModeComboBox.ItemsSource = ViewModel.AvailableModes;
                    })
                    .Bind(
                        x => x.BuildProject.Settings.DepthColumnMode,
                        x => x.DepthColumnModeComboBox.SelectedValue)
                    .Do(() =>
                    {
                        EncodingComboBox.DisplayMemberPath = nameof(EncodingWrapper.Header);
                        EncodingComboBox.SelectedValuePath = nameof(EncodingWrapper.Encoding);
                        EncodingComboBox.ItemsSource = ViewModel.AvailableEncodings;
                    })
                    .Bind(
                        x => x.BuildProject.Settings.Encoding,
                        x => x.EncodingComboBox.SelectedValue)
                    .Do(() =>
                    {
                        ViewModel.WhenAnyValue(x => x.SectionInfo)
                            .Subscribe(info =>
                            {
                                SectionInfoSizeHeaderRun.Text = info.SectionSizeHeader;
                                SectionInfoSizeWidthRun.Text = info.SectionWidth.ToString();
                                SectionInfoSizeHeightRun.Text = info.SectionHeight.ToString();
                                SectionInfoFullSizeHeaderRun.Text = info.FullSectionSizeHeader;
                                SectionInfoFullSizeWidthRun.Text = info.FullSectionWidth.ToString();
                                SectionInfoFullSizeHeightRun.Text =
                                    info.FullSectionHeight.ToString();
                                SectionInfoResolutionCountHeaderRun.Text =
                                    info.ResolutionCountHeader;
                                SectionInfoResolutionCountHorizontalRun.Text =
                                    info.HorizontalPointsCount.ToString();
                                SectionInfoResolutionCountVerticalRun.Text =
                                    info.VerticalPointsCount.ToString();
                            })
                            .DisposeWith(cleanUp);
                    })
                    ;

                #endregion

                #region Дополнительные элементы оформления

                binding
                    .Do(() =>
                    {
                        ViewModel.WhenAnyValue(x => x.BuildProject)
                            .Subscribe(project =>
                            {
                                DecorationColumnsDataGrid.ItemsSource = project?.DecorationColumns;
                            })
                            .DisposeWith(cleanUp);
                    })
                    .BindCommand(
                        x => x.AddOilBearingFormation,
                        x => x.AddOilBearingFormationButton)
                    .Do(() =>
                    {
                        OilBearingFormationsDataGrid.ItemsSource =
                            ViewModel.OilBearingFormations;
                    })
                    .BindCommand(
                        x => x.AddWellLabel,
                        x => x.AddWellLabelButton)
                    .Do(() =>
                    {
                        WellLabelsDataGrid.ItemsSource = ViewModel.WellLabels;
                    })
                    ;

                #endregion

                #endregion

                ;
            });
        }
    }
}