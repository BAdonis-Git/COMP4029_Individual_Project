namespace NeuroSpectatorMAUI.Pages;

public partial class BrowsePage : ContentPage
{
	public BrowsePage(BrowsePageModel model)
	{
		InitializeComponent();
        BindingContext = model;
    }
}