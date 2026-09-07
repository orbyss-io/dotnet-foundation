using Microsoft.AspNetCore.Authorization;

namespace Orbyss.Foundation.Authentication.Assurance;

/// <summary>Identifies one named assurance requirement inside ASP.NET authorization.</summary>
internal sealed record AssuranceRequirement(string Policy) : IAuthorizationRequirement;
