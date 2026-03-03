using ELKH.Repositories;
using ELKH.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ELKH.Controllers
{
    //[Authorize(Roles = "Admin")]
    [Authorize]
    public class AdminRoleController : Controller

    {
        private readonly Role_repo _roleRepo;
        private readonly UserRole_repo _userRoleRepo;

        public AdminRoleController(Role_repo roleRepo, UserRole_repo userRoleRepo)
        {
            _roleRepo = roleRepo;
            _userRoleRepo = userRoleRepo;
        }

        // GET: AdminRoleController/Create Roles
        public ActionResult ListRoles()
        {

            List<RoleVM> roleVM = new List<RoleVM>();
            return View(roleVM);
        }

        // GET: AdminRoleController/Create Role
        public ActionResult CreateRole()
        {
            return View();
        }
        // GET: AdminRoleController/Create Role
        public ActionResult EditRole(int roleId)
        {
            return View();
        }
        // GET: AdminRoleController/Create Role
        public ActionResult DeleteRole(int roleId)
        {
            return View();
        }

        //get: AdminRoleController/AssignRoles to users
        public ActionResult AssignRoles()
        {
            return View();
        }

        // POST: AdminController/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(IFormCollection collection)
        {
            try
            {
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return View();
            }
        }


        // POST: AdminController/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(int id, IFormCollection collection)
        {
            try
            {
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return View();
            }
        }


        // POST: AdminController/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id, IFormCollection collection)
        {
            try
            {
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return View();
            }
        }
    }
}
