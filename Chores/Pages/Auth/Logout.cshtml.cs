using Chores.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Chores.Pages.Auth;

public class LogoutModel : PageModel
{
    public async Task<IActionResult> OnPostAsync()
    {
        Response.Cookies.Delete(AgendaViewState.CookieName(User.Identity?.Name), new CookieOptions
        {
            Path = Request.PathBase.HasValue ? Request.PathBase.Value! : "/"
        });

        await HttpContext.SignOutAsync("Cookies");
        return RedirectToPage("/Auth/Login");
    }
}
