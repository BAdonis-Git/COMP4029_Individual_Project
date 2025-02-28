using NeuroSpectatorMAUI.Services;

namespace NeuroSpectatorMAUI.Pages;

public partial class YourDevicesPage : ContentPage
{
    private readonly MuseDeviceManager _deviceManager;
    private bool _isScanning = false;

    public YourDevicesPage(YourDevicesPageModel pageModel)
    {
        InitializeComponent();
        BindingContext = pageModel;

        // Create device manager
        _deviceManager = new MuseDeviceManager();
        _deviceManager.DeviceDiscovered += DeviceManager_DeviceDiscovered;
        _deviceManager.ConnectionError += DeviceManager_ConnectionError;
        _deviceManager.DataReceived += DeviceManager_DataReceived;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Initialize device manager when page appears
        if (_deviceManager != null && !_deviceManager.IsInitialized)
        {
            // Don't auto-initialize as we have a button for that
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        // Clean up when page disappears
        if (_isScanning)
        {
            StopScanning();
        }

        _deviceManager?.Dispose();
    }

    private async void InitializeDeviceManager()
    {
        try
        {
            bool result = _deviceManager.Initialize();
            if (result)
            {
                StatusLabel.Text = "Muse Manager initialized successfully";
                InitialiseButton.IsEnabled = false;
                InitialiseButton.Text = "Initialised";
                ScanButton.IsEnabled = true;

                // Refresh device list
                await _deviceManager.RefreshDeviceListAsync();
                UpdateDeviceList();
            }
            else
            {
                StatusLabel.Text = "Failed to initialize Muse Manager";
            }
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Error initializing Muse Manager: {ex.Message}";
        }
    }

    private void UpdateDeviceList()
    {
        // Clear existing items
        DevicesList.ItemsSource = null;

        // Set new items
        DevicesList.ItemsSource = _deviceManager.AvailableDevices;
    }

    private void OnInitialiseClicked(object sender, EventArgs e)
    {
        InitializeDeviceManager();
    }

    private async void OnScanClicked(object sender, EventArgs e)
    {
        if (_isScanning)
        {
            await StopScanning();
        }
        else
        {
            await StartScanning();
        }
    }

    private async Task StartScanning()
    {
        try
        {
            _isScanning = await _deviceManager.StartScanningAsync();
            if (_isScanning)
            {
                StatusLabel.Text = "Scanning for Muse devices...";
                ScanButton.Text = "Stop Scanning";
            }
            else
            {
                StatusLabel.Text = "Failed to start scanning";
            }
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Error starting scan: {ex.Message}";
        }
    }

    private async Task StopScanning()
    {
        try
        {
            await _deviceManager.StopScanningAsync();
            _isScanning = false;
            StatusLabel.Text = "Scanning stopped";
            ScanButton.Text = "Start Scanning";
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Error stopping scan: {ex.Message}";
        }
    }

    private async void OnConnectClicked(object sender, EventArgs e)
    {
        try
        {
            // Get selected device
            var selectedDevice = DevicesList.SelectedItem as MuseDevice;
            if (selectedDevice == null)
            {
                StatusLabel.Text = "Please select a device first";
                return;
            }

            // Connect to device
            bool connected = await _deviceManager.ConnectToDeviceAsync(selectedDevice);
            if (connected)
            {
                StatusLabel.Text = $"Connected to {selectedDevice.Name}";

                // Register for EEG data
                await _deviceManager.RegisterForDataAsync(MuseDataPacketType.Eeg);

                // Update UI
                ConnectButton.IsEnabled = false;
                DisconnectButton.IsEnabled = true;
            }
            else
            {
                StatusLabel.Text = $"Failed to connect to {selectedDevice.Name}";
            }
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Error connecting to device: {ex.Message}";
        }
    }

    private async void OnDisconnectClicked(object sender, EventArgs e)
    {
        try
        {
            // Disconnect from current device
            bool disconnected = await _deviceManager.DisconnectCurrentDeviceAsync();
            if (disconnected)
            {
                StatusLabel.Text = "Disconnected from device";

                // Update UI
                ConnectButton.IsEnabled = true;
                DisconnectButton.IsEnabled = false;
                DataValueLabel.Text = "No data";
            }
            else
            {
                StatusLabel.Text = "Failed to disconnect from device";
            }
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Error disconnecting from device: {ex.Message}";
        }
    }

    private void DeviceManager_DeviceDiscovered(object sender, MuseDevice device)
    {
        MainThread.BeginInvokeOnMainThread(() => {
            StatusLabel.Text = $"Discovered device: {device.Name}";
            UpdateDeviceList();
        });
    }

    private void DeviceManager_ConnectionError(object sender, string errorMessage)
    {
        MainThread.BeginInvokeOnMainThread(() => {
            StatusLabel.Text = $"Connection error: {errorMessage}";
        });
    }

    private void DeviceManager_DataReceived(object sender, (MuseDataPacketType type, double[] data) data)
    {
        MainThread.BeginInvokeOnMainThread(() => {
            // Update UI with the received data
            if (data.type == MuseDataPacketType.Eeg)
            {
                // For EEG data, show the first channel value
                DataValueLabel.Text = $"EEG1: {data.data[0]:F2} ?V";
            }
        });
    }
}