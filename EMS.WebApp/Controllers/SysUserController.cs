using EMS.WebApp.Data;
using EMS.WebApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Caching.Memory;
using System.Text.RegularExpressions;
using System.Web;

namespace EMS.WebApp.Controllers
{
    public class SysUserController : Controller
    {
        private readonly ISysUserRepository _repo;
        private readonly IMemoryCache _cache;

        public SysUserController(ISysUserRepository repo, IMemoryCache cache)
        {
            _repo = repo;
            _cache = cache;
        }

        // GET: /SysUser
        public IActionResult Index() => View();

        public async Task<IActionResult> LoadData()
        {
            try
            {
                var list = await _repo.ListWithBaseAsync();

                var result = list.Select(x => new {
                    x.user_id,
                    x.adid,
                    role_name = x.SysRole != null ? x.SysRole.role_name : "",
                    x.full_name,
                    x.email,
                    x.is_active
                });

                return Json(new { data = result });
            }
            catch (Exception ex)
            {
                // Log the error
                return Json(new { data = new List<object>(), error = "Error loading data." });
            }
        }

        // GET: create form partial
        public async Task<IActionResult> Create()
        {
            try
            {
                var roleList = await _repo.GetBaseListAsync();

                if (!roleList.Any())
                {
                    ViewBag.SysRoleList = new SelectList(Enumerable.Empty<SelectListItem>());
                    ViewBag.Error = "⚠ No roles found! Please create roles first.";
                }
                else
                {
                    ViewBag.SysRoleList = new SelectList(roleList, "role_id", "role_name");
                }

                return PartialView("_CreateEdit", new SysUser());
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Error loading create form.";
                return PartialView("_CreateEdit", new SysUser());
            }
        }

        // POST: create
        [HttpPost]
        public async Task<IActionResult> Create(SysUser model)
        {
            try
            {
                // Sanitize input before processing
                model = SanitizeInput(model);

                // Additional security validation
                if (!IsInputSecure(model))
                {
                    ModelState.AddModelError("", "Invalid input detected. Please remove any script tags or unsafe characters.");
                    ViewBag.SysRoleList = new SelectList(await _repo.GetBaseListAsync(), "role_id", "role_name", model.role_id);
                    return PartialView("_CreateEdit", model);
                }

                // Check for duplicate email
                if (await IsEmailExistsAsync(model.email))
                {
                    ModelState.AddModelError("email", "A user with this email already exists. Please use a different email.");
                }

                // Check for duplicate ADID if provided
                if (!string.IsNullOrEmpty(model.adid) && await IsAdidExistsAsync(model.adid))
                {
                    ModelState.AddModelError("adid", "A user with this ADID already exists. Please use a different ADID.");
                }

                if (!ModelState.IsValid)
                {
                    ViewBag.SysRoleList = new SelectList(await _repo.GetBaseListAsync(), "role_id", "role_name", model.role_id);
                    return PartialView("_CreateEdit", model);
                }

                // Rate limiting logic
                var userId = User.FindFirst("user_id")?.Value ?? "anonymous";
                var cacheKey = $"rate_limit_create_sysuser_{userId}";

                var timestamps = _cache.GetOrCreate(cacheKey, entry =>
                {
                    entry.SlidingExpiration = TimeSpan.FromMinutes(5);
                    return new List<DateTime>();
                });

                timestamps.RemoveAll(t => t < DateTime.UtcNow.AddMinutes(-5));

                if (timestamps.Count >= 5)
                {
                    ViewBag.SysRoleList = new SelectList(await _repo.GetBaseListAsync(), "role_id", "role_name", model.role_id);
                    ViewBag.Error = "⚠ You can only create 5 users every 5 minutes. Please wait and try again.";
                    return PartialView("_CreateEdit", model);
                }

                timestamps.Add(DateTime.UtcNow);
                _cache.Set(cacheKey, timestamps, TimeSpan.FromMinutes(5));

                await _repo.AddAsync(model);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                // Handle database constraint violations
                if (ex.InnerException?.Message.Contains("email") == true)
                {
                    ModelState.AddModelError("email", "A user with this email already exists.");
                }
                else if (ex.InnerException?.Message.Contains("adid") == true)
                {
                    ModelState.AddModelError("adid", "A user with this ADID already exists.");
                }
                else
                {
                    ViewBag.Error = "An error occurred while creating the user. Please try again.";
                }

                ViewBag.SysRoleList = new SelectList(await _repo.GetBaseListAsync(), "role_id", "role_name", model.role_id);
                return PartialView("_CreateEdit", model);
            }
        }

        // GET: edit form partial
        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var item = await _repo.GetByIdWithBaseAsync(id);
                if (item == null) return NotFound();

