using EMS.WebApp.Data;
using EMS.WebApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Web;

namespace EMS.WebApp.Controllers
{
    [Authorize]
    public class MedExamCategoryController : Controller
    {
        private readonly IMedExamCategoryRepository _repo;
        private readonly IMemoryCache _cache;

        public MedExamCategoryController(IMedExamCategoryRepository repo, IMemoryCache cache)
        {
            _repo = repo;
            _cache = cache;
        }

        public IActionResult Index() => View();

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

        public IActionResult Create() => PartialView("_CreateEdit", new MedExamCategory());

        [HttpPost]
        public async Task<IActionResult> Create(MedExamCategory model)
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

                // Check for duplicate category details combination
                if (await _repo.IsCategoryDetailsExistsAsync(model.CatName, model.YearsFreq, model.AnnuallyRule, model.MonthsSched))
                {
                    ModelState.AddModelError("", "A category with this combination of name, years frequency, annually rule, and months schedule already exists. Please choose different values.");
                    // Add specific field errors for better UX
                    ModelState.AddModelError("CatName", "This combination already exists.");
                    ModelState.AddModelError("YearsFreq", "This combination already exists.");
                    ModelState.AddModelError("AnnuallyRule", "This combination already exists.");
                    ModelState.AddModelError("MonthsSched", "This combination already exists.");
                }

                if (!ModelState.IsValid)
                    return PartialView("_CreateEdit", model);

                // Rate limiting logic
                var userId = User.FindFirst("user_id")?.Value ?? "anonymous";
                var cacheKey = $"rate_limit_create_medexamcategory_{userId}";

                var timestamps = _cache.GetOrCreate(cacheKey, entry =>
                {
                    entry.SlidingExpiration = TimeSpan.FromMinutes(5);
                    return new List<DateTime>();
                });

                timestamps.RemoveAll(t => t < DateTime.UtcNow.AddMinutes(-5));

                if (timestamps.Count >= 5)
                {
                    ViewBag.Error = "⚠ You can only create 5 MedExamCategory records every 5 minutes. Please wait and try again.";
                    return PartialView("_CreateEdit", model);
                }

                timestamps.Add(DateTime.UtcNow);
                _cache.Set(cacheKey, timestamps, TimeSpan.FromMinutes(5));

                await _repo.AddAsync(model);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                // Handle database constraint violation
                if (ex.InnerException?.Message.Contains("IX_MedExamCategory_CatNameYearsFreqAnnuallyRuleMonthsSched_Unique") == true)
                {
                    ModelState.AddModelError("", "A category with this combination already exists. Please choose different values.");
                    ModelState.AddModelError("CatName", "This combination already exists.");
                    ModelState.AddModelError("YearsFreq", "This combination already exists.");
                    ModelState.AddModelError("AnnuallyRule", "This combination already exists.");
                    ModelState.AddModelError("MonthsSched", "This combination already exists.");
                }
                else
                {
                    ViewBag.Error = "An error occurred while creating the exam category. Please try again.";
                }

                return PartialView("_CreateEdit", model);
            }
        }

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

        [HttpPost]
        public async Task<IActionResult> Edit(MedExamCategory model)
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

                // Check for duplicate category details combination (excluding current record)
                if (await _repo.IsCategoryDetailsExistsAsync(model.CatName, model.YearsFreq, model.AnnuallyRule, model.MonthsSched, model.CatId))
                {
                    ModelState.AddModelError("", "A category with this combination of name, years frequency, annually rule, and months schedule already exists. Please choose different values.");
                    ModelState.AddModelError("CatName", "This combination already exists.");
                    ModelState.AddModelError("YearsFreq", "This combination already exists.");
                    ModelState.AddModelError("AnnuallyRule", "This combination already exists.");
                    ModelState.AddModelError("MonthsSched", "This combination already exists.");
                }

                if (!ModelState.IsValid)
                    return PartialView("_CreateEdit", model);

                // Rate limiting logic
                var userId = User.FindFirst("user_id")?.Value ?? "anonymous";
                var cacheKey = $"rate_limit_edit_medexamcategory_{userId}";

                var timestamps = _cache.GetOrCreate(cacheKey, entry =>
                {
                    entry.SlidingExpiration = TimeSpan.FromMinutes(5);
                    return new List<DateTime>();
                });

                timestamps.RemoveAll(t => t < DateTime.UtcNow.AddMinutes(-5));

                if (timestamps.Count >= 10)
                {
                    ViewBag.Error = "⚠ You can only edit 10 MedExamCategory records every 5 minutes. Please wait and try again.";
                    return PartialView("_CreateEdit", model);
                }

                timestamps.Add(DateTime.UtcNow);
                _cache.Set(cacheKey, timestamps, TimeSpan.FromMinutes(5));

                await _repo.UpdateAsync(model);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                // Handle database constraint violation
                if (ex.InnerException?.Message.Contains("IX_MedExamCategory_CatNameYearsFreqAnnuallyRuleMonthsSched_Unique") == true)
                {
                    ModelState.AddModelError("", "A category with this combination already exists. Please choose different values.");
                    ModelState.AddModelError("CatName", "This combination already exists.");
                    ModelState.AddModelError("YearsFreq", "This combination already exists.");
                    ModelState.AddModelError("AnnuallyRule", "This combination already exists.");
                    ModelState.AddModelError("MonthsSched", "This combination already exists.");
                }
                else
                {
                    ViewBag.Error = "An error occurred while updating the exam category. Please try again.";
                }

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
                return Json(new { success = false, message = "An error occurred while deleting the exam category." });
            }
        }

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
        public async Task<IActionResult> CheckCategoryDetailsExists(string catName, byte yearsFreq, string annuallyRule, string monthsSched, int? catId = null)
        {
            if (string.IsNullOrWhiteSpace(catName) || string.IsNullOrWhiteSpace(annuallyRule) || string.IsNullOrWhiteSpace(monthsSched))
                return Json(new { exists = false });

            // Sanitize inputs before checking
            catName = SanitizeString(catName);
            annuallyRule = SanitizeString(annuallyRule);
            monthsSched = SanitizeString(monthsSched);

            var exists = await _repo.IsCategoryDetailsExistsAsync(catName, yearsFreq, annuallyRule, monthsSched, catId);
            return Json(new { exists = exists });
        }

        #region Private Methods for Input Sanitization and Validation

        private MedExamCategory SanitizeInput(MedExamCategory model)
        {
            if (model == null) return model;

            model.CatName = SanitizeString(model.CatName);
            model.AnnuallyRule = SanitizeString(model.AnnuallyRule);
            model.MonthsSched = SanitizeString(model.MonthsSched);
            model.Remarks = SanitizeString(model.Remarks);

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

        private bool IsInputSecure(MedExamCategory model)
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

            var inputsToCheck = new[] { model.CatName, model.AnnuallyRule, model.MonthsSched, model.Remarks };

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