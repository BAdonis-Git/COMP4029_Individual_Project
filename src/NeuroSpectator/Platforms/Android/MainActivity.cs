using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;

namespace NeuroSpectator
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    [IntentFilter(
        new[] { Android.Content.Intent.ActionView },
        Categories = new[] { Android.Content.Intent.CategoryBrowsable, Android.Content.Intent.CategoryDefault },
        DataHost = "auth",
        DataScheme = "msal8148bc5a-c57b-491a-97fd-30ae8e61f960")]
    public class MainActivity : MauiAppCompatActivity
    {
        private const int RequestCode = 100;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            // This MSAL version requires all three parameters
            Microsoft.Identity.Client.AuthenticationContinuationHelper
                .SetAuthenticationContinuationEventArgs(RequestCode, Android.App.Result.Ok, Intent);
        }

        protected override void OnNewIntent(Android.Content.Intent intent)
        {
            base.OnNewIntent(intent);
            Microsoft.Identity.Client.AuthenticationContinuationHelper
                .SetAuthenticationContinuationEventArgs(RequestCode, Android.App.Result.Ok, intent);
        }
    }
}