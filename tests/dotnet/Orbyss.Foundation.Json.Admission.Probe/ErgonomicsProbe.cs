using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CShells.AspNetCore.Configuration;
using CShells.AspNetCore.Extensions;
using CShells.AspNetCore.Resolution;
using CShells.DependencyInjection;
using CShells.Lifecycle;
using Orbyss.Foundation.Json.Admission.Probe.Contracts;
using Orbyss.Foundation.Json.AspNetCore;

namespace Orbyss.Foundation.Json.Admission.Probe;

/// <summary>Checks descriptive contracts against actual shell activation, HTTP admission and native OpenAPI.</summary>
internal static class ErgonomicsProbe
{
    /// <summary>Runs scoped registration controls and the real producer/runtime compatibility cases.</summary>
    internal static async Task RunAsync()
    {
        ContractRegistrationChecks.Run();
        await NumericSchemaPreservationChecks.RunAsync();
        await ProblemMetadataChecks.RunAsync();
        await using var app = CreateApp();
        app.MapShells();
        var registry = app.Services.GetRequiredService<IShellRegistry>();
        var shell = await registry.GetOrActivateAsync("ergonomics");
        await app.StartAsync();

        try
        {
            var address = app.Urls.Single();
            var baseAddress = new Uri(address);
            using var client = new HttpClient { BaseAddress = baseAddress, Timeout = TimeSpan.FromSeconds(15) };
            await VerifyConditionalOperationAsync(client);
            await VerifyNumericRuntimeAsync(client);
            await VerifyNumericFamiliesAsync(client);
            await VerifyOpenApiAsync(client);

            var metadata = shell.ServiceProvider.GetServices<IJsonContractMetadata>();
            var shared = metadata.Count(item => item.ContractType == typeof(ValueResponse) && item.Direction == JsonContractDirection.Response);
            Require(shared == 1, "two operations registered more than one shared response contract");
            var profiles = shell.ServiceProvider.GetRequiredService<JsonProfileCatalog>();
            var request = profiles.Get(JsonProfileKeys.StrictRequest);
            Require(request.MaxBytes == 8192 && request.MaxDepth == 8, "the active profile lost its actual deployment budget");
        }
        finally
        {
            await app.StopAsync();
        }

        Console.WriteLine("Descriptive shared contracts, scoped/cold DI, conditional header metadata, actual numeric profiles and native OpenAPI parity passed.");
    }

    /// <summary>Creates only the actual selected shell features and bounded JSON profiles.</summary>
    private static WebApplication CreateApp()
    {
        const string prefix = "CShells:Shells:ergonomics";
        var configuration = new Dictionary<string, string?>
        {
            [prefix + ":Features:JsonErgonomicsProbe"] = "true",
            [prefix + ":Configuration:WebRouting:Path"] = "ergonomics",
            [prefix + ":Configuration:Foundation:Json:Profiles:strict-request:MaxBytes"] = "8192",
            [prefix + ":Configuration:Foundation:Json:Profiles:strict-request:MaxDepth"] = "8",
            [prefix + ":Configuration:Foundation:Json:Profiles:success-response:MaxBytes"] = "8192",
            [prefix + ":Configuration:Foundation:Json:Profiles:success-response:MaxDepth"] = "8",
            [prefix + ":Configuration:Foundation:Json:Profiles:custom-integer:Preset"] = "tolerant-response",
            [prefix + ":Configuration:Foundation:Json:Profiles:custom-integer:MaxBytes"] = "8192",
            [prefix + ":Configuration:Foundation:Json:Profiles:custom-integer:MaxDepth"] = "8",
            [prefix + ":Configuration:Foundation:Json:Profiles:custom-integer:Extensions:0"] = "custom-integer"
        };
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(configuration);

        void ConfigureRouting(WebRoutingShellResolverOptions options) => options.EnablePathRouting = true;
        void ConfigureShells(CShellsBuilder shells)
        {
            shells
                .WithConfigurationProvider(builder.Configuration)
                .WithWebRouting(ConfigureRouting);
        }

        builder.Services.AddCShellsAspNetCore(ConfigureShells);
        var app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0");
        return app;
    }

    /// <summary>Exercises actual optional binding, duplicate headers, stale conditions and shared responses.</summary>
    private static async Task VerifyConditionalOperationAsync(HttpClient client)
    {
        var identifier = Guid.NewGuid();
        var route = "/ergonomics/values/" + identifier;
        foreach (var (header, expected) in new[] { ((string?)null, 428), ("W/\"1\"", 400), ("\"2\"", 412), ("\"1\"", 200) })
        {
            using var request = new HttpRequestMessage(HttpMethod.Patch, route);
            request.Content = JsonContent("{\"label\":\"changed\",\"sequence\":12}");
            if (header is not null)
                request.Headers.TryAddWithoutValidation("If-Match", header);
            using var response = await client.SendAsync(request);
            Require((int)response.StatusCode == expected, "conditional operation changed its actual status semantics");
        }

        using var duplicate = new HttpRequestMessage(HttpMethod.Patch, route);
        duplicate.Content = JsonContent("{\"label\":\"changed\",\"sequence\":null}");
        var duplicateTags = new[] { "\"1\"", "\"1\"" };
        duplicate.Headers.TryAddWithoutValidation("If-Match", duplicateTags);
        using var denied = await client.SendAsync(duplicate);
        Require(denied.StatusCode == HttpStatusCode.BadRequest, "duplicate header cardinality was flattened into a valid precondition");

        using var read = await client.GetAsync(route);
        Require(read.StatusCode == HttpStatusCode.OK, "the second operation could not use the same response contract");
    }

    /// <summary>Uses actual registered readers/results, including nulls and independent read/write flags.</summary>
    private static async Task VerifyNumericRuntimeAsync(HttpClient client)
    {
        const string valid = "{\"strictCount\":1,\"strictSize\":2,\"inheritedCount\":\"3\",\"inheritedSize\":\"4\",\"requiredCount\":5,\"requiredSize\":6,\"writeOnlyCount\":7,\"bothSize\":\"8\"}";
        using var accepted = await PostJsonAsync(
            client,
            "/ergonomics/numeric/request",
            valid
        );
        Require(accepted.StatusCode == HttpStatusCode.NoContent, "class/property number handling was not honored by the registered reader");

        var invalidStrict = valid.Replace(
            "\"strictCount\":1",
            "\"strictCount\":\"1\"",
            StringComparison.Ordinal
        );
        var invalidWriteOnly = valid.Replace(
            "\"writeOnlyCount\":7",
            "\"writeOnlyCount\":\"7\"",
            StringComparison.Ordinal
        );
        var invalidBodies = new[] { invalidStrict, invalidWriteOnly };
        foreach (var invalid in invalidBodies)
        {
            using var response = await PostJsonAsync(
                client,
                "/ergonomics/numeric/request",
                invalid
            );
            Require(response.StatusCode == HttpStatusCode.BadRequest, "a write-only/strict numeric flag broadened accepted input");
        }

        using var responseNumbers = await client.GetAsync("/ergonomics/numeric/response");
        var bytes = await responseNumbers.Content.ReadAsByteArrayAsync();
        using var output = JsonDocument.Parse(bytes);
        foreach (var name in new[] { "strictCount", "strictSize", "inheritedCount", "inheritedSize", "requiredCount", "requiredSize" })
        {
            var numericOutputPreserved = output.RootElement.GetProperty(name).ValueKind == JsonValueKind.Number;
            Require(numericOutputPreserved, "read permissiveness leaked into response writing");
        }
        foreach (var name in new[] { "writeOnlyCount", "bothSize" })
        {
            var quotedOutputPreserved = output.RootElement.GetProperty(name).ValueKind == JsonValueKind.String;
            Require(quotedOutputPreserved, "WriteAsString response was published as a number");
        }
        using var nulls = await PostJsonAsync(
            client,
            "/ergonomics/numeric/strict",
            "{\"count\":null,\"size\":null}"
        );
        var nullText = await nulls.Content.ReadAsStringAsync();
        var nullsAccepted = nulls.StatusCode == HttpStatusCode.OK && nullText.Contains("\"count\":null", StringComparison.Ordinal);
        Require(nullsAccepted, "legitimate nulls failed the same-type contract");
        using var quoted = await PostJsonAsync(
            client,
            "/ergonomics/numeric/strict",
            "{\"count\":\"1\",\"size\":null}"
        );
        Require(quoted.StatusCode == HttpStatusCode.BadRequest, "actual strict profile default admitted numeric strings");
    }

    /// <summary>Checks the real producer, including wire type kinds, requiredness, headers, failures and support intervals.</summary>
    private static async Task VerifyOpenApiAsync(HttpClient client)
    {
        var text = await client.GetStringAsync("/ergonomics/_orbyss-foundation/openapi/v1.json");
        var directory = Path.Combine("artifacts", "api-ergonomics");
        Directory.CreateDirectory(directory);
        var outputPath = Path.Combine(directory, "actual-contract.openapi.json");
        File.WriteAllText(outputPath, text);
        using var document = JsonDocument.Parse(text);
        var root = document.RootElement;
        var paths = root.GetProperty("paths");
        var valuePath = paths
            .EnumerateObject()
            .Single(IsValuePath).Value;
        var change = valuePath.GetProperty("patch");
        var operationNameMatches = change
            .GetProperty("operationId")
            .GetString() == Operations.ChangeValue.Endpoint.Name;
        Require(operationNameMatches, "operation identity drifted");
        var header = change
            .GetProperty("parameters")
            .EnumerateArray()
            .Single(IsConditionalHeader);
        var requiredHeader = header.TryGetProperty("required", out var required) && required.GetBoolean();
        var headerMetadataMatches = header
            .GetProperty("in")
            .GetString() == "header" && !requiredHeader;
        Require(headerMetadataMatches, "handler-owned nullable header metadata was lost");
        foreach (var status in new[] { "200", "400", "412", "428" })
        {
            var failureDeclared = change
                        .GetProperty("responses")
                        .TryGetProperty(status, out _);
            Require(failureDeclared, "actual operation failure metadata was omitted");
        }
        foreach (var contract in change
                    .GetProperty("x-foundation-json-contracts")
                    .EnumerateArray())
        {
            var supportedCapacityPreserved = contract
                .GetProperty("maximumBytes")
                .GetInt32() == 16_777_216;
            Require(supportedCapacityPreserved, "supported capacity was silently replaced by active deployment limits");
            var supportedDepthPreserved = contract
                .GetProperty("maximumDepth")
                .GetInt32() == 64;
            Require(supportedDepthPreserved, "supported nesting contract changed");
        }

        var schemas = root
            .GetProperty("components")
            .GetProperty("schemas");
        var request = FindSchema<RequestNumbers>(schemas);
        var response = FindSchema<ResponseNumbers>(schemas);
        foreach (var name in new[] { "strictCount", "strictSize", "inheritedCount", "inheritedSize", "requiredCount", "requiredSize", "writeOnlyCount", "bothSize" })
        {
            var nullable = name is not ("requiredCount" or "requiredSize");
            var acceptsString = name is "inheritedCount" or "inheritedSize" or "bothSize";
            AssertKinds(
                request,
                name,
                numeric: true,
                text: acceptsString,
                nullable
            );
            var writesString = name is "writeOnlyCount" or "bothSize";
            AssertKinds(
                response,
                name,
                numeric: !writesString,
                text: writesString,
                nullable
            );
        }
        var constructorMembersRequired = request
            .GetProperty("required")
            .GetArrayLength() == 8;
        Require(constructorMembersRequired, "constructor requiredness was dropped during numeric correction");
        var strict = FindSchema<StrictNumbers>(schemas);
        AssertKinds(
            strict,
            "count",
            numeric: true,
            text: false,
            nullable: true
        );
        AssertKinds(
            strict,
            "size",
            numeric: true,
            text: false,
            nullable: true
        );
        VerifyFamilySchemas(schemas);
    }

    /// <summary>Checks actual parser variants and custom/named/derived response representations.</summary>
    private static async Task VerifyNumericFamiliesAsync(HttpClient client)
    {
        const string family = "{\"byte\":\"01\",\"signedByte\":\"+1\",\"small\":\"-01\",\"unsignedSmall\":\"1\",\"unsigned\":\"01\",\"unsignedLong\":\"1\",\"wide\":\"1\",\"unsignedWide\":\"1\",\"price\":\".5\",\"single\":\"1.\",\"ratio\":\"1e1\",\"half\":\"+1\"}";
        using var accepted = await PostJsonAsync(
            client,
            "/ergonomics/numeric/family",
            family
        );
        Require(accepted.StatusCode == HttpStatusCode.NoContent, "native-valid lexical variants were rejected by the actual profile");
        var oversizedSmall = family.Replace(
            "\"small\":\"-01\"",
            "\"small\":\"32768\"",
            StringComparison.Ordinal
        );
        using var range = await PostJsonAsync(
            client,
            "/ergonomics/numeric/family",
            oversizedSmall
        );
        Require(range.StatusCode == HttpStatusCode.BadRequest, "schema ergonomics removed runtime numeric range enforcement");

        using var named = await PostJsonAsync(
            client,
            "/ergonomics/numeric/named",
            "{\"value\":\"NaN\"}"
        );
        using var numericString = await PostJsonAsync(
            client,
            "/ergonomics/numeric/named",
            "{\"value\":\"1\"}"
        );
        Require(named.StatusCode == HttpStatusCode.NoContent && numericString.StatusCode == HttpStatusCode.BadRequest, "named-only reading lost its finite string distinction");

        using var special = await client.GetAsync("/ergonomics/numeric/special");
        var specialBytes = await special.Content.ReadAsByteArrayAsync();
        using var specialBody = JsonDocument.Parse(specialBytes);
        var specialOutputMatches = specialBody.RootElement
            .GetProperty("named")
            .GetString() == "NaN"
            && specialBody.RootElement
            .GetProperty("quoted")
            .GetString() == "12.5"
            && specialBody.RootElement.GetProperty("plain").ValueKind == JsonValueKind.Number
            && specialBody.RootElement
            .GetProperty("quotedSpecial")
            .GetString() == "Infinity";
        Require(
specialOutputMatches,
            "actual named/quoted output does not match its registered policy"
        );

        using var derived = await client.GetAsync("/ergonomics/numeric/derived");
        var derivedBytes = await derived.Content.ReadAsByteArrayAsync();
        using var derivedBody = JsonDocument.Parse(derivedBytes);
        var derivedOutputMatches = derivedBody.RootElement
            .GetProperty("kind")
            .GetString() == "derived"
            && derivedBody.RootElement.GetProperty("count").ValueKind == JsonValueKind.Null
            && derivedBody.RootElement.GetProperty("amount").ValueKind == JsonValueKind.Number;
        Require(
derivedOutputMatches,
            "declared derived metadata was not serialized by the actual base contract"
        );

        using var custom = await client.GetAsync("/ergonomics/numeric/custom");
        var customBytes = await custom.Content.ReadAsByteArrayAsync();
        using var customBody = JsonDocument.Parse(customBytes);
        var customOutputMatches = customBody.RootElement
            .GetProperty("number")
            .GetString() == "id:7"
            && customBody.RootElement.GetProperty("ordinary").ValueKind == JsonValueKind.Number;
        Require(
customOutputMatches,
            "custom underlying integer conversion was replaced by a nullable-wrapper guess"
        );
    }

    /// <summary>Asserts family kinds, declared polymorphism and owned custom constraints in the actual producer output.</summary>
    private static void VerifyFamilySchemas(JsonElement schemas)
    {
        var family = FindSchema<NumericFamily>(schemas);
        foreach (var property in family
            .GetProperty("properties")
            .EnumerateObject())
        {
            var type = property.Value.GetProperty("type");
            var kinds = type
                .EnumerateArray()
                .Select(ReadString)
                .ToArray();
            var familyKindsMatch = kinds.Contains("null") && kinds.Contains("string") && (kinds.Contains("integer") || kinds.Contains("number"));
            Require(familyKindsMatch, "numeric family schema lost actual nullable/permissive kinds");
            var producerPatternRemoved = !property.Value.TryGetProperty("pattern", out _);
            Require(producerPatternRemoved, "producer retained a numeric-string regex that rejects valid native input");
        }

        var derived = FindSchema<NumericDerived>(schemas);
        AssertKinds(
            derived,
            "count",
            numeric: true,
            text: false,
            nullable: true
        );
        var amount = derived
            .GetProperty("properties")
            .GetProperty("amount")
            .GetProperty("type");
        var derivedNumberKindsMatch = amount
            .EnumerateArray()
            .Any(IsNumberKind) && !amount
            .EnumerateArray()
            .Any(IsStringKind);
        Require(derivedNumberKindsMatch, "declared polymorphic numeric metadata was omitted");

        var custom = FindSchema<CustomNumbers>(schemas);
        AssertKinds(
            custom,
            "number",
            numeric: false,
            text: true,
            nullable: true
        );
        AssertKinds(
            custom,
            "ordinary",
            numeric: true,
            text: false,
            nullable: true
        );
        var customPatternPreserved = custom
            .GetProperty("properties")
            .GetProperty("number")
            .GetProperty("pattern")
            .GetString() == "^id:[0-9]+$";
        Require(customPatternPreserved, "numeric correction erased the custom converter's owned constraint");
        var special = FindSchema<NumericSpecialResponse>(schemas);
        var named = special
            .GetProperty("properties")
            .GetProperty("named");
        var namedOutputConstrained = named
            .GetProperty("allOf")
            .EnumerateArray()
            .Any(HasAlternatives);
        Require(namedOutputConstrained, "named-only floating output lost its finite enum constraint");
    }

    /// <summary>Finds the actual schema independently of namespace-based producer naming.</summary>
    private static JsonElement FindSchema<T>(JsonElement schemas)
    {
        var name = typeof(T).Name;
        bool MatchesName(JsonProperty item) => item.Name.EndsWith(name, StringComparison.Ordinal);
        return schemas
            .EnumerateObject()
            .Single(MatchesName).Value;
    }

    /// <summary>Asserts emitted primitive types rather than relying on source attributes.</summary>
    private static void AssertKinds(
        JsonElement schema,
        string property,
        bool numeric,
        bool text,
        bool nullable
    )
    {
        var type = schema
            .GetProperty("properties")
            .GetProperty(property)
            .GetProperty("type");
        var kinds = type.ValueKind == JsonValueKind.Array
            ? type
            .EnumerateArray()
            .Select(ReadString)
            .ToArray()
            : new[] { type.GetString() };
        var kindsMatch = kinds.Contains("integer") == numeric && kinds.Contains("string") == text && kinds.Contains("null") == nullable;
        Require(kindsMatch, "exported numeric kinds disagree with actual profile behavior for " + property);
    }

    /// <summary>Preserves actual HTTP POST while preparing bounded JSON content before dispatch.</summary>
    private static async Task<HttpResponseMessage> PostJsonAsync(
        HttpClient client,
        string route,
        string body
    )
    {
        using var content = JsonContent(body);
        return await client.PostAsync(route, content);
    }

    /// <summary>Selects the actual operation route from native paths.</summary>
    private static bool IsValuePath(JsonProperty path) => path.Name.EndsWith("/values/{valueId}", StringComparison.Ordinal);

    /// <summary>Selects the handler-owned conditional header.</summary>
    private static bool IsConditionalHeader(JsonElement item)
    {
        var name = item.GetProperty("name");
        return name.GetString() == "If-Match";
    }

    /// <summary>Selects numeric schema kinds.</summary>
    private static bool IsNumberKind(JsonElement item) => item.GetString() == "number";

    /// <summary>Selects string schema kinds.</summary>
    private static bool IsStringKind(JsonElement item) => item.GetString() == "string";

    /// <summary>Finds the native named-literal alternative constraint.</summary>
    private static bool HasAlternatives(JsonElement item) => item.TryGetProperty("anyOf", out _);

    /// <summary>Reads primitive schema-kind labels without a nested projection body.</summary>
    private static string? ReadString(JsonElement item) => item.GetString();

    /// <summary>Creates UTF-8 request bytes for actual bounded HTTP admission.</summary>
    private static StringContent JsonContent(string text) => new(
        text,
        Encoding.UTF8,
        "application/json"
    );

    /// <summary>Retains an independent observable failure oracle.</summary>
    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
