namespace NeuroSpectatorMAUI.Pages
{
	public partial class YourNexusPage : ContentPage
	{
		public YourNexusPage(YourNexusPageModel model)
		{
			InitializeComponent();
            BindingContext = model;
        }
	}
}