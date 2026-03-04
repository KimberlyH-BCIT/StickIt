using ELKH.Repositories;
using ELKH.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ELKH.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly IRole_repo _roleRepo;


        public AdminController(IRole_repo roleRepo)
        {
            _roleRepo = roleRepo;
        }
        // GET: AdminController
        public ActionResult Index()
        {
            return View();
        }
        public IActionResult ListUsers()
        {
            return View();
        }

        public IActionResult ManageUserRole()
        {
            ManageRoleVM manageRoleVM = new ManageRoleVM();
            
            manageRoleVM.Roles = _roleRepo.GetAllRoles();

            return View(manageRoleVM);
        }



        public IActionResult CustomerAccountDetails()
        {
            return View();

        }

        public IActionResult StaffAccountDetails()
        {
            return View();
        }

        public IActionResult ManageSales()
        {
            return View();

        }
    }
}
