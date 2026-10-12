namespace Deskovia.WebApi.Security;

public sealed class EntraOptions
{
    public const string Scheme = "MicrosoftEntra";
    public bool Enabled { get; set; }
    public Guid TenantId { get; set; }
    public Guid ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string FrontendOrigin { get; set; } = "https://localhost:7178";
    public bool Configured => Enabled && TenantId != Guid.Empty && ClientId != Guid.Empty && !string.IsNullOrWhiteSpace(ClientSecret);
    public string ErrorUrl(string code) => FrontendOrigin + "/login?authError=" + Uri.EscapeDataString(code);
    public string ReturnUrl(string? path)
    {
        // Restrict redirects to this deployment's frontend, including encoded separators.
        try
        {
            var decoded = Uri.UnescapeDataString(path ?? "");
            if (!decoded.StartsWith('/') || decoded.StartsWith("//") || decoded.Contains((char)92) || decoded.Any(char.IsControl)) return FrontendOrigin + "/";
            return FrontendOrigin + decoded;
        }
        catch (UriFormatException) { return FrontendOrigin + "/"; }
    }
}
