using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NeuroSpectatorMAUI.Data;
using NeuroSpectatorMAUI.Models;
using NeuroSpectatorMAUI.Services;


namespace NeuroSpectatorMAUI.PageModels
{
    public partial class YourNexusPageModel : ObservableObject
    {
        public YourNexusPageModel()
        {
        }

        private async Task LoadData()
        {
            await Task.Delay(1000);
        }

        [RelayCommand]
        private Task Appearing()
            => LoadData();
    }
}