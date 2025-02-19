namespace NeuroSpectatorMAUI.Pages;

public partial class YourDevicesPage : ContentPage
{
    private readonly MuseDevice _museDevice;
    public YourDevicesPage(YourDevicesPageModel pageModel)
    {
        InitializeComponent();
        BindingContext = pageModel;
        _museDevice = new MuseDevice();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is YourDevicesPageModel viewModel)
        {
            viewModel.AppearingCommand.Execute(null);
        }
    }

    private void OnInitialiseClicked(object sender, EventArgs e)
    {
        try
        {
            bool result = _museDevice.Initialise();
            StatusLabel.Text = result ? "Muse Manager initialised successfully" : "Failed to initialise Muse Manager";
            if (result)
            {
                InitialiseButton.IsEnabled = false;
                InitialiseButton.Text = "Initialised";
            }
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Error initialising Muse Manager: {ex.Message}";
        }
    }
}