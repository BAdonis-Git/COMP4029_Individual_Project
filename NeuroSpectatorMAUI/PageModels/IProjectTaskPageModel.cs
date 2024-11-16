using CommunityToolkit.Mvvm.Input;
using NeuroSpectatorMAUI.Models;

namespace NeuroSpectatorMAUI.PageModels
{
    public interface IProjectTaskPageModel
    {
        IAsyncRelayCommand<ProjectTask> NavigateToTaskCommand { get; }
        bool IsBusy { get; }
    }
}