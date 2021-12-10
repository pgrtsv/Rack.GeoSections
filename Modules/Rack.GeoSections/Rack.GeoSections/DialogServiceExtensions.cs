using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Rack.GeoSections.Model;
using Rack.GeoSections.ViewModels;
using Rack.Navigation;

namespace Rack.GeoSections
{
    public static class DialogServiceExtensions
    {
        public static Task ShowSectionShapeAsync(
            this IDialogService dialogService,
            IObservable<BuildProject> buildProject) =>
            dialogService.ShowAsync<SectionShapeViewerViewModel>(
                new Dictionary<string, object>
                {
                    ["BuildProject"] = buildProject
                });

        public static Task ShowGeophysicalDataAsync(
            this IDialogService dialogService,
            Well well) =>
            dialogService.ShowDialogAsync<GeophysicalViewerViewModel>(new Dictionary<string, object>
            {
                ["Well"] = well
            });

        public static Task ShowDefaultScalesEditorDialogAsync(
            this IDialogService dialogService) =>
            dialogService.ShowDialogAsync<DefaultScalesEditorViewModel>(
                new Dictionary<string, object>());
    }
}