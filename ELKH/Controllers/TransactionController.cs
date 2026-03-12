using ELKH.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ELKH.Controllers
{
    [Authorize(Roles = "Admin,Manager")]
    public class TransactionController : Controller
    {
        private readonly ITransactionRepo _repo;
        public TransactionController(ITransactionRepo repo)
        {
            _repo = repo;
        }
        public async Task<IActionResult> Index()
        {
            var transactions = await _repo.GetAllTransactions();
            return View(transactions);
        }
    }
}
