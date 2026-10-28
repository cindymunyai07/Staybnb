namespace Staybnb.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        if (User.IsStaff()) return RedirectToAction("Dashboard", "Admin");
        if (User.IsInRole(AppRoles.Host)) return RedirectToAction("Dashboard", "Host");
        return RedirectToAction("Index", "Properties");
    }

    public IActionResult Error() => View();
}
