using ELKH.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace ELKH.Controllers
{
    public class TransactionController : Controller
    {
        private readonly TransactionRepo _repo;
        public TransactionController(TransactionRepo repo)
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
