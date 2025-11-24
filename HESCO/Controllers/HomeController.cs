using Dapper;
using HESCO.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using MySql.Data.MySqlClient;
using Org.BouncyCastle.Crypto.Generators;
using System.Data;
using System.Diagnostics;
using System.Security.Cryptography;
using BCrypt.Net;
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
                if (loggedInUserRole != "1")
                {
                    rolesData = rolesData.Where(r => r.Value != "1").ToList();
                }

                ViewBag.SelectRole = new SelectList(rolesData, "Value", "Text");

                string teamQuery = "SELECT Id as value, Title as text FROM user_groups";
                var teamData = (await db.QueryAsync<DTODropdown>(teamQuery)).Distinct().ToList();
                ViewBag.SelectTeam = new SelectList(teamData, "Value", "Text");
            }

            return View();
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
            INSERT INTO user (username, auth_key, password_hash, name, email, created_at, contact_number, user_role, group_id)
            VALUES (@Username, @AuthKey, @Password, @Name, @Email, @CreatedAt, @ContactNumber, @UserRole, @Team)";

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
