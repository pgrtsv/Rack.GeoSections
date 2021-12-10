using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Windows;
using System.Windows.Controls;
using Rack.GeoSections.Infrastructure;
using Rack.GeoSections.Model;
using Rack.Wpf.Reactive;
using ReactiveUI;
using UnitsNet.Units;

namespace Rack.GeoSections.Views
{
    public partial class WellLabelEditor : ReactiveUserControl<WellLabel>
    {
        public static readonly DependencyProperty WellsProperty = DependencyProperty.Register(
            "Wells", typeof(IEnumerable<Well>), typeof(WellLabelEditor));

        public IEnumerable<Well> Wells
        {
            get => (IEnumerable<Well>) GetValue(WellsProperty);
            set => SetValue(WellsProperty, value);
        }

        public WellLabelEditor()
        {
            InitializeComponent();

            this.WhenActivated(cleanUp =>
            {
                WellComboBox.DisplayMemberPath = nameof(Well.Name);
                WellComboBox.ItemsSource = Wells;
                WellComboBox.SelectedValuePath = nameof(Well.Name);
                this.Bind(
                    ViewModel,
                    x => x.Well,
                    x => x.WellComboBox.SelectedValue)
                    .DisposeWith(cleanUp);
                this.BindValidationError(
                        ViewModel,
                        x => x.Well,
                        x => x.WellComboBox)
                    .DisposeWith(cleanUp);
                this.Bind(
                        ViewModel,
                        x => x.Top,
                        x => x.TopTextBox.Text,
                        TopTextBox.Events().LostKeyboardFocus,
                        length => length.ToString(),
                        text => LengthConvert.FromString(text, LengthUnit.Meter))
                    .DisposeWith(cleanUp);
                this.BindValidationError(
                        ViewModel,
                        x => x.Top,
                        x => x.TopTextBox)
                    .DisposeWith(cleanUp);
                this.Bind(
                        ViewModel,
                        x => x.Bottom,
                        x => x.BottomTextBox.Text,
                        BottomTextBox.Events().LostKeyboardFocus,
                        length => length.ToString(),
                        text => LengthConvert.FromString(text, LengthUnit.Meter))
                    .DisposeWith(cleanUp);
                this.BindValidationError(
                        ViewModel,
                        x => x.Bottom,
                        x => x.BottomTextBox)
                    .DisposeWith(cleanUp);
                this.Bind(
                        ViewModel,
                        x => x.Text,
                        x => x.TextTextBox.Text,
                        TextTextBox.Events().LostKeyboardFocus)
                    .DisposeWith(cleanUp);
                this.BindValidationError(
                        ViewModel,
                        x => x.Text,
                        x => x.TextTextBox)
                    .DisposeWith(cleanUp);
            });
        }
    }
}
