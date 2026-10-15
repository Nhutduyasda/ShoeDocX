using System.Globalization;
using System.Security.Claims;

namespace ShoeExportInvoice.Api.Features.UniversalDocEngine.Application;

public interface IUniversalDocCurrentActor { UniversalDocActor Resolve(); }
public sealed class UniversalDocCurrentActor(IHttpContextAccessor accessor, IConfiguration configuration, TimeProvider time)
    : IUniversalDocCurrentActor
{
    public UniversalDocActor Resolve()
    {
        var principal = accessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true) throw new StudioFault(401, "AUTHENTICATION_REQUIRED");
        var ids = principal.FindAll(ClaimTypes.NameIdentifier).Select(c => c.Value).ToArray();
        var roles = principal.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray();
        var expires = principal.FindAll("exp").Select(c => c.Value).ToArray();
        if (ids.Length != 1 || string.IsNullOrWhiteSpace(ids[0]) || ids[0].Length > 128 ||
            roles.Length != 1 || expires.Length != 1 || !long.TryParse(expires[0], NumberStyles.None,
                CultureInfo.InvariantCulture, out var seconds) || seconds <= time.GetUtcNow().ToUnixTimeSeconds() ||
            seconds > DateTimeOffset.MaxValue.ToUnixTimeSeconds()) throw new StudioFault(401, "INVALID_OR_EXPIRED_IDENTITY");
        // Exact server-owned membership lookup. No header/body/query/tenant-claim fallback.
        var binding = configuration.GetSection("UniversalDocGate4A:Users").GetChildren()
            .SingleOrDefault(c => string.Equals(c.Key, ids[0], StringComparison.Ordinal));
        var tenant = binding?.Value;
        if (string.IsNullOrWhiteSpace(tenant) || tenant.Length > 128) throw new StudioFault(403, "STUDIO_MEMBERSHIP_REQUIRED");
        return new(ids[0], tenant, roles[0], DateTimeOffset.FromUnixTimeSeconds(seconds));
    }
}
