using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Vairas.VehicleManagement.Business;
using Vairas.VehicleManagement.Middleware;
using Vairas.VehicleManagement.Domain;
using Vairas.VehicleManagement.Configuration.Options;

namespace Vairas.VehicleManagement.Security;

public sealed record EntryRequirement(string? Scope = null) : IAuthorizationRequirement;
