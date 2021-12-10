using System;
using System.Windows;
using GongSolutions.Wpf.DragDrop;

namespace Rack.GeoSections.Infrastructure
{
    public sealed class DragDropHandler<T>: IDropTarget
    {
        private readonly Action<T, T> _dropAction;

        public DragDropHandler(Action<T, T> dropAction)
        {
            _dropAction = dropAction;
        }

        public void DragOver(IDropInfo dropInfo)
        {
            if (dropInfo.Data is T && dropInfo.TargetItem is T)
            {
                dropInfo.DropTargetAdorner = DropTargetAdorners.Insert;
                dropInfo.Effects = DragDropEffects.Move;
            }
        }

        public void Drop(IDropInfo dropInfo)
        {
            if (dropInfo.Data is T source && dropInfo.TargetItem is T target)
            {
                _dropAction?.Invoke(source, target);
            }
        }
    }
}