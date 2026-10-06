namespace Orbyss.Foundation.Json.AspNetCore;

/// <summary>Owns deployment page count admission independently of response bytes.</summary>
public sealed class JsonPageOptions
{
    /// <summary>Gets or sets the default page item count.</summary>
    public int DefaultSize { get; set; } = 100;
    /// <summary>Gets or sets the largest admitted requested page count.</summary>
    public int MaximumSize { get; set; } = 500;
    /// <summary>Rejects invalid configured count limits.</summary>
    public void Validate()
    {
        if (DefaultSize < 1 || MaximumSize < DefaultSize || MaximumSize > 10_000)
            throw new InvalidOperationException("Foundation:Json paging limits are invalid.");
    }
    /// <summary>Admits a caller's requested page count; applications retain section/cursor semantics.</summary>
    public int Admit(int? requested)
    {
        Validate();
        if (requested is < 1 || requested > MaximumSize) throw new JsonProfileException(JsonFailureCodes.PageSizeInvalid);
        return requested ?? DefaultSize;
    }
}