                ViewBag.SysRoleList = new SelectList(await _repo.GetBaseListAsync(), "role_id", "role_name", item.role_id);
                return PartialView("_CreateEdit", item);
            }
            catch (Exception ex)
            {
                return NotFound();
            }
        }

        // POST: edit
        [HttpPost]
        public async Task<IActionResult> Edit(SysUser model)
        {
            try
            {
                // Sanitize input before processing
                model = SanitizeInput(model);

                // Additional security validation
                if (!IsInputSecure(model))
                {
                    ModelState.AddModelError("", "Invalid input detected. Please remove any script tags or unsafe characters.");
                    ViewBag.SysRoleList = new SelectList(await _repo.GetBaseListAsync(), "role_id", "role_name", model.role_id);
                    return PartialView("_CreateEdit", model);
                }

                // Check for duplicate email (excluding current user)
                if (await IsEmailExistsAsync(model.email, model.user_id))
                {
                    ModelState.AddModelError("email", "A user with this email already exists. Please use a different email.");
                }

                // Check for duplicate ADID if provided (excluding current user)
                if (!string.IsNullOrEmpty(model.adid) && await IsAdidExistsAsync(model.adid, model.user_id))
                {
                    ModelState.AddModelError("adid", "A user with this ADID already exists. Please use a different ADID.");
                }

                if (!ModelState.IsValid)
                {
                    ViewBag.SysRoleList = new SelectList(await _repo.GetBaseListAsync(), "role_id", "role_name", model.role_id);
                    return PartialView("_CreateEdit", model);
                }

                // Rate limiting logic
                var userId = User.FindFirst("user_id")?.Value ?? "anonymous";
                var cacheKey = $"rate_limit_edit_sysuser_{userId}";

                var timestamps = _cache.GetOrCreate(cacheKey, entry =>
                {
                    entry.SlidingExpiration = TimeSpan.FromMinutes(5);
                    return new List<DateTime>();
                });

                timestamps.RemoveAll(t => t < DateTime.UtcNow.AddMinutes(-5));

                if (timestamps.Count >= 10)
                {
                    ViewBag.SysRoleList = new SelectList(await _repo.GetBaseListAsync(), "role_id", "role_name", model.role_id);
                    ViewBag.Error = "⚠ You can only edit 10 users every 5 minutes. Please wait and try again.";
                    return PartialView("_CreateEdit", model);
                }

                timestamps.Add(DateTime.UtcNow);
                _cache.Set(cacheKey, timestamps, TimeSpan.FromMinutes(5));

                await _repo.UpdateAsync(model);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                // Handle database constraint violations
                if (ex.InnerException?.Message.Contains("email") == true)
                {
                    ModelState.AddModelError("email", "A user with this email already exists.");
                }
                else if (ex.InnerException?.Message.Contains("adid") == true)
                {
                    ModelState.AddModelError("adid", "A user with this ADID already exists.");
                }
                else
                {
                    ViewBag.Error = "An error occurred while updating the user. Please try again.";
                }

                ViewBag.SysRoleList = new SelectList(await _repo.GetBaseListAsync(), "role_id", "role_name", model.role_id);
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
                return Json(new { success = false, message = "An error occurred while deleting the user." });
            }
        }

        // GET: /SysUser/Details/5
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var item = await _repo.GetByIdWithBaseAsync(id);
                if (item == null) return NotFound();
                return PartialView("_View", item);
            }
            catch (Exception ex)
            {
                return NotFound();
            }
        }

        // AJAX methods for real-time validation
        [HttpPost]
        public async Task<IActionResult> CheckEmailExists(string email, int? userId = null)
        {
            if (string.IsNullOrWhiteSpace(email))
                return Json(new { exists = false });

            // Sanitize input before checking
            email = SanitizeString(email);

            var exists = await IsEmailExistsAsync(email, userId);
            return Json(new { exists = exists });
        }

        [HttpPost]
        public async Task<IActionResult> CheckAdidExists(string adid, int? userId = null)
        {
            if (string.IsNullOrWhiteSpace(adid))
                return Json(new { exists = false });

            // Sanitize input before checking
            adid = SanitizeString(adid);

            var exists = await IsAdidExistsAsync(adid, userId);
            return Json(new { exists = exists });
        }

        #region Private Methods for Input Sanitization and Validation

        private SysUser SanitizeInput(SysUser model)
        {
            if (model == null) return model;

            model.adid = SanitizeString(model.adid);
            model.full_name = SanitizeString(model.full_name);
            model.email = SanitizeString(model.email);

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

        private bool IsInputSecure(SysUser model)
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

            var inputsToCheck = new[] { model.adid, model.full_name, model.email };

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

        private async Task<bool> IsEmailExistsAsync(string email, int? excludeUserId = null)
        {
            try
            {
                var users = await _repo.ListAsync();
                var query = users.Where(u => u.email.ToLower() == email.ToLower());

                if (excludeUserId.HasValue)
                {
                    query = query.Where(u => u.user_id != excludeUserId.Value);
                }

                return query.Any();
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> IsAdidExistsAsync(string adid, int? excludeUserId = null)
        {
            try
            {
                var users = await _repo.ListAsync();
                var query = users.Where(u => !string.IsNullOrEmpty(u.adid) && u.adid.ToLower() == adid.ToLower());

                if (excludeUserId.HasValue)
                {
                    query = query.Where(u => u.user_id != excludeUserId.Value);
                }

                return query.Any();
            }
            catch
            {
                return false;
            }
        }

        #endregion
    }
}