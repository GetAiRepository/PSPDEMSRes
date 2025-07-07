using EMS.WebApp.Data;
using EMS.WebApp.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.DirectoryServices.AccountManagement;
using System.Security.Claims;

namespace EMS.WebApp.Controllers
{
    public class AccountController : Controller
    {
        private readonly IAccountLoginRepository _repo;

        public AccountController(IAccountLoginRepository repo) => _repo = repo;

        // GET: /Account/Login
        //public IActionResult Login() => View();
        public async Task<IActionResult> Login()
        {
            // ✅ Always sign out any existing identity cookie — even if invalid
            await HttpContext.SignOutAsync();

            // ✅ Ensure TempData is not cleared by middleware accidentally
            Response.Cookies.Delete(".AspNetCore.Cookies");

            return View();
        }
        // POST: /Account/Login
        [HttpPost]
        public async Task<IActionResult> Login(string user_name, string password)
        {
            if (string.IsNullOrWhiteSpace(user_name) || string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "Email and Password are required.";
                return View();
            }

            var user = await _repo.GetByEmailAndPasswordAsync(user_name, password);

            if (user == null)
            {
                ViewBag.Error = "Invalid credentials or user is inactive.";
                return View();
            }

            // If already logged in and user hasn't confirmed override
            if (!string.IsNullOrEmpty(user.SessionToken))
            {
                TempData["user_name"] = user_name;
                TempData["password"] = password;
                return RedirectToAction("ConfirmSessionOverride");
            }
            await SignInUser(user);
            return RedirectToAction("Index", "Home");
        }
        [HttpGet]
        public IActionResult ConfirmSessionOverride()
        {
            ViewBag.user_name = TempData["user_name"];
            ViewBag.password = TempData["password"];
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ProceedConfirmedLogin(string user_name, string password)
        {
            var user = await _repo.GetByEmailAndPasswordAsync(user_name, password);

            if (user == null)
            {
                ViewBag.Error = "Invalid credentials.";
                return RedirectToAction("Login");
            }

            await SignInUser(user);
            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        public IActionResult ConfirmSessionOverride(string user_name, string password)
        {
            return RedirectToAction("Login", new { user_name, password, confirm = true });
        }
        private async Task SignInUser(AccountLogin user)
        {
            var sessionToken = Guid.NewGuid().ToString();
            user.SessionToken = sessionToken;
            user.TokenIssuedAt = DateTime.UtcNow;
            await _repo.UpdateAsync(user);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.user_name),
                new Claim("LoginId", user.login_id.ToString()),
                new Claim("LoginType", "local"),
                new Claim("SessionToken", sessionToken)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
        }



        //Autologin method for AD users
        [Authorize(AuthenticationSchemes = NegotiateDefaults.AuthenticationScheme)]
        public async Task<IActionResult> AutoLogin(bool confirm = false)
        {
            var adid = HttpContext.User.Identity?.Name;
            Console.WriteLine("Adid:-" + adid);
            if (string.IsNullOrEmpty(adid))
            {
                TempData["Error"] = "Unable to retrieve AD identity.";
                return RedirectToAction("Login");
            }

            var usernameOnly = adid.Contains('\\') ? adid.Split('\\')[1] : adid;
            //var usernameOnly = adid;

            var user = await _repo.GetByAdidAsync(usernameOnly);
            Console.WriteLine("user------------" + user);
            if (user == null)
            {
                Console.WriteLine("AD user not registered.");
                TempData["Error"] = "AD user not registered.";
                return RedirectToAction("Login");
            }

            if (!confirm && !string.IsNullOrEmpty(user.SessionToken))
            {
                TempData["adid"] = usernameOnly;
                return RedirectToAction("ConfirmSessionOverrideAD");
            }

            await SignInUser(user);
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult ConfirmSessionOverrideAD()
        {
            ViewBag.adid = TempData["adid"];
            return View();
        }

        [HttpPost]
        public IActionResult ConfirmSessionOverrideAD(string adid)
        {
            return RedirectToAction("AutoLogin", new { confirm = true });
        }




        // GET: /Account/Logout
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            //await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            //return RedirectToAction("Login", "Account");
            var userName = User.Identity?.Name;
            if (userName != null)
            {
                var user = await _repo.GetByEmailAsync(userName);
                if (user != null)
                {
                    user.SessionToken = null;
                    user.TokenIssuedAt = null;
                    await _repo.UpdateAsync(user);
                }
            }

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Account");
        }

        public IActionResult LogoutView()
        {
            return View();
        }
        public IActionResult NewTab()
        {
            return View();
        }
        //[Authorize]
        //[HttpGet]
        //public async Task<IActionResult> Logout();
    }
}
