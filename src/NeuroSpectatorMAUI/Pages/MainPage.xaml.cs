using NeuroSpectatorMAUI.Models;
using NeuroSpectatorMAUI.PageModels;

namespace NeuroSpectatorMAUI.Pages
{
    public partial class MainPage : ContentPage
    {
        public MainPage(MainPageModel model)
        {
            InitializeComponent();
            BindingContext = model;
        }
    }
}