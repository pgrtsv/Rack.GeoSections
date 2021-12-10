using System;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Runtime.Serialization;
using DynamicData;
using DynamicData.Binding;
using Rack.Shared.Configuration;

namespace Rack.GeoSections
{
    /// <summary>
    /// История успешно загруженных файлов.
    /// </summary>
    [DataContract]
    public sealed class History : IConfiguration
    {
        [DataMember]
        private readonly ObservableCollection<string> _history;

        public History() => _history = new ObservableCollection<string>();

        public IObservable<IChangeSet<string, string>> Files => _history
            .ToObservableChangeSet(x => x);

        public void Append(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentNullException();

            var index = _history.IndexOf(path, StringComparer.OrdinalIgnoreCase);
            if (index < 0)
            {
                _history.Insert(0, path);
                if (_history.Count > Length)
                    _history.RemoveAt(_history.Count - 1);
            }
            else
            {
                _history.Move(index, 0);
            }
        }

        public int Length { get; set; } = 10;

        /// <inheritdoc />
        public Version Version => Assembly.GetEntryAssembly().GetName().Version;
    }
}