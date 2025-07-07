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
    public class SysRoleController : Controller
    {
        private readonly ISysRoleRepository _repo;
        private readonly IMemoryCache _cache;

        public SysRoleController(ISysRoleRepository repo, IMemoryCache cache)
        {
            _repo = repo;
            _cache = cache;
        }

        public IActionResult Index() => View();

        public async Task<IActionResult> LoadData()
        {
            var list = await _repo.ListAsync();
            return Json(new { data = list });
        }

        public IActionResult Create() => PartialView("_CreateEdit", new SysRole());

        [HttpPost]
        public async Task<IActionResult> Create(SysRole model)
        {
            // Sanitize input before processing
            model = SanitizeInput(model);

            // Additional security validation
            if (!IsInputSecure(model))
            {
                ModelState.AddModelError("", "Invalid input detected. Please remove any script tags or unsafe characters.");
                return PartialView("_CreateEdit", model);
            }

            // Check for duplicate role name
            if (await _repo.IsRoleNameExistsAsync(model.role_name))
            {
                ModelState.AddModelError("role_name", "A role with this name already exists. Please choose a different name.");
            }

            if (!ModelState.IsValid)
                return PartialView("_CreateEdit", model);

            // Rate limiting logic
            var userId = User.FindFirst("user_id")?.Value ?? "anonymous";
            var cacheKey = $"rate_limit_create_sysrole_{userId}";

            var timestamps = _cache.GetOrCreate(cacheKey, entry =>
            {
                entry.SlidingExpiration = TimeSpan.FromMinutes(5);
                return new List<DateTime>();
            });

            timestamps.RemoveAll(t => t < DateTime.UtcNow.AddMinutes(-5));

            if (timestamps.Count >= 5)
            {
                ViewBag.Error = "⚠ You can only create 5 roles every 5 minutes. Please wait and try again.";
                return PartialView("_CreateEdit", model);
            }

            timestamps.Add(DateTime.UtcNow);
            _cache.Set(cacheKey, timestamps, TimeSpan.FromMinutes(5));

            try
            {
                await _repo.AddAsync(model);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                // Handle database constraint violation
                if (ex.InnerException?.Message.Contains("IX_SysRole_RoleName_Unique") == true)
                {
                    ModelState.AddModelError("role_name", "A role with this name already exists. Please choose a different name.");
                    return PartialView("_CreateEdit", model);
                }

                // Log the error and return a generic error message
                ViewBag.Error = "An error occurred while creating the role. Please try again.";
                return PartialView("_CreateEdit", model);
            }
        }

        public async Task<IActionResult> Edit(int id)
        {
            var item = await _repo.GetByIdAsync(id);
            return PartialView("_CreateEdit", item);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(SysRole model)
        {
            // Sanitize input before processing
            model = SanitizeInput(model);

            // Additional security validation
            if (!IsInputSecure(model))
            {
                ModelState.AddModelError("", "Invalid input detected. Please remove any script tags or unsafe characters.");
                return PartialView("_CreateEdit", model);
            }

            // Check for duplicate role name (excluding current record)
            if (await _repo.IsRoleNameExistsAsync(model.role_name, model.role_id))
            {
                ModelState.AddModelError("role_name", "A role with this name already exists. Please choose a different name.");
            }

            if (!ModelState.IsValid)
                return PartialView("_CreateEdit", model);

            // Rate limiting logic
            var userId = User.FindFirst("user_id")?.Value ?? "anonymous";
            var cacheKey = $"rate_limit_edit_sysrole_{userId}";

            var timestamps = _cache.GetOrCreate(cacheKey, entry =>
            {
                entry.SlidingExpiration = TimeSpan.FromMinutes(5);
                return new List<DateTime>();
            });

            timestamps.RemoveAll(t => t < DateTime.UtcNow.AddMinutes(-5));

            if (timestamps.Count >= 10)
            {
                ViewBag.Error = "⚠ You can only edit 10 roles every 5 minutes. Please wait and try again.";
                return PartialView("_CreateEdit", model);
            }

            timestamps.Add(DateTime.UtcNow);
            _cache.Set(cacheKey, timestamps, TimeSpan.FromMinutes(5));

            try
            {
                await _repo.UpdateAsync(model);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                // Handle database constraint violation
                if (ex.InnerException?.Message.Contains("IX_SysRole_RoleName_Unique") == true)
                {
                    ModelState.AddModelError("role_name", "A role with this name already exists. Please choose a different name.");
                    return PartialView("_CreateEdit", model);
                }

                // Log the error and return a generic error message
                ViewBag.Error = "An error occurred while updating the role. Please try again.";
                return PartialView("_CreateEdit", model);
            }
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                await _repo.DeleteAsync(id);
                return Json(new { success = true });
            }
            catch (Exception)
            {
                return Json(new { success = false, message = "An error occurred while deleting the role." });
            }
        }

        public async Task<IActionResult> Details(int id)
        {
            var item = await _repo.GetByIdAsync(id);
            if (item == null) return NotFound();
            return PartialView("_View", item);
        }

        // Add AJAX method for real-time validation
        [HttpPost]
        public async Task<IActionResult> CheckRoleNameExists(string roleName, int? roleId = null)
        {
            if (string.IsNullOrWhiteSpace(roleName))
                return Json(new { exists = false });

            // Sanitize input before checking
            roleName = SanitizeString(roleName);

            var exists = await _repo.IsRoleNameExistsAsync(roleName, roleId);
            return Json(new { exists = exists });
        }

        #region Private Methods for Input Sanitization

        private SysRole SanitizeInput(SysRole model)
        {
            if (model == null) return model;

            model.role_name = SanitizeString(model.role_name);
            model.role_desc = SanitizeString(model.role_desc);

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

        private bool IsInputSecure(SysRole model)
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

            var inputsToCheck = new[] { model.role_name, model.role_desc };

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

        #endregion
    }
}