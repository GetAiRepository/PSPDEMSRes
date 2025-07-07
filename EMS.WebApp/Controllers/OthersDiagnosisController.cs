using EMS.WebApp.Data;
using EMS.WebApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EMS.WebApp.Controllers
{
    public class OthersDiagnosisController : Controller
    {
        private readonly IOthersDiagnosisRepository _repository;
        private readonly IMedicalDataMaskingService _maskingService;
        private readonly ILogger<OthersDiagnosisController> _logger;

        public OthersDiagnosisController(
            IOthersDiagnosisRepository repository,
            IMedicalDataMaskingService maskingService,
            ILogger<OthersDiagnosisController> logger)
        {
            _repository = repository;
            _maskingService = maskingService;
            _logger = logger;
        }

        // GET: OthersDiagnosis
        public async Task<IActionResult> Index()
        {
            try
            {
                var diagnoses = await _repository.GetAllDiagnosesAsync();

                // Check user role for pending approval access and masking
                var userRole = await GetUserRoleAsync();
                ViewBag.UserRole = userRole;
                ViewBag.IsDoctor = userRole?.ToLower() == "doctor";
                ViewBag.ShouldMaskData = _maskingService.ShouldMaskData(userRole);

                return View(diagnoses);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading others diagnosis list");
                TempData["Error"] = "Error loading diagnosis records.";
                return View(new List<OthersDiagnosisListViewModel>());
            }
        }

        // GET: OthersDiagnosis/Add
        public async Task<IActionResult> Add()
        {
            try
            {
                // Check user role and apply masking
                var userRole = await GetUserRoleAsync();
                ViewBag.ShouldMaskData = _maskingService.ShouldMaskData(userRole);
                ViewBag.UserRole = userRole;

                var model = new OthersDiagnosisViewModel
                {
                    TreatmentId = string.Empty,
                    DiagnosedBy = User.Identity?.Name ?? "SYSTEM ADMIN",
                    VisitType = "Regular Visitor", // Default visit type
                    AvailableDiseases = await _repository.GetDiseasesAsync(),
                    // ======= UPDATED: Use compounder medicines with stock =======
                    AvailableMedicines = await _repository.GetCompounderMedicinesAsync()
                };

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading add diagnosis form");
                TempData["Error"] = "Error loading form.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(OthersDiagnosisViewModel model)
        {
            try
            {
                // Check permissions for saving
                var userRole = await GetUserRoleAsync();
                if (_maskingService.ShouldMaskData(userRole))
                {
                    TempData["Error"] = "You don't have permission to save diagnoses.";
                    return RedirectToAction(nameof(Index));
                }

                if (!ModelState.IsValid)
                {
                    model.AvailableDiseases = await _repository.GetDiseasesAsync();
                    model.AvailableMedicines = await _repository.GetMedicinesAsync();
                    return View(model);
                }

                var createdBy = User.Identity?.Name ?? "SYSTEM ADMIN";
                var (success, errorMessage) = await _repository.SaveDiagnosisAsync(model, createdBy);

                if (success)
                {
                    TempData["Success"] = errorMessage; // Contains appropriate message based on approval status
                    return RedirectToAction(nameof(Index));
                }
                else
                {
                    ModelState.AddModelError("", errorMessage);
                    model.AvailableDiseases = await _repository.GetDiseasesAsync();
                    model.AvailableMedicines = await _repository.GetMedicinesAsync();
                    return View(model);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving diagnosis");
                ModelState.AddModelError("", $"An error occurred while saving: {ex.Message}");
                model.AvailableDiseases = await _repository.GetDiseasesAsync();
                model.AvailableMedicines = await _repository.GetMedicinesAsync();
                return View(model);
            }
        }

        // POST: OthersDiagnosis/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                // Check permissions for deletion
                var userRole = await GetUserRoleAsync();
                if (_maskingService.ShouldMaskData(userRole))
                {
                    TempData["Error"] = "You don't have permission to delete diagnoses.";
                    return RedirectToAction(nameof(Index));
                }

                var success = await _repository.DeleteDiagnosisAsync(id);
                if (success)
                {
                    TempData["Success"] = "Diagnosis record deleted successfully.";
                }
                else
                {
                    TempData["Error"] = "Failed to delete diagnosis record.";
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting diagnosis with ID: {id}");
                TempData["Error"] = "Error deleting diagnosis record.";
                return RedirectToAction(nameof(Index));
            }
        }

        // AJAX Methods

        [HttpGet]
        public async Task<IActionResult> SearchPatient(string treatmentId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(treatmentId))
                {
                    return Json(new { success = false, message = "Treatment ID is required." });
                }

                var patientData = await _repository.GetPatientForEditAsync(treatmentId);
                if (patientData == null)
                {
                    return Json(new { success = false, message = "Patient not found." });
                }

                return Json(new { success = true, patient = patientData });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error searching for patient with Treatment ID: {treatmentId}");
                return Json(new { success = false, message = "Error searching for patient." });
            }
        }

        // ======= UPDATED: Enhanced prescription data with batch information and stock =======
        [HttpGet]
        public async Task<IActionResult> GetPrescriptionData()
        {
            try
            {
                _logger.LogInformation("🔍 Getting prescription data with batch information for Others Diagnosis");

                var diseases = await _repository.GetDiseasesAsync();
                var medicineStocks = await _repository.GetMedicinesFromCompounderIndentAsync();

                _logger.LogInformation($"✅ Found {diseases.Count} diseases and {medicineStocks.Count} medicine stocks");

                // Convert to dropdown format with batch and stock info
                var medicineDropdownItems = medicineStocks.Select(m => new
                {
                    indentItemId = m.IndentItemId,
                    medItemId = m.MedItemId,
                    text = m.DisplayName, // "Medicine Name - Batch No"
                    stockInfo = $"Stock: {m.AvailableStock}",
                    expiryInfo = m.ExpiryDateFormatted,
                    daysToExpiry = m.DaysToExpiry,
                    availableStock = m.AvailableStock,
                    batchNo = m.BatchNo,
                    expiryDate = m.ExpiryDate?.ToString("yyyy-MM-dd"),
                    companyName = m.CompanyName,
                    // Add styling classes based on expiry
                    expiryClass = m.DaysToExpiry switch
                    {
                        < 0 => "text-danger", // Expired
                        <= 7 => "text-warning", // Expires within a week
                        <= 30 => "text-info", // Expires within a month
                        _ => "text-success" // Good
                    },
                    expiryLabel = m.DaysToExpiry switch
                    {
                        < 0 => "EXPIRED",
                        <= 7 => $"Expires in {m.DaysToExpiry} days",
                        <= 30 => $"Expires in {m.DaysToExpiry} days",
                        _ => $"Expires: {m.ExpiryDateFormatted}"
                    },
                    isNearExpiry = m.DaysToExpiry <= 30 && m.DaysToExpiry >= 0,
                    isExpired = m.DaysToExpiry < 0
                }).ToList();

                return Json(new
                {
                    diseases = diseases.Select(d => new { value = d.DiseaseId, text = d.DiseaseName }),
                    medicines = medicineDropdownItems
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error loading prescription data");
                return Json(new
                {
                    diseases = new List<object>(),
                    medicines = new List<object>()
                });
            }
        }

        // ======= NEW: Check available stock for a medicine batch =======
        [HttpGet]
        public async Task<IActionResult> CheckMedicineStock(int indentItemId)
        {
            try
            {
                var availableStock = await _repository.GetAvailableStockAsync(indentItemId);

                return Json(new
                {
                    success = true,
                    availableStock = availableStock
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking stock for indent item {indentItemId}");
                return Json(new { success = false, availableStock = 0 });
            }
        }

        // ======= NEW: Validate prescription quantities against available stock =======
        [HttpPost]
        public async Task<IActionResult> ValidatePrescriptionStock(List<OthersPrescriptionMedicine> medicines)
        {
            try
            {
                var validationResults = new List<OthersStockValidationResult>();

                if (medicines?.Any() == true)
                {
                    foreach (var medicine in medicines)
                    {
                        if (medicine.IndentItemId.HasValue && medicine.IndentItemId.Value > 0)
                        {
                            var availableStock = await _repository.GetAvailableStockAsync(medicine.IndentItemId.Value);

                            var validationResult = new OthersStockValidationResult
                            {
                                IsValid = medicine.Quantity <= availableStock,
                                AvailableStock = availableStock,
                                RequestedQuantity = medicine.Quantity,
                                MedicineName = medicine.MedicineName,
                                BatchNo = medicine.BatchNo ?? "N/A"
                            };

                            if (!validationResult.IsValid)
                            {
                                validationResult.ErrorMessage = $"Insufficient stock for {medicine.MedicineName} (Batch: {medicine.BatchNo}). Available: {availableStock}, Requested: {medicine.Quantity}";
                            }

                            validationResults.Add(validationResult);
                        }
                    }
                }

                var allValid = validationResults.All(r => r.IsValid);
                var errorMessages = validationResults.Where(r => !r.IsValid).Select(r => r.ErrorMessage).ToList();

                return Json(new
                {
                    success = allValid,
                    isValid = allValid,
                    errors = errorMessages,
                    validationResults = validationResults
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating prescription stock");
                return Json(new { success = false, isValid = false, errors = new[] { "Error validating stock availability" } });
            }
        }

        [HttpPost]
        public async Task<IActionResult> SaveDiagnosisAjax([FromBody] OthersDiagnosisViewModel model)
        {
            try
            {
                _logger.LogInformation("=== SaveDiagnosisAjax Started ===");
                _logger.LogInformation("TreatmentId: {TreatmentId}", model.TreatmentId);
                _logger.LogInformation("PatientName: {PatientName}", model.PatientName);
                _logger.LogInformation("Age: {Age}", model.Age);
                _logger.LogInformation("DiagnosedBy: {DiagnosedBy}", model.DiagnosedBy);
                _logger.LogInformation("VisitType: {VisitType}", model.VisitType);
                _logger.LogInformation("Diseases Count: {DiseaseCount}", model.SelectedDiseaseIds?.Count ?? 0);
                _logger.LogInformation("Medicines Count: {MedicineCount}", model.PrescriptionMedicines?.Count ?? 0);

                if (model.PrescriptionMedicines?.Any() == true)
                {
                    _logger.LogInformation("=== Medicine Details ===");
                    foreach (var med in model.PrescriptionMedicines)
                    {
                        _logger.LogInformation("Medicine: MedItemId={MedItemId}, Quantity={Quantity}, Dose={Dose}, Name={Name}, IndentItemId={IndentItemId}, BatchNo={BatchNo}",
                            med.MedItemId, med.Quantity, med.Dose, med.MedicineName, med.IndentItemId, med.BatchNo);
                    }
                }

                // Check permissions for saving
                var userRole = await GetUserRoleAsync();
                if (_maskingService.ShouldMaskData(userRole))
                {
                    return Json(new { success = false, message = "You don't have permission to save diagnoses." });
                }

                // Validation
                if (string.IsNullOrWhiteSpace(model.TreatmentId))
                {
                    return Json(new { success = false, message = "Treatment ID is required" });
                }

                if (string.IsNullOrWhiteSpace(model.PatientName))
                {
                    return Json(new { success = false, message = "Patient name is required" });
                }

                if (model.Age <= 0 || model.Age > 120)
                {
                    return Json(new { success = false, message = "Valid age (1-120) is required" });
                }

                if (string.IsNullOrWhiteSpace(model.DiagnosedBy))
                {
                    model.DiagnosedBy = User.Identity?.Name ?? "SYSTEM ADMIN";
                }

                // Default visit type if not provided
                if (string.IsNullOrWhiteSpace(model.VisitType))
                {
                    model.VisitType = "Regular Visitor";
                }

                // ======= NEW: Validate stock before saving =======
                if (model.PrescriptionMedicines?.Any() == true)
                {
                    foreach (var medicine in model.PrescriptionMedicines)
                    {
                        if (medicine.IndentItemId.HasValue && medicine.IndentItemId.Value > 0)
                        {
                            var availableStock = await _repository.GetAvailableStockAsync(medicine.IndentItemId.Value);
                            if (medicine.Quantity > availableStock)
                            {
                                return Json(new
                                {
                                    success = false,
                                    message = $"Insufficient stock for {medicine.MedicineName} (Batch: {medicine.BatchNo}). Available: {availableStock}, Requested: {medicine.Quantity}"
                                });
                            }
                        }
                    }
                }

                var createdBy = User.Identity?.Name ?? "SYSTEM ADMIN";
                _logger.LogInformation("Calling SaveDiagnosisAsync with createdBy: {CreatedBy}", createdBy);

                var (success, errorMessage) = await _repository.SaveDiagnosisAsync(model, createdBy);

                _logger.LogInformation("SaveDiagnosisAsync result: Success={Success}, Error={Error}", success, errorMessage);

                if (success)
                {
                    return Json(new { success = true, message = errorMessage });
                }
                else
                {
                    return Json(new { success = false, message = errorMessage });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SaveDiagnosisAjax. Model: {@Model}", model);
                return Json(new { success = false, message = $"An error occurred while saving: {ex.Message}" });
            }
        }

        // GET: OthersDiagnosis/View/5
        public async Task<IActionResult> View(int id)
        {
            try
            {
                _logger.LogInformation("Loading diagnosis view for ID: {DiagnosisId}", id);

                var diagnosis = await _repository.GetDiagnosisDetailsAsync(id);
                if (diagnosis == null)
                {
                    TempData["Error"] = "Diagnosis record not found.";
                    return RedirectToAction(nameof(Index));
                }

                // Get user role and apply masking
                var userRole = await GetUserRoleAsync();
                ViewBag.UserRole = userRole;
                ViewBag.ShouldMaskData = _maskingService.ShouldMaskData(userRole);

                // Apply masking to sensitive data if user doesn't have appropriate role
                _maskingService.MaskObject(diagnosis, userRole);

                _logger.LogInformation("Loaded diagnosis with {MedicineCount} medicines and {DiseaseCount} diseases",
                    diagnosis.Medicines?.Count ?? 0, diagnosis.Diseases?.Count ?? 0);

                return View(diagnosis);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading diagnosis for ID: {DiagnosisId}", id);
                TempData["Error"] = "Error loading diagnosis details.";
                return RedirectToAction(nameof(Index));
            }
        }

        // ======= APPROVAL ENDPOINTS =======

        [HttpGet]
        public async Task<IActionResult> GetPendingApprovalCount()
        {
            try
            {
                // Check if user has doctor role
                var userRole = await GetUserRoleAsync();
                if (userRole?.ToLower() != "doctor")
                {
                    return Json(new { success = false, message = "Access denied. Only doctors can view pending approvals." });
                }

                var count = await _repository.GetPendingApprovalCountAsync();
                return Json(new { success = true, count = count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting pending approval count");
                return Json(new { success = false, count = 0 });
            }
        }

        [HttpGet]
        public async Task<IActionResult> PendingApprovals()
        {
            try
            {
                // Check if user has doctor role
                var userRole = await GetUserRoleAsync();
                if (userRole?.ToLower() != "doctor")
                {
                    return Json(new { success = false, message = "Access denied. Only doctors can view pending approvals." });
                }

                var pendingApprovals = await _repository.GetPendingApprovalsAsync();

                // Apply masking for non-doctors (though this shouldn't happen as we check above)
                foreach (var approval in pendingApprovals)
                {
                    _maskingService.MaskObject(approval, userRole);
                }

                ViewBag.UserRole = userRole;

                return PartialView("_PendingApprovalsModal", pendingApprovals);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading pending approvals");
                return Json(new { success = false, message = "Error loading pending approvals." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ApproveDiagnosis(int diagnosisId)
        {
            try
            {
                // Check if user has doctor role
                var userRole = await GetUserRoleAsync();
                if (userRole?.ToLower() != "doctor")
                {
                    return Json(new { success = false, message = "Access denied. Only doctors can approve diagnoses." });
                }

                var approvedBy = User.FindFirst("user_id")?.Value ?? User.Identity?.Name ?? "unknown";
                var success = await _repository.ApproveDiagnosisAsync(diagnosisId, approvedBy);

                if (success)
                {
                    return Json(new { success = true, message = "Diagnosis approved successfully." });
                }
                else
                {
                    return Json(new { success = false, message = "Diagnosis not found or already processed." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error approving diagnosis {diagnosisId}");
                return Json(new { success = false, message = "Error approving diagnosis." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> RejectDiagnosis(int diagnosisId, string rejectionReason)
        {
            try
            {
                // Check if user has doctor role
                var userRole = await GetUserRoleAsync();
                if (userRole?.ToLower() != "doctor")
                {
                    return Json(new { success = false, message = "Access denied. Only doctors can reject diagnoses." });
                }

                if (string.IsNullOrWhiteSpace(rejectionReason) || rejectionReason.Length < 10)
                {
                    return Json(new { success = false, message = "Please provide a detailed rejection reason (minimum 10 characters)." });
                }

                var rejectedBy = User.FindFirst("user_id")?.Value ?? User.Identity?.Name ?? "unknown";
                var success = await _repository.RejectDiagnosisAsync(diagnosisId, rejectionReason, rejectedBy);

                if (success)
                {
                    return Json(new { success = true, message = "Diagnosis rejected successfully." });
                }
                else
                {
                    return Json(new { success = false, message = "Diagnosis not found or already processed." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error rejecting diagnosis {diagnosisId}");
                return Json(new { success = false, message = "Error rejecting diagnosis." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ApproveAllDiagnoses(List<int> diagnosisIds)
        {
            try
            {
                // Check if user has doctor role
                var userRole = await GetUserRoleAsync();
                if (userRole?.ToLower() != "doctor")
                {
                    return Json(new { success = false, message = "Access denied. Only doctors can approve diagnoses." });
                }

                if (diagnosisIds == null || !diagnosisIds.Any())
                {
                    return Json(new { success = false, message = "No diagnoses selected for approval." });
                }

                var approvedBy = User.FindFirst("user_id")?.Value ?? User.Identity?.Name ?? "unknown";
                var approvedCount = await _repository.ApproveAllDiagnosesAsync(diagnosisIds, approvedBy);

                if (approvedCount > 0)
                {
                    return Json(new { success = true, message = $"{approvedCount} diagnosis(es) approved successfully." });
                }
                else
                {
                    return Json(new { success = false, message = "No diagnoses were approved. They may have been already processed." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving multiple diagnoses");
                return Json(new { success = false, message = "Error approving diagnoses." });
            }
        }

        // ======= DEBUG/TEST METHODS =======

        // Test endpoint to check medicine data (for debugging)
        [HttpGet]
        public async Task<IActionResult> TestMedicineData(int diagnosisId)
        {
            try
            {
                var diagnosis = await _repository.GetDiagnosisDetailsAsync(diagnosisId);

                dynamic medicineData = diagnosis?.Medicines?.Select(m => new {
                    MedItemId = m.MedItemId,
                    MedicineName = m.MedicineName,
                    Quantity = m.Quantity,
                    Dose = m.Dose,
                    Instructions = m.Instructions
                }) ?? Enumerable.Empty<object>();

                return Json(new
                {
                    success = true,
                    diagnosisFound = diagnosis != null,
                    medicineCount = diagnosis?.Medicines?.Count ?? 0,
                    medicines = medicineData
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in TestMedicineData for diagnosis ID: {DiagnosisId}", diagnosisId);
                return Json(new
                {
                    success = false,
                    error = ex.Message
                });
            }
        }

        [HttpGet]
        public async Task<IActionResult> DebugMedicineFlow(int? diagnosisId = null)
        {
            try
            {
                var debugInfo = new
                {
                    timestamp = DateTime.Now,
                    availableMedicines = await _repository.GetMedicinesAsync(),
                    totalMedicineCount = await _repository.GetMedicinesAsync().ContinueWith(t => t.Result.Count),
                    latestDiagnosis = diagnosisId ?? (await _repository.GetAllDiagnosesAsync()).FirstOrDefault()?.DiagnosisId,
                    medicineDataForLatest = diagnosisId.HasValue ?
                        await _repository.GetDiagnosisDetailsAsync(diagnosisId.Value) :
                        null
                };

                return Json(debugInfo);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message, stackTrace = ex.StackTrace });
            }
        }

        [HttpGet]
        public async Task<IActionResult> RawMedicineQuery(int diagnosisId)
        {
            try
            {
                var result = await _repository.GetRawMedicineDataAsync(diagnosisId);
                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        // Helper method to get user role
        private async Task<string?> GetUserRoleAsync()
        {
            try
            {
                var userName = User.Identity?.Name;
                if (string.IsNullOrEmpty(userName))
                    return null;

                using var scope = HttpContext.RequestServices.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var user = await dbContext.SysUsers
                    .Include(u => u.SysRole)
                    .FirstOrDefaultAsync(u => u.full_name == userName || u.email == userName || u.adid == userName);

                return user?.SysRole?.role_name;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user role");
                return null;
            }
        }
    }
}