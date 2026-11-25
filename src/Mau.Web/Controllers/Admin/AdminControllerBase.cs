using Mau.Core.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mau.Web.Controllers.Admin;

/// <summary>Base controller for every admin page.</summary>
[Authorize(Roles = AppRoles.Admin)]
public abstract class AdminControllerBase : Controller
{
}
