using System.Reactive.Disposables;
using System.Windows.Controls;
using MahApps.Metro.Controls;
using Microsoft.Xaml.Behaviors;
using Rack.GeoSections.ViewModels;
using Rack.GeoTools;
using Rack.Wpf.Behaviors;
using ReactiveUI;

namespace Rack.GeoSections.Views
{
    public partial class DefaultScalesEditor : ReactiveUserControl<DefaultScalesEditorViewModel>
    {
        public DefaultScalesEditor()
        {
            InitializeComponent();

            Interaction.GetBehaviors(SaveButton)
                .Add(new UpdateOnExecuteCommandBehavior());

            foreach (var textbox in this.FindChildren<TextBox>())
            {
                var behaviors = Interaction.GetBehaviors(textbox);
                behaviors.Add(new TextBoxSelectAllOnFocusBehavior());
                behaviors.Add(new FocusNextOnEnterBehavior());
            }

            this.WhenActivated(cleanUp =>
            {
                this.BindCommand(
                    ViewModel,
                    x => x.Save,
                    x => x.SaveButton)
                    .DisposeWith(cleanUp);

                this.Bind(
                    ViewModel,
                    x => x.Settings.DefaultVerticalScale1,
                    x => x.DefaultVerticalScale1TextBox.Text,
                    DefaultVerticalScale1TextBox.Events().LostKeyboardFocus,
                    ScaleConvert.Convert,
                    ScaleConvert.ConvertFrom)
                    .DisposeWith(cleanUp);
                this.Bind(
                    ViewModel,
                    x => x.Settings.DefaultVerticalScale2,
                    x => x.DefaultVerticalScale2TextBox.Text,
                    DefaultVerticalScale2TextBox.Events().LostKeyboardFocus,
                    ScaleConvert.Convert,
                    ScaleConvert.ConvertFrom)
                    .DisposeWith(cleanUp);
                this.Bind(
                    ViewModel,
                    x => x.Settings.DefaultVerticalScale3,
                    x => x.DefaultVerticalScale3TextBox.Text,
                    DefaultVerticalScale3TextBox.Events().LostKeyboardFocus,
                    ScaleConvert.Convert,
                    ScaleConvert.ConvertFrom)
                    .DisposeWith(cleanUp);

                this.Bind(
                    ViewModel,
                    x => x.Settings.DefaultHorizontalScale1,
                    x => x.DefaultHorizontalScale1TextBox.Text,
                    DefaultHorizontalScale1TextBox.Events().LostKeyboardFocus,
                    ScaleConvert.Convert,
                    ScaleConvert.ConvertFrom)
                    .DisposeWith(cleanUp);
                this.Bind(
                    ViewModel,
                    x => x.Settings.DefaultHorizontalScale2,
                    x => x.DefaultHorizontalScale2TextBox.Text,
                    DefaultHorizontalScale2TextBox.Events().LostKeyboardFocus,
                    ScaleConvert.Convert,
                    ScaleConvert.ConvertFrom)
                    .DisposeWith(cleanUp);
                this.Bind(
                    ViewModel,
                    x => x.Settings.DefaultHorizontalScale3,
                    x => x.DefaultHorizontalScale3TextBox.Text,
                    DefaultHorizontalScale3TextBox.Events().LostKeyboardFocus,
                    ScaleConvert.Convert,
                    ScaleConvert.ConvertFrom)
                    .DisposeWith(cleanUp);
            });
        }
    }
}
