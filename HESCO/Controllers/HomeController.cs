using BCrypt.Net;
using Dapper;
using HESCO.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using MySql.Data.MySqlClient;
using NuGet.Protocol.Plugins;
using Org.BouncyCastle.Crypto.Generators;
using System;
using System.Data;
using System.Diagnostics;
using System.Security.Cryptography;
using X.PagedList.Extensions;

namespace HESCO.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        private readonly IConfiguration _configuration;
        public HomeController(ILogger<HomeController> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public IActionResult Index()
        {

            return View();
        }
        [AuthorizeUserEx]
        [HttpGet]
        public async Task<IActionResult> CreateUser()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var loggedInUserRole = HttpContext.Session.GetString("UserRole");

            using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                string rolesQuery = "SELECT Id as value, Name as text FROM roles";
                var rolesData = (await db.QueryAsync<DTODropdown>(rolesQuery)).Distinct().ToList();

                // If the logged-in user is NOT a Super Admin (role ID != 1), remove "Super Admin" from the list
                //if (loggedInUserRole != "1")
                //{
                //    rolesData = rolesData.Where(r => r.Value != "1").ToList();
                //}

                ViewBag.SelectRole = new SelectList(rolesData, "Value", "Text");

                string teamQuery = "SELECT Id as value, Title as text FROM user_groups";
                var teamData = (await db.QueryAsync<DTODropdown>(teamQuery)).Distinct().ToList();
                ViewBag.SelectTeam = new SelectList(teamData, "Value", "Text");
            }

            return View();
        }
        [HttpGet]
        public IActionResult GetUserRbacTree()
        {
            var tree = GetUserTree();
            return Json(tree); // return as JSON for AJAX
        }
        [HttpGet]
        public IActionResult GetRightofRoleTree(int role_id)
        {
            using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
               

                // 2️⃣ Get all RBAC IDs assigned to the role
                string roleRightsQuery = "SELECT rbac_id FROM roles_permissions WHERE role_id = @RoleID";
                var assignedIds = db.Query<int>(roleRightsQuery, new { RoleID = role_id }).ToList();

                // 3️⃣ Return both as JSON
                return Json(new { assignedIds = assignedIds });
            }
        }

        public IEnumerable<dynamic> GetUserTree()
        {
            using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                string sql = @"
WITH RECURSIVE rbac_tree AS (
    -- Anchor member: direct permissions for the user
    SELECT rba.id, rba.controller, rba.action, rba.parent_id
    FROM USER u
    JOIN roles_permissions r ON u.user_role = r.role_id
    JOIN rbac_new rba ON r.rbac_id = rba.id
    WHERE rba.is_active = 1  

    UNION ALL

    -- Recursive member: children of previous level
    SELECT child.id, child.controller, child.action, child.parent_id
    FROM rbac_new child
    JOIN rbac_tree parent ON child.parent_id = parent.id
    WHERE child.is_active = 1
)
SELECT id, controller, action, parent_id
FROM rbac_tree
GROUP BY id, controller, action, parent_id
ORDER BY parent_id, id;";

                return db.Query(sql);
            }
        }

        [HttpGet]
        public IActionResult ShowActionRights(int role_id)
        {
            using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                string rolesQuery = @"SELECT rn.id AS id, rn.action
                                    FROM roles_permissions rp
                                    JOIN rbac_new rn 
                                        ON rn.id = rp.rbac_id
                                    JOIN user_menu_new um 
		                                ON um.link != rn.action
                                    WHERE (rn.parent_id != 0 OR rn.parent_id IS NOT NULL) AND rp.role_id = @RoleID
                                    GROUP BY rn.id,rn.action;";
                var actionData = (db.Query(rolesQuery, new { RoleID = role_id }))
                                     .ToList();

                return new JsonResult(actionData); // ASP.NET Core handles GET JSON safely
            }
        }

        [HttpGet]
        public IActionResult GetRoleRights(int role_id)
        {
            using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                var parameters = new DynamicParameters();
                parameters.Add("p_role_id", role_id, DbType.Int32, ParameterDirection.Input);

                var result = db.Query(
                    "GetRoleRights", // Stored procedure name
                    parameters,
                    commandType: CommandType.StoredProcedure
                ).ToList();

                return new JsonResult(result); // ASP.NET Core handles GET JSON safely
            }
        }


        //[HttpGet]
        //public async Task<IActionResult> CreateUser()
        //{

        //    using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
        //    {                
        //        string rolesQuery = "SELECT Id as value, Name as text FROM roles";
        //        var rolesData = await db.QueryAsync<DTODropdown>(rolesQuery);
        //        ViewBag.SelectRole = new SelectList(rolesData.Distinct(), "Value", "Text");

        //        string teamQuery = "SELECT Id as value, Title as text FROM user_groups";
        //        var teamData = await db.QueryAsync<DTODropdown>(teamQuery);
        //        ViewBag.SelectTeam = new SelectList(teamData.Distinct(), "Value", "Text");                
        //    }

        //    return View();
        //}
        [HttpPost]
        public async Task<IActionResult> CreateUser(UserData userData)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var loggedInUserRole = HttpContext.Session.GetString("UserRole");
            userData.CreatedAt = int.Parse(DateTime.Now.ToString("yyyyMMdd"));
            userData.Password = BCrypt.Net.BCrypt.HashPassword(userData.Password);

            // Generate the auth key
            string authKey = GenerateAuthKey(userData.Username);

            string insertQuery = @"
            INSERT INTO user (username, auth_key, password_hash, name, email, created_at, contact_number, user_role, group_id,updated_at)
            VALUES (@Username, @AuthKey, @Password, @Name, @Email, @CreatedAt, @ContactNumber, @UserRole, @Team, @UpdatedAt)";

            if (ModelState.IsValid)
            {
                try
                {
                    using (MySqlConnection connection = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                    {
                        using (MySqlCommand command = new MySqlCommand(insertQuery, connection))
                        {
                            command.Parameters.Add("@Username", MySqlDbType.VarChar).Value = userData.Username;
                            command.Parameters.Add("@AuthKey", MySqlDbType.VarChar).Value = authKey;
                            command.Parameters.Add("@Password", MySqlDbType.VarChar).Value = userData.Password;
                            command.Parameters.Add("@Name", MySqlDbType.VarChar).Value = userData.Name;
                            command.Parameters.Add("@Email", MySqlDbType.VarChar).Value = userData.Email;
                            command.Parameters.Add("@CreatedAt", MySqlDbType.Int32).Value = userData.CreatedAt;
                            command.Parameters.Add("@ContactNumber", MySqlDbType.VarChar).Value = userData.ContactNumber;
                            command.Parameters.Add("@UserRole", MySqlDbType.Int32).Value = userData.UserRole;
                            command.Parameters.Add("@UpdatedAt", MySqlDbType.Int32).Value = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                            // Set the Team parameter to DBNull if the role is not "Team Lead" or "Team Member"
                            if (userData.UserRole == "3" || userData.UserRole == "4" || userData.UserRole == "19")
                            {
                                command.Parameters.Add("@Team", MySqlDbType.Int32).Value = userData.Team;
                            }
                            else
                            {
                                command.Parameters.Add("@Team", MySqlDbType.Int32).Value = DBNull.Value;
                            }
                            connection.Open();
                            await command.ExecuteNonQueryAsync();
                        }
                        var getIdQuery = "SELECT LAST_INSERT_ID();";

                        int newUserId = connection.ExecuteScalar<int>(getIdQuery);

                        foreach (var right in userData.SelectedRights)
                        {
                            var sql = @"
                                        UPDATE user_menu_new AS um
                                        JOIN rbac_new AS rn ON 
                                            um.controller = rn.controller
                                             AND um.link LIKE CONCAT('%', rn.action, '%')
                                        SET um.allow_access =
                                            CASE
                                                WHEN um.allow_access IS NULL OR um.allow_access = '' 
                                                    THEN @UserId
                                                WHEN FIND_IN_SET(@UserId, um.allow_access) = 0
                                                    THEN CONCAT(um.allow_access, ',', @UserId)
                                                ELSE um.allow_access
                                            END
                                        WHERE rn.id = @RightId;
                                    ";

                            connection.Execute(sql, new
                            {
                                UserId = newUserId.ToString(),
                                RightId = right.Id
                            });
                            // Insert into action_master
                            string insertSql = @"
        INSERT INTO action_master (action_id, action_name, user_id)
        VALUES (@ActionId, @ActionName, @UserId);";

                            connection.Execute(insertSql, new
                            {
                                ActionId = right.Id,
                                ActionName = right.Label,
                                UserId = newUserId.ToString()
                            });
                        }

                       


                    }
                }
                catch (Exception ex)
                {
                    // Add the error message to ModelState so it can be displayed in the view
                    ModelState.AddModelError(string.Empty, "Duplicate Username.");
                    Console.WriteLine(ex.Message);
                    using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                    {
                        string rolesQuery = "SELECT Id as value, Name as text FROM roles";
                        var rolesData = (await db.QueryAsync<DTODropdown>(rolesQuery)).Distinct().ToList();

                        // If the logged-in user is NOT a Super Admin (role ID != 1), remove "Super Admin" from the list
                        if (loggedInUserRole != "1")
                        {
                            rolesData = rolesData.Where(r => r.Value != "1").ToList();
                        }

                        ViewBag.SelectRole = new SelectList(rolesData, "Value", "Text");

                        string teamQuery = "SELECT Id as value, Title as text FROM user_groups";
                        var teamData = (await db.QueryAsync<DTODropdown>(teamQuery)).Distinct().ToList();
                        ViewBag.SelectTeam = new SelectList(teamData, "Value", "Text");
                    }
                    // Return the same view with the model to show the error message
                    return View(userData);
                }

                // If everything is successful, redirect to another action or view
                return RedirectToAction("ViewUser");
            }
            using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                string rolesQuery = "SELECT Id as value, Name as text FROM roles";
                var rolesData = (await db.QueryAsync<DTODropdown>(rolesQuery)).Distinct().ToList();

                // If the logged-in user is NOT a Super Admin (role ID != 1), remove "Super Admin" from the list
                if (loggedInUserRole != "1")
                {
                    rolesData = rolesData.Where(r => r.Value != "1").ToList();
                }

                ViewBag.SelectRole = new SelectList(rolesData, "Value", "Text");

                string teamQuery = "SELECT Id as value, Title as text FROM user_groups";
                var teamData = (await db.QueryAsync<DTODropdown>(teamQuery)).Distinct().ToList();
                ViewBag.SelectTeam = new SelectList(teamData, "Value", "Text");
            }
            // If ModelState is invalid, return the view with the current model and validation errors
            return View(userData);
        }
        private string GenerateAuthKey(string username)
        {
            string part1 = MD5Hash(DateTime.Now.ToString());
            string part2 = new Random().Next(2, 98).ToString("D2"); // Ensure two digits
            string part3 = MD5Hash(new Random().Next(3, 87).ToString());
            string part4 = MD5Hash(username);

            string key = "a" + part1 + part2 + part3 + part4;

            // Truncate or pad the key to ensure it is exactly 32 characters
            if (key.Length > 32)
            {
                key = key.Substring(0, 32);
            }
            else if (key.Length < 32)
            {
                key = key.PadRight(32, '0'); // Pad with '0' to make it 32 characters
            }

            return key;
        }

        private string MD5Hash(string input)
        {
            using (MD5 md5 = MD5.Create())
            {
                byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(input);
                byte[] hashBytes = md5.ComputeHash(inputBytes);
                return Convert.ToHexString(hashBytes).ToLower();
            }
        }
        [AuthorizeUserEx]
        [HttpGet]
        public async Task<IActionResult> ViewUserDetail(int id)
        {

            UserData userData = null;

            string selectQuery = @"
            SELECT u.id, u.username, u.password_hash, u.name, u.email, u.contact_number, 
                   r.name AS RoleName, g.title AS TeamTitle
            FROM user u
            JOIN roles r ON u.user_role = r.id
            LEFT JOIN user_groups g ON u.group_id = g.id
            WHERE u.id = @UserId";

            try
            {
                using (MySqlConnection connection = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                {
                    using (MySqlCommand command = new MySqlCommand(selectQuery, connection))
                    {
                        command.Parameters.Add("@UserId", MySqlDbType.Int32).Value = id;

                        connection.Open();
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                userData = new UserData
                                {
                                    UserId = reader["id"] != DBNull.Value ? Convert.ToInt32(reader["id"]) : 0,
                                    Username = reader["username"] != DBNull.Value ? reader["username"].ToString() : string.Empty,
                                    Password = reader["password_hash"] != DBNull.Value ? reader["password_hash"].ToString() : string.Empty,
                                    Name = reader["name"] != DBNull.Value ? reader["name"].ToString() : string.Empty,
                                    Email = reader["email"] != DBNull.Value ? reader["email"].ToString() : string.Empty,
                                    ContactNumber = reader["contact_number"] != DBNull.Value ? reader["contact_number"].ToString() : string.Empty,
                                    UserRole = reader["RoleName"] != DBNull.Value ? reader["RoleName"].ToString() : string.Empty, // Role Name
                                    Team = reader["TeamTitle"] != DBNull.Value ? reader["TeamTitle"].ToString() : "-" // Display dash if null
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log the exception (consider using a logging framework here)
                Console.WriteLine(ex.Message);
                // You might want to return an error view or a specific error message
                return StatusCode(500, "Internal server error.");
            }

            if (userData == null)
            {
                return NotFound();
            }

            return View(userData);
        }
        [AuthorizeUserEx]
        [HttpGet]
        public async Task<IActionResult> ViewUser(string usernameFilter, string userRoleFilter, string teamFilter, int? page)
        {
            int pageSize = 10;
            int pageNumber = page ?? 1;

            List<UserData> userDatas = new List<UserData>();

            string selectQuery = @"
            SELECT u.id, u.username, u.name, r.name AS RoleName, g.title AS TeamTitle 
            FROM user u
            JOIN roles r ON u.user_role = r.id
            LEFT JOIN user_groups g ON u.group_id = g.id
            WHERE (@username IS NULL OR u.username LIKE CONCAT('%', @username, '%'))
            AND (@userRole IS NULL OR r.name LIKE CONCAT('%', @userRole, '%'))
            AND (@team IS NULL OR g.title LIKE CONCAT('%', @team, '%'))
            ORDER BY u.id";

            try
            {
                using (MySqlConnection connection = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                {
                    using (MySqlCommand command = new MySqlCommand(selectQuery, connection))
                    {
                        command.Parameters.AddWithValue("@username", string.IsNullOrEmpty(usernameFilter) ? (object)DBNull.Value : usernameFilter);
                        command.Parameters.AddWithValue("@userRole", string.IsNullOrEmpty(userRoleFilter) ? (object)DBNull.Value : userRoleFilter);
                        command.Parameters.AddWithValue("@team", string.IsNullOrEmpty(teamFilter) ? (object)DBNull.Value : teamFilter);

                        connection.Open();
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                userDatas.Add(new UserData
                                {
                                    UserId = reader["id"] != DBNull.Value ? Convert.ToInt32(reader["id"]) : 0,
                                    Username = reader["username"]?.ToString() ?? string.Empty,
                                    Name = reader["name"]?.ToString() ?? string.Empty,
                                    UserRole = reader["RoleName"]?.ToString() ?? string.Empty,
                                    Team = reader["TeamTitle"]?.ToString() ?? "-"
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Internal server error.");
            }

            var pagedList = userDatas.ToPagedList(pageNumber, pageSize);
            return View(pagedList);
        }

        public async Task<IActionResult> GetUserSuggestions(string searchTerm, string filterType)
        {
            if (string.IsNullOrWhiteSpace(searchTerm) || searchTerm.Length < 3)
                return Json(new { suggestions = new List<string>() });

            using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                string query = "";

                switch (filterType)
                {
                    case "username":
                        query = "SELECT DISTINCT username FROM user WHERE username LIKE @SearchTerm LIMIT 10";
                        break;
                    case "userRole":
                        query = "SELECT DISTINCT r.name FROM roles r WHERE r.name LIKE @SearchTerm LIMIT 10";
                        break;
                    case "team":
                        query = "SELECT DISTINCT g.title FROM user_groups g WHERE g.title LIKE @SearchTerm LIMIT 10";
                        break;
                    default:
                        return Json(new { suggestions = new List<string>() });
                }

                var suggestions = await db.QueryAsync<string>(query, new { SearchTerm = $"%{searchTerm}%" });
                return Json(new { suggestions });
            }
        }

        //[HttpGet]
        //public async Task<IActionResult> ViewUser(int? page)
        //{
        //    int pageSize = 10;  // Number of records per page
        //    int pageNumber = page ?? 1; // Default to first page

        //    List<UserData> userDatas = new List<UserData>();

        //    string selectQuery = @"
        //SELECT u.id, u.username, u.name, r.name AS RoleName, g.title AS TeamTitle 
        //FROM user u
        //JOIN roles r ON u.user_role = r.id
        //LEFT JOIN user_groups g ON u.group_id = g.id
        //ORDER BY u.id";

        //    try
        //    {
        //        using (MySqlConnection connection = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
        //        {
        //            using (MySqlCommand command = new MySqlCommand(selectQuery, connection))
        //            {
        //                connection.Open();
        //                using (var reader = await command.ExecuteReaderAsync())
        //                {
        //                    while (await reader.ReadAsync())
        //                    {
        //                        UserData userData = new UserData
        //                        {
        //                            UserId = reader["id"] != DBNull.Value ? Convert.ToInt32(reader["id"]) : 0,
        //                            Username = reader["username"] != DBNull.Value ? reader["username"].ToString() : string.Empty,
        //                            Name = reader["name"] != DBNull.Value ? reader["name"].ToString() : string.Empty,
        //                            UserRole = reader["RoleName"] != DBNull.Value ? reader["RoleName"].ToString() : string.Empty,
        //                            Team = reader["TeamTitle"] != DBNull.Value ? reader["TeamTitle"].ToString() : "-"
        //                        };
        //                        userDatas.Add(userData);
        //                    }
        //                }
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine(ex.Message);
        //        return StatusCode(500, "Internal server error.");
        //    }

        //    // Convert to paged list
        //    var pagedList = userDatas.ToPagedList(pageNumber, pageSize);

        //    return View(pagedList);
        //}

        [AuthorizeUserEx]
        [HttpGet]
        public async Task<IActionResult> EditUserDetails(int id)
        {

            UserData userData = null;
            var userRoles = new List<SelectListItem>();
            var teams = new List<SelectListItem>();

            string selectQuery = "SELECT * FROM user WHERE id = @UserId";
            string rolesQuery = "SELECT id, name FROM roles";
            string teamsQuery = "SELECT id, title FROM user_groups";

            try
            {
                using (MySqlConnection connection = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                {
                    using (MySqlCommand command = new MySqlCommand(selectQuery, connection))
                    {
                        command.Parameters.Add("@UserId", MySqlDbType.Int32).Value = id;
                        connection.Open();

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                userData = new UserData
                                {
                                    UserId = reader["id"] != DBNull.Value ? Convert.ToInt32(reader["id"]) : 0,
                                    Username = reader["username"] != DBNull.Value ? reader["username"].ToString() : string.Empty,
                                    Name = reader["name"] != DBNull.Value ? reader["name"].ToString() : string.Empty,
                                    Email = reader["email"] != DBNull.Value ? reader["email"].ToString() : string.Empty,
                                    ContactNumber = reader["contact_number"] != DBNull.Value ? reader["contact_number"].ToString() : string.Empty,
                                    UserRole = reader["user_role"] != DBNull.Value ? reader["user_role"].ToString() : string.Empty,
                                    Team = reader["group_id"] != DBNull.Value ? reader["group_id"].ToString() : string.Empty
                                };
                            }
                        }
                        connection.Close();
                    }
                    ViewBag.UserRole = userData.UserRole;

                    //Fetch roles
                    using (MySqlCommand command = new MySqlCommand(rolesQuery, connection))
                    {
                        connection.Open();
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                userRoles.Add(new SelectListItem
                                {
                                    Value = reader["id"].ToString(),
                                    Text = reader["name"].ToString()
                                });
                            }
                        }
                        connection.Close();
                    }

                    // Fetch teams
                    using (MySqlCommand command = new MySqlCommand(teamsQuery, connection))
                    {
                        connection.Open();
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                teams.Add(new SelectListItem
                                {
                                    Value = reader["id"].ToString(),
                                    Text = reader["title"].ToString()
                                });
                            }
                        }
                        connection.Close();
                    }
                    // Get user role for the given user
                    string UserRoleQuery = "SELECT user_role FROM user WHERE id = @UserID";
                    int role_id = await connection.ExecuteScalarAsync<int>(UserRoleQuery, new { UserID = id });
                    // Fetch all previous rights of the user as a list
                    string userPreviousRightsQuery = "SELECT action_id FROM action_master WHERE user_id = @UserID";
                    var userPreviousRights = (await connection.QueryAsync<int>(
                        userPreviousRightsQuery,
                        new { UserID = id }
                    )).ToList();

                    // Store in ViewBag for view usage (pre-select checkboxes)
                    ViewBag.UserPreviousRights = userPreviousRights;



                }
            }
            catch (Exception ex)
            {
                // Log the exception (consider using a logging framework here)
                Console.WriteLine(ex.Message);
                // You might want to return an error view or a specific error message
                return StatusCode(500, "Internal server error.");
            }

            if (userData == null)
            {
                return NotFound();
            }
            ViewBag.UserRoles = userRoles;
            ViewBag.Teams = teams;
            return View(userData);
        }
        [HttpPost]
        public async Task<IActionResult> EditUserDetails(UserData userData)
        {
            userData.UpdatedAt = int.Parse(DateTime.Now.ToString("yyyyMMdd"));
            if (ModelState.IsValid)
            {

                string updateQuery = @"
                UPDATE user
                SET username = @Username, name = @Name, email = @Email, 
                    contact_number = @ContactNumber, updated_at=@UpdatedAt, user_role = @UserRole, group_id = @Team
                WHERE id = @UserId";

                try
                {
                    using (MySqlConnection connection = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                    {
                        // Check if the username already exists for a different user
                        string checkQuery = "SELECT COUNT(*) FROM user WHERE username = @Username AND id != @UserId";
                        using (MySqlCommand checkCommand = new MySqlCommand(checkQuery, connection))
                        {
                            checkCommand.Parameters.AddWithValue("@Username", userData.Username);
                            checkCommand.Parameters.AddWithValue("@UserId", userData.UserId);

                            connection.Open();
                            int count = Convert.ToInt32(await checkCommand.ExecuteScalarAsync());
                            connection.Close();

                            if (count > 0)
                            {
                                ModelState.AddModelError("Username", "Username already exists.");
                                // Reload the roles and teams dropdown lists
                                await PopulateDropdownData();
                                return View(userData);
                            }
                        }
                        using (MySqlCommand command = new MySqlCommand(updateQuery, connection))
                        {
                            command.Parameters.Add("@UserId", MySqlDbType.Int32).Value = userData.UserId;
                            command.Parameters.Add("@Username", MySqlDbType.VarChar).Value = userData.Username;
                            command.Parameters.Add("@Name", MySqlDbType.VarChar).Value = userData.Name;
                            command.Parameters.Add("@Email", MySqlDbType.VarChar).Value = userData.Email;
                            command.Parameters.Add("@ContactNumber", MySqlDbType.VarChar).Value = userData.ContactNumber;
                            command.Parameters.Add("@UserRole", MySqlDbType.Int32).Value = userData.UserRole;
                            command.Parameters.Add("@Team", MySqlDbType.Int32).Value = userData.Team;
                            command.Parameters.Add("@UpdatedAt", MySqlDbType.Int32).Value = userData.UpdatedAt;
                            connection.Open();
                            await command.ExecuteNonQueryAsync();

                        }
                        // 1) Remove the user ID from ALL allow_access rows (only if present)
                        var deletePreviousRights = @"
    UPDATE user_menu_new
    SET allow_access = 
        CASE
            WHEN allow_access IS NULL OR allow_access = '' THEN NULL
            WHEN TRIM(allow_access) = CAST(@UserId AS CHAR) THEN NULL
            ELSE NULLIF(
                TRIM(BOTH ',' FROM REPLACE(CONCAT(',', allow_access, ','), CONCAT(',', CAST(@UserId AS CHAR), ','), ',')),
                ''
            )
        END
    WHERE FIND_IN_SET(@UserId, allow_access);
";

                        // Execute user cleanup
                        connection.Execute(deletePreviousRights, new { UserId = userData.UserId });
                        string deletePreviousQuery = @"DELETE FROM action_master WHERE user_id = @UserId;";

                        connection.Execute(deletePreviousQuery, new
                        {
                            UserId = userData.UserId
                        });

                        // 2) Insert new rights
                        foreach (var right in userData.SelectedRights)
                        {
                            var sql = @"
                                        UPDATE user_menu_new AS um
                                        JOIN rbac_new AS rn ON 
                                            um.controller = rn.controller
                                             AND um.link LIKE CONCAT('%', rn.action, '%')
                                        SET um.allow_access =
                                            CASE
                                                WHEN um.allow_access IS NULL OR um.allow_access = '' 
                                                    THEN @UserId
                                                WHEN FIND_IN_SET(@UserId, um.allow_access) = 0
                                                    THEN CONCAT(um.allow_access, ',', @UserId)
                                                ELSE um.allow_access
                                            END
                                        WHERE rn.id = @RightId;
                                    ";

                            connection.Execute(sql, new
                            {
                                UserId = userData.UserId,
                                RightId = right.Id
                            });
                            // Insert into action_master
                            string insertSql = @"
        INSERT INTO action_master (action_id, action_name, user_id)
        VALUES (@ActionId, @ActionName, @UserId);";

                            connection.Execute(insertSql, new
                            {
                                ActionId = right.Id,
                                ActionName = right.Label,
                                UserId = userData.UserId
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Log the exception (consider using a logging framework here)
                    Console.WriteLine(ex.Message);
                    // You might want to return an error view or a specific error message
                    return StatusCode(500, "Internal server error.");
                }

                return RedirectToAction("ViewUser");
            }

            // Re-fetch the dropdown data in case of validation errors
            await PopulateDropdownData();

            return View(userData);
        }

        private async Task PopulateDropdownData()
        {
            var userRoles = new List<SelectListItem>();
            var teams = new List<SelectListItem>();

            string rolesQuery = "SELECT id, name FROM roles";
            string teamsQuery = "SELECT id, title FROM user_groups";

            try
            {
                using (MySqlConnection connection = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                {
                    using (MySqlCommand command = new MySqlCommand(rolesQuery, connection))
                    {
                        connection.Open();
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                userRoles.Add(new SelectListItem
                                {
                                    Value = reader["id"].ToString(),
                                    Text = reader["name"].ToString()
                                });
                            }
                        }
                        connection.Close();
                    }

                    // Fetch teams
                    using (MySqlCommand command = new MySqlCommand(teamsQuery, connection))
                    {
                        connection.Open();
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                teams.Add(new SelectListItem
                                {
                                    Value = reader["id"].ToString(),
                                    Text = reader["title"].ToString()
                                });
                            }
                        }
                        connection.Close();
                    }
                }
            }
            catch (Exception ex)
            {
                // Log the exception (consider using a logging framework here)
                Console.WriteLine(ex.Message);
                // You might want to return an error view or a specific error message
            }

            ViewBag.UserRoles = userRoles;
            ViewBag.Teams = teams;
        }
        [AuthorizeUserEx]
        [HttpGet]
        public async Task<IActionResult> ChangePassword(int id)
        {
            ViewBag.Id = id;

            return View();
            //Models.ChangePassword passwordData = null;

            //string selectQuery = @"
            //SELECT id,username 
            //FROM user 
            //WHERE id = @UserId";
            //try
            //{
            //    using (MySqlConnection connection = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            //    {
            //        using (MySqlCommand command = new MySqlCommand(selectQuery, connection))
            //        {
            //            command.Parameters.Add("@UserId", MySqlDbType.Int32).Value = userId;

            //            connection.Open();
            //            using (var reader = await command.ExecuteReaderAsync())
            //            {
            //                if (await reader.ReadAsync())
            //                {
            //                    passwordData = new Models.ChangePassword
            //                    {
            //                        //UserId = reader["id"] != DBNull.Value ? Convert.ToInt32(reader["id"]) : 0,
            //                        Username = reader["username"] != DBNull.Value ? reader["username"].ToString() : string.Empty                                    
            //                    };
            //                }
            //            }
            //        }
            //    }
            //}
            //catch (Exception ex)
            //{
            //    // Log the exception (consider using a logging framework here)
            //    Console.WriteLine(ex.Message);
            //    // You might want to return an error view or a specific error message
            //    return StatusCode(500, "Internal server error.");
            //}

            //if (passwordData == null)
            //{
            //    return NotFound();
            //}

            //return View(passwordData);
        }
        [HttpPost]
        public async Task<IActionResult> ChangePassword(int userId, string newPassword, string confirmPassword)
        {
            if (newPassword != confirmPassword)
            {
                ModelState.AddModelError("", "Passwords do not match.");
                return RedirectToAction("ChangePassword", new { id = userId });
            }

            // Hash the new password (use your existing password hashing method)
            string hashedPassword = BCrypt.Net.BCrypt.HashPassword(newPassword);

            // Update the password in the database
            try
            {
                using (MySqlConnection connection = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                {
                    await connection.OpenAsync();
                    string updateQuery = "UPDATE user SET password_hash = @PasswordHash WHERE id = @UserId";
                    using (MySqlCommand command = new MySqlCommand(updateQuery, connection))
                    {
                        command.Parameters.AddWithValue("@PasswordHash", hashedPassword);
                        command.Parameters.AddWithValue("@UserId", userId);

                        await command.ExecuteNonQueryAsync();
                    }
                }
                return RedirectToAction("ViewUser");
            }
            catch (Exception ex)
            {
                // Log the exception (consider using a logging framework here)
                Console.WriteLine(ex.Message);
                // You might want to return an error view or a specific error message
                return StatusCode(500, "Internal server error.");
            }
        }
        [AuthorizeUserEx]
        [HttpGet]
        public async Task<IActionResult> DeleteUser(int id)
        {
            if (id == 0)
            {
                return NotFound();
            }

            try
            {
                using (MySqlConnection connection = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                {
                    string deleteQuery = "DELETE FROM user WHERE id = @UserId";

                    using (MySqlCommand command = new MySqlCommand(deleteQuery, connection))
                    {
                        command.Parameters.Add("@UserId", MySqlDbType.Int32).Value = id;

                        connection.Open();
                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                // Log the exception (consider using a logging framework here)
                Console.WriteLine(ex.Message);
                return StatusCode(500, "Internal server error.");
            }
            return RedirectToAction("ViewUser"); // Redirect to the user list view
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
        public class Role
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        public class Team
        {
            public int Id { get; set; }
            public string Title { get; set; }
        }
    }
}
