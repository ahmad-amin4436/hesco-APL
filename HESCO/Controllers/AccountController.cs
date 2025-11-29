using HESCO.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
//using Microsoft.DotNet.Scaffolding.Shared.Messaging;
using MySql.Data.MySqlClient;
using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Data.Common;
using Dapper;
using Newtonsoft.Json;
using System.Configuration;

namespace HESCO.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApiService _apiService;
        private readonly IConfiguration _configuration;
        private readonly MenuService _menuService;
        public AccountController(ApiService apiService, IConfiguration configuration, MenuService menuService)
        {
            _apiService = apiService;
            _configuration = configuration;
            _menuService = menuService;
        }
        public IActionResult Index()
        {
            // Example of using the stored secret key
            var secretKey = HttpContext.Session.GetString("SecretKey");

            if (string.IsNullOrEmpty(secretKey))
            {
                // If the user is not authenticated, redirect to login page
                return RedirectToAction("LoginUser", "Account");
            }
            return View();
        }

        public async Task<IActionResult> LoginUser()
        {
            var projects = await _menuService.GetProjectLinksAsync();
            ViewBag.ProjectLinks = projects;
            return View();
        }
        //[HttpPost]
        //public async Task<IActionResult> LoginUser(string username, string password, bool rememberMe)
        //{
        //    // Call the API to authenticate the user
        //    var result = await _apiService.AuthenticateUser(username, password);

        //    if (result.IsAuthenticated)
        //    {
        //        int userId = await GetUserIdByUsername(username);

        //        if (userId > 0)
        //        {

        //            // Store the secret key and user ID in the session
        //            HttpContext.Session.SetString("SecretKey", result.SecretKey);
        //            HttpContext.Session.SetInt32("UserId", userId);
        //            HttpContext.Session.SetString("Username", username);

        //            var permissions = (await GetUserPermissions(userId)).GroupBy(x => x.Controller).ToDictionary(x => x.Key, x => x.Select(y => y.Action).ToList());
        //            HttpContext.Session.SetString("UserPermissions", JsonConvert.SerializeObject(permissions));

        //            // Create an authentication cookie with user roles
        //            var claims = new List<Claim>
        //            {
        //                new Claim(ClaimTypes.Name, username),
        //                new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        //            };

        //            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

        //            var authProperties = new AuthenticationProperties
        //            {
        //                IsPersistent = true,
        //                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(96),
        //            };

        //            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
        //                new ClaimsPrincipal(claimsIdentity),
        //                authProperties);
        //            return RedirectToAction("Index", "Home");
        //        }
        //        else
        //        {
        //            ViewBag.ErrorMessage = "User ID not found.";
        //            return View("LoginUser");
        //        }
        //    }
        //    else
        //    {
        //        // Display the error message on the login page
        //        ViewBag.ErrorMessage = result.ErrorMessage;
        //        return View("LoginUser");
        //    }
        //}

        [HttpPost]
        public async Task<IActionResult> LoginUser(string username, string password, bool rememberMe)
        {
            using (var connection = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                string query = "SELECT id AS UserId, username, password_hash AS Password, user_role AS UserRole FROM user WHERE username = @Username AND user_status = 1";
                var user = await connection.QueryFirstOrDefaultAsync<User>(query, new { Username = username });

                if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.Password))
                {
                    var projects = await _menuService.GetProjectLinksAsync();
                    ViewBag.ProjectLinks = projects;
                    ViewBag.ErrorMessage = "Invalid username or password.";
                    return View("LoginUser");
                }

                int userId = user.UserId;
                int roleId = user.UserRole;
                // Store user session info
                HttpContext.Session.SetInt32("UserId", userId);
                HttpContext.Session.SetString("Username", username);
                HttpContext.Session.SetInt32("RoleId", roleId);

                // Optional: If you want to store a fake "secret key", generate or fetch it here
                var secretKey = Guid.NewGuid().ToString(); // Replace with actual logic if needed
                HttpContext.Session.SetString("SecretKey", secretKey);

                var permissions = (await GetUserPermissions(userId)).GroupBy(x => x.Controller).ToDictionary(x => x.Key, x => x.Select(y => y.Action).ToList());
                HttpContext.Session.SetString("UserPermissions", JsonConvert.SerializeObject(permissions));

                // Authentication cookie
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, username),
                    new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                     new Claim("role_id", roleId.ToString())
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = rememberMe,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(48)
                };

                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    authProperties);

                return RedirectToAction("Index", "Home");
            }
        }
        //// Method to authenticate user
        private async Task<bool> AuthenticateUser(string username, string password)
        {
            using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                // Query to get the hashed password from the database for the given username
                string query = "SELECT password_hash FROM user WHERE username = @Username";
                var hashedPassword = await db.QueryFirstOrDefaultAsync<string>(query, new { Username = username });

                // If user is found, compare the provided password with the hashed password
                if (!string.IsNullOrEmpty(hashedPassword))
                {
                    return BCrypt.Net.BCrypt.Verify(password, hashedPassword);
                }
            }
            return false; // Authentication failed
        }
        public ActionResult AccessDenied()
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            string unauthorizedMessage = "You are not authorized to access this page.";
            string loginUrl = Url.Action("LoginUser", "Account"); // Fallback login URL

            // Get the referer URL from the request headers
            var refererUrl = HttpContext.Request.Headers["Referer"].ToString();

            // Use the referer URL if it exists, otherwise fall back to the login URL
            var redirectUrl = string.IsNullOrEmpty(refererUrl) ? loginUrl : refererUrl;
            if(!User.Identity.IsAuthenticated)
            {
                return RedirectToAction("LoginUser");

            }
            //    // Return an HTML response that includes the unauthorized message and JavaScript redirection
            //    string htmlContent = $@"
            //<html>
            //    <body>
            //        <h3>{unauthorizedMessage}</h3>
            //        <script>
            //            setTimeout(function() {{
            //                window.location.href = '{redirectUrl}';
            //            }}, 2000); // Redirects after 2 seconds
            //        </script>
            //    </body>
            //</html>";

            //    return Content(htmlContent, "text/html");
            ViewBag.RedirectUrl = redirectUrl;
            return View();
        }      
        private async Task<int> GetUserIdByUsername(string username)
        {
            using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                // Query to fetch user ID based on username
                string query = "SELECT id FROM user WHERE username = @Username";
                var user = await db.QueryFirstOrDefaultAsync<int?>(query, new { Username = username });

                // Return the user ID or 0 if not found
                return user ?? 0;
            }
        }
        private async Task<List<ActionInfo>> GetUserPermissions(int userId)
        {
            using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                string query = @"SELECT rba.controller, am.action_name as action FROM accurate_hesco_maam.action_master am
JOIN rbac_new rba ON rba.id = am.action_id
WHERE user_id = @UserId;
";

                //                string query = @"SELECT 
                //    rn.controller,
                //    rn.action,
                //    GROUP_CONCAT(DISTINCT rp.id) AS role_permission_ids,
                //    GROUP_CONCAT(DISTINCT um.id) AS menu_ids
                //FROM roles_permissions AS rp
                //INNER JOIN rbac_new AS rn 
                //       ON rp.rbac_id = rn.id
                //JOIN user_menu_new AS um
                //       ON um.controller = rn.controller
                //      AND um.link LIKE CONCAT('%', rn.action, '%')
                //WHERE FIND_IN_SET(@UserId, um.allow_access)
                //GROUP BY rn.controller, rn.action;";

                var data = await db.QueryAsync<ActionInfo>(query, new { UserId = userId });
                return data.ToList();               
            }
        }
        public class UserRoleResult
        {
            public int UserRole { get; set; }
            public string Name { get; set; }
        }
        private string GenerateAuthKey(string password)
        {
            string part1 = MD5Hash(DateTime.Now.ToString());
            string part2 = new Random().Next(2, 98).ToString("D2"); // Ensure two digits
            string part3 = MD5Hash(new Random().Next(3, 87).ToString());
            string part4 = MD5Hash(password);

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
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            // Sign out the user
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            HttpContext.Session.Clear();
            // Redirect to the login page or home page
            return RedirectToAction("LoginUser", "Account");
        }
    }
}