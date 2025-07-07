using EMS.WebApp.Data;
using EMS.WebApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

namespace EMS.WebApp.Controllers
{
    [Authorize]
    public class CompounderIndentController : Controller
    {
        private readonly ICompounderIndentRepository _repo;
        private readonly IMemoryCache _cache;

        public CompounderIndentController(ICompounderIndentRepository repo, IMemoryCache cache)
        {
            _repo = repo;
            _cache = cache;
        }

        public IActionResult Index() => View();

        public async Task<IActionResult> LoadData(string indentType = null)
        {
            var currentUser = User.Identity?.Name;

            // Get the list with user filtering for drafts
            IEnumerable<CompounderIndent> list;

            if (string.IsNullOrEmpty(indentType))
            {
                list = await _repo.ListAsync(currentUser);
            }
            else if (indentType == "Compounder Inventory")
            {
                // For Compounder Inventory, show all approved indents
                list = await _repo.ListByStatusAsync("Approved", currentUser);
            }
            else
            {
                list = await _repo.ListByTypeAsync(indentType, currentUser);
            }

            // Check if user has doctor role
            var userRole = await GetUserRoleAsync();
            var isDoctor = userRole?.ToLower() == "doctor";

            var result = new List<object>();

            foreach (var x in list)
            {
                // Get items with their pending quantities to determine button visibility
                var items = await _repo.GetItemsByIndentIdAsync(x.IndentId);
                var allItemsReceived = items.Any() && items.All(item => item.PendingQuantity == 0);
                var hasItems = items.Any();

                var itemData = new
                {
                    x.IndentId,
                    IndentNo = x.IndentId.ToString(),
                    IndentType = indentType == "Compounder Inventory" ? "Compounder Inventory" : x.IndentType,
                    IndentDate = x.IndentDate.ToString("dd/MM/yyyy"),
                    x.Status,
                    x.CreatedBy,
                    CreatedDate = x.CreatedDate.ToString("dd/MM/yyyy HH:mm"),
                    // IMPORTANT: Review & Approve button only shows for doctors with pending indents
                    CanApproveReject = isDoctor && x.Status == "Pending" && x.IndentType != "Draft Indent" && indentType != "Compounder Inventory",
                    // IMPORTANT: For Compounder Inventory, show edit only if not all items are fully received
                    CanEdit = indentType == "Compounder Inventory" ?
                        (!isDoctor && hasItems && !allItemsReceived) :
                        (!isDoctor && ((x.IndentType == "Draft Indent" && x.CreatedBy == currentUser) || // Only creator can edit drafts
                         (x.IndentType != "Draft Indent" && (x.Status == "Pending" || string.IsNullOrEmpty(x.Status))))), // Others can edit if pending
                    CanDelete = indentType != "Compounder Inventory" && !isDoctor && ((x.IndentType == "Draft Indent" && x.CreatedBy == currentUser) || // Only creator can delete drafts
                               (x.Status == "Pending" || string.IsNullOrEmpty(x.Status))), // Others can delete if pending
                    IsDoctor = isDoctor, // Pass doctor status to frontend
                    AllItemsReceived = allItemsReceived,
                    HasItems = hasItems
                };

                result.Add(itemData);
            }

            return Json(new { data = result });
        }

        // Method to get user role
        private async Task<string?> GetUserRoleAsync()
        {
            try
            {
                var userName = User.Identity?.Name;
                if (string.IsNullOrEmpty(userName))
                    return null;

                // You'll need to implement this based on your user/role system
                // This is a placeholder - adjust according to your actual implementation
                using var scope = HttpContext.RequestServices.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var user = await dbContext.SysUsers
                    .Include(u => u.SysRole)
                    .FirstOrDefaultAsync(u => u.full_name == userName || u.email == userName || u.adid == userName);

                return user?.SysRole?.role_name;
            }
            catch (Exception ex)
            {
                // Log error if needed
                System.Diagnostics.Debug.WriteLine($"Error getting user role: {ex.Message}");
                return null;
            }
        }

        // Action for approve/reject modal
        public async Task<IActionResult> ApproveReject(int id, string indentType = null)
        {
            var item = await _repo.GetByIdWithItemsAsync(id);
            if (item == null)
                return NotFound();

            // Check if user has doctor role
            var userRole = await GetUserRoleAsync();
            if (userRole?.ToLower() != "doctor")
            {
                return Json(new { success = false, message = "Access denied. Only doctors can approve/reject indents." });
            }

            // Check if status is pending
            if (item.Status != "Pending")
            {
                return Json(new { success = false, message = "Only pending indents can be approved or rejected." });
            }

            // Populate medicine dropdown for editing
            await PopulateMedicineDropdownAsync();

            // Set ViewBag flag for Compounder Inventory mode
            ViewBag.IsCompounderInventoryMode = indentType == "Compounder Inventory";

            return PartialView("_ApproveRejectModal", item);
        }

        //public async Task<IActionResult> ApproveReject(int id)
        //{
        //    var item = await _repo.GetByIdWithItemsAsync(id);
        //    if (item == null)
        //        return NotFound();

        //    // Check if user has doctor role
        //    var userRole = await GetUserRoleAsync();
        //    if (userRole?.ToLower() != "doctor")
        //    {
        //        return Json(new { success = false, message = "Access denied. Only doctors can approve/reject indents." });
        //    }

        //    // Check if status is pending
        //    if (item.Status != "Pending")
        //    {
        //        return Json(new { success = false, message = "Only pending indents can be approved or rejected." });
        //    }

        //    // Populate medicine dropdown for editing
        //    await PopulateMedicineDropdownAsync();

        //    return PartialView("_ApproveRejectModal", item);
        //}

        // Action to update status
        [HttpPost]
        public async Task<IActionResult> UpdateStatus(int indentId, string status, string comments)
        {
            try
            {
                // Validate input
                if (string.IsNullOrWhiteSpace(comments) || comments.Length < 10)
                {
                    return Json(new { success = false, message = "Please provide detailed comments (minimum 10 characters)." });
                }

                if (status != "Approved" && status != "Rejected")
                {
                    return Json(new { success = false, message = "Invalid status." });
                }

                // Check if user has doctor role
                var userRole = await GetUserRoleAsync();
                if (userRole?.ToLower() != "doctor")
                {
                    return Json(new { success = false, message = "Access denied. Only doctors can approve/reject indents." });
                }

                // Get the indent
                var indent = await _repo.GetByIdAsync(indentId);
                if (indent == null)
                {
                    return Json(new { success = false, message = "Compounder Indent not found." });
                }

                // Check if status is pending
                if (indent.Status != "Pending")
                {
                    return Json(new { success = false, message = "Only pending indents can be approved or rejected." });
                }

                // Update the indent
                indent.Status = status;
                indent.Comments = comments;
                indent.ApprovedBy = User.Identity?.Name;
                indent.ApprovedDate = DateTime.Now;

                // Also update IndentType to match the new status
                indent.IndentType = status == "Approved" ? "Approved Indents" : "Rejected Indents";

                await _repo.UpdateAsync(indent);

                return Json(new { success = true, message = $"Compounder Indent {status.ToLower()} successfully." });
            }
            catch (Exception ex)
            {
                // Log the actual error for debugging
                System.Diagnostics.Debug.WriteLine($"UpdateStatus Error: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while updating the status." });
            }
        }

        public async Task<IActionResult> Create()
        {
            await PopulateMedicineDropdownAsync();
            await PopulateIndentTypeDropdownAsync(); // New method for indent types
            var model = new CompounderIndent
            {
                IndentDate = DateTime.Today,
                IndentType = "Pending Indents", // Default to pending
                Status = "Pending"
            };
            return PartialView("_CreateEdit", model);
        }

        [HttpPost]
        public async Task<IActionResult> Create(CompounderIndent model, string medicinesJson, string actionType = "submit")
        {
            await PopulateMedicineDropdownAsync();
            await PopulateIndentTypeDropdownAsync();

            // Debug: Log incoming data
            System.Diagnostics.Debug.WriteLine($"Create - Action Type: {actionType}");
            System.Diagnostics.Debug.WriteLine($"Create - Received medicinesJson: {medicinesJson ?? "NULL"}");

            // Set indent type based on action
            if (actionType.ToLower() == "save")
            {
                model.IndentType = "Draft Indent";
                model.Status = "Draft";
            }
            else
            {
                // For submit, if it was a draft, change to pending
                if (model.IndentType == "Draft Indent")
                {
                    model.IndentType = "Pending Indents";
                    model.Status = "Pending";
                }
                else
                {
                    // Set status based on indent type for other types
                    SetStatusBasedOnIndentType(model);
                }
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Error = "Please check the form for validation errors.";
                return PartialView("_CreateEdit", model);
            }

            // Rate limiting logic
            var userId = User.FindFirst("user_id")?.Value ?? "anonymous";
            var cacheKey = $"rate_limit_create_compounderindent_{userId}";

            var timestamps = _cache.GetOrCreate(cacheKey, entry =>
            {
                entry.SlidingExpiration = TimeSpan.FromMinutes(5);
                return new List<DateTime>();
            });

            timestamps.RemoveAll(t => t < DateTime.UtcNow.AddMinutes(-5));

            if (timestamps.Count >= 5)
            {
                ViewBag.Error = "⚠ You can only create 5 Compounder Indents every 5 minutes. Please wait.";
                return PartialView("_CreateEdit", model);
            }

            timestamps.Add(DateTime.UtcNow);
            _cache.Set(cacheKey, timestamps, TimeSpan.FromMinutes(5));

            try
            {
                model.CreatedBy = User.Identity?.Name;
                model.CreatedDate = DateTime.Now;
                await _repo.AddAsync(model);

                System.Diagnostics.Debug.WriteLine($"Compounder Indent created with ID: {model.IndentId}");

                // Process medicines if provided
                var medicineValidationResult = await ProcessMedicinesForCreate(model.IndentId, medicinesJson);
                if (!medicineValidationResult.Success)
                {
                    ViewBag.Error = medicineValidationResult.ErrorMessage;
                    return PartialView("_CreateEdit", model);
                }

                if (actionType.ToLower() == "save")
                {
                    // For save, return JSON response instead of keeping form open
                    if (medicineValidationResult.HasMedicines)
                    {
                        return Json(new { success = true, message = "Compounder Indent saved as draft successfully with medicines!", indentId = model.IndentId, actionType = "save" });
                    }
                    else
                    {
                        return Json(new { success = true, message = "Compounder Indent saved as draft successfully! You can continue editing or submit when ready.", indentId = model.IndentId, actionType = "save" });
                    }
                }
                else
                {
                    // For submit, close the modal
                    if (medicineValidationResult.HasMedicines)
                    {
                        ViewBag.Success = "Compounder Indent submitted successfully with medicines!";
                    }
                    else
                    {
                        ViewBag.Success = "Compounder Indent submitted successfully! You can now add medicines.";
                    }

                    return Json(new { success = true, redirectToEdit = !medicineValidationResult.HasMedicines, indentId = model.IndentId });
                }
            }
            catch (Exception ex)
            {
                // Log the actual error for debugging
                System.Diagnostics.Debug.WriteLine($"Create Error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Inner Exception: {ex.InnerException?.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
                ViewBag.Error = $"An error occurred while creating the indent: {ex.Message}";
                return PartialView("_CreateEdit", model);
            }
        }

        //public async Task<IActionResult> Edit(int id)
        //{
        //    var item = await _repo.GetByIdWithItemsAsync(id);
        //    if (item == null) return NotFound();

        //    // Security check: Doctors cannot edit indents - they can only review
        //    var userRole = await GetUserRoleAsync();
        //    if (userRole?.ToLower() == "doctor")
        //    {
        //        return Json(new { success = false, message = "Access denied. Doctors can only review indents, not edit them." });
        //    }

        //    // Security check: Only allow editing drafts by their creators
        //    var currentUser = User.Identity?.Name;
        //    if (item.IndentType == "Draft Indent" && item.CreatedBy != currentUser)
        //    {
        //        return Json(new { success = false, message = "Access denied. You can only edit your own drafts." });
        //    }

        //    await PopulateMedicineDropdownAsync();
        //    await PopulateIndentTypeDropdownAsync(item.CreatedBy); // Pass creator for draft filtering
        //    return PartialView("_CreateEdit", item);
        //}
        public async Task<IActionResult> Edit(int id, string indentType = null)
        {
            var item = await _repo.GetByIdWithItemsAsync(id);
            if (item == null) return NotFound();

            // Security check: Doctors cannot edit indents - they can only review
            var userRole = await GetUserRoleAsync();
            if (userRole?.ToLower() == "doctor")
            {
                return Json(new { success = false, message = "Access denied. Doctors can only review indents, not edit them." });
            }

            // Security check: Only allow editing drafts by their creators
            var currentUser = User.Identity?.Name;
            if (item.IndentType == "Draft Indent" && item.CreatedBy != currentUser)
            {
                return Json(new { success = false, message = "Access denied. You can only edit your own drafts." });
            }

            await PopulateMedicineDropdownAsync();
            await PopulateIndentTypeDropdownAsync(item.CreatedBy); // Pass creator for draft filtering

            // Set ViewBag flag for Compounder Inventory mode
            ViewBag.IsCompounderInventoryMode = indentType == "Compounder Inventory";

            return PartialView("_CreateEdit", item);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(CompounderIndent model, string medicinesJson, string actionType = "submit")
        {
            // Security check: Doctors cannot edit indents - they can only review
            var userRole = await GetUserRoleAsync();
            if (userRole?.ToLower() == "doctor")
            {
                return Json(new { success = false, message = "Access denied. Doctors can only review indents, not edit them." });
            }

            await PopulateMedicineDropdownAsync();
            await PopulateIndentTypeDropdownAsync(model.CreatedBy);

            try
            {
                if (!ModelState.IsValid)
                {
                    ViewBag.Error = "Please check the form for validation errors.";
                    return PartialView("_CreateEdit", model);
                }

                // Get existing entity to preserve creation info
                var existingIndent = await _repo.GetByIdAsync(model.IndentId);
                if (existingIndent == null)
                {
                    ViewBag.Error = "Compounder Indent not found.";
                    return PartialView("_CreateEdit", model);
                }

                // Handle draft to pending transition
                if (actionType.ToLower() == "submit" && existingIndent.IndentType == "Draft Indent")
                {
                    model.IndentType = "Pending Indents";
                    model.Status = "Pending";
                }
                else if (actionType.ToLower() == "save")
                {
                    // Keep as draft if saving
                    if (existingIndent.IndentType == "Draft Indent")
                    {
                        model.IndentType = "Draft Indent";
                        model.Status = "Draft";
                    }
                }
                else
                {
                    // Set status based on indent type for other cases
                    SetStatusBasedOnIndentType(model);
                }

                // Update the existing tracked entity with new values
                existingIndent.IndentType = model.IndentType;
                existingIndent.IndentDate = model.IndentDate;
                existingIndent.Status = model.Status;
                // Keep original CreatedBy and CreatedDate (don't update these)

                await _repo.UpdateAsync(existingIndent);

                // Process medicines
                var medicineValidationResult = await ProcessMedicinesForEdit(model.IndentId, medicinesJson);
                if (!medicineValidationResult.Success)
                {
                    ViewBag.Error = medicineValidationResult.ErrorMessage;
                    return PartialView("_CreateEdit", model);
                }

                if (actionType.ToLower() == "save")
                {
                    // For save, return JSON response to keep modal open
                    return Json(new { success = true, message = "Compounder Indent saved successfully! You can continue editing or submit when ready.", actionType = "save" });
                }
                else
                {
                    // For submit, close the modal
                    ViewBag.Success = "Compounder Indent updated successfully!";
                    return Json(new { success = true });
                }
            }
            catch (Exception ex)
            {
                // Log the actual error for debugging
                System.Diagnostics.Debug.WriteLine($"Edit Error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Inner Exception: {ex.InnerException?.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
                ViewBag.Error = $"An error occurred while updating the indent: {ex.Message}";
                return PartialView("_CreateEdit", model);
            }
        }

        // Add individual medicine item (for approve/reject modal) - no batch/expiry
        [HttpPost]
        public async Task<IActionResult> AddMedicineItem(int indentId, int medItemId, string vendorCode, int raisedQuantity, int receivedQuantity = 0)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"AddMedicineItem called - IndentId: {indentId}, MedItemId: {medItemId}, VendorCode: {vendorCode}");

                // Get the indent to check permissions
                var indent = await _repo.GetByIdAsync(indentId);
                if (indent == null)
                {
                    return Json(new { success = false, message = "Compounder Indent not found." });
                }

                // Security check
                var userRole = await GetUserRoleAsync();
                var currentUser = User.Identity?.Name;

                if (indent.Status != "Pending" && userRole?.ToLower() != "doctor")
                {
                    return Json(new { success = false, message = "Only pending indents can be modified, or doctors can modify during approval." });
                }

                // Check for duplicates
                if (await _repo.IsMedicineAlreadyAddedAsync(indentId, medItemId))
                {
                    return Json(new { success = false, message = "This medicine is already added to this indent." });
                }

                if (await _repo.IsVendorCodeExistsAsync(indentId, vendorCode))
                {
                    return Json(new { success = false, message = "This vendor code already exists in this indent." });
                }

                // Create new medicine item (batch no, expiry date, and available stock will be null by default)
                var newItem = new CompounderIndentItem
                {
                    IndentId = indentId,
                    MedItemId = medItemId,
                    VendorCode = vendorCode,
                    RaisedQuantity = raisedQuantity,
                    ReceivedQuantity = receivedQuantity
                    // BatchNo, ExpiryDate, and AvailableStock are null by default
                };

                await _repo.AddItemAsync(newItem);

                System.Diagnostics.Debug.WriteLine($"Medicine item added successfully with ID: {newItem.IndentItemId}");

                return Json(new
                {
                    success = true,
                    message = "Medicine item added successfully!",
                    itemId = newItem.IndentItemId
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AddMedicineItem Error: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while adding the medicine item." });
            }
        }

      
        [HttpPost]
        public async Task<IActionResult> UpdateMedicineItem(int indentItemId, int? medItemId = null, string vendorCode = null, int? raisedQuantity = null, int? receivedQuantity = null, decimal? unitPrice = null, string batchNo = null, DateTime? expiryDate = null, int? availableStock = null)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"UpdateMedicineItem called with ID: {indentItemId}");

                // Get existing item
                var existingItem = await _repo.GetItemByIdAsync(indentItemId);
                if (existingItem == null)
                {
                    return Json(new { success = false, message = "Medicine item not found." });
                }

                // Get the indent to check if it's approved (Compounder Inventory mode)
                var indent = await _repo.GetByIdAsync(existingItem.IndentId);
                var isCompounderInventory = indent?.Status == "Approved";

                // Update fields if provided
                if (medItemId.HasValue)
                    existingItem.MedItemId = medItemId.Value;

                if (!string.IsNullOrEmpty(vendorCode))
                    existingItem.VendorCode = vendorCode;

                if (raisedQuantity.HasValue)
                {
                    if (raisedQuantity.Value <= 0)
                    {
                        return Json(new { success = false, message = "Raised quantity must be greater than 0." });
                    }
                    existingItem.RaisedQuantity = raisedQuantity.Value;
                }

                if (receivedQuantity.HasValue)
                {
                    if (receivedQuantity.Value < 0)
                    {
                        return Json(new { success = false, message = "Received quantity cannot be negative." });
                    }

                    if (receivedQuantity.Value > existingItem.RaisedQuantity)
                    {
                        return Json(new { success = false, message = "Received quantity cannot exceed raised quantity." });
                    }

                    existingItem.ReceivedQuantity = receivedQuantity.Value;
                }

                if (unitPrice.HasValue)
                {
                    existingItem.UnitPrice = unitPrice;
                    existingItem.TotalAmount = unitPrice.Value * existingItem.ReceivedQuantity;
                }

                // Update batch number, expiry date, and available stock only if in Compounder Inventory mode (approved status)
                if (isCompounderInventory)
                {
                    if (batchNo != null)
                    {
                        existingItem.BatchNo = string.IsNullOrWhiteSpace(batchNo) ? null : batchNo.Trim();
                    }

                    if (expiryDate.HasValue)
                    {
                        existingItem.ExpiryDate = expiryDate.Value;
                    }

                    if (availableStock.HasValue)
                    {
                        if (availableStock.Value < 0)
                        {
                            return Json(new { success = false, message = "Available stock cannot be negative." });
                        }
                        existingItem.AvailableStock = availableStock.Value;
                    }
                }

                await _repo.UpdateItemAsync(existingItem);

                // Return updated values for UI refresh
                return Json(new
                {
                    success = true,
                    message = "Medicine item updated successfully!",
                    data = new
                    {
                        receivedQuantity = existingItem.ReceivedQuantity,
                        pendingQuantity = existingItem.PendingQuantity,
                        unitPrice = existingItem.UnitPrice,
                        totalAmount = existingItem.TotalAmount,
                        batchNo = existingItem.BatchNo,
                        expiryDate = existingItem.ExpiryDate?.ToString("yyyy-MM-dd"),
                        availableStock = existingItem.AvailableStock
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateMedicineItem Error: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while updating the medicine item." });
            }
        }

        // Delete individual medicine item
        [HttpPost]
        public async Task<IActionResult> DeleteMedicineItem(int indentItemId)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"DeleteMedicineItem called with ID: {indentItemId}");

                // Get existing item to check permissions
                var existingItem = await _repo.GetItemByIdAsync(indentItemId);
                if (existingItem == null)
                {
                    System.Diagnostics.Debug.WriteLine($"Medicine item not found: {indentItemId}");
                    return Json(new { success = false, message = "Medicine item not found." });
                }

                // Get the indent to check status and permissions
                var indent = await _repo.GetByIdAsync(existingItem.IndentId);
                if (indent == null)
                {
                    System.Diagnostics.Debug.WriteLine($"Compounder Indent not found: {existingItem.IndentId}");
                    return Json(new { success = false, message = "Compounder Indent not found." });
                }

                // Security check: Only allow deleting from pending indents or by doctors during approval
                var userRole = await GetUserRoleAsync();
                var currentUser = User.Identity?.Name;

                System.Diagnostics.Debug.WriteLine($"User: {currentUser}, Role: {userRole}, Indent Status: {indent.Status}");

                if (indent.Status != "Pending" && userRole?.ToLower() != "doctor")
                {
                    return Json(new { success = false, message = "Only pending indents can be modified, or doctors can modify during approval." });
                }

                // Additional check for draft indents - only creator can delete
                if (indent.IndentType == "Draft Indent" && indent.CreatedBy != currentUser && userRole?.ToLower() != "doctor")
                {
                    return Json(new { success = false, message = "Access denied. You can only delete items from your own drafts." });
                }

                // Delete the item
                await _repo.DeleteItemAsync(indentItemId);
                System.Diagnostics.Debug.WriteLine($"Medicine item deleted successfully: {indentItemId}");

                return Json(new { success = true, message = "Medicine item deleted successfully!" });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DeleteMedicineItem Error: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while deleting the medicine item." });
            }
        }
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                // Security check: Doctors cannot delete indents - they can only review
                var userRole = await GetUserRoleAsync();
                if (userRole?.ToLower() == "doctor")
                {
                    return Json(new { success = false, message = "Access denied. Doctors can only review indents, not delete them." });
                }

                // Get the item first to check permissions
                var item = await _repo.GetByIdAsync(id);
                if (item == null)
                {
                    return Json(new { success = false, message = "Compounder Indent not found." });
                }

                // Security check: Only allow deleting drafts by their creators
                var currentUser = User.Identity?.Name;
                if (item.IndentType == "Draft Indent" && item.CreatedBy != currentUser)
                {
                    return Json(new { success = false, message = "Access denied. You can only delete your own drafts." });
                }

                // Additional check: Don't allow deleting approved/rejected indents
                if (item.Status == "Approved" || item.Status == "Rejected")
                {
                    return Json(new { success = false, message = "Cannot delete approved or rejected indents." });
                }

                await _repo.DeleteAsync(id);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "An error occurred while deleting the indent." });
            }
        }
        public async Task<IActionResult> Details(int id, string indentType = null)
        {
            var item = await _repo.GetByIdWithItemsAsync(id);
            if (item == null) return NotFound();

            // Security check: Only allow viewing drafts by their creators
            var currentUser = User.Identity?.Name;
            if (item.IndentType == "Draft Indent" && item.CreatedBy != currentUser)
            {
                return Json(new { success = false, message = "Access denied. You can only view your own drafts." });
            }

            // Set ViewBag flag for Compounder Inventory mode
            ViewBag.IsCompounderInventoryMode = indentType == "Compounder Inventory";

            return PartialView("_View", item);
        }
        public async Task<IActionResult> GetMedicineDetails(int medItemId)
        {
            var medicine = await _repo.GetMedicineByIdAsync(medItemId);
            if (medicine == null)
                return Json(new { success = false, message = "Medicine not found" });

            return Json(new
            {
                success = true,
                data = new
                {
                    medItemId = medicine.MedItemId,
                    medItemName = medicine.MedItemName,
                    companyName = medicine.CompanyName ?? "Not Defined",
                    reorderLimit = medicine.ReorderLimit
                }
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetMedicineItems(int indentId)
        {
            try
            {
                var items = await _repo.GetItemsByIndentIdAsync(indentId);
                var result = items.Select((item, index) => new {
                    indentItemId = item.IndentItemId,
                    slNo = index + 1,
                    medItemId = item.MedItemId,
                    medItemName = item.MedMaster?.MedItemName,
                    companyName = item.MedMaster?.CompanyName ?? "Not Defined",
                    vendorCode = item.VendorCode,
                    raisedQuantity = item.RaisedQuantity,
                    receivedQuantity = item.ReceivedQuantity,
                    pendingQuantity = item.PendingQuantity,
                    unitPrice = item.UnitPrice,
                    totalAmount = item.TotalAmount,
                    batchNo = item.BatchNo,
                    expiryDate = item.ExpiryDate?.ToString("yyyy-MM-dd"),
                    availableStock = item.AvailableStock
                });

                return Json(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "An error occurred while loading medicine items." });
            }
        }

        private async Task PopulateMedicineDropdownAsync()
        {
            var medicines = await _repo.GetMedicinesAsync();
            ViewBag.MedicineList = new SelectList(medicines, "MedItemId", "MedItemName");
        }

        // Method to populate indent type dropdown with proper filtering
        private async Task PopulateIndentTypeDropdownAsync(string createdBy = null)
        {
            var currentUser = User.Identity?.Name;
            var indentTypes = new List<SelectListItem>();

            // Standard options available to all users
            indentTypes.Add(new SelectListItem { Value = "Pending Indents", Text = "Pending Indents" });
            indentTypes.Add(new SelectListItem { Value = "Approved Indents", Text = "Approved Indents" });
            indentTypes.Add(new SelectListItem { Value = "Rejected Indents", Text = "Rejected Indents" });

            // Draft option - only show to the creator or for new items
            if (string.IsNullOrEmpty(createdBy) || createdBy == currentUser)
            {
                indentTypes.Add(new SelectListItem { Value = "Draft Indent", Text = "Draft Indent" });
            }

            ViewBag.IndentTypeList = indentTypes;
        }

        /// <summary>
        /// Sets the Status property based on the IndentType
        /// </summary>
        private void SetStatusBasedOnIndentType(CompounderIndent model)
        {
            model.Status = model.IndentType switch
            {
                "Pending Indents" => "Pending",
                "Approved Indents" => "Approved",
                "Rejected Indents" => "Rejected",
                "Draft Indent" => "Draft",
                _ => "Pending" // Default fallback
            };
        }

        // Helper methods for medicine processing
        private async Task<MedicineProcessResult> ProcessMedicinesForCreate(int indentId, string medicinesJson)
        {
            System.Diagnostics.Debug.WriteLine($"ProcessMedicinesForCreate - IndentId: {indentId}, JSON: {medicinesJson ?? "NULL"}");

            if (string.IsNullOrEmpty(medicinesJson))
            {
                System.Diagnostics.Debug.WriteLine("No medicines JSON provided, returning success with no medicines");
                return new MedicineProcessResult { Success = true, HasMedicines = false };
            }

            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true
                };

                System.Diagnostics.Debug.WriteLine("Attempting to deserialize JSON...");
                var medicines = JsonSerializer.Deserialize<List<MedicineDto>>(medicinesJson, options);

                System.Diagnostics.Debug.WriteLine($"Deserialized medicines count: {medicines?.Count ?? 0}");

                if (medicines == null || !medicines.Any())
                {
                    System.Diagnostics.Debug.WriteLine("No medicines in deserialized data");
                    return new MedicineProcessResult { Success = true, HasMedicines = false };
                }

                // Log each medicine
                foreach (var med in medicines)
                {
                    System.Diagnostics.Debug.WriteLine($"Medicine: ID={med.MedItemId}, Name={med.MedItemName}, VendorCode={med.VendorCode}, RaisedQty={med.RaisedQuantity}, ReceivedQty={med.ReceivedQuantity}, BatchNo={med.BatchNo}, ExpiryDate={med.ExpiryDate}, AvailableStock={med.AvailableStock}, IsNew={med.IsNew}");
                }

                // Validate medicines
                System.Diagnostics.Debug.WriteLine("Validating medicines...");
                var validationResult = await ValidateMedicines(indentId, medicines);
                if (!validationResult.Success)
                {
                    System.Diagnostics.Debug.WriteLine($"Validation failed: {validationResult.ErrorMessage}");
                    return validationResult;
                }

                // Add all medicines
                var newMedicines = medicines.Where(m => m.IsNew).ToList();
                System.Diagnostics.Debug.WriteLine($"Adding {newMedicines.Count} new medicines...");

                foreach (var medicine in newMedicines)
                {
                    System.Diagnostics.Debug.WriteLine($"Adding medicine: {medicine.MedItemName} with vendor code: {medicine.VendorCode}");

                    var item = new CompounderIndentItem
                    {
                        IndentId = indentId,
                        MedItemId = medicine.MedItemId,
                        VendorCode = medicine.VendorCode,
                        RaisedQuantity = medicine.RaisedQuantity,
                        ReceivedQuantity = medicine.ReceivedQuantity,
                        UnitPrice = medicine.UnitPrice,
                        TotalAmount = medicine.TotalAmount,
                        // Only set batch no, expiry date, and available stock if provided (for Compounder Inventory)
                        BatchNo = medicine.BatchNo,
                        ExpiryDate = medicine.ExpiryDate,
                        AvailableStock = medicine.AvailableStock
                    };

                    await _repo.AddItemAsync(item);
                    System.Diagnostics.Debug.WriteLine($"Successfully added medicine item with ID: {item.IndentItemId}");
                }

                System.Diagnostics.Debug.WriteLine("All medicines processed successfully");
                return new MedicineProcessResult { Success = true, HasMedicines = true };
            }
            catch (Exception ex)
            {
                // Log the actual error for debugging
                System.Diagnostics.Debug.WriteLine($"ProcessMedicinesForCreate Error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Inner Exception: {ex.InnerException?.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
                return new MedicineProcessResult { Success = false, ErrorMessage = $"Error processing medicines data: {ex.Message}" };
            }
        }

        private async Task<MedicineProcessResult> ProcessMedicinesForEdit(int indentId, string medicinesJson)
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true
                };

                var medicines = string.IsNullOrEmpty(medicinesJson)
                    ? new List<MedicineDto>()
                    : JsonSerializer.Deserialize<List<MedicineDto>>(medicinesJson, options);

                if (medicines == null)
                {
                    medicines = new List<MedicineDto>();
                }

                // Get existing medicines
                var existingItems = await _repo.GetItemsByIndentIdAsync(indentId);
                var existingIds = existingItems.Select(x => x.IndentItemId).ToList();
                var submittedExistingIds = medicines.Where(m => !m.IsNew && m.IndentItemId.HasValue)
                                                  .Select(m => m.IndentItemId.Value).ToList();

                // Delete removed medicines
                var toDelete = existingIds.Except(submittedExistingIds).ToList();
                foreach (var deleteId in toDelete)
                {
                    await _repo.DeleteItemAsync(deleteId);
                }

                // Validate remaining medicines
                var validationResult = await ValidateMedicines(indentId, medicines, submittedExistingIds);
                if (!validationResult.Success)
                {
                    return validationResult;
                }

                // Update existing medicines
                foreach (var medicine in medicines.Where(m => !m.IsNew && m.IndentItemId.HasValue))
                {
                    var existingItem = existingItems.FirstOrDefault(x => x.IndentItemId == medicine.IndentItemId.Value);
                    if (existingItem != null)
                    {
                        existingItem.VendorCode = medicine.VendorCode;
                        existingItem.RaisedQuantity = medicine.RaisedQuantity;
                        existingItem.ReceivedQuantity = medicine.ReceivedQuantity;
                        existingItem.UnitPrice = medicine.UnitPrice;
                        existingItem.TotalAmount = medicine.TotalAmount;
                        // Only update batch no, expiry date, and available stock if provided (for Compounder Inventory)
                        existingItem.BatchNo = medicine.BatchNo;
                        existingItem.ExpiryDate = medicine.ExpiryDate;
                        existingItem.AvailableStock = medicine.AvailableStock;
                        await _repo.UpdateItemAsync(existingItem);
                    }
                }

                // Add new medicines
                foreach (var medicine in medicines.Where(m => m.IsNew))
                {
                    var item = new CompounderIndentItem
                    {
                        IndentId = indentId,
                        MedItemId = medicine.MedItemId,
                        VendorCode = medicine.VendorCode,
                        RaisedQuantity = medicine.RaisedQuantity,
                        ReceivedQuantity = medicine.ReceivedQuantity,
                        UnitPrice = medicine.UnitPrice,
                        TotalAmount = medicine.TotalAmount,
                        // Only set batch no, expiry date, and available stock if provided (for Compounder Inventory)
                        BatchNo = medicine.BatchNo,
                        ExpiryDate = medicine.ExpiryDate,
                        AvailableStock = medicine.AvailableStock
                    };

                    await _repo.AddItemAsync(item);
                }

                return new MedicineProcessResult { Success = true, HasMedicines = true };
            }
            catch (Exception ex)
            {
                // Log the actual error for debugging
                System.Diagnostics.Debug.WriteLine($"ProcessMedicinesForEdit Error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Inner Exception: {ex.InnerException?.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
                return new MedicineProcessResult { Success = false, ErrorMessage = $"Error processing medicines data: {ex.Message}" };
            }
        }

        private async Task<MedicineProcessResult> ValidateMedicines(int indentId, List<MedicineDto> medicines, List<int> excludeItemIds = null)
        {
            var vendorCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var medicineIds = new HashSet<int>();

            foreach (var medicine in medicines)
            {
                // Check for duplicate vendor codes within the submission
                if (vendorCodes.Contains(medicine.VendorCode))
                {
                    return new MedicineProcessResult { Success = false, ErrorMessage = $"Duplicate vendor code '{medicine.VendorCode}' found in medicines list." };
                }
                vendorCodes.Add(medicine.VendorCode);

                // Check for duplicate medicines within the submission
                if (medicineIds.Contains(medicine.MedItemId))
                {
                    return new MedicineProcessResult { Success = false, ErrorMessage = $"Duplicate medicine found in medicines list." };
                }
                medicineIds.Add(medicine.MedItemId);

                // Check against existing data (excluding items being updated)
                var excludeId = medicine.IsNew ? null : medicine.IndentItemId;
                if (await _repo.IsVendorCodeExistsAsync(indentId, medicine.VendorCode, excludeId))
                {
                    return new MedicineProcessResult { Success = false, ErrorMessage = $"Vendor code '{medicine.VendorCode}' already exists in this indent." };
                }

                if (await _repo.IsMedicineAlreadyAddedAsync(indentId, medicine.MedItemId, excludeId))
                {
                    return new MedicineProcessResult { Success = false, ErrorMessage = "One or more medicines are already added to this indent." };
                }
            }

            return new MedicineProcessResult { Success = true };
        }
        [HttpPost]
        public async Task<IActionResult> UpdateMedicineItemWithReason(int indentItemId, int? medItemId = null, string vendorCode = null, int? raisedQuantity = null, int? receivedQuantity = null, decimal? unitPrice = null, string batchNo = null, DateTime? expiryDate = null, int? availableStock = null, string reason = null)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"UpdateMedicineItemWithReason called with ID: {indentItemId}");

                // Validate reason
                if (string.IsNullOrWhiteSpace(reason) || reason.Length < 10)
                {
                    return Json(new { success = false, message = "Please provide a detailed reason (minimum 10 characters) for editing this fully received item." });
                }

                // Get existing item
                var existingItem = await _repo.GetItemByIdAsync(indentItemId);
                if (existingItem == null)
                {
                    return Json(new { success = false, message = "Medicine item not found." });
                }

                // Get the indent to check if it's approved (Compounder Inventory mode)
                var indent = await _repo.GetByIdAsync(existingItem.IndentId);
                var isCompounderInventory = indent?.Status == "Approved";

                if (!isCompounderInventory)
                {
                    return Json(new { success = false, message = "Edit with reason is only available for Compounder Inventory items." });
                }

                // Store original values for audit trail
                var originalRaisedQty = existingItem.RaisedQuantity;
                var originalReceivedQty = existingItem.ReceivedQuantity;
                var originalUnitPrice = existingItem.UnitPrice;
                var originalBatchNo = existingItem.BatchNo;
                var originalExpiryDate = existingItem.ExpiryDate;
                var originalAvailableStock = existingItem.AvailableStock;

                // Update fields if provided
                if (medItemId.HasValue)
                    existingItem.MedItemId = medItemId.Value;

                if (!string.IsNullOrEmpty(vendorCode))
                    existingItem.VendorCode = vendorCode;

                if (raisedQuantity.HasValue)
                {
                    if (raisedQuantity.Value <= 0)
                    {
                        return Json(new { success = false, message = "Raised quantity must be greater than 0." });
                    }
                    existingItem.RaisedQuantity = raisedQuantity.Value;
                }

                if (receivedQuantity.HasValue)
                {
                    if (receivedQuantity.Value < 0)
                    {
                        return Json(new { success = false, message = "Received quantity cannot be negative." });
                    }

                    if (receivedQuantity.Value > existingItem.RaisedQuantity)
                    {
                        return Json(new { success = false, message = "Received quantity cannot exceed raised quantity." });
                    }

                    existingItem.ReceivedQuantity = receivedQuantity.Value;
                }

                if (unitPrice.HasValue)
                {
                    existingItem.UnitPrice = unitPrice;
                    existingItem.TotalAmount = unitPrice.Value * existingItem.ReceivedQuantity;
                }

                // Update batch number, expiry date, and available stock (only in Compounder Inventory mode)
                if (batchNo != null)
                {
                    existingItem.BatchNo = string.IsNullOrWhiteSpace(batchNo) ? null : batchNo.Trim();
                }

                if (expiryDate.HasValue)
                {
                    existingItem.ExpiryDate = expiryDate.Value;
                }

                if (availableStock.HasValue)
                {
                    if (availableStock.Value < 0)
                    {
                        return Json(new { success = false, message = "Available stock cannot be negative." });
                    }
                    existingItem.AvailableStock = availableStock.Value;
                }

                await _repo.UpdateItemAsync(existingItem);

                // Log the edit with reason (you can implement audit logging here)
                System.Diagnostics.Debug.WriteLine($"Medicine item {indentItemId} edited with reason: {reason}");
                System.Diagnostics.Debug.WriteLine($"Changes: RaisedQty({originalRaisedQty}->{existingItem.RaisedQuantity}), ReceivedQty({originalReceivedQty}->{existingItem.ReceivedQuantity}), UnitPrice({originalUnitPrice}->{existingItem.UnitPrice}), BatchNo({originalBatchNo}->{existingItem.BatchNo}), ExpiryDate({originalExpiryDate}->{existingItem.ExpiryDate}), AvailableStock({originalAvailableStock}->{existingItem.AvailableStock})");

                // Return updated values for UI refresh
                return Json(new
                {
                    success = true,
                    message = "Medicine item updated successfully with reason!",
                    data = new
                    {
                        receivedQuantity = existingItem.ReceivedQuantity,
                        pendingQuantity = existingItem.PendingQuantity,
                        unitPrice = existingItem.UnitPrice,
                        totalAmount = existingItem.TotalAmount,
                        batchNo = existingItem.BatchNo,
                        expiryDate = existingItem.ExpiryDate?.ToString("yyyy-MM-dd"),
                        availableStock = existingItem.AvailableStock
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateMedicineItemWithReason Error: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while updating the medicine item." });
            }
        }

        // Add these methods to your existing CompounderIndentController class

        /// <summary>
        /// Generate Compounder Indent Report
        /// </summary>
        /// <param name="fromDate">Start date for filtering</param>
        /// <param name="toDate">End date for filtering</param>
        /// <returns>Compounder Indent Report data</returns>
        [HttpGet]
        public async Task<IActionResult> CompounderIndentReport(DateTime? fromDate = null, DateTime? toDate = null)
        {
            try
            {
                // Set default date range if not provided (current month)
                if (!fromDate.HasValue)
                    fromDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

                if (!toDate.HasValue)
                    toDate = DateTime.Now.Date;

                var reportData = await _repo.GetCompounderIndentReportAsync(fromDate, toDate);

                var result = new
                {
                    success = true,
                    data = reportData.Select((item, index) => new
                    {
                        slNo = index + 1,
                        indentId = item.IndentId,
                        indentDate = item.IndentDate.ToString("dd/MM/yyyy"),
                        medicineName = item.MedicineName,
                        potency = item.Potency,
                        manufacturerName = item.ManufacturerName,
                        quantity = item.Quantity,
                        raisedBy = item.RaisedBy
                    }),
                    reportInfo = new
                    {
                        title = "COMPOUNDER INDENT",
                        fromDate = fromDate?.ToString("dd/MM/yyyy"),
                        toDate = toDate?.ToString("dd/MM/yyyy"),
                        generatedBy = User.Identity?.Name ?? "System",
                        generatedOn = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"),
                        totalRecords = reportData.Count()
                    }
                };

                return Json(result);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CompounderIndentReport Error: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while generating the report." });
            }
        }

        /// <summary>
        /// Generate Compounder Inventory Report
        /// </summary>
        /// <param name="fromDate">Start date for filtering</param>
        /// <param name="toDate">End date for filtering</param>
        /// <returns>Compounder Inventory Report data</returns>
        [HttpGet]
        public async Task<IActionResult> CompounderInventoryReport(DateTime? fromDate = null, DateTime? toDate = null)
        {
            try
            {
                // Set default date range if not provided (current month)
                if (!fromDate.HasValue)
                    fromDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

                if (!toDate.HasValue)
                    toDate = DateTime.Now.Date;

                var reportData = await _repo.GetCompounderInventoryReportAsync(fromDate, toDate);

                var result = new
                {
                    success = true,
                    data = reportData.Select((item, index) => new
                    {
                        slNo = index + 1,
                        indentId = item.IndentId,
                        raisedDate = item.RaisedDate.ToString("dd/MM/yyyy"),
                        medicineName = item.MedicineName,
                        raisedQuantity = item.RaisedQuantity,
                        receivedQuantity = item.ReceivedQuantity,
                        potency = item.Potency,
                        manufacturerBy = item.ManufacturerBy,
                        batchNo = item.BatchNo,
                        manufactureDate = item.ManufactureDate?.ToString("dd/MM/yyyy") ?? "",
                        expiryDate = item.ExpiryDate?.ToString("dd/MM/yyyy") ?? "",
                        raisedBy = item.RaisedBy
                    }),
                    reportInfo = new
                    {
                        title = "COMPOUNDER INVENTORY",
                        fromDate = fromDate?.ToString("dd/MM/yyyy"),
                        toDate = toDate?.ToString("dd/MM/yyyy"),
                        generatedBy = User.Identity?.Name ?? "System",
                        generatedOn = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"),
                        totalRecords = reportData.Count()
                    }
                };

                return Json(result);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CompounderInventoryReport Error: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while generating the inventory report." });
            }
        }

        /// <summary>
        /// Display Compounder Indent Report View
        /// </summary>
        /// <returns>Report view</returns>
        public IActionResult CompounderIndentReportView()
        {
            return View("CompounderIndentReport");
        }

        /// <summary>
        /// Display Compounder Inventory Report View
        /// </summary>
        /// <returns>Report view</returns>
        public IActionResult CompounderInventoryReportView()
        {
            return View("CompounderInventoryReport");
        }

        /// <summary>
        /// Export Compounder Indent Report to Excel
        /// </summary>
        /// <param name="fromDate">Start date for filtering</param>
        /// <param name="toDate">End date for filtering</param>
        /// <returns>Excel file</returns>
        [HttpGet]
        public async Task<IActionResult> ExportCompounderIndentReport(DateTime? fromDate = null, DateTime? toDate = null)
        {
            try
            {
                // Set default date range if not provided
                if (!fromDate.HasValue)
                    fromDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

                if (!toDate.HasValue)
                    toDate = DateTime.Now.Date;

                var reportData = await _repo.GetCompounderIndentReportAsync(fromDate, toDate);

                // You can implement Excel export logic here using EPPlus or similar library
                // For now, returning CSV format
                var csv = new System.Text.StringBuilder();
                csv.AppendLine("INDENT ID,INDENT DATE,MEDICINE NAME,POTENCY,MANUFACTURER NAME,QUANTITY,RAISED BY");

                foreach (var item in reportData)
                {
                    csv.AppendLine($"{item.IndentId},{item.IndentDate:dd/MM/yyyy},{item.MedicineName},{item.Potency},{item.ManufacturerName},{item.Quantity},{item.RaisedBy}");
                }

                var fileName = $"CompounderIndentReport_{fromDate:ddMMyyyy}_to_{toDate:ddMMyyyy}.csv";
                return File(System.Text.Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", fileName);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ExportCompounderIndentReport Error: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while exporting the report." });
            }
        }

        /// <summary>
        /// Export Compounder Inventory Report to Excel
        /// </summary>
        /// <param name="fromDate">Start date for filtering</param>
        /// <param name="toDate">End date for filtering</param>
        /// <returns>Excel file</returns>
        [HttpGet]
        public async Task<IActionResult> ExportCompounderInventoryReport(DateTime? fromDate = null, DateTime? toDate = null)
        {
            try
            {
                // Set default date range if not provided
                if (!fromDate.HasValue)
                    fromDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

                if (!toDate.HasValue)
                    toDate = DateTime.Now.Date;

                var reportData = await _repo.GetCompounderInventoryReportAsync(fromDate, toDate);

                // CSV format for now
                var csv = new System.Text.StringBuilder();
                csv.AppendLine("INDENT ID,RAISED DATE,MEDICINE NAME,RAISED QUANTITY,RECEIVED QUANTITY,POTENCY,MANUFACTURER BY,BATCH NO,MANUFACTURE DATE,EXPIRY DATE,RAISED BY");

                foreach (var item in reportData)
                {
                    csv.AppendLine($"{item.IndentId},{item.RaisedDate:dd/MM/yyyy},{item.MedicineName},{item.RaisedQuantity},{item.ReceivedQuantity},{item.Potency},{item.ManufacturerBy},{item.BatchNo},{item.ManufactureDate:dd/MM/yyyy},{item.ExpiryDate:dd/MM/yyyy},{item.RaisedBy}");
                }

                var fileName = $"CompounderInventoryReport_{fromDate:ddMMyyyy}_to_{toDate:ddMMyyyy}.csv";
                return File(System.Text.Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", fileName);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ExportCompounderInventoryReport Error: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while exporting the inventory report." });
            }
        }
    }
}