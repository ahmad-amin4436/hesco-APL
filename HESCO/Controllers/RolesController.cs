using Dapper;
using HESCO.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using MySql.Data.MySqlClient;
using System.Data;
using System.Drawing.Printing;
using System.Runtime.Intrinsics.X86;
using X.PagedList.Extensions;

namespace HESCO.Controllers
{
    [AuthorizeUserEx]
    public class RolesController : Controller
    {
        private readonly IConfiguration _configuration;
        public RolesController(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        [HttpGet]
        public async Task<IActionResult> CreateRole()
        {
            using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                // Query to get all menus, including the controller if needed
                //string menuQuery = "SELECT id, title, parent_id AS ParentId, controller FROM user_menu_new";
                //var allMenus = (await db.QueryAsync<MenuItems>(menuQuery)).ToList();

                // Query to get actions for each controller
                //string actionQuery = @"
                //SELECT id,parent_id,controller, action
                //FROM rbac
                //where controller like '%Complaint%'
                //Order by controller, action";
                string actionQuery = @"
                SELECT id,parent_id,controller, action
                FROM rbac_new
                WHERE is_active=1";

                var allActions = (await db.QueryAsync<ActionInfo>(actionQuery)).ToList();

                foreach (var item in allActions)
                {
                    item.SubActions = allActions.Where(x => x.Parent_id == item.Id).OrderBy(x => x.Action).ToList();
                    foreach (var subItem in item.SubActions)
                    {
                        subItem.SubActions = allActions.Where(y => y.Parent_id == subItem.Id).OrderBy(y => y.Controller).ThenBy(y => y.Action).ToList();
                    }
                }
                allActions.RemoveAll(y => y.Parent_id > 0);

                // Separate parent menus and child menus
                //var parentMenus = allMenus.Where(m => m.ParentId == 0).ToList();
                //var childMenus = allMenus.Where(m => m.ParentId != 0).ToList();
                //var menuGroups = parentMenus.Select(parent => new MenuGroup
                //{
                //    Parent = parent,
                //    Children = childMenus.Where(child => child.ParentId == parent.Id).ToList(),
                //    Actions = allActions.Where(a => a.Controller == parent.Controller).ToList()
                //}).ToList();

                //ViewBag.MenuGroups = menuGroups;

                return View(allActions);
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateRole(string name, int[] selectedMenuIds)
        {
            // Ensure model is valid
            try
            {
                using (var connection = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                {
                    await connection.OpenAsync();

                    // Use parameterized query to avoid SQL injection
                    string insertQuery = @"
                        INSERT INTO roles (name)
                        VALUES (@Name)";

                    using (var command = new MySqlCommand(insertQuery, connection))
                    {
                        command.Parameters.AddWithValue("@Name", name);
                        await command.ExecuteNonQueryAsync();
                        var roleId = (int)command.LastInsertedId;

                        if (selectedMenuIds != null)
                        {
                            foreach (var menuId in selectedMenuIds)
                            {
                                command.CommandText = $@"INSERT INTO roles_permissions(role_id,rbac_id) VALUES (@role_id,@rbac_id)";
                                command.Parameters.Clear();
                                command.Parameters.AddWithValue("@role_id", roleId);
                                command.Parameters.AddWithValue("@rbac_id", menuId);
                                await command.ExecuteNonQueryAsync();
                            }
                        }
                    }
                }

                return RedirectToAction("ViewRoles");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return StatusCode(500, "Internal server error while processing request.");
            }
        }
        public async Task<IActionResult> ViewRoles(int? page = 1)
        {
            using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                var query = @"
                SELECT  DISTINCT
                    id, name, status FROM roles";

                var rolesData = await db.QueryAsync<RoleMenuViewModel>(query);
                var rolesPagedList = rolesData.ToPagedList(page ?? 1, 10);

                return View(rolesPagedList);

            }
        }

        [HttpGet]

        public async Task<IActionResult> ViewRoleDetail(int Id)
        {
            using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                string viewQuery = @"
        SELECT 
            r.id, 
            r.name AS Name, 
            COALESCE(GROUP_CONCAT(DISTINCT rb.action ORDER BY rb.action SEPARATOR ', '), 'No Actions Assigned') AS ActionTitle
        FROM roles r 
        LEFT JOIN roles_permissions um ON r.id = um.role_id 
        LEFT JOIN rbac_new rb ON um.rbac_id = rb.id 
        WHERE r.id = @Id AND r.status = 1
        GROUP BY r.id, r.name";

                var parameters = new { Id = Id };
                var roleData = await db.QueryAsync<RoleMenuViewModel>(viewQuery, parameters);

                if (!roleData.Any())
                {
                    return NotFound("No data found for the given role.");
                }

                return View(roleData.ToList());
            }
        }



        //public async Task<IActionResult> ViewRoleDetail(int Id)
        //{
        //    using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
        //    {
        //        //        string viewQuery = $@"
        //        //SELECT 
        //        //    r.id AS RoleId, 
        //        //    r.name AS Name, 
        //        //    um.rbac_id AS RBACId
        //        //FROM roles r
        //        //LEFT JOIN roles_permissions um ON r.id = um.role_id
        //        //WHERE r.id = {Id}";  
        //        string viewQuery = @"
        //SELECT DISTINCT
        //    r.id, 
        //    r.name AS Name, 
        //    GROUP_CONCAT(DISTINCT rb.action ORDER BY rb.action SEPARATOR ', ') AS ActionTitle
        //FROM roles r
        //LEFT JOIN roles_permissions um ON r.id = um.role_id 
        //LEFT JOIN rbac_new rb ON  um.rbac_id  = rb.id 
        //WHERE r.id = @Id
        //GROUP BY r.id, r.name";
        //        var parameters = new { Id = Id };
        //        var rolesData = await db.QueryAsync<RoleMenuViewModel>(viewQuery, parameters);

        //        if (!rolesData.Any())
        //        {
        //            return NotFound("No data found for the given role.");
        //        }

        //        // Convert to a list of RoleMenuViewModel
        //        var roleDetailsList = rolesData.ToList();
        //        //var roleDetailsList = rolesData.GroupBy(x => x.RoleId)
        //        //    .Select(g => new RoleMenuViewModel
        //        //    {
        //        //        Id = g.Key,
        //        //        Name = g.First().RoleName,
        //        //        RBACId = g.Where(x => x.RBACId != null).Select(x => (int)x.RBACId).Distinct().ToList()
        //        //    })
        //        //    .ToList();

        //        // Apply pagination to the list of role details


        //        return View(roleDetailsList);
        //    }
        //}

        [HttpGet]
        public IActionResult ConfirmRoleDelete(int id)
        {
            using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                var sql = "SELECT id, name, status FROM roles WHERE id = @Id";
                var role = db.QueryFirstOrDefault<RoleMenuViewModel>(sql, new { Id = id });

                if (role == null)
                {
                    return NotFound();
                }

                return View(role);
            }
        }


