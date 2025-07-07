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
    public class MedDiseaseController : Controller
    {
        private readonly IMedDiseaseRepository _repo;
        private readonly IMemoryCache _cache;

        public MedDiseaseController(IMedDiseaseRepository repo, IMemoryCache cache)
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

        public IActionResult Create() => PartialView("_CreateEdit", new MedDisease());

        [HttpPost]
        public async Task<IActionResult> Create(MedDisease model)
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

                // Check for duplicate disease name
                if (await _repo.IsDiseaseNameExistsAsync(model.DiseaseName))
                {
                    ModelState.AddModelError("DiseaseName", "A disease with this name already exists. Please choose a different name.");
                }

                if (!ModelState.IsValid)
                    return PartialView("_CreateEdit", model);

                // Rate limiting logic
                var userId = User.FindFirst("user_id")?.Value ?? "anonymous";
                var cacheKey = $"rate_limit_create_meddisease_{userId}";

                var timestamps = _cache.GetOrCreate(cacheKey, entry =>
                {
                    entry.SlidingExpiration = TimeSpan.FromMinutes(5);
                    return new List<DateTime>();
                });

                timestamps.RemoveAll(t => t < DateTime.UtcNow.AddMinutes(-5));

                if (timestamps.Count >= 5)
                {
                    ViewBag.Error = "⚠ You can only create 5 MedDisease records every 5 minutes. Please wait and try again.";
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
                if (ex.InnerException?.Message.Contains("IX_MedDisease_DiseaseName_Unique") == true)
                {
                    ModelState.AddModelError("DiseaseName", "A disease with this name already exists. Please choose a different name.");
                }
                else
                {
                    ViewBag.Error = "An error occurred while creating the disease record. Please try again.";
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
        public async Task<IActionResult> Edit(MedDisease model)
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

                // Check for duplicate disease name (excluding current record)
                if (await _repo.IsDiseaseNameExistsAsync(model.DiseaseName, model.DiseaseId))
                {
                    ModelState.AddModelError("DiseaseName", "A disease with this name already exists. Please choose a different name.");
                }

                if (!ModelState.IsValid)
                    return PartialView("_CreateEdit", model);

                // Rate limiting logic
                var userId = User.FindFirst("user_id")?.Value ?? "anonymous";
                var cacheKey = $"rate_limit_edit_meddisease_{userId}";

                var timestamps = _cache.GetOrCreate(cacheKey, entry =>
                {
                    entry.SlidingExpiration = TimeSpan.FromMinutes(5);
                    return new List<DateTime>();
                });

                timestamps.RemoveAll(t => t < DateTime.UtcNow.AddMinutes(-5));

                if (timestamps.Count >= 10)
                {
                    ViewBag.Error = "⚠ You can only edit 10 MedDisease records every 5 minutes. Please wait and try again.";
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
                if (ex.InnerException?.Message.Contains("IX_MedDisease_DiseaseName_Unique") == true)
                {
                    ModelState.AddModelError("DiseaseName", "A disease with this name already exists. Please choose a different name.");
                }
                else
                {
                    ViewBag.Error = "An error occurred while updating the disease record. Please try again.";
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
                return Json(new { success = false, message = "An error occurred while deleting the disease record." });
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
        public async Task<IActionResult> CheckDiseaseNameExists(string diseaseName, int? diseaseId = null)
        {
            if (string.IsNullOrWhiteSpace(diseaseName))
                return Json(new { exists = false });

            // Sanitize input before checking
            diseaseName = SanitizeString(diseaseName);

            var exists = await _repo.IsDiseaseNameExistsAsync(diseaseName, diseaseId);
            return Json(new { exists = exists });
        }

        #region Private Methods for Input Sanitization and Validation

        private MedDisease SanitizeInput(MedDisease model)
        {
            if (model == null) return model;

            model.DiseaseName = SanitizeString(model.DiseaseName);
            model.DiseaseDesc = SanitizeString(model.DiseaseDesc);

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

        private bool IsInputSecure(MedDisease model)
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

            var inputsToCheck = new[] { model.DiseaseName, model.DiseaseDesc };

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