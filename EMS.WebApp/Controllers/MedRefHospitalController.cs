using Microsoft.AspNetCore.Mvc;
using EMS.WebApp.Data;
using EMS.WebApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Web;

namespace EMS.WebApp.Controllers
{
    [Authorize]
    public class MedRefHospitalController : Controller
    {
        private readonly IMedRefHospitalRepository _repo;
        private readonly IMemoryCache _cache;

        public MedRefHospitalController(IMedRefHospitalRepository repo, IMemoryCache cache)
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

        public IActionResult Create() => PartialView("_CreateEdit", new MedRefHospital());

        [HttpPost]
        public async Task<IActionResult> Create(MedRefHospital model)
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

                // Check for duplicate hospital name and code combination
                if (await _repo.IsHospitalNameCodeExistsAsync(model.hosp_name, model.hosp_code))
                {
                    ModelState.AddModelError("", "A hospital with this name and code combination already exists. Please choose a different name or code.");
                    ModelState.AddModelError("hosp_name", "This combination already exists.");
                    ModelState.AddModelError("hosp_code", "This combination already exists.");
                }

                if (!ModelState.IsValid)
                    return PartialView("_CreateEdit", model);

                // Rate limiting logic
                var userId = User.FindFirst("user_id")?.Value ?? "anonymous";
                var cacheKey = $"rate_limit_create_medrefhospital_{userId}";

                var timestamps = _cache.GetOrCreate(cacheKey, entry =>
                {
                    entry.SlidingExpiration = TimeSpan.FromMinutes(5);
                    return new List<DateTime>();
                });

                timestamps.RemoveAll(t => t < DateTime.UtcNow.AddMinutes(-5));

                if (timestamps.Count >= 5)
                {
                    ViewBag.Error = "⚠ You can only create 5 MedRefHospital records every 5 minutes. Please wait and try again.";
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
                if (ex.InnerException?.Message.Contains("IX_MedRefHospital_HospNameCode_Unique") == true)
                {
                    ModelState.AddModelError("", "A hospital with this name and code combination already exists. Please choose a different name or code.");
                    ModelState.AddModelError("hosp_name", "This combination already exists.");
                    ModelState.AddModelError("hosp_code", "This combination already exists.");
                }
                else
                {
                    ViewBag.Error = "An error occurred while creating the hospital record. Please try again.";
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
        public async Task<IActionResult> Edit(MedRefHospital model)
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

                // Check for duplicate hospital name and code combination (excluding current record)
                if (await _repo.IsHospitalNameCodeExistsAsync(model.hosp_name, model.hosp_code, model.hosp_id))
                {
                    ModelState.AddModelError("", "A hospital with this name and code combination already exists. Please choose a different name or code.");
                    ModelState.AddModelError("hosp_name", "This combination already exists.");
                    ModelState.AddModelError("hosp_code", "This combination already exists.");
                }

                if (!ModelState.IsValid)
                    return PartialView("_CreateEdit", model);

                // Rate limiting logic
                var userId = User.FindFirst("user_id")?.Value ?? "anonymous";
                var cacheKey = $"rate_limit_edit_medrefhospital_{userId}";

                var timestamps = _cache.GetOrCreate(cacheKey, entry =>
                {
                    entry.SlidingExpiration = TimeSpan.FromMinutes(5);
                    return new List<DateTime>();
                });

                timestamps.RemoveAll(t => t < DateTime.UtcNow.AddMinutes(-5));

                if (timestamps.Count >= 10)
                {
                    ViewBag.Error = "⚠ You can only edit 10 MedRefHospital records every 5 minutes. Please wait and try again.";
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
                if (ex.InnerException?.Message.Contains("IX_MedRefHospital_HospNameCode_Unique") == true)
                {
                    ModelState.AddModelError("", "A hospital with this name and code combination already exists. Please choose a different name or code.");
                    ModelState.AddModelError("hosp_name", "This combination already exists.");
                    ModelState.AddModelError("hosp_code", "This combination already exists.");
                }
                else
                {
                    ViewBag.Error = "An error occurred while updating the hospital record. Please try again.";
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
                return Json(new { success = false, message = "An error occurred while deleting the hospital record." });
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
        public async Task<IActionResult> CheckHospitalNameCodeExists(string hospName, string hospCode, int? hospId = null)
        {
            if (string.IsNullOrWhiteSpace(hospName) || string.IsNullOrWhiteSpace(hospCode))
                return Json(new { exists = false });

            // Sanitize input before checking
            hospName = SanitizeString(hospName);
            hospCode = SanitizeString(hospCode);

            var exists = await _repo.IsHospitalNameCodeExistsAsync(hospName, hospCode, hospId);
            return Json(new { exists = exists });
        }

        #region Private Methods for Input Sanitization and Validation

        private MedRefHospital SanitizeInput(MedRefHospital model)
        {
            if (model == null) return model;

            model.hosp_name = SanitizeString(model.hosp_name);
            model.hosp_code = SanitizeString(model.hosp_code);
            model.speciality = SanitizeString(model.speciality);
            model.address = SanitizeString(model.address);
            model.description = SanitizeString(model.description);
            model.vendor_name = SanitizeString(model.vendor_name);
            model.vendor_code = SanitizeString(model.vendor_code);
            model.contact_person_name = SanitizeString(model.contact_person_name);
            model.contact_person_email_id = SanitizeString(model.contact_person_email_id);

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

        private bool IsInputSecure(MedRefHospital model)
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

            var inputsToCheck = new[] {
                model.hosp_name,
                model.hosp_code,
                model.speciality,
                model.address,
                model.description,
                model.vendor_name,
                model.vendor_code,
                model.contact_person_name,
                model.contact_person_email_id
            };

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