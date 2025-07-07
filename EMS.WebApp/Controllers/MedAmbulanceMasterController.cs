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
    public class MedAmbulanceMasterController : Controller
    {
        private readonly IMedAmbulanceMasterRepository _repo;
        private readonly IMemoryCache _cache;

        public MedAmbulanceMasterController(IMedAmbulanceMasterRepository repo, IMemoryCache cache)
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

        public IActionResult Create() => PartialView("_CreateEdit", new med_ambulance_master());

        [HttpPost]
        public async Task<IActionResult> Create(med_ambulance_master model)
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

                // Check for duplicate vehicle number
                if (await _repo.IsVehicleNumberExistsAsync(model.vehicle_no))
                {
                    ModelState.AddModelError("", "A vehicle with this number already exists. Please choose a different vehicle number.");
                    ModelState.AddModelError("vehicle_no", "This vehicle number already exists.");
                }

                if (!ModelState.IsValid)
                    return PartialView("_CreateEdit", model);

                // Rate limiting logic
                var userId = User.FindFirst("user_id")?.Value ?? "anonymous";
                var cacheKey = $"rate_limit_create_ambulance_{userId}";

                var timestamps = _cache.GetOrCreate(cacheKey, entry =>
                {
                    entry.SlidingExpiration = TimeSpan.FromMinutes(5);
                    return new List<DateTime>();
                });

                timestamps.RemoveAll(t => t < DateTime.UtcNow.AddMinutes(-5));

                if (timestamps.Count >= 5)
                {
                    ViewBag.Error = "⚠ You can only create 5 ambulance records every 5 minutes. Please wait and try again.";
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
                if (ex.InnerException?.Message.Contains("IX_MedAmbulanceMaster_VehicleNo_Unique") == true)
                {
                    ModelState.AddModelError("", "A vehicle with this number already exists. Please choose a different vehicle number.");
                    ModelState.AddModelError("vehicle_no", "This vehicle number already exists.");
                }
                else
                {
                    ViewBag.Error = "An error occurred while creating the ambulance record. Please try again.";
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
        public async Task<IActionResult> Edit(med_ambulance_master model)
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

                // Check for duplicate vehicle number (excluding current record)
                if (await _repo.IsVehicleNumberExistsAsync(model.vehicle_no, model.amb_id))
                {
                    ModelState.AddModelError("", "A vehicle with this number already exists. Please choose a different vehicle number.");
                    ModelState.AddModelError("vehicle_no", "This vehicle number already exists.");
                }

                if (!ModelState.IsValid)
                    return PartialView("_CreateEdit", model);

                // Rate limiting logic
                var userId = User.FindFirst("user_id")?.Value ?? "anonymous";
                var cacheKey = $"rate_limit_edit_ambulance_{userId}";

                var timestamps = _cache.GetOrCreate(cacheKey, entry =>
                {
                    entry.SlidingExpiration = TimeSpan.FromMinutes(5);
                    return new List<DateTime>();
                });

                timestamps.RemoveAll(t => t < DateTime.UtcNow.AddMinutes(-5));

                if (timestamps.Count >= 10)
                {
                    ViewBag.Error = "⚠ You can only edit 10 ambulance records every 5 minutes. Please wait and try again.";
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
                if (ex.InnerException?.Message.Contains("IX_MedAmbulanceMaster_VehicleNo_Unique") == true)
                {
                    ModelState.AddModelError("", "A vehicle with this number already exists. Please choose a different vehicle number.");
                    ModelState.AddModelError("vehicle_no", "This vehicle number already exists.");
                }
                else
                {
                    ViewBag.Error = "An error occurred while updating the ambulance record. Please try again.";
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
                return Json(new { success = false, message = "An error occurred while deleting the ambulance record." });
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
        public async Task<IActionResult> CheckVehicleNumberExists(string vehicleNo, int? ambId = null)
        {
            if (string.IsNullOrWhiteSpace(vehicleNo))
                return Json(new { exists = false });

            // Sanitize input before checking
            vehicleNo = SanitizeString(vehicleNo);

            var exists = await _repo.IsVehicleNumberExistsAsync(vehicleNo, ambId);
            return Json(new { exists = exists });
        }

        #region Private Methods for Input Sanitization and Validation

        private med_ambulance_master SanitizeInput(med_ambulance_master model)
        {
            if (model == null) return model;

            model.vehicle_no = SanitizeString(model.vehicle_no);
            model.provider = SanitizeString(model.provider);
            model.vehicle_type = SanitizeString(model.vehicle_type);

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

        private bool IsInputSecure(med_ambulance_master model)
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

            var inputsToCheck = new[] { model.vehicle_no, model.provider, model.vehicle_type };

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