        [HttpPost]
        public IActionResult DeleteRole(int id)
        {
            using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                db.Open();
                using (var transaction = db.BeginTransaction())
                {
                    try
                    {
                        // Fetch role name and current status
                        var currentStatusQuery = "SELECT name, status FROM roles WHERE id = @Id";
                        var role = db.QuerySingleOrDefault<(string Name, int? Status)>(currentStatusQuery, new { Id = id }, transaction);

                        if (role.Status == null)
                        {
                            return Json(new { success = false, message = "Role not found." });
                        }

                        var newStatus = role.Status == 1 ? 0 : 1;
                        var statusText = newStatus == 1 ? "Active" : "Inactive";

                        // Update status in the database
                        var updateStatusQuery = "UPDATE roles SET status = @NewStatus WHERE id = @Id";
                        var rowsAffected = db.Execute(updateStatusQuery, new { NewStatus = newStatus, Id = id }, transaction);

                        if (rowsAffected > 0)
                        {
                            transaction.Commit();
                            return Json(new { success = true, message = $"The {role.Name} status has been updated to {statusText}.", newStatusText = statusText, newStatus = newStatus });
                        }
                        else
                        {
                            transaction.Rollback();
                            return Json(new { success = false, message = "Failed to update role status." });
                        }
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        return Json(new { success = false, message = $"Error: {ex.Message}" });
                    }
                }
            }
        }


