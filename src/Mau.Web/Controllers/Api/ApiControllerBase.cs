using Mau.Web.Api;
using Microsoft.AspNetCore.Mvc;

namespace Mau.Web.Controllers.Api;

[ApiController]
[TypeFilter(typeof(ApiExceptionFilter))]
public abstract class ApiControllerBase : ControllerBase
{
}
