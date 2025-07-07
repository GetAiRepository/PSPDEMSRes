using EMS.WebApp.Data;
using EMS.WebApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Text.RegularExpressions;
using System.Web;

namespace EMS.WebApp.Controllers
{
    [Authorize]
    public class SystemScreenMasterController : Controller
    {
        private readonly ISystemScreenMasterRepository _repo;
        private readonly IMemoryCache _cache;

        public SystemScreenMasterController(ISystemScreenMasterRepository repo, IMemoryCache cache)
        {
            _repo = repo;
            _cache = cache;
        }

        // GET: /SystemScreenMaster
        public IActionResult Index() => View();

        // AJAX for DataTable
        public async Task<IActionResult> LoadData()
        {
            try
            {
                var list = await _repo.ListAsync();
                return Json(new { data = list });
            }
            catch (Exception ex)
            {
                return Json(new { data = new List<object>(), error = "Error loading data." });
            }
        }

        // GET: create form partial
        public IActionResult Create() => PartialView("_CreateEdit", new sys_screen_name());

        // POST: create with enhanced security
        [HttpPost]
        public async Task<IActionResult> Create(sys_screen_name model)
        {
            try
            {
                // Sanitize input before processing
                model = SanitizeInput(model);

                // Additional security validation
                if (!IsInputSecure(model))
                {
                    ModelState.AddModelError("", "Invalid input detected. Please remove any script tags or unsafe characters.");
                    return PartialView("_CreateEdit", model);
                }

                // Check for duplicate screen name
                if (await IsScreenNameExistsAsync(model.screen_name))
                {
                    ModelState.AddModelError("screen_name", "A screen with this name already exists. Please choose a different name.");
                }

                if (!ModelState.IsValid)
                    return PartialView("_CreateEdit", model);

                // Rate limiting logic
                var userId = User.FindFirst("user_id")?.Value ?? "anonymous";
                var cacheKey = $"rate_limit_create_systemscreenmaster_{userId}";

                var timestamps = _cache.GetOrCreate(cacheKey, entry =>
                {
                    entry.SlidingExpiration = TimeSpan.FromMinutes(5);
                    return new List<DateTime>();
                });

                timestamps.RemoveAll(t => t < DateTime.UtcNow.AddMinutes(-5));

                if (timestamps.Count >= 5)
                {
                    ViewBag.Error = "⚠ You can only create 5 screen masters every 5 minutes. Please wait and try again.";
                    return PartialView("_CreateEdit", model);
                }

                timestamps.Add(DateTime.UtcNow);
                _cache.Set(cacheKey, timestamps, TimeSpan.FromMinutes(5));

                // Check if controller exists
                var result = await _repo.AddIfControllerExistsAsync(model);
                if (!result.Success)
                {
                    var controllerList = string.Join(", ", result.AvailableControllers);
                    ModelState.AddModelError("screen_name", $"No Screen with this name exists. Available Screen: {controllerList}");
                    return PartialView("_CreateEdit", model);
                }

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                // Handle database constraint violations
                if (ex.InnerException?.Message.Contains("screen_name") == true)
                {
                    ModelState.AddModelError("screen_name", "A screen with this name already exists.");
                }
                else
                {
                    ViewBag.Error = "An error occurred while creating the screen master. Please try again.";
                }

                return PartialView("_CreateEdit", model);
            }
        }

        // GET: edit form partial
        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var item = await _repo.GetByIdAsync(id);
                if (item == null) return NotFound();
                return PartialView("_CreateEdit", item);
            }
            catch (Exception ex)
            {
                return NotFound();
            }
        }

        // POST: edit with enhanced security
        [HttpPost]
        public async Task<IActionResult> Edit(sys_screen_name model)
        {
            try
            {
                // Sanitize input before processing
                model = SanitizeInput(model);

                // Additional security validation
                if (!IsInputSecure(model))
                {
                    ModelState.AddModelError("", "Invalid input detected. Please remove any script tags or unsafe characters.");
                    return PartialView("_CreateEdit", model);
                }

                // Check for duplicate screen name (excluding current record)
                if (await IsScreenNameExistsAsync(model.screen_name, model.screen_uid))
                {
                    ModelState.AddModelError("screen_name", "A screen with this name already exists. Please choose a different name.");
                }

                if (!ModelState.IsValid)
                    return PartialView("_CreateEdit", model);

                // Rate limiting logic
                var userId = User.FindFirst("user_id")?.Value ?? "anonymous";
                var cacheKey = $"rate_limit_edit_systemscreenmaster_{userId}";

                var timestamps = _cache.GetOrCreate(cacheKey, entry =>
                {
                    entry.SlidingExpiration = TimeSpan.FromMinutes(5);
                    return new List<DateTime>();
                });

                timestamps.RemoveAll(t => t < DateTime.UtcNow.AddMinutes(-5));

                if (timestamps.Count >= 10)
                {
                    ViewBag.Error = "⚠ You can only edit 10 screen masters every 5 minutes. Please wait and try again.";
                    return PartialView("_CreateEdit", model);
                }

                timestamps.Add(DateTime.UtcNow);
                _cache.Set(cacheKey, timestamps, TimeSpan.FromMinutes(5));

                // Check if controller exists
                var result = await _repo.UpdateIfControllerExistsAsync(model);
                if (!result.Success)
                {
                    var controllerList = string.Join(", ", result.AvailableControllers);
                    ModelState.AddModelError("screen_name", $"No controller with this name exists. Available controllers: {controllerList}");
                    return PartialView("_CreateEdit", model);
                }

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                // Handle database constraint violations
                if (ex.InnerException?.Message.Contains("screen_name") == true)
                {
                    ModelState.AddModelError("screen_name", "A screen with this name already exists.");
                }
                else
                {
                    ViewBag.Error = "An error occurred while updating the screen master. Please try again.";
                }

                return PartialView("_CreateEdit", model);
            }
        }

        // POST: delete
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                await _repo.DeleteAsync(id);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "An error occurred while deleting the screen master." });
            }
        }

        // GET: /SystemScreenMaster/Details/5
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var item = await _repo.GetByIdAsync(id);
                if (item == null) return NotFound();
                return PartialView("_View", item);
            }
            catch (Exception ex)
            {
                return NotFound();
            }
        }

        // AJAX method for real-time validation
        [HttpPost]
        public async Task<IActionResult> CheckScreenNameExists(string screenName, int? screenUid = null)
        {
            if (string.IsNullOrWhiteSpace(screenName))
                return Json(new { exists = false });

            // Sanitize input before checking
            screenName = SanitizeString(screenName);

            var exists = await IsScreenNameExistsAsync(screenName, screenUid);
            return Json(new { exists = exists });
        }

        // Get available controllers for dropdown/autocomplete
        [HttpGet]
        public async Task<IActionResult> GetAvailableControllers()
        {
            try
            {
                var controllers = GetAvailableControllerNames();
                return Json(new { controllers = controllers });
            }
            catch (Exception ex)
            {
                return Json(new { controllers = new List<string>() });
            }
        }

        #region Private Methods for Input Sanitization and Validation

        private sys_screen_name SanitizeInput(sys_screen_name model)
        {
            if (model == null) return model;

            model.screen_name = SanitizeString(model.screen_name);
            model.screen_description = SanitizeString(model.screen_description);

            return model;
        }

        private string SanitizeString(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            // HTML encode the input to prevent XSS
            input = HttpUtility.HtmlEncode(input);

            // Remove or replace potentially dangerous characters
            input = Regex.Replace(input, @"[<>""'&]", "", RegexOptions.IgnoreCase);

            // Remove script tags and javascript
            input = Regex.Replace(input, @"<script[^>]*>.*?</script>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            input = Regex.Replace(input, @"javascript:", "", RegexOptions.IgnoreCase);
            input = Regex.Replace(input, @"vbscript:", "", RegexOptions.IgnoreCase);
            input = Regex.Replace(input, @"on\w+\s*=", "", RegexOptions.IgnoreCase);

            return input.Trim();
        }

        private bool IsInputSecure(sys_screen_name model)
        {
            if (model == null) return false;

            // Check for potentially dangerous patterns
            var dangerousPatterns = new[]
            {
                @"<script",
                @"</script>",
                @"javascript:",
                @"vbscript:",
                @"on\w+\s*=",
                @"eval\s*\(",
                @"expression\s*\(",
                @"<iframe",
                @"<object",
                @"<embed",
                @"<form",
                @"<input"
            };

            var inputsToCheck = new[] { model.screen_name, model.screen_description };

            foreach (var input in inputsToCheck)
            {
                if (string.IsNullOrEmpty(input)) continue;

                foreach (var pattern in dangerousPatterns)
                {
                    if (Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private async Task<bool> IsScreenNameExistsAsync(string screenName, int? excludeScreenUid = null)
        {
            try
            {
                var screens = await _repo.ListAsync();
                var query = screens.Where(s => s.screen_name.ToLower() == screenName.ToLower());

                if (excludeScreenUid.HasValue)
                {
                    query = query.Where(s => s.screen_uid != excludeScreenUid.Value);
                }

                return query.Any();
            }
            catch
            {
                return false;
            }
        }

        private List<string> GetAvailableControllerNames()
        {
            try
            {
                return System.Reflection.Assembly.GetExecutingAssembly()
                    .GetTypes()
                    .Where(t => typeof(Controller).IsAssignableFrom(t) && !t.IsAbstract)
                    .Select(t => t.Name.Replace("Controller", ""))
                    .OrderBy(name => name)
                    .ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        #endregion
    }
}