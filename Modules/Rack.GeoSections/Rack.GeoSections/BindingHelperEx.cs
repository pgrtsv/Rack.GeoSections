using System;
using System.Linq.Expressions;
using System.Windows;
using Rack.GeoSections.Infrastructure;
using Rack.Wpf.Reactive;
using ReactiveUI;
using UnitsNet;
using UnitsNet.Units;

namespace Rack.GeoSections
{
    public static class BindingHelperEx
    {
        public static BindingHelper<TView, TViewModel> BindLength<TView, TViewModel, TDontCare>(
            this BindingHelper<TView, TViewModel> binding,
            Expression<Func<TViewModel, Length>> lengthProperty,
            Expression<Func<TView, string>> textProperty,
            IObservable<TDontCare> signalUpdate,
            LengthUnit defaultUnit = LengthUnit.Meter) 
            where TViewModel : class, IActivatableViewModel
            where TView : DependencyObject, IViewFor<TViewModel> =>
            binding.Bind(
                lengthProperty,
                textProperty,
                signalUpdate,
                length => length.ToString(),
                text => LengthConvert.FromString(text, defaultUnit));

        public static BindingHelper<TView, TViewModel> BindLength<TView, TViewModel, TDontCare>(
            this BindingHelper<TView, TViewModel> binding,
            Expression<Func<TViewModel, Length>> lengthProperty,
            Expression<Func<TView, string>> textProperty,
            IObservable<TDontCare> signalUpdate,
            Func<LengthUnit> defaultUnit)
            where TViewModel : class, IActivatableViewModel
            where TView : DependencyObject, IViewFor<TViewModel> =>
            binding.Bind(
                lengthProperty,
                textProperty,
                signalUpdate,
                length => length.ToUnit(defaultUnit.Invoke()).ToString(),
                text =>
                {
                    if (Length.TryParse(text, out var length))
                        return length;
                    if (double.TryParse(text, out var value))
                        return new Length(value, defaultUnit.Invoke());
                    return Length.Zero;
                });
    }
}