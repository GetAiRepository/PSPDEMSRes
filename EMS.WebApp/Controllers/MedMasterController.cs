using EMS.WebApp.Data;
using EMS.WebApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Caching.Memory;
using System.Text.RegularExpressions;
using System.Web;

namespace EMS.WebApp.Controllers
{
    [Authorize]
    public class MedMasterController : Controller
    {
        private readonly IMedMasterRepository _repo;
        private readonly IMemoryCache _cache;

        public MedMasterController(IMedMasterRepository repo, IMemoryCache cache)
        {
            _repo = repo;
            _cache = cache;
        }

        public IActionResult Index() => View();

        public async Task<IActionResult> LoadData()
        {
            try
            {
                var list = await _repo.ListWithBaseAsync();
                var result = list.Select(x => new {
                    x.MedItemId,
                    x.MedItemName,
                    BaseName = x.MedBase != null ? x.MedBase.BaseName : "",
                    x.CompanyName,
                    x.ReorderLimit
                });
                return Json(new { data = result });
            }
            catch (Exception ex)
            {
                return Json(new { data = new List<object>(), error = "Error loading data." });
            }
        }

        public async Task<IActionResult> Create()
        {
            try
            {
                var baseList = await _repo.GetBaseListAsync();

                if (!baseList.Any())
                {
                    ViewBag.MedBaseList = new SelectList(Enumerable.Empty<SelectListItem>());
                    ViewBag.Error = "⚠ No medicine bases found! Please create medicine bases first.";
                }
                else
                {
                    ViewBag.MedBaseList = new SelectList(baseList, "BaseId", "BaseName");
                }

                return PartialView("_CreateEdit", new MedMaster());
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Error loading create form.";
                return PartialView("_CreateEdit", new MedMaster());
            }
        }

        [HttpPost]
        public async Task<IActionResult> Create(MedMaster model)
        {
            try
            {
                // Sanitize input before processing
                model = SanitizeInput(model);

                // Additional security validation
                if (!IsInputSecure(model))
                {
                    ModelState.AddModelError("", "Invalid input detected. Please remove any script tags or unsafe characters.");
                    ViewBag.MedBaseList = new SelectList(await _repo.GetBaseListAsync(), "BaseId", "BaseName", model.BaseId);
                    return PartialView("_CreateEdit", model);
                }

                var baseList = await _repo.GetBaseListAsync();
                ViewBag.MedBaseList = new SelectList(baseList, "BaseId", "BaseName", model.BaseId);

                // Check for duplicate med item details combination
                if (await _repo.IsMedItemDetailsExistsAsync(model.MedItemName, model.BaseId, model.CompanyName))
                {
                    ModelState.AddModelError("", "A medical item with this combination of name, base, and company already exists. Please choose different values.");
                    ModelState.AddModelError("MedItemName", "This combination already exists.");
                    ModelState.AddModelError("BaseId", "This combination already exists.");
                    ModelState.AddModelError("CompanyName", "This combination already exists.");
                }

                if (!ModelState.IsValid)
                    return PartialView("_CreateEdit", model);

                // Rate limiting logic
                var userId = User.FindFirst("user_id")?.Value ?? "anonymous";
                var cacheKey = $"rate_limit_create_medmaster_{userId}";

                var timestamps = _cache.GetOrCreate(cacheKey, entry =>
                {
                    entry.SlidingExpiration = TimeSpan.FromMinutes(5);
                    return new List<DateTime>();
                });

                timestamps.RemoveAll(t => t < DateTime.UtcNow.AddMinutes(-5));

                if (timestamps.Count >= 5)
                {
                    ViewBag.Error = "⚠ You can only create 5 medicine records every 5 minutes. Please wait and try again.";
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
                if (ex.InnerException?.Message.Contains("IX_MedMaster_MedItemNameBaseIdCompanyName_Unique") == true)
                {
                    ModelState.AddModelError("", "A medical item with this combination already exists. Please choose different values.");
                    ModelState.AddModelError("MedItemName", "This combination already exists.");
                    ModelState.AddModelError("BaseId", "This combination already exists.");
                    ModelState.AddModelError("CompanyName", "This combination already exists.");
                }
                else
                {
                    ViewBag.Error = "An error occurred while creating the medicine. Please try again.";
                }

                ViewBag.MedBaseList = new SelectList(await _repo.GetBaseListAsync(), "BaseId", "BaseName", model.BaseId);
                return PartialView("_CreateEdit", model);
            }
        }

        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var item = await _repo.GetByIdWithBaseAsync(id);
                if (item == null) return NotFound();

                ViewBag.MedBaseList = new SelectList(await _repo.GetBaseListAsync(), "BaseId", "BaseName", item.BaseId);
                return PartialView("_CreateEdit", item);
            }
            catch (Exception ex)
            {
                return NotFound();
            }
        }

        [HttpPost]
        public async Task<IActionResult> Edit(MedMaster model)
        {
            try
            {
                // Sanitize input before processing
                model = SanitizeInput(model);

                // Additional security validation
                if (!IsInputSecure(model))
                {
                    ModelState.AddModelError("", "Invalid input detected. Please remove any script tags or unsafe characters.");
                    ViewBag.MedBaseList = new SelectList(await _repo.GetBaseListAsync(), "BaseId", "BaseName", model.BaseId);
                    return PartialView("_CreateEdit", model);
                }

                var baseList = await _repo.GetBaseListAsync();
                ViewBag.MedBaseList = new SelectList(baseList, "BaseId", "BaseName", model.BaseId);

                // Check for duplicate med item details combination (excluding current record)
                if (await _repo.IsMedItemDetailsExistsAsync(model.MedItemName, model.BaseId, model.CompanyName, model.MedItemId))
                {
                    ModelState.AddModelError("", "A medical item with this combination of name, base, and company already exists. Please choose different values.");
                    ModelState.AddModelError("MedItemName", "This combination already exists.");
                    ModelState.AddModelError("BaseId", "This combination already exists.");
                    ModelState.AddModelError("CompanyName", "This combination already exists.");
                }

                if (!ModelState.IsValid)
                    return PartialView("_CreateEdit", model);

                // Rate limiting logic
                var userId = User.FindFirst("user_id")?.Value ?? "anonymous";
                var cacheKey = $"rate_limit_edit_medmaster_{userId}";

                var timestamps = _cache.GetOrCreate(cacheKey, entry =>
                {
                    entry.SlidingExpiration = TimeSpan.FromMinutes(5);
                    return new List<DateTime>();
                });

                timestamps.RemoveAll(t => t < DateTime.UtcNow.AddMinutes(-5));

                if (timestamps.Count >= 10)
                {
                    ViewBag.Error = "⚠ You can only edit 10 medicine records every 5 minutes. Please wait and try again.";
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
                if (ex.InnerException?.Message.Contains("IX_MedMaster_MedItemNameBaseIdCompanyName_Unique") == true)
                {
                    ModelState.AddModelError("", "A medical item with this combination already exists. Please choose different values.");
                    ModelState.AddModelError("MedItemName", "This combination already exists.");
                    ModelState.AddModelError("BaseId", "This combination already exists.");
                    ModelState.AddModelError("CompanyName", "This combination already exists.");
                }
                else
                {
                    ViewBag.Error = "An error occurred while updating the medicine. Please try again.";
                }

                ViewBag.MedBaseList = new SelectList(await _repo.GetBaseListAsync(), "BaseId", "BaseName", model.BaseId);
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
            catch (Exception ex)
            {
                return Json(new { success = false, message = "An error occurred while deleting the medicine." });
            }
        }

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

        // AJAX method for real-time validation
        [HttpPost]
        public async Task<IActionResult> CheckMedItemDetailsExists(string medItemName, int? baseId, string? companyName, int? medItemId = null)
        {
            if (string.IsNullOrWhiteSpace(medItemName))
                return Json(new { exists = false });

            // Sanitize input before checking
            medItemName = SanitizeString(medItemName);
            companyName = SanitizeString(companyName);

            var exists = await _repo.IsMedItemDetailsExistsAsync(medItemName, baseId, companyName, medItemId);
            return Json(new { exists = exists });
        }

        #region Private Methods for Input Sanitization and Validation

        private MedMaster SanitizeInput(MedMaster model)
        {
            if (model == null) return model;

            model.MedItemName = SanitizeString(model.MedItemName);
            model.CompanyName = SanitizeString(model.CompanyName);

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

        private bool IsInputSecure(MedMaster model)
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

            var inputsToCheck = new[] { model.MedItemName, model.CompanyName };

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