using Orbyss.Foundation.Json.Admission.Probe.Contracts;
using Orbyss.Foundation.Json.Admission.Probe.Operations.ChangeValue;
using Orbyss.Foundation.Json.AspNetCore;

namespace Orbyss.Foundation.Json.Admission.Probe;

/// <summary>Exercises native service composition without executing request/effect factories.</summary>
internal static class ContractRegistrationChecks
{
    /// <summary>Requires idempotent equivalence, complete metadata pairs and visible conflicts before mutation.</summary>
    internal static void Run()
    {
        ContractFactoryChecks.Run();
        var services = new ServiceCollection();
        services.AddJsonContract(Request.Contract);
        var equivalent = JsonContractMetadata<Request>.Request();
        services.AddJsonContract(equivalent);
        Require(services.Count == 2, "equivalent repeated contract registration changed the service graph");

        var original = services.ToArray();
        var profile = new JsonProfileKey("other-request");
        var differentProfile = JsonContractMetadata<Request>.Request(profile: profile);
        void RegisterDifferentProfile() => services.AddJsonContract(differentProfile);
        Reject(RegisterDifferentProfile);
        var supported = JsonProfileRequirement.StrictRequest(maximumBytes: 1024);
        var differentRequirement = JsonContractMetadata<Request>.Request(supported);
        void RegisterDifferentRequirement() => services.AddJsonContract(differentRequirement);
        Reject(RegisterDifferentRequirement);
        var graphUnchanged = original.SequenceEqual(services);
        Require(graphUnchanged, "conflicting registration partially mutated the graph");

        var metadataOnly = new ServiceCollection();
        metadataOnly.AddSingleton<IJsonContractMetadata>(Request.Contract);
        metadataOnly.AddJsonContract(equivalent);
        var readerRegistered = metadataOnly.Count == 2 && metadataOnly.Single(item => item.ServiceType == typeof(IJsonRequestReader<Request>)).ImplementationFactory is not null;
        Require(readerRegistered, "metadata-only declaration was treated as a complete reader registration");

        var custom = new ServiceCollection();
        IJsonRequestReader<Request> CustomReader(IServiceProvider provider) => throw new InvalidOperationException("Custom reader must not execute during registration.");
        custom.AddSingleton<IJsonRequestReader<Request>>(CustomReader);
        var descriptor = custom.Single();
        custom.AddJsonContract(Request.Contract);
        var customReaderPreserved = custom.Contains(descriptor) && custom.Count == 2;
        Require(customReaderPreserved, "native registration replaced an intentional custom reader factory");

        var factoryCalls = 0;
        var unknown = new ServiceCollection();
        IJsonContractMetadata UnknownMetadata(IServiceProvider provider)
        {
            factoryCalls++;
            return Request.Contract;
        }
        unknown.AddSingleton<IJsonContractMetadata>(UnknownMetadata);
        unknown.AddJsonContract(Request.Contract);
        Require(factoryCalls == 0, "registration executed an unknown metadata factory");

        VerifySchemaConflict();
        VerifyRecursiveCollection();
        VerifyScopedEndpoint();
    }

    /// <summary>Rejects incompatible read/write numeric meanings for one shared schema.</summary>
    private static void VerifySchemaConflict()
    {
        var options = new FoundationJsonOptions();
        var catalog = new JsonProfileCatalog(options.Profiles);
        var output = JsonContractMetadata<RequestNumbers>.Response();
        var definitions = new IJsonContractMetadata[] { RequestNumbers.Contract, output };
        void CreateConflictingSchema() => _ = new JsonContractSchemaTransformer(catalog, definitions);
        Reject(CreateConflictingSchema);
    }

    /// <summary>Uses the actual helper and DI scope cache, with disposal owned by those scopes.</summary>
    private static void VerifyScopedEndpoint()
    {
        var services = new ServiceCollection();
        services.AddJsonEndpoint<ScopedEndpointControl>();
        var options = new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true };
        using var provider = services.BuildServiceProvider(options);
        using (var first = provider.CreateScope())
        {
            var one = first.ServiceProvider.GetRequiredService<ScopedEndpointControl>();
            var same = first.ServiceProvider.GetRequiredService<ScopedEndpointControl>();
            using var second = provider.CreateScope();
            var another = second.ServiceProvider.GetRequiredService<ScopedEndpointControl>();
            var scopedIdentityPreserved = ReferenceEquals(one, same) && !ReferenceEquals(one, another);
            Require(scopedIdentityPreserved, "endpoint helper lost scoped identity");
        }
        Require(ScopedEndpointControl.Disposed == 2, "request scopes did not own endpoint disposal");

        var cold = new ServiceCollection();
        cold.AddJsonEndpoint<ColdEndpointControl>();
        using var metadataProvider = cold.BuildServiceProvider(options);
        Require(ColdEndpointControl.Created == 0, "cold metadata composition instantiated runtime endpoint dependencies");
    }

    /// <summary>Requires finite metadata traversal without silently changing native collection serialization.</summary>
    private static void VerifyRecursiveCollection()
    {
        var options = new FoundationJsonOptions();
        var catalog = new JsonProfileCatalog(options.Profiles);
        var definition = JsonContractMetadata<RecursiveCollection>.Response();
        var contracts = new IJsonContractMetadata[] { definition };
        _ = new JsonContractSchemaTransformer(catalog, contracts);
        var profile = catalog.Get(JsonProfileKeys.SuccessResponse);
        var value = new RecursiveCollection();
        var actual = profile.Serialize(value);
        var expected = "[]"u8.ToArray();
        var emptyCollectionPreserved = actual.SequenceEqual(expected);
        Require(emptyCollectionPreserved, "recursive collection control did not exercise native serialization");
    }

    /// <summary>Requires the intended ownership/conflict failure rather than accepting a setup error.</summary>
    private static void Reject(Action action)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException error) when (error.Message.Contains("Conflicting typed JSON", StringComparison.Ordinal)
            || error.Message.Contains("incompatible numeric schemas", StringComparison.Ordinal))
        {
            return;
        }

        throw new InvalidOperationException("The intended JSON declaration conflict was not rejected.");
    }

    /// <summary>Reports only actual failed observations.</summary>
    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
