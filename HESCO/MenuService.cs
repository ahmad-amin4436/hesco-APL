using MySql.Data.MySqlClient;
using System.Data;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using HESCO.Models;
using Microsoft.Web.Administration;

namespace HESCO
{
    public class MenuService
    {
        private readonly IConfiguration _configuration;
        public MenuService(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        public async Task<IEnumerable<MenuItem>> GetMenuItemsAsync(int roleId)
        {
            using (IDbConnection dbDefault = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                var query = @"
        SELECT 
            id AS Id,
            title AS Title,
            link AS Link, 
            controller AS Controller, 
            allow_access AS AllowAccess, 
            parent_id AS ParentId, 
            display_order AS DisplayOrder,
            active_list AS IsActive, 
            sub_active_list AS IsSubActive,
            fa_icon AS FaIcon
        FROM user_menu_new 
        WHERE FIND_IN_SET(@RoleId, allow_access) > 0
        ORDER BY display_order ASC";

                var parameters = new { RoleId = roleId };

                var menuItems = await dbDefault.QueryAsync<MenuItem>(query, parameters);
                return menuItems;
            }
        }
        //public async Task<IEnumerable<MenuItem>> GetMenuItemsAsync()
        //{
        //    using (IDbConnection dbDefault = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
        //    {
        //        var query = @"
        //        SELECT 
        //            id AS Id,
        //            title AS Title,
        //            link AS Link, 
        //            controller AS Controller, 
        //            allow_access AS AllowAccess, 
        //            parent_id AS ParentId, 
        //            display_order AS DisplayOrder,
        //            active_list AS IsActive, 
        //            sub_active_list AS IsSubActive,
        //            fa_icon AS FaIcon
        //        FROM user_menu_new 
        //        WHERE allow_access IS NOT NULL
        //        ORDER BY display_order ASC";

        //        var menuItems = await dbDefault.QueryAsync<MenuItem>(query);
        //        return menuItems;
        //    }
        //}

        //public async Task<IEnumerable<MenuItem>> GetMenuItemsAsync(int roleId)
        //{
        //    using (IDbConnection dbDefault = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
        //    {
        //        var query = @"
        //    SELECT 
        //        umn.id AS Id,
        //        umn.title AS Title,
        //        umn.link AS Link, 
        //        umn.controller AS Controller, 
        //        umn.allow_access AS AllowAccess, 
        //        umn.parent_id AS ParentId, 
        //        umn.display_order AS DisplayOrder,
        //        umn.active_list AS IsActive, 
        //        umn.sub_active_list AS IsSubActive,
        //        umn.fa_icon AS FaIcon,
        //        umn.action_id AS ActionId,
        //        CASE 
        //            WHEN rp.rbac_id IS NOT NULL THEN TRUE
        //            ELSE FALSE
        //        END AS IsActionAllowed
        //    FROM user_menu_new umn
        //    LEFT JOIN roles_permissions rp 
        //        ON rp.role_id = @RoleId AND rp.rbac_id = umn.action_id
        //    WHERE FIND_IN_SET(@RoleId, umn.allow_access) > 0
        //    ORDER BY umn.display_order ASC";

        //        var parameters = new { RoleId = roleId };

        //        var menuItems = await dbDefault.QueryAsync<MenuItem>(query, parameters);
        //        return menuItems;
        //    }
        //}


        public async Task<IEnumerable<ProjectLink>> GetProjectLinksAsync()
        {
            using (IDbConnection dbMaster = new MySqlConnection(_configuration.GetConnectionString("MasterDBConnection")))
            {
                dbMaster.Open();
                var query = @"
                SELECT 
                    id AS Id, 
                    name, 
                   project_url 
                FROM projects 
                WHERE is_active = 1";

                var projects = await dbMaster.QueryAsync<ProjectLink>(query);
                return projects;
            }
        }
        public async Task<string?> GetProjectNameAsync()
        {
            using (IDbConnection dbDefault = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            using (IDbConnection dbMaster = new MySqlConnection(_configuration.GetConnectionString("MasterDBConnection")))
            {
                dbDefault.Open();
                dbMaster.Open();

                // Step 1: Get the current database name
                var currentDbQuery = "SELECT DATABASE();";
                var currentDbName = await dbDefault.ExecuteScalarAsync<string>(currentDbQuery);

                if (string.IsNullOrEmpty(currentDbName))
                    return null;

                // Step 2: Get project_id from project_databases table using db_name
                var getDbIdQuery = "SELECT id FROM project_databases WHERE db_name = @DbName LIMIT 1;";
                var dbId = await dbMaster.ExecuteScalarAsync<int?>(getDbIdQuery, new { DbName = currentDbName });

                if (!dbId.HasValue)
                    return null;

                // Step 3: Get project name from projects table using id
                var getProjectNameQuery = "SELECT name FROM projects WHERE db_id = @dbId LIMIT 1;";
                var projectName = await dbMaster.ExecuteScalarAsync<string>(getProjectNameQuery, new { dbId = dbId.Value });

                return projectName;
            }
        }

        //public async Task<List<ProjectLink>> GetProjectLinksAsync()
        //{
        //    var projectLinks = new List<ProjectLink>();
        //    var publicIPAddress = await GetAwsPublicIpAsync();
        //    var discoPrefixes = await GetDiscoPrefixesAsync();
        //    //var currentHost = _httpContextAccessor.HttpContext?.Request.Host.Host;
        //    using (ServerManager serverManager = new ServerManager())
        //    {
        //        foreach (var site in serverManager.Sites)
        //        {
        //            string sitePrefix = site.Name.Substring(0, Math.Min(4, site.Name.Length));

        //            if (!discoPrefixes.Contains(sitePrefix, StringComparer.OrdinalIgnoreCase))
        //                continue;

        //            foreach (var binding in site.Bindings)
        //            {
        //                var parts = binding.BindingInformation.Split(':');
        //                if (parts.Length < 2) continue;

        //                string ipAddress = parts[0] == "*" ? publicIPAddress : parts[0];
        //                string port = parts[1];
        //                string protocol = binding.Protocol;
        //                string url = $"{protocol}://{ipAddress}:{port}";
        //                var projectHost = new Uri(url).Host;

        //                //if (string.Equals(currentHost, projectHost, StringComparison.OrdinalIgnoreCase))
        //                //    continue; // Exclude the current site
        //                projectLinks.Add(new ProjectLink
        //                {
        //                    name = site.Name,
        //                    project_url = url
        //                });
        //            }
        //        }
        //    }

        //    return projectLinks;
        //}

        //private async Task<List<string>> GetDiscoPrefixesAsync()
        //{
        //    using var connection = new MySqlConnection(_configuration.GetConnectionString("MasterDBConnection"));
        //    var query = "SELECT DISTINCT LEFT(disco_name, 4) FROM discos";
        //    var result = await connection.QueryAsync<string>(query);
        //    return result.ToList();
        //}

        private async Task<string> GetAwsPublicIpAsync()
        {
            using var client = new HttpClient();
            try
            {
                return await client.GetStringAsync("http://169.254.169.254/latest/meta-data/public-ipv4");
            }
            catch
            {
                return "127.0.0.1"; // fallback
            }
        }
    }
}