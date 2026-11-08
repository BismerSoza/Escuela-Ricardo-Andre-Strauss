using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Escuela_WEB.Filters
{
    public class SessionAuthorizeAttribute : Attribute, IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var token = context.HttpContext.Session.GetString("Token");

            if (string.IsNullOrEmpty(token))
            {
                context.Result = new RedirectToActionResult("Index", "Home", null);
            }
        }
    }

    // Restringe el acceso a una accion segun el rol guardado en sesion.
    // Admite uno o varios roles permitidos, ej: [SessionRol("Director","Docente")]
    public class SessionRolAttribute(params string[] rolesPermitidos) : Attribute, IAuthorizationFilter
    {
        private readonly string[] _rolesPermitidos = rolesPermitidos;

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var rol = context.HttpContext.Session.GetString("Rol");

            if (rol == null || !_rolesPermitidos.Contains(rol))
            {
                context.Result = new RedirectToActionResult("Principal", "Home", null);
            }
        }
    }
}
