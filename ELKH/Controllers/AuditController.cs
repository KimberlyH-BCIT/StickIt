using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ELKH.Data;
using ELKH.Models;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace ELKH.Controllers
{
    /// <summary>
    /// Audit log viewer for administrative compliance and security monitoring.
    /// Provides filtering, pagination, and CSV export of all audit trail entries.
    /// </summary>
    /// <remarks>
    /// TABLE OF CONTENTS
    /// ================================================================================
    /// 1. Constructor & Dependencies
    /// 2. Audit Log Viewing
    ///    - Index()                               // GET: List audit entries with filters
    ///    - Details(id)                           // GET: View single audit entry
    /// ================================================================================
    /// 
    /// Features:
    /// - Date range filtering (from/to)
    /// - Actor filtering (username search)
    /// - Action filtering (action type search)
    /// - CSV export for compliance reporting
    /// - Pagination (50 entries per page)
    /// 
    /// Routes: /Admin/Audit/{action}
    /// Authorization: Admin role required
    /// </remarks>
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class AuditController : Controller
    {
        private readonly ApplicationDbContext _db;

        /// <summary>
        /// Initializes a new instance of <see cref="AuditController"/> with the required database context.
        /// </summary>
        /// <param name="db">The EF Core database context used to query <see cref="AuditEntryModel"/> records.</param>
        public AuditController(ApplicationDbContext db) 
        { 
            _db = db; 
        }

        /// <summary>
        /// Displays a paginated, filterable list of audit log entries.
        /// When <c>export=csv</c> is present in the query string, streams all matching
        /// entries as a UTF-8 CSV file instead of rendering the view.
        /// </summary>
        /// <param name="page">1-based page index for pagination (default: 1).</param>
        /// <param name="pageSize">Number of records per page (default: 50).</param>
        /// <returns>
        /// The audit index view populated with the current page of entries, or a
        /// <c>text/csv</c> file download containing every matching entry when
        /// <c>export=csv</c> is supplied.
        /// </returns>
        /// <remarks>
        /// All filters are optional and combinable. Filters are applied as additional
        /// <c>WHERE</c> clauses on a single composable <see cref="IQueryable{T}"/>, so only
        /// one database round-trip occurs regardless of how many filters are active.
        ///
        /// Supported query string parameters:
        /// - <c>from</c>  : Inclusive start date/time (any format parseable by <see cref="DateTime.TryParse(string, out DateTime)"/>).
        /// - <c>to</c>    : Inclusive end date/time.
        /// - <c>actor</c> : Case-sensitive substring match on the actor username.
        /// - <c>action</c>: Case-sensitive substring match on the action type.
        /// - <c>export</c>: Set to <c>csv</c> to download all filtered results as a file.
        ///
        /// Example: <c>/Admin/Audit?from=2026-01-01&amp;actor=admin&amp;export=csv</c>
        /// </remarks>
        public async Task<IActionResult> Index(int page = 1, int pageSize = 50)
        {
            // Start with the full AuditEntryModel set as a composable IQueryable.
            // Filters are appended as deferred WHERE clauses — no query is sent to the
            // database until ToListAsync() or CountAsync() is called further below.
            var q = _db.AuditEntries.AsQueryable();
            var req = Request.Query;

            // --- Date range filters (both boundaries are inclusive) ---
            // TryParse guards against malformed date strings without throwing exceptions.
            if (req.ContainsKey("from"))
            {
                if (DateTime.TryParse(req["from"], out var from))
                    q = q.Where(a => a.Timestamp >= from);
            }
            if (req.ContainsKey("to"))
            {
                if (DateTime.TryParse(req["to"], out var to))
                    q = q.Where(a => a.Timestamp <= to);
            }
            
            // --- Actor filter: substring match on the username who performed the action ---
            // Empty/whitespace values are ignored to avoid unintentionally filtering everything.
            if (req.ContainsKey("actor"))
            {
                var actor = req["actor"].ToString();
                if (!string.IsNullOrEmpty(actor))
                    q = q.Where(a => a.Actor.Contains(actor));
            }

            // --- Action filter: substring match on the action type (e.g. "Delete", "Login") ---
            if (req.ContainsKey("action"))
            {
                var action = req["action"].ToString();
                if (!string.IsNullOrEmpty(action))
                    q = q.Where(a => a.Action.Contains(action));
            }
            
            // --- CSV export path ---
            // Checked before pagination so the export always contains every matching record,
            // not just the current page. StringBuilder is used for efficient string concatenation
            // when iterating over potentially thousands of rows.
            if (req.ContainsKey("export") && req["export"].ToString() == "csv")
            {
                var all = await q.OrderByDescending(a => a.Timestamp).ToListAsync();
                var csv = new StringBuilder();

                // RFC 4180 compliant header row.
                csv.AppendLine("Timestamp,Actor,Action,Reason,AffectedKeys,Details");

                // All string fields are wrapped in double-quotes.
                // Any embedded double-quotes in the Details field are replaced with two single-quotes
                // to prevent breaking the CSV column structure.
                // The "u" format specifier outputs a sortable UTC timestamp (e.g. 2026-01-15 09:30:00Z).
                foreach (var a in all)
                {
                    csv.AppendLine($"\"{a.Timestamp:u}\",\"{a.Actor}\",\"{a.Action}\",\"{a.Reason}\",{a.AffectedKeysCount},\"{a.Details.Replace("\"", "''")}\"");
                }
                
                return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", "audit.csv");
            }
            
            // --- Pagination ---
            // CountAsync() issues a SELECT COUNT(*) using the already-composed query filters.
            // Math.Ceiling ensures a partial final page is still counted (e.g. 101 items at
            // 50 per page yields 3 pages, not 2).
            var total = await q.CountAsync();
            var totalPages = (int)Math.Ceiling(total / (double)pageSize);

            // Skip((page - 1) * pageSize) offsets to the correct page start;
            // Take(pageSize) limits the result set to one page of records.
            var items = await q
                .OrderByDescending(a => a.Timestamp)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Page = page;
            ViewBag.TotalPages = totalPages;
            
            return View(items);
        }

        /// <summary>
        /// Displays the full detail view for a single audit log entry.
        /// </summary>
        /// <param name="id">The primary key of the <see cref="AuditEntryModel"/> to retrieve.</param>
        /// <returns>
        /// The audit details view populated with the matching entry, or
        /// <see cref="NotFoundResult"/> if no entry with the given <paramref name="id"/> exists.
        /// </returns>
        public async Task<IActionResult> Details(int id)
        {
            var entry = await _db.AuditEntries.FindAsync(id);
            if (entry is null) return NotFound();
            return View(entry);
        }
    }
}
