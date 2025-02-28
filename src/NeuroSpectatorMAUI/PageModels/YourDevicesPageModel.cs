using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NeuroSpectatorMAUI.Data;
using NeuroSpectatorMAUI.Models;
using NeuroSpectatorMAUI.Services;
using System.Collections.ObjectModel;


namespace NeuroSpectatorMAUI.PageModels
{
    public partial class YourDevicesPageModel : ObservableObject
    {
        private readonly MuseDeviceManager _deviceManager;

        [ObservableProperty]
        private MuseDevice? currentDevice;

        [ObservableProperty]
        private bool isScanning;

        [ObservableProperty]
        private string statusMessage = string.Empty;

        public YourDevicesPageModel(/*MuseDeviceManager deviceManager*/)
        {
            //_deviceManager = deviceManager;
        }

        private async Task LoadData()
        {
            try
            {
                await Task.Delay(1000);
                //var devices = _deviceManager.AvailableDevices;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        [RelayCommand]
        private Task Appearing()
            => LoadData();
    }
}