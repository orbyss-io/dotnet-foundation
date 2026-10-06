namespace Orbyss.Foundation.Json;

/// <summary>Names stable JSON admission and output failure classifications owned by Foundation.</summary>
public static class JsonFailureCodes
{
    /// <summary>Client JSON exceeds its decoded byte budget.</summary>
    public const string SizeExceeded = "json_size_exceeded";
    /// <summary>Client JSON contains a duplicate decoded member name.</summary>
    public const string DuplicateMember = "json_duplicate_member";
    /// <summary>Client JSON violates its declared typed contract.</summary>
    public const string InvalidContract = "json_invalid_contract";
    /// <summary>Client JSON contains invalid syntax.</summary>
    public const string InvalidSyntax = "json_invalid_syntax";
    /// <summary>Client JSON contains invalid Unicode.</summary>
    public const string InvalidUnicode = "json_invalid_unicode";
    /// <summary>Client JSON has an inadmissible null root.</summary>
    public const string NullRoot = "json_null_root";
    /// <summary>The selected canonical algorithm rejects negative zero.</summary>
    public const string NegativeZero = "json_negative_zero";
    /// <summary>The selected canonical algorithm cannot represent a number.</summary>
    public const string NumberNotRepresentable = "json_number_not_representable";
    /// <summary>Server JSON exceeds its output byte budget.</summary>
    public const string ResponseSizeExceeded = "json_response_size_exceeded";
    /// <summary>Server JSON violates its declared typed contract.</summary>
    public const string ResponseInvalidContract = "json_response_invalid_contract";
    /// <summary>Server JSON contains invalid Unicode.</summary>
    public const string ResponseInvalidUnicode = "json_response_invalid_unicode";
    /// <summary>Server JSON has an inadmissible null root.</summary>
    public const string ResponseNullRoot = "json_response_null_root";
    /// <summary>An operation bypasses its declared request reader.</summary>
    public const string RequestProfileBypass = "json_request_profile_bypass";
    /// <summary>An operation bypasses its declared response factory.</summary>
    public const string ResponseProfileBypass = "json_response_profile_bypass";
    /// <summary>The requested page count exceeds configured admission.</summary>
    public const string PageSizeInvalid = "json_page_size_invalid";
}
