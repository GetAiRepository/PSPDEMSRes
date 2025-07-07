using EMS.WebApp.Data;
using EMS.WebApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EMS.WebApp.Controllers
{
    [Authorize]
    public class ExpiredMedicineController : Controller
    {
        private readonly IExpiredMedicineRepository _repo;

        public ExpiredMedicineController(IExpiredMedicineRepository repo)
        {
            _repo = repo;
        }

        public IActionResult Index()
        {
            return View();
        }

        // Load data for DataTables
        public async Task<IActionResult> LoadData(string status = "pending")
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"LoadData called with status: {status}");

                IEnumerable<ExpiredMedicine> expiredMedicines;

                System.Diagnostics.Debug.WriteLine("Attempting to fetch expired medicines...");

                if (status.ToLower() == "disposed")
                {
                    expiredMedicines = await _repo.ListDisposedAsync();
                    System.Diagnostics.Debug.WriteLine($"Found {expiredMedicines.Count()} disposed medicines");
                }
                else
                {
                    expiredMedicines = await _repo.ListPendingDisposalAsync();
                    System.Diagnostics.Debug.WriteLine($"Found {expiredMedicines.Count()} pending medicines");
                }

                var result = expiredMedicines.Select((item, index) => new
                {
                    slNo = index + 1,
                    expiredMedicineId = item.ExpiredMedicineId,
                    medicineName = item.MedicineName ?? "Unknown",
                    companyName = item.CompanyName ?? "Not Defined",
                    batchNumber = item.BatchNumber ?? "N/A",
                    vendorCode = item.VendorCode ?? "N/A",
                    expiredOn = item.ExpiryDate.ToString("dd/MM/yyyy"),
                    daysOverdue = item.DaysOverdue,
                    qtyExpired = item.QuantityExpired,
                    unitPrice = item.UnitPrice?.ToString("C") ?? "N/A",
                    totalValue = item.TotalValue?.ToString("C") ?? "N/A",
                    indentNo = item.IndentNumber ?? item.IndentId.ToString(),
                    status = item.Status ?? "Unknown",
                    priorityLevel = item.PriorityLevel,
                    detectedDate = item.DetectedDate.ToString("dd/MM/yyyy"),
                    issuedDate = item.BiomedicalWasteIssuedDate?.ToString("dd/MM/yyyy HH:mm") ?? "",
                    issuedBy = item.BiomedicalWasteIssuedBy ?? "",
                    typeOfMedicine = item.TypeOfMedicine ?? "Select Type of Medicine", // Include medicine type
                    typeBadgeClass = item.TypeBadgeClass, // Include badge class
                    isDisposed = item.IsDisposed,
                    isCritical = item.IsCritical,
                    canDispose = item.TypeOfMedicine != "Select Type of Medicine" // NEW: Add disposal eligibility flag
                }).ToList();

                System.Diagnostics.Debug.WriteLine($"Returning {result.Count} items to DataTable");
                return Json(new { data = result });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadData Error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");

                // Return more detailed error for debugging
                return Json(new
                {
                    data = new List<object>(),
                    error = $"Error: {ex.Message}",
                    details = ex.InnerException?.Message ?? "No inner exception"
                });
            }
        }

        // Issue single item to biomedical waste
        [HttpPost]
        public async Task<IActionResult> IssueToBiomedicalWaste(int id, string? remarks = null)
        {
            try
            {
                var currentUser = User.Identity?.Name;
                if (string.IsNullOrEmpty(currentUser))
                {
                    return Json(new { success = false, message = "User not identified." });
                }

                // Get the item to verify it's pending disposal
                var item = await _repo.GetByIdAsync(id);
                if (item == null)
                {
                    return Json(new { success = false, message = "Expired medicine record not found." });
                }

                if (item.Status != "Pending Disposal")
                {
                    return Json(new { success = false, message = "Medicine has already been disposed of." });
                }

                // NEW: Validate medicine type before disposal
                if (item.TypeOfMedicine == "Select Type of Medicine" || string.IsNullOrEmpty(item.TypeOfMedicine))
                {
                    return Json(new
                    {
                        success = false,
                        message = "Please select a valid medicine type (Solid, Liquid, or Gel) before disposing this medicine.",
                        requiresTypeSelection = true
                    });
                }

                // Issue to biomedical waste (remarks parameter is ignored since property doesn't exist)
                await _repo.IssueToBiomedicalWasteAsync(id, currentUser, remarks);

                return Json(new
                {
                    success = true,
                    message = "Medicine successfully issued to biomedical waste.",
                    issuedDate = DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
                    issuedBy = currentUser
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"IssueToBiomedicalWaste Error: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while issuing to biomedical waste." });
            }
        }

        // Bulk issue to biomedical waste
        [HttpPost]
        public async Task<IActionResult> BulkIssueToBiomedicalWaste(string ids, string? remarks = null)
        {
            try
            {
                var currentUser = User.Identity?.Name;
                if (string.IsNullOrEmpty(currentUser))
                {
                    return Json(new { success = false, message = "User not identified." });
                }

                if (string.IsNullOrEmpty(ids))
                {
                    return Json(new { success = false, message = "No items selected." });
                }

                var idList = ids.Split(',').Select(int.Parse).ToList();

                // NEW: Validate all selected items have proper medicine types
                var itemsToValidate = new List<ExpiredMedicine>();
                foreach (var id in idList)
                {
                    var item = await _repo.GetByIdAsync(id);
                    if (item != null)
                    {
                        itemsToValidate.Add(item);
                    }
                }

                // Check for items with invalid medicine types
                var invalidTypeItems = itemsToValidate
                    .Where(item => item.TypeOfMedicine == "Select Type of Medicine" || string.IsNullOrEmpty(item.TypeOfMedicine))
                    .ToList();

                if (invalidTypeItems.Any())
                {
                    var invalidMedicineNames = invalidTypeItems.Select(item => $"• {item.MedicineName} (Batch: {item.BatchNumber})").ToList();
                    var invalidItemsMessage = string.Join("\n", invalidMedicineNames);

                    return Json(new
                    {
                        success = false,
                        message = $"The following medicines need to have their type selected (Solid, Liquid, or Gel) before disposal:\n\n{invalidItemsMessage}\n\nPlease update the medicine types and try again.",
                        requiresTypeSelection = true,
                        invalidItems = invalidTypeItems.Select(item => new {
                            id = item.ExpiredMedicineId,
                            name = item.MedicineName,
                            batch = item.BatchNumber
                        }).ToList()
                    });
                }

                // Bulk issue to biomedical waste (remarks parameter is ignored since property doesn't exist)
                await _repo.BulkIssueToBiomedicalWasteAsync(idList, currentUser, remarks);

                return Json(new
                {
                    success = true,
                    message = $"Successfully issued {idList.Count} items to biomedical waste.",
                    issuedDate = DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
                    issuedBy = currentUser,
                    count = idList.Count
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"BulkIssueToBiomedicalWaste Error: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while processing bulk issue." });
            }
        }

        // NEW: Method to validate disposal eligibility for selected items
        [HttpPost]
        public async Task<IActionResult> ValidateDisposalEligibility(string ids)
        {
            try
            {
                if (string.IsNullOrEmpty(ids))
                {
                    return Json(new { success = false, message = "No items provided for validation." });
                }

                var idList = ids.Split(',').Select(int.Parse).ToList();
                var invalidItems = new List<object>();

                foreach (var id in idList)
                {
                    var item = await _repo.GetByIdAsync(id);
                    if (item != null && (item.TypeOfMedicine == "Select Type of Medicine" || string.IsNullOrEmpty(item.TypeOfMedicine)))
                    {
                        invalidItems.Add(new
                        {
                            id = item.ExpiredMedicineId,
                            name = item.MedicineName,
                            batch = item.BatchNumber,
                            currentType = item.TypeOfMedicine
                        });
                    }
                }

                return Json(new
                {
                    success = true,
                    isValid = invalidItems.Count == 0,
                    invalidItems = invalidItems,
                    message = invalidItems.Count == 0
                        ? "All selected items are ready for disposal."
                        : $"{invalidItems.Count} item(s) need medicine type selection before disposal."
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ValidateDisposalEligibility Error: {ex.Message}");
                return Json(new { success = false, message = "An error occurred during validation." });
            }
        }

        // View details
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var item = await _repo.GetByIdWithDetailsAsync(id);
                if (item == null)
                {
                    return NotFound();
                }

                return PartialView("_Details", item);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Details Error: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while loading details." });
            }
        }

        // REMOVED: Edit actions since we're using inline editing only
        // The _Edit.cshtml modal is no longer used

        // Sync expired medicines manually
        [HttpPost]
        public async Task<IActionResult> SyncExpiredMedicines()
        {
            try
            {
                var currentUser = User.Identity?.Name ?? "System";
                var newItems = await _repo.DetectNewExpiredMedicinesAsync(currentUser);

                if (newItems.Any())
                {
                    foreach (var item in newItems)
                    {
                        await _repo.AddAsync(item);
                    }

                    return Json(new
                    {
                        success = true,
                        message = $"Successfully detected and added {newItems.Count} new expired medicines.",
                        count = newItems.Count
                    });
                }
                else
                {
                    return Json(new
                    {
                        success = true,
                        message = "No new expired medicines detected.",
                        count = 0
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SyncExpiredMedicines Error: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while syncing expired medicines." });
            }
        }

        // Get statistics for dashboard
        public async Task<IActionResult> GetStatistics()
        {
            try
            {
                var stats = new
                {
                    totalExpired = await _repo.GetTotalExpiredCountAsync(),
                    pendingDisposal = await _repo.GetPendingDisposalCountAsync(),
                    disposed = await _repo.GetDisposedCountAsync(),
                    totalValue = await _repo.GetTotalExpiredValueAsync(),
                    criticalCount = (await _repo.GetCriticalExpiredMedicinesAsync()).Count()
                };

                return Json(new { success = true, data = stats });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetStatistics Error: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while loading statistics." });
            }
        }

        // Print report
        public async Task<IActionResult> PrintReport(string ids)
        {
            try
            {
                if (string.IsNullOrEmpty(ids))
                {
                    return Json(new { success = false, message = "No items selected for printing." });
                }

                var idList = ids.Split(',').Select(int.Parse).ToList();
                var items = await _repo.GetExpiredMedicinesForPrintAsync(idList);

                if (!items.Any())
                {
                    return Json(new { success = false, message = "No valid items found for printing." });
                }

                return PartialView("_PrintReport", items);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PrintReport Error: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while generating the print report." });
            }
        }

        // Delete expired medicine record (admin only)
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var item = await _repo.GetByIdAsync(id);
                if (item == null)
                {
                    return Json(new { success = false, message = "Expired medicine record not found." });
                }

                // Only allow deleting records that haven't been disposed yet
                if (item.Status == "Issued to Biomedical Waste")
                {
                    return Json(new { success = false, message = "Cannot delete records that have been disposed of." });
                }

                await _repo.DeleteAsync(id);
                return Json(new { success = true, message = "Expired medicine record deleted successfully." });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Delete Error: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while deleting the record." });
            }
        }

        // Update medicine type inline
        [HttpPost]
        public async Task<IActionResult> UpdateMedicineType(int id, string typeOfMedicine)
        {
            try
            {
                // Validate the medicine type
                var validTypes = new[] { "Select Type of Medicine", "Solid", "Liquid", "Gel" };
                if (!validTypes.Contains(typeOfMedicine))
                {
                    return Json(new { success = false, message = "Invalid medicine type." });
                }

                // Get the item to verify it exists and is editable
                var item = await _repo.GetByIdAsync(id);
                if (item == null)
                {
                    return Json(new { success = false, message = "Expired medicine record not found." });
                }

                // Update the medicine type
                await _repo.UpdateMedicineTypeAsync(id, typeOfMedicine);

                // Get the badge class for the new type
                var badgeClass = typeOfMedicine.ToLower() switch
                {
                    "liquid" => "bg-info",
                    "solid" => "bg-success",
                    "gel" => "bg-warning text-dark",
                    "select type of medicine" => "bg-secondary",
                    _ => "bg-secondary"
                };

                return Json(new
                {
                    success = true,
                    message = "Medicine type updated successfully.",
                    typeOfMedicine = typeOfMedicine,
                    badgeClass = badgeClass,
                    canDispose = typeOfMedicine != "Select Type of Medicine" // NEW: Return disposal eligibility
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateMedicineType Error: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while updating medicine type." });
            }
        }

        // Get medicine types for dropdown
        public IActionResult GetMedicineTypes()
        {
            try
            {
                var types = ExpiredMedicine.MedicineTypes.Select(type => new
                {
                    value = type,
                    text = type,
                    badgeClass = type.ToLower() switch
                    {
                        "liquid" => "bg-info",
                        "solid" => "bg-success",
                        "gel" => "bg-warning text-dark",
                        "select type of medicine" => "bg-secondary",
                        _ => "bg-secondary"
                    }
                }).ToList();

                return Json(new { success = true, data = types });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetMedicineTypes Error: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while loading medicine types." });
            }
        }

        // Test database connectivity (if this method exists in your original controller)
        public async Task<IActionResult> TestDatabase()
        {
            try
            {
                // Simple test to check if database is accessible
                var count = await _repo.GetTotalExpiredCountAsync();
                return Json(new { success = true, message = "Database connection successful", count = count });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TestDatabase Error: {ex.Message}");
                return Json(new { success = false, message = "Database connection failed: " + ex.Message });
            }
        }
    }
}