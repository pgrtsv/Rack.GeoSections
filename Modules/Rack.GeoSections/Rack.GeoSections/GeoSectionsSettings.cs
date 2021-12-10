using System;
using System.ComponentModel;
using Rack.Shared.Configuration;

namespace Rack.GeoSections
{
    public sealed class GeoSectionsSettings : INotifyPropertyChanged, IConfiguration, ICloneable
    {
        public double DefaultVerticalScale1 { get; set; } = 1.0 / 1000;
        public double DefaultVerticalScale2 { get; set; } = 1.0 / 2000;
        public double DefaultVerticalScale3 { get; set; } = 1.0 / 5000;

        public double DefaultHorizontalScale1 { get; set; } = 1.0 / 10000;
        public double DefaultHorizontalScale2 { get; set; } = 1.0 / 25000;
        public double DefaultHorizontalScale3 { get; set; } = 1.0 / 50000;

        object ICloneable.Clone() => Clone();

        public Version Version { get; } = new Version(1, 0);

        public event PropertyChangedEventHandler PropertyChanged;

        public void Map(GeoSectionsSettings settings)
        {
            DefaultVerticalScale1 = settings.DefaultVerticalScale1;
            DefaultVerticalScale2 = settings.DefaultVerticalScale2;
            DefaultVerticalScale3 = settings.DefaultVerticalScale3;
            DefaultHorizontalScale1 = settings.DefaultHorizontalScale1;
            DefaultHorizontalScale2 = settings.DefaultHorizontalScale2;
            DefaultHorizontalScale3 = settings.DefaultHorizontalScale3;
        }

        public GeoSectionsSettings Clone()
        {
            return new GeoSectionsSettings
            {
                DefaultHorizontalScale1 = DefaultHorizontalScale1,
                DefaultHorizontalScale2 = DefaultHorizontalScale2,
                DefaultHorizontalScale3 = DefaultHorizontalScale3,
                DefaultVerticalScale1 = DefaultVerticalScale1,
                DefaultVerticalScale2 = DefaultVerticalScale2,
                DefaultVerticalScale3 = DefaultVerticalScale3
            };
        }
    }
}