namespace Orbyss.Foundation.WebDefaults;

/// <summary>Names the built-in response policies; shells can select additional named policies.</summary>
public static class WebResponsePolicyNames
{
    /// <summary>Identifies the default response policy.</summary>
    public const string Default = "default";
    /// <summary>Identifies the private response policy.</summary>
    public const string Private = "private";
    /// <summary>Identifies the immutable public asset policy.</summary>
    public const string PublicAsset = "public-asset";
}
