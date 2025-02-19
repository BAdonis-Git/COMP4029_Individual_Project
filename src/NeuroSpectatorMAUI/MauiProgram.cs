using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Syncfusion.Maui.Toolkit.Hosting;

namespace NeuroSpectatorMAUI
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .ConfigureSyncfusionToolkit()
                .ConfigureMauiHandlers(handlers =>
                {
                })
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    fonts.AddFont("SegoeUI-Semibold.ttf", "SegoeSemibold");
                    fonts.AddFont("FluentSystemIcons-Regular.ttf", FluentUI.FontFamily);
                });

#if DEBUG
    		builder.Logging.AddDebug();
    		builder.Services.AddLogging(configure => configure.AddDebug());
#endif
            //Singleton Services
            builder.Services.AddSingleton<ProjectRepository>();
            builder.Services.AddSingleton<TaskRepository>();
            builder.Services.AddSingleton<CategoryRepository>();
            builder.Services.AddSingleton<TagRepository>();
            builder.Services.AddSingleton<SeedDataService>();
            builder.Services.AddSingleton<ModalErrorHandler>();
            builder.Services.AddSingleton<MuseDeviceManager>();//HERE

            //Transient Services
            builder.Services.AddTransient<MainPageModel>();
            builder.Services.AddTransient<ProjectListPageModel>();
            builder.Services.AddTransient<ManageMetaPageModel>();
            builder.Services.AddTransient<YourNexusPageModel>();//HERE
            builder.Services.AddTransient<YourDevicesPageModel>();
            builder.Services.AddTransient<BrowsePageModel>();

            //Page Routes
            builder.Services.AddTransientWithShellRoute<ProjectDetailPage, ProjectDetailPageModel>("project");
            builder.Services.AddTransientWithShellRoute<TaskDetailPage, TaskDetailPageModel>("task");
            builder.Services.AddTransientWithShellRoute<YourNexusPage, YourNexusPageModel>("nexus");//HERE
            builder.Services.AddTransientWithShellRoute<YourDevicesPage, YourDevicesPageModel>("devices");
            builder.Services.AddTransientWithShellRoute<BrowsePage, BrowsePageModel>("browse");

            return builder.Build();
        }
    }
}
