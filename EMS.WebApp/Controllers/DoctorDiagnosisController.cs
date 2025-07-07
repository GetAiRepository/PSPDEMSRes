using EMS.WebApp.Data;
using Microsoft.EntityFrameworkCore;
using EMS.WebApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EMS.WebApp.Controllers
{
    [Authorize]
    public class DoctorDiagnosisController : Controller
    {
        private readonly IDoctorDiagnosisRepository _doctorDiagnosisRepository;
        private readonly IHealthProfileRepository _healthProfileRepository;
        private readonly IMedicalDataMaskingService _maskingService;
        private readonly ILogger<DoctorDiagnosisController> _logger;

        public DoctorDiagnosisController(
            IDoctorDiagnosisRepository doctorDiagnosisRepository,
            IHealthProfileRepository healthProfileRepository,
            IMedicalDataMaskingService maskingService,
            ILogger<DoctorDiagnosisController> logger)
        {
            _doctorDiagnosisRepository = doctorDiagnosisRepository;
            _healthProfileRepository = healthProfileRepository;
            _maskingService = maskingService;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var model = new DoctorDiagnosisViewModel();

            // Pass user role to view for client-side masking
            var userRole = await GetUserRoleAsync();
            ViewBag.UserRole = userRole;
            ViewBag.ShouldMaskData = _maskingService.ShouldMaskData(userRole);

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> SearchEmployee(string empId, DateTime? examDate = null, string? visitType = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(empId))
                {
                    return Json(new { success = false, message = "Employee ID is required." });
                }

                // Find employee by emp_id
                var employee = await _doctorDiagnosisRepository.GetEmployeeByEmpIdAsync(empId);

                if (employee == null)
                {
                    return Json(new { success = false, message = "Employee not found." });
                }

                // Set exam date to today if not provided
                var searchDate = examDate ?? DateTime.Now.Date;

                // Set visit type to default if not provided
                var selectedVisitType = visitType ?? "Regular Visitor";

                // Load health profile data
                var healthProfile = await _healthProfileRepository.LoadFormData(employee.emp_uid, searchDate);

                // Get medical conditions for health profile display
                var medConditions = await _doctorDiagnosisRepository.GetMedicalConditionsAsync();

                var model = new DoctorDiagnosisViewModel
                {
                    VisitType = selectedVisitType,
                    EmpId = empId,
                    ExamDate = searchDate,
                    Employee = employee,
                    MedConditions = medConditions,
                    SelectedConditionIds = healthProfile?.SelectedConditionIds ?? new List<int>()
                };

                return Json(new { success = true, data = model });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error searching for employee with ID: {empId}");
                return Json(new { success = false, message = "An error occurred while searching for the employee." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetEmployeeDetails(string empId, DateTime? examDate = null, string? visitType = null)
        {
            try
            {
                _logger.LogInformation($"🔍 GetEmployeeDetails called - empId: '{empId}', examDate: {examDate}, visitType: '{visitType}'");

                if (string.IsNullOrWhiteSpace(empId))
                {
                    _logger.LogWarning("❌ Employee ID is null or empty");
                    return BadRequest("Employee ID is required.");
                }

                _logger.LogInformation($"🔍 Searching for employee with ID: '{empId}'");
                var employee = await _doctorDiagnosisRepository.GetEmployeeByEmpIdAsync(empId);

                if (employee == null)
                {
                    _logger.LogWarning($"❌ Employee not found for ID: '{empId}'");
                    return NotFound($"Employee with ID '{empId}' not found.");
                }

                _logger.LogInformation($"✅ Employee found: {employee.emp_name}");

                var searchDate = examDate ?? DateTime.Now.Date;
                var selectedVisitType = visitType ?? "Regular Visitor";

                _logger.LogInformation($"🔍 Loading health profile for date: {searchDate}, visit type: {selectedVisitType}");

                var healthProfile = await _healthProfileRepository.LoadFormData(employee.emp_uid, searchDate);
                var medConditions = await _doctorDiagnosisRepository.GetMedicalConditionsAsync();

                _logger.LogInformation($"✅ Health profile loaded, conditions count: {medConditions.Count}");

                var model = new DoctorDiagnosisViewModel
                {
                    VisitType = selectedVisitType,
                    EmpId = empId,
                    ExamDate = searchDate,
                    Employee = employee,
                    MedConditions = medConditions,
                    SelectedConditionIds = healthProfile?.SelectedConditionIds ?? new List<int>()
                };

                // Get user role and apply masking if needed
                var userRole = await GetUserRoleAsync();
                ViewBag.UserRole = userRole;
                ViewBag.ShouldMaskData = _maskingService.ShouldMaskData(userRole);

                // Mask sensitive data if user doesn't have appropriate role
                _maskingService.MaskObject(model, userRole);

                return PartialView("_EmployeeDetailsPartial", model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"💥 Error in GetEmployeeDetails for empId: '{empId}' - {ex.Message}");
                return BadRequest($"Error loading employee details: {ex.Message}");
            }
        }

        // UPDATED: Enhanced prescription data with batch information and stock
        [HttpGet]
        public async Task<IActionResult> GetPrescriptionData()
        {
            try
            {
                _logger.LogInformation("🔍 Getting prescription data with batch information");

                var diseases = await _doctorDiagnosisRepository.GetDiseasesAsync();
                var medicineStocks = await _doctorDiagnosisRepository.GetMedicinesFromCompounderIndentAsync();

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
                    success = true,
                    diseases = diseases.Select(d => new { value = d.DiseaseId, text = d.DiseaseName }),
                    medicines = medicineDropdownItems
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error loading prescription data");
                return Json(new
                {
                    success = false,
                    diseases = new List<object>(),
                    medicines = new List<object>(),
                    message = "Error loading medicine data with batch information"
                });
            }
        }

        // NEW: Check available stock for a medicine batch
        [HttpGet]
        public async Task<IActionResult> CheckMedicineStock(int indentItemId)
        {
            try
            {
                var availableStock = await _doctorDiagnosisRepository.GetAvailableStockAsync(indentItemId);

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

        // NEW: Validate prescription quantities against available stock
        [HttpPost]
        public async Task<IActionResult> ValidatePrescriptionStock(List<PrescriptionMedicine> medicines)
        {
            try
            {
                var validationResults = new List<StockValidationResult>();

                if (medicines?.Any() == true)
                {
                    foreach (var medicine in medicines)
                    {
                        if (medicine.IndentItemId.HasValue && medicine.IndentItemId.Value > 0)
                        {
                            var availableStock = await _doctorDiagnosisRepository.GetAvailableStockAsync(medicine.IndentItemId.Value);

                            var validationResult = new StockValidationResult
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
        public async Task<IActionResult> SavePrescription(string empId, DateTime examDate,
            List<int> selectedDiseases, List<PrescriptionMedicine> medicines,
            string? bloodPressure, string? pulse, string? temperature, string? visitType = null)
        {
            try
            {
                // Check if user has permission to save prescriptions
                var userRole = await GetUserRoleAsync();
                if (_maskingService.ShouldMaskData(userRole))
                {
                    return Json(new { success = false, message = "You don't have permission to save prescriptions." });
                }

                var selectedVisitType = visitType ?? "Regular Visitor";
                _logger.LogInformation($"💊 Saving prescription for employee {empId} on {examDate} - Visit Type: {selectedVisitType}");

                // Validate input
                if (string.IsNullOrWhiteSpace(empId))
                {
                    return Json(new { success = false, message = "Employee ID is required." });
                }

                if (selectedDiseases == null || !selectedDiseases.Any())
                {
                    return Json(new { success = false, message = "Please select at least one disease." });
                }

                // NEW: Validate stock before saving
                if (medicines?.Any() == true)
                {
                    foreach (var medicine in medicines)
                    {
                        if (medicine.IndentItemId.HasValue && medicine.IndentItemId.Value > 0)
                        {
                            var availableStock = await _doctorDiagnosisRepository.GetAvailableStockAsync(medicine.IndentItemId.Value);
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

                var vitalSigns = new VitalSigns
                {
                    BloodPressure = bloodPressure,
                    Pulse = pulse,
                    Temperature = temperature
                };

                var userId = User.FindFirst("user_id")?.Value ?? User.Identity?.Name ?? "anonymous";

                // Data will be encrypted in the repository and stock will be updated automatically
                var success = await _doctorDiagnosisRepository.SavePrescriptionAsync(
                    empId, examDate, selectedDiseases, medicines ?? new List<PrescriptionMedicine>(),
                    vitalSigns, userId, selectedVisitType);

                if (success)
                {
                    _logger.LogInformation($"✅ Prescription saved successfully for {empId} with stock updates");
                    return Json(new { success = true, message = "Prescription saved successfully. Medicine stock has been updated." });
                }
                else
                {
                    return Json(new { success = false, message = "Failed to save prescription. Please check if the employee exists." });
                }
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Insufficient stock"))
            {
                _logger.LogWarning($"⚠️ Stock validation failed: {ex.Message}");
                return Json(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"💥 Error saving prescription for employee {empId}: {ex.Message}");
                var errorMessage = "Error saving prescription.";
                return Json(new { success = false, message = errorMessage });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetEmployeeDiagnoses(string empId)
        {
            try
            {
                var diagnoses = await _doctorDiagnosisRepository.GetEmployeeDiagnosesAsync(empId);
                return Json(diagnoses);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading diagnoses for employee {empId}");
                return Json(new List<DiagnosisEntry>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetPrescriptionDetails(int prescriptionId)
        {
            try
            {
                _logger.LogInformation($"🔍 Getting prescription details for ID: {prescriptionId}");

                var prescriptionDetails = await _doctorDiagnosisRepository.GetPrescriptionDetailsAsync(prescriptionId);

                if (prescriptionDetails == null)
                {
                    _logger.LogWarning($"❌ Prescription not found for ID: {prescriptionId}");
                    return Json(new { success = false, message = "Prescription not found." });
                }

                // Get user role and apply masking if needed
                var userRole = await GetUserRoleAsync();
                _maskingService.MaskObject(prescriptionDetails, userRole);

                _logger.LogInformation($"✅ Prescription details loaded for ID: {prescriptionId}");
                return Json(new
                {
                    success = true,
                    prescription = prescriptionDetails,
                    shouldMaskData = _maskingService.ShouldMaskData(userRole)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"💥 Error loading prescription details for ID: {prescriptionId}");
                return Json(new { success = false, message = "Error loading prescription details." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> SearchEmployeeIds(string term)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(term))
                {
                    return Json(new List<string>());
                }

                var matchingIds = await _doctorDiagnosisRepository.SearchEmployeeIdsAsync(term);
                return Json(matchingIds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error searching employee IDs with term: {term}");
                return Json(new List<string>());
            }
        }

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

                var count = await _doctorDiagnosisRepository.GetPendingApprovalCountAsync();
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

                var pendingApprovals = await _doctorDiagnosisRepository.GetPendingApprovalsAsync();

                // No masking needed for doctors viewing pending approvals
                ViewBag.UserRole = userRole;
                ViewBag.ShouldMaskData = false; // Doctors can see all data

                return PartialView("_PendingApprovalsModal", pendingApprovals);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading pending approvals");
                return Json(new { success = false, message = "Error loading pending approvals." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ApprovePrescription(int prescriptionId)
        {
            try
            {
                // Check if user has doctor role
                var userRole = await GetUserRoleAsync();
                if (userRole?.ToLower() != "doctor")
                {
                    return Json(new { success = false, message = "Access denied. Only doctors can approve prescriptions." });
                }

                var approvedBy = User.FindFirst("user_id")?.Value ?? User.Identity?.Name ?? "unknown";
                var success = await _doctorDiagnosisRepository.ApprovePrescriptionAsync(prescriptionId, approvedBy);

                if (success)
                {
                    return Json(new { success = true, message = "Prescription approved successfully." });
                }
                else
                {
                    return Json(new { success = false, message = "Prescription not found or already processed." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error approving prescription {prescriptionId}");
                return Json(new { success = false, message = "Error approving prescription." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> RejectPrescription(int prescriptionId, string rejectionReason)
        {
            try
            {
                // Check if user has doctor role
                var userRole = await GetUserRoleAsync();
                if (userRole?.ToLower() != "doctor")
                {
                    return Json(new { success = false, message = "Access denied. Only doctors can reject prescriptions." });
                }

                if (string.IsNullOrWhiteSpace(rejectionReason) || rejectionReason.Length < 10)
                {
                    return Json(new { success = false, message = "Please provide a detailed rejection reason (minimum 10 characters)." });
                }

                var rejectedBy = User.FindFirst("user_id")?.Value ?? User.Identity?.Name ?? "unknown";
                var success = await _doctorDiagnosisRepository.RejectPrescriptionAsync(prescriptionId, rejectionReason, rejectedBy);

                if (success)
                {
                    return Json(new { success = true, message = "Prescription rejected successfully." });
                }
                else
                {
                    return Json(new { success = false, message = "Prescription not found or already processed." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error rejecting prescription {prescriptionId}");
                return Json(new { success = false, message = "Error rejecting prescription." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ApproveAllPrescriptions(List<int> prescriptionIds)
        {
            try
            {
                // Check if user has doctor role
                var userRole = await GetUserRoleAsync();
                if (userRole?.ToLower() != "doctor")
                {
                    return Json(new { success = false, message = "Access denied. Only doctors can approve prescriptions." });
                }

                if (prescriptionIds == null || !prescriptionIds.Any())
                {
                    return Json(new { success = false, message = "No prescriptions selected for approval." });
                }

                var approvedBy = User.FindFirst("user_id")?.Value ?? User.Identity?.Name ?? "unknown";
                var approvedCount = await _doctorDiagnosisRepository.ApproveAllPrescriptionsAsync(prescriptionIds, approvedBy);

                if (approvedCount > 0)
                {
                    return Json(new { success = true, message = $"{approvedCount} prescription(s) approved successfully." });
                }
                else
                {
                    return Json(new { success = false, message = "No prescriptions were approved. They may have been already processed." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving multiple prescriptions");
                return Json(new { success = false, message = "Error approving prescriptions." });
            }
        }

        // Helper method to get user role (similar to StoreIndentController)
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