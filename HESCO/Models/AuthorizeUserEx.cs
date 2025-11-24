using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace HESCO.Models
{
    public class AuthorizeUserEx : Attribute, IAsyncAuthorizationFilter
    {

        public AuthorizeUserEx()
        {
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            bool isAllowed = false;
            var permissions = context.HttpContext.Session.GetString("UserPermissions");
            if (context.HttpContext.User.Identity.IsAuthenticated && !string.IsNullOrEmpty(permissions))
            {
                var userPermissions = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(permissions);
                if (userPermissions != null)
                {
                    var controller = context.HttpContext.GetRouteValue("controller")?.ToString() ?? "";
                    var action = context.HttpContext.GetRouteValue("action")?.ToString() ?? "";
                    if (userPermissions.ContainsKey(controller))
                    {
                        if (userPermissions[controller].Contains(action))
                        {
                            isAllowed = true;   
                        }
                    }
                }
            }
            if (!isAllowed)
            {
                context.Result = new ForbidResult();
            }
        }
    }
}
