using NeuroSpectator.Pages;

namespace NeuroSpectator;

/// <summary>
/// Shell for authenticated users with the full application navigation
/// </summary>
public class AuthAppShell : AppShell
{
    public AuthAppShell() : base()
    {
        // AppShell constructor already registers the routes
    }
}

/// <summary>
/// Shell used for the login process
/// </summary>
public class LoginShell : Shell
{
    public LoginShell()
    {
        // Register the pages needed for login flow
        Routing.RegisterRoute(nameof(LoginPage), typeof(LoginPage));

        // Add the login page to this shell
        Items.Add(new ShellContent
        {
            Route = nameof(LoginPage),
            ContentTemplate = new DataTemplate(typeof(LoginPage))
        });
    }
}