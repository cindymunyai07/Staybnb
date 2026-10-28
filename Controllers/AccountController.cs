namespace Staybnb.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly IActivityLogger _log;
    private readonly INotificationService _notify;

    public AccountController(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn,
        IActivityLogger log, INotificationService notify)
    {
        _users = users;
        _signIn = signIn;
        _log = log;
        _notify = notify;
    }

    [HttpGet]
    public IActionResult Register() => User.Identity.IsAuthenticated ? RedirectToAction("Index", "Home") : View(new RegisterViewModel());

    [HttpPost]
    public async Task<IActionResult> Register(RegisterViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var user = new ApplicationUser
        {
            UserName = vm.Email,
            Email = vm.Email,
            FirstName = vm.FirstName.Trim(),
            LastName = vm.LastName.Trim(),
            EmailConfirmed = true,
            CreatedAt = DateTime.Now
        };
        var result = await _users.CreateAsync(user, vm.Password);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors) ModelState.AddModelError(string.Empty, e.Description);
            return View(vm);
        }

        await _users.AddToRoleAsync(user, AppRoles.Guest);
        await _log.LogAsync(user.Id, "UserRegistered", ActivityType.Registration, $"{user.Email} registered as Guest.");
        await _notify.NotifyAsync(user.Id, "Welcome to Staybnb", "Browse stays, or apply to become a Host.", NotificationType.Success, "/Properties");

        await _signIn.SignInAsync(user, isPersistent: false);
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult Login(string returnUrl = null) =>
        User.Identity.IsAuthenticated ? RedirectToAction("Index", "Home") : View(new LoginViewModel { ReturnUrl = returnUrl });

    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var user = await _users.FindByEmailAsync(vm.Email);
        if (user != null)
        {
            var result = await _signIn.PasswordSignInAsync(user.UserName, vm.Password, vm.RememberMe, lockoutOnFailure: true);
            if (result.Succeeded)
            {
                await _log.LogAsync(user.Id, "UserLoggedIn", ActivityType.Login, $"{user.Email} signed in.");
                if (!string.IsNullOrEmpty(vm.ReturnUrl) && Url.IsLocalUrl(vm.ReturnUrl))
                    return LocalRedirect(vm.ReturnUrl);
                return RedirectToAction("Index", "Home");
            }

            if (result.IsLockedOut)
            {
                await _log.LogAsync(user.Id, "AccountLockedOut", ActivityType.Login, $"{user.Email} was locked out after repeated failures.");
                ModelState.AddModelError(string.Empty, "Too many failed attempts. Try again in a few minutes.");
                return View(vm);
            }

            await _log.LogAsync(user.Id, "LoginFailed", ActivityType.Login, $"Wrong password for {user.Email}.");
        }

        ModelState.AddModelError(string.Empty, "Invalid email or password.");
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        var userId = User.GetUserId();
        if (userId != null) await _log.LogAsync(userId, "UserLoggedOut", ActivityType.Logout, "User signed out.");
        await _signIn.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    public IActionResult AccessDenied() => View();
}