        //[HttpPost]
        //public IActionResult DeleteRole(int id)
        //{
        //    using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
        //    {
        //        db.Open();
        //        using (var transaction = db.BeginTransaction())
        //        {
        //            try
        //            {
        //                var currentStatusQuery = "SELECT name , status FROM roles WHERE id = @Id";
        //                var role = db.QuerySingleOrDefault<(string Name, int? Status)>(currentStatusQuery, new { Id = id }, transaction);

        //                if (role.Status == null)
        //                {
        //                    return NotFound();
        //                }
        //                var newStatus = role.Status == 1 ? 0 : 1;
        //                var statusText = newStatus == 1 ? "Active" : "Inactive";


        //                var updateStatusQuery = "UPDATE roles SET status = @NewStatus WHERE id = @Id";
        //                var rowsAffected = db.Execute(updateStatusQuery, new { NewStatus = newStatus, Id = id }, transaction);

        //                Console.WriteLine($"Role ID: {id}, Role Name: {role.Name}, Old Status: {role.Status}, New Status: {newStatus}, Rows Affected: {rowsAffected}");

        //                if (rowsAffected > 0)
        //                {
        //                    transaction.Commit();
        //                    TempData["Message"] = $"The \"{role.Name}\" status has been updated to {statusText}.";
        //                    return RedirectToAction("ViewRoles");
        //                }
        //                else
        //                {
        //                    transaction.Rollback();
        //                    return NotFound();
        //                }
        //            }
        //            catch (Exception ex)
        //            {
        //                transaction.Rollback();
        //                Console.WriteLine($"Error: {ex.Message}");
        //                return StatusCode(500, "An error occurred while updating the role status");
        //            }
        //        }
        //    }
        //}



        //[HttpPost]
        //public IActionResult DeleteRole(int id)
        //{
        //    using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
        //    {
        //        db.Open();
        //        using (var transaction = db.BeginTransaction())
        //        {

        //            try
        //            {
        //                //var deleteRoleQuery = "DELETE FROM roles WHERE id = @Id";
        //                //var rowsAffected = db.Execute(deleteRoleQuery, new { Id = id }, transaction);
        //                var currentStatusQuery = "SELECT status FROM roles WHERE id = @Id";
        //                var currentStatus = db.Execute(currentStatusQuery, new { Id = id }, transaction);

        //                var newStatus = currentStatus == 1 ? 0 : 1;


        //                var updateStatusQuery = "UPDATE roles  SET status = @NewStatus WHERE id = @Id";
        //                var rowsAffected = db.Execute(updateStatusQuery, new { NewStatus = newStatus, Id = id }, transaction);

        //                transaction.Commit();

        //                if (rowsAffected > 0)
        //                {
        //                    return RedirectToAction("ViewRoles");
        //                }
        //                else
        //                {
        //                    return NotFound();
        //                }
        //            }
        //            catch (Exception)
        //            {
        //                transaction.Rollback();
        //                return StatusCode(500, "An error occured while deleting the role");
        //            }
        //        }
        //    }
        //}



        [HttpGet]

        //public async Task<IActionResult> ViewRoleDetail(int id)
        //{
        //    using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
        //    {

        //        string viewQuery = @"
        //         SELECT 
        //         r.id,
        //         r.name AS Name,
        //         um.rbac_id AS RBACId
        //     FROM roles r
        //     LEFT JOIN roles_permissions um ON r.id = um.role_id";

        //        var rolesData = (await db.QueryAsync<RoleMenuViewModel>(viewQuery, new { Id = id })).ToList();

        //        var roleDetails = new RoleMenuViewModel
        //        {
        //            Id = id,
        //            Name = rolesData.FirstOrDefault()?.Name,
        //            RBACId = rolesData.Select(x => x.RBACId).ToList()
        //        };


        //        return View(rolesData);
        //    }
        //}


        [HttpGet]
        public async Task<IActionResult> EditRoleDetails(int id)
        {
            using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                // 1. Fetch the role name (optional, if you display it)
                string roleQuery = "SELECT name FROM roles WHERE id = @Id";
                var roleName = await db.QueryFirstOrDefaultAsync<string>(roleQuery, new { Id = id });

                // 2. Get assigned IDs from roles_permissions
                string assignedActionsQuery = "SELECT rbac_id FROM roles_permissions WHERE role_id = @Id";
                var assignedActionIds = (await db.QueryAsync<int>(assignedActionsQuery, new { Id = id })).ToList();

                // 3. Fetch all possible actions from rbac_new
                string actionQuery = @"
                SELECT 
                    rb.id,
                    rb.parent_id,
                    rb.controller,
                    rb.action,
                    '' as name  -- or rb.name if you have a name column
                FROM rbac_new rb
                WHERE rb.is_active = 1
            ";

                var allActions = (await db.QueryAsync<ActionInfo>(actionQuery)).ToList();

                // 4. Recursively add parent IDs so if a child is assigned, 
                //    we also treat the parent as assigned (in memory)
                var visited = new HashSet<int>();
                foreach (var childId in assignedActionIds.ToList())
                {
                    AddParentIds(childId, allActions, assignedActionIds, visited);
                }

                // 5. Build the hierarchical structure (SubActions)
                //    For each item, find its immediate children
                foreach (var item in allActions)
                {
                    item.SubActions = allActions
                        .Where(x => x.Parent_id == item.Id)
                        .OrderBy(x => x.Action) // or any desired ordering
                        .ToList();
                }

                // 6. Keep only top-level items in allActions (Parent_id == null)
                //    The rest are accessible via item.SubActions
                allActions.RemoveAll(a => a.Parent_id != null);

                // 7. Pass data to the View
                ViewBag.Name = roleName;
                ViewBag.AssignedActionIds = assignedActionIds;

                return View(allActions);
            }
        }


        // Helper to climb up the parent chain and add each parent to assignedActionIds
        private void AddParentIds(
            int childId,
            List<ActionInfo> allActions,
            List<int> assignedIds,
            HashSet<int> visited)
        {
            // Prevent infinite loops if there's a cycle
            if (visited.Contains(childId)) return;
            visited.Add(childId);

            var child = allActions.FirstOrDefault(a => a.Id == childId);
            if (child == null) return;

            // If parent_id is null, that means top-level => no further parent
            if (child.Parent_id == null)
                return;

            int parentId = child.Parent_id.Value;
            if (!assignedIds.Contains(parentId))
            {
                assignedIds.Add(parentId);
            }

            // Recurse up
            AddParentIds(parentId, allActions, assignedIds, visited);
        }


        [HttpPost]
        public async Task<IActionResult> EditRoleDetails(RoleMenuEditModel model, int[] selectedMenuIds, int[] selectedActionsIds)
        {
            //if (!ModelState.IsValid)
            //{
            //    return View(model);
            //}

            try
            {
                using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                {
                    // 1. (Optional) Update the role's name in the roles table
                    string updateRoleQuery = @"
                UPDATE roles
                SET name = @Name
                WHERE Id = @Id";

                    await db.ExecuteAsync(updateRoleQuery, new
                    {
                        Name = model.Name,
                        Id = model.Id
                    });

                    // 2. Remove old permissions from roles_permissions
                    string deletePermissionsQuery = "DELETE FROM roles_permissions WHERE role_id = @RoleId";
                    await db.ExecuteAsync(deletePermissionsQuery, new { RoleId = model.Id });

                    // 3. Fetch all actions (with nullable parent_id) for recursion
                    string actionQuery = "SELECT id, parent_id FROM rbac_new WHERE is_active = 1";
                    var allActions = (await db.QueryAsync<ActionInfo>(actionQuery)).ToList();

                    // 4. Combine the selected IDs (menus + actions) if you want them all in the same table
                    var finalSelectedIds = selectedMenuIds.Concat(selectedActionsIds).ToList();

                    // 5. Recursively add parents so if a child is selected, 
                    //    the parent(s) are also included in finalSelectedIds.
                    var visited = new HashSet<int>();
                    foreach (var childId in finalSelectedIds.ToList())
                    {
                        AddParentIds(childId, allActions, finalSelectedIds, visited);
                    }

                    // 6. Insert the final set of IDs into roles_permissions
                    string insertPermissionsQuery = @"
                INSERT INTO roles_permissions (role_id, rbac_id)
                VALUES (@RoleId, @RbacId)";

                    foreach (var rbacId in finalSelectedIds.Distinct())
                    {
                        await db.ExecuteAsync(insertPermissionsQuery, new { RoleId = model.Id, RbacId = rbacId });
                    }

                    // 7. Done: redirect to some listing page
                    return RedirectToAction("ViewRoles");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return StatusCode(500, "Internal server error while processing request.");
            }
        }


        //[HttpGet]
        //public async Task<IActionResult> EditRoleDetails(int id)
        //{
        //    using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
        //    {
        //        string roleQuery = "SELECT name FROM roles WHERE id = @Id";
        //        var roleName = await db.QueryFirstOrDefaultAsync<string>(roleQuery, new { Id = id });

        //        string assignedActionsQuery = "SELECT rbac_id FROM roles_permissions WHERE role_id = @Id";
        //        var assignedActionIds = (await db.QueryAsync<int>(assignedActionsQuery, new { Id = id })).ToList();

        //        string actionQuery = @"
        //        SELECT rb.id, rb.parent_id, rb.controller, rb.action,r.name
        //        FROM rbac_new rb
        //        LEFT JOIN roles r ON r.id=@Id
        //        WHERE is_active = 1";
        //        //string actionQuery = @"
        //        //SELECT r.id,r.name,rba.parent_id,rba.controller, rba.action
        //        //FROM roles r 
        //        //LEFT JOIN roles_permissions um ON r.id = um.role_id 
        //        //LEFT JOIN rbac rba ON rba.id=um.rbac_id
        //        //where r.Id = @Id";

        //        var allActions = (await db.QueryAsync<ActionInfo>(actionQuery, new { Id = id })).ToList();
        //        //ViewBag.Name =allActions.Name;
        //        //var allActions = (await db.QueryAsync<ActionInfo>(actionQuery)).ToList();

        //        foreach (var item in allActions)
        //        {
        //            item.SubActions = allActions.Where(x => x.Parent_id == item.Id).OrderBy(x => x.Action).ToList();
        //            foreach (var subItem in item.SubActions)
        //            {
        //                subItem.SubActions = allActions.Where(y => y.Parent_id == subItem.Id).OrderBy(y => y.Controller).ThenBy(y => y.Action).ToList();
        //            }
        //        }
        //        allActions.RemoveAll(y => y.Parent_id > 0);

        //        ViewBag.Name = roleName;
        //        ViewBag.AssignedActionIds = assignedActionIds;

        //        return View(allActions);
        //    }
        //}

        //[HttpPost]
        //public async Task<IActionResult> EditRoleDetails(RoleMenuEditModel model, int[] selectedMenuIds, int[] selectedActionsIds)
        //{
        //    if (ModelState.IsValid)
        //    {
        //        string menuIds = string.Join(",", selectedMenuIds);
        //        string actionIds = string.Join(",", selectedActionsIds);

        //        try
        //        {
        //            using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
        //            {
        //                var updateQuery = @"
        //                UPDATE roles
        //                SET name = @Name, menu_id = @MenuId, action_id = @ActionId
        //                WHERE Id = @Id";

        //                await db.ExecuteAsync(updateQuery, new
        //                {
        //                    Name = model.Name,
        //                    MenuId = menuIds,
        //                    ActionId = actionIds,
        //                    Id = model.Id
        //                });

        //                return RedirectToAction("ViewRoles");
        //            }
        //        }
        //        catch (Exception ex)
        //        {
        //            Console.WriteLine($"Error: {ex.Message}");
        //            return StatusCode(500, "Internal server error while processing request.");
        //        }
        //    }
        //    return View(model);
        //}


    }
}