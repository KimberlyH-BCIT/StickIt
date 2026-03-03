using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ELKH.Controllers
{
    [Authorize(Roles = "Manager")]
    public class ManagerController : Controller
    {
        // GET: ManagerController
        public ActionResult Index()
        {
            return View();
        }
        public ActionResult ListOfProducts()
        {
            return View();
        }
        public ActionResult AddNewProduct()
        {
            return View();
        }

        public ActionResult ProductDetails(int id)
        {
            return View();
        }

        public ActionResult UpdateProductDetails(int id)
        {
            return View();
        }
        public ActionResult DeleteProduct(int id)
        {
            return View();
        }

        public ActionResult ListOfStaffAccount()
        {
            return View();
        }
        public ActionResult AllTransactions()
        {
            return View();
        }
    }

}
        