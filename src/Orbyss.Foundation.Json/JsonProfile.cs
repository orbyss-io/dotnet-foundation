using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Orbyss.Foundation.Json;

/// <summary>Provides typed, bounded JSON operations over frozen serializer options.</summary>
public sealed class JsonProfile
{
    /// <summary>Stores immutable serializer options.</summary>
    private readonly JsonSerializerOptions options;
    /// <summary>Gets the maximum encoded size.</summary>
    public int MaxBytes { get; }
    /// <summary>Gets the maximum nesting depth.</summary>
    public int MaxDepth { get; }
    /// <summary>Gets the admitted parser/serializer preset identity.</summary>
    public string Preset { get; }

    /// <summary>Compiles a fixed preset and explicitly selected stateless extensions.</summary>
    public JsonProfile(JsonProfileSettings settings, IEnumerable<IJsonProfileExtension>? extensions = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.MaxBytes is < 1 or > 16_777_216 || settings.MaxDepth is < 1 or > 64 ||
            settings.Preset is not (JsonProfileKeys.StrictRequest or JsonProfileKeys.TolerantResponse))
            throw new InvalidOperationException("Foundation:Json profile has invalid limits or preset.");
        MaxBytes = settings.MaxBytes;
        MaxDepth = settings.MaxDepth;
        Preset = settings.Preset;
        var strict = settings.Preset == JsonProfileKeys.StrictRequest;
        options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = !strict,
            UnmappedMemberHandling = strict ? JsonUnmappedMemberHandling.Disallow : JsonUnmappedMemberHandling.Skip,
            AllowDuplicateProperties = false,
            RespectNullableAnnotations = true,
            RespectRequiredConstructorParameters = true,
            NumberHandling = JsonNumberHandling.Strict,
            MaxDepth = MaxDepth,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver()
        };
        var registered = new Dictionary<string, IJsonProfileExtension>(StringComparer.Ordinal);
        foreach (var extension in extensions ?? [])
            if (string.IsNullOrWhiteSpace(extension.Id) || !registered.TryAdd(extension.Id, extension))
                throw new InvalidOperationException("Foundation:Json has conflicting extension identities.");
        var selected = new HashSet<string>(StringComparer.Ordinal);
        IJsonTypeInfoResolver? resolver = null;
        foreach (var id in settings.Extensions ?? throw new InvalidOperationException("Foundation:Json extensions are missing."))
        {
            if (!selected.Add(id) || !registered.TryGetValue(id, out var extension))
                throw new InvalidOperationException("Foundation:Json selects an unknown or duplicate extension.");
            foreach (var converter in extension.Converters) options.Converters.Add(converter);
            if (extension.Resolver is not null)
            {
                if (resolver is not null) throw new InvalidOperationException("Foundation:Json has conflicting resolvers.");
                resolver = extension.Resolver;
            }
        }
        if (resolver is not null) options.TypeInfoResolver = resolver;
        options.Converters.Add(new BoundedStringJsonConverter(MaxBytes));
        options.TypeInfoResolver = new GuardedJsonTypeInfoResolver(options.TypeInfoResolver!);
        options.MakeReadOnly();
    }

    /// <summary>Resolves metadata governed by this profile; source-generated resolvers receive the same options.</summary>
    public JsonTypeInfo<T> TypeInfo<T>()
    {
        if (options.Converters.Count(converter => converter.CanConvert(typeof(T))) > 1)
            throw new InvalidOperationException("Foundation:Json has conflicting converters for the requested type.");
        return (JsonTypeInfo<T>)options.GetTypeInfo(typeof(T));
    }

    /// <summary>Deserializes a known contract with this profile's metadata.</summary>
    public T Deserialize<T>(ReadOnlySpan<byte> utf8) => Deserialize(utf8, TypeInfo<T>());

    /// <summary>Deserializes with explicit metadata belonging to this immutable profile.</summary>
    public T Deserialize<T>(ReadOnlySpan<byte> utf8, JsonTypeInfo<T> typeInfo)
    {
        RequireOptions(typeInfo);
        ValidateInput(utf8);
        try { return JsonSerializer.Deserialize(utf8, typeInfo) ?? throw new JsonProfileException(JsonFailureCodes.NullRoot); }
        catch (JsonException) { throw new JsonProfileException(JsonFailureCodes.InvalidContract); }
    }

    /// <summary>Serializes a known contract, retaining explicitly nullable members.</summary>
    public byte[] Serialize<T>(T value) => Serialize(value, TypeInfo<T>());

    /// <summary>Serializes with explicit profile-governed metadata.</summary>
    public byte[] Serialize<T>(T value, JsonTypeInfo<T> typeInfo)
        => Serialize(value, typeInfo, CancellationToken.None);

    /// <summary>Serializes within finite retained/scratch bounds and observes caller cancellation before commitment.</summary>
    public byte[] Serialize<T>(T value, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RequireOptions(typeInfo);
        if (value is null) throw new JsonResponseContractException(JsonFailureCodes.ResponseNullRoot);
        try
        {
            var buffer = new BoundedJsonBuffer(MaxBytes, cancellationToken);
            using var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { MaxDepth = MaxDepth });
            JsonSerializer.Serialize(writer, value, typeInfo);
            writer.Flush();
            var bytes = buffer.ToArray();
            try { ValidateInput(bytes); }
            catch (JsonProfileException) { throw new JsonResponseContractException(JsonFailureCodes.ResponseInvalidContract); }
            return bytes;
        }
        catch (JsonException) { throw new JsonResponseContractException(JsonFailureCodes.ResponseInvalidContract); }
    }

    /// <summary>Reads at most the configured byte limit plus one detection byte, honoring cancellation.</summary>
    public async Task<T> ReadAsync<T>(Stream stream, CancellationToken cancellationToken = default)
    {
        using var data = new MemoryStream();
        var buffer = new byte[Math.Min(MaxBytes + 1, 8192)];
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var count = await stream.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, MaxBytes + 1 - (int)data.Length)), cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (count == 0) break;
            data.Write(buffer, 0, count);
            if (data.Length > MaxBytes) throw new JsonProfileException(JsonFailureCodes.SizeExceeded);
        }
        cancellationToken.ThrowIfCancellationRequested();
        return Deserialize<T>(data.GetBuffer().AsSpan(0, (int)data.Length));
    }

    /// <summary>Rejects malformed Unicode, duplicate decoded keys, excessive depth, and extra roots before typed conversion.</summary>
    public void ValidateInput(ReadOnlySpan<byte> utf8)
    {
        if (utf8.Length > MaxBytes) throw new JsonProfileException(JsonFailureCodes.SizeExceeded);
        try
        {
            var reader = new Utf8JsonReader(utf8, new JsonReaderOptions { MaxDepth = MaxDepth });
            var objects = new Stack<HashSet<string>>();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.StartObject)
                    objects.Push(new HashSet<string>(options.PropertyNameCaseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal));
                else if (reader.TokenType == JsonTokenType.EndObject) objects.Pop();
                else if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    if (!objects.Peek().Add(reader.GetString()!)) throw new JsonProfileException(JsonFailureCodes.DuplicateMember);
                }
                else if (reader.TokenType == JsonTokenType.String) _ = reader.GetString();
            }
            if (reader.BytesConsumed == 0) throw new JsonProfileException(JsonFailureCodes.InvalidSyntax);
        }
        catch (JsonException) { throw new JsonProfileException(JsonFailureCodes.InvalidSyntax); }
        catch (InvalidOperationException) { throw new JsonProfileException(JsonFailureCodes.InvalidUnicode); }
    }

    /// <summary>Prevents supplied metadata from bypassing the selected profile's guarantees.</summary>
    private void RequireOptions(JsonTypeInfo typeInfo)
    {
        if (!ReferenceEquals(typeInfo.Options, options))
            throw new InvalidOperationException("Use metadata resolved by JsonProfile.TypeInfo with the registered resolver.");
    }
}
