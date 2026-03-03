using ELKH.Repositories;
using ELKH.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ELKH.Controllers
{
    //[Authorize(Roles = "Admin")]
    [Authorize]
    public class AdminController : Controller

    {
        private readonly Role_repo _roleRepo;
        private readonly UserRole_repo _userRoleRepo;

        public AdminController(Role_repo roleRepo, UserRole_repo userRoleRepo)
        {
            _roleRepo = roleRepo;
            _userRoleRepo = userRoleRepo;
        }
        // GET: AdminController
        public ActionResult Index()
        {
            return View();
        }
        public IActionResult ManageSales()
        {
            return View();

        }

        public IActionResult ManageUserRole()
        {
            //var model = new RoleVM();
            //model.Roles = _roleRepo.GetAllRoles();

            //return View(model);
            return View();

        }
        public IActionResult ListOfAllUsers()
        {
            return View();
        }


        public IActionResult CustomerAccountDetails()
        {
            return View();

        }

        public IActionResult StaffAccountDetails()
        {
            return View();
        }


        }
}
