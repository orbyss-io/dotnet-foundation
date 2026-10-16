using Microsoft.AspNetCore.Http;

namespace Orbyss.Foundation.Authentication;

/// <summary>Native profile operations used by independently selected browser presentation.</summary>
public interface IBrowserAuthenticationSession
{
    /// <summary>Reads the validated local session independently of recovery URL parameters.</summary>
    bool IsAuthenticated(HttpContext context);
    /// <summary>Starts the selected native challenge with a previously admitted local return target.</summary>
    Task SignInAsync(HttpContext context, string returnTarget);
    /// <summary>Clears the native local session and initiates provider logout to the configured native landing.</summary>
    Task SignOutAsync(HttpContext context);
}
