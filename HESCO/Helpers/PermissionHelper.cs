namespace HESCO.Helpers
{
    public class PermissionHelper
    {
        public static bool HasPermission(Dictionary<string, List<string>> permissions, string controller, string action)
        {
            return permissions != null &&
                   permissions.TryGetValue(controller, out var actions) &&
                   actions.Contains(action);
        }
    }
}
