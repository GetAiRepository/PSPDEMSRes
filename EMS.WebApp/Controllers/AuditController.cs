using EMS.WebApp.Data;
using EMS.WebApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EMS.WebApp.Controllers
{
    [Authorize]
    public class AuditController : Controller
    {
        private readonly IAuditService _auditService;
        private readonly ILogger<AuditController> _logger;

        public AuditController(IAuditService auditService, ILogger<AuditController> logger)
        {
            _auditService = auditService;
            _logger = logger;
        }

        // GET: Audit
        public async Task<IActionResult> Index(AuditFilterViewModel filter)
        {
            try
            {
                await _auditService.LogUserActionAsync("ViewAuditTrail", "Audit", "Viewed audit trail index");

                var auditTrails = await _auditService.GetAuditTrailAsync(
                    filter.TableName, filter.UserId, filter.FromDate, filter.ToDate, filter.Page, filter.PageSize);

                ViewBag.Filter = filter;
                return View(auditTrails);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading audit trail");
                TempData["Error"] = "Error loading audit trail.";
                return View(new List<AuditTrailViewModel>());
            }
        }

        // GET: Audit/UserActions
        public async Task<IActionResult> UserActions(AuditFilterViewModel filter)
        {
            try
            {
                await _auditService.LogUserActionAsync("ViewUserActions", "Audit", "Viewed user action logs");

                var userActions = await _auditService.GetUserActionLogsAsync(
                    filter.UserId, filter.Operation, filter.FromDate, filter.ToDate, filter.Page, filter.PageSize);

                ViewBag.Filter = filter;
                return View(userActions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading user action logs");
                TempData["Error"] = "Error loading user action logs.";
                return View(new List<UserActionLogViewModel>());
            }
        }

        // GET: Audit/Summary
        public async Task<IActionResult> Summary(DateTime? fromDate = null, DateTime? toDate = null)
        {
            try
            {
                await _auditService.LogUserActionAsync("ViewAuditSummary", "Audit", "Viewed audit summary");

                var summary = await _auditService.GetAuditSummaryAsync(fromDate, toDate);

                ViewBag.FromDate = fromDate;
                ViewBag.ToDate = toDate;
                return View(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading audit summary");
                TempData["Error"] = "Error loading audit summary.";
                return View(new AuditSummaryViewModel());
            }
        }

        // GET: Audit/EntityHistory
        public async Task<IActionResult> EntityHistory(string tableName, string primaryKey)
        {
            try
            {
                if (string.IsNullOrEmpty(tableName) || string.IsNullOrEmpty(primaryKey))
                {
                    TempData["Error"] = "Table name and primary key are required.";
                    return RedirectToAction(nameof(Index));
                }

                await _auditService.LogUserActionAsync("ViewEntityHistory", "Audit",
                    $"Viewed entity history for {tableName}:{primaryKey}");

                var history = await _auditService.GetEntityAuditHistoryAsync(tableName, primaryKey);

                ViewBag.TableName = tableName;
                ViewBag.PrimaryKey = primaryKey;
                return View(history);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading entity history for {TableName}:{PrimaryKey}", tableName, primaryKey);
                TempData["Error"] = "Error loading entity history.";
                return RedirectToAction(nameof(Index));
            }
        }

        // API endpoint for getting audit details
        [HttpGet]
        public async Task<IActionResult> GetAuditDetails(int auditId)
        {
            try
            {
                var auditTrails = await _auditService.GetAuditTrailAsync();
                var audit = auditTrails.FirstOrDefault(a => a.AuditId == auditId);

                if (audit == null)
                {
                    return Json(new { success = false, message = "Audit record not found." });
                }

                return Json(new { success = true, data = audit });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting audit details for ID: {AuditId}", auditId);
                return Json(new { success = false, message = "Error loading audit details." });
            }
        }
    }
}