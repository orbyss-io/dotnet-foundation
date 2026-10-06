using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Orbyss.Foundation.Collections.Core;
using Orbyss.Foundation.Json;

var source = new[] { "a", "b" };
var owned = new ValueSequence<string>(source);
source[0] = "changed";
Require(owned[0] == "a", "caller retained ownership");
var equal = new ValueSequence<string>(["a", "b"]);
Require(owned.Equals(equal) && owned.GetHashCode() == equal.GetHashCode(), "ordered value equality changed");
Require(!owned.Equals(new ValueSequence<string>(["b", "a"])), "order ignored");
Require(new Holder(owned) == new Holder(equal), "containing record equality changed");
Require(!ImmutableArray.Create("a", "b").Equals(ImmutableArray.Create("a", "b")), "BCL baseline assumption changed");
Require(ValueSequence<string>.Empty.Equals(new ValueSequence<string>([])), "empty semantics changed");
Reject<ArgumentNullException>(() => _ = new ValueSequence<string>(null!));
Reject<ArgumentException>(() => _ = new ValueSequence<string>([null!]));
var item = new StringBuilder("before");
var shallow = new ValueSequence<StringBuilder>([item]);
item.Append(" after");
Require(shallow[0].ToString() == "before after", "collection unexpectedly deep freezes elements");
var profile = new JsonProfile(new() { Extensions = [ValueSequenceJsonExtension.ExtensionId] }, [new ValueSequenceJsonExtension()]);
var bytes = profile.Serialize(new Holder(owned));
Require(Encoding.UTF8.GetString(bytes) == "{\"items\":[\"a\",\"b\"]}", "wire shape changed");
Require(profile.Deserialize<Holder>(bytes) == new Holder(owned), "wire roundtrip lost equality");
Reject<JsonProfileException>(() => profile.Deserialize<Holder>("{\"items\":[null]}"u8));
Reject<JsonProfileException>(() => profile.Deserialize<Holder>("{\"items\":null}"u8));
var generated = new JsonProfile(new() { Extensions = [ValueSequenceJsonExtension.ExtensionId, "holder-generated-v1"] },
    [new ValueSequenceJsonExtension(), new HolderJsonExtension()]);
Require(generated.Deserialize<Holder>(generated.Serialize(new Holder(owned))) == new Holder(owned), "generated collection metadata drifted");
var nested = new NestedHolder(new([owned, ValueSequence<string>.Empty]));
Require(generated.Deserialize<NestedHolder>(generated.Serialize(nested)) == nested, "nested generated collection equality drifted");
Require(Encoding.UTF8.GetString(generated.Serialize(nested)) == "{\"items\":[[\"a\",\"b\"],[]]}", "nested collection wire shape changed");
using var vectors = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "canonical-v1.json")));
foreach (var vector in vectors.RootElement.EnumerateArray())
{
    using var document = JsonDocument.Parse(vector.GetProperty("canonical").GetString()!);
    using var buffer = new MemoryStream();
    using var writer = new CanonicalUtf8Writer(1024 * 1024, buffer);
    Emit(writer, document.RootElement);
    var hash = writer.CompleteSha256();
    Require(Convert.ToHexStringLower(buffer.ToArray()) == vector.GetProperty("utf8Hex").GetString(), "historical bytes drifted");
    Require(hash == vector.GetProperty("sha256").GetString(), "historical hash drifted");
    using var hashOnly = new CanonicalUtf8Writer(1024 * 1024);
    Emit(hashOnly, document.RootElement);
    Require(hashOnly.CompleteSha256() == hash && hashOnly.ByteCount == buffer.Length, "streaming hash differs");
}
using (var exact = new CanonicalUtf8Writer(6)) { exact.String("😀"); Require(exact.ByteCount == 6, "Unicode count changed"); }
using (var over = new CanonicalUtf8Writer(5)) { Reject<JsonOutputLimitException>(() => over.String("😀")); Require(over.ByteCount <= 5, "retained too much output"); }
using (var malformed = new CanonicalUtf8Writer(100))
{
    malformed.Raw("partial"); Reject<EncoderFallbackException>(() => malformed.String("\ud800"));
    Reject<InvalidOperationException>(() => malformed.CompleteSha256());
}
using (var malformed = new CanonicalUtf8Writer(100))
{
    malformed.Raw("partial"); Reject<DecoderFallbackException>(() => malformed.RawUtf8(new byte[] { 255 }));
    Reject<InvalidOperationException>(() => malformed.CompleteSha256());
}
using (var large = new CanonicalUtf8Writer(30_000_000))
{
    var fragment = new string('x', 8192);
    var before = GC.GetAllocatedBytesForCurrentThread();
    for (var index = 0; index < 3000; index++) large.Raw(fragment);
    _ = large.CompleteSha256();
    Require(GC.GetAllocatedBytesForCurrentThread() - before < 100_000, "incremental hash allocated aggregate bytes");
    Reject<InvalidOperationException>(() => large.Raw("after seal"));
}
Console.WriteLine("Collection ownership/equality, typed JSON and historical streaming canonical bytes/hashes passed.");
static void Emit(CanonicalUtf8Writer writer, JsonElement value)
{
    switch (value.ValueKind)
    {
        case JsonValueKind.Object:
            writer.Raw("{"); var first = true;
            foreach (var property in value.EnumerateObject())
            {
                if (!first) writer.Raw(","); first = false;
                writer.String(property.Name); writer.Raw(":"); Emit(writer, property.Value);
            }
            writer.Raw("}"); break;
        case JsonValueKind.Array:
            writer.Raw("["); var firstItem = true;
            foreach (var element in value.EnumerateArray()) { if (!firstItem) writer.Raw(","); firstItem = false; Emit(writer, element); }
            writer.Raw("]"); break;
        case JsonValueKind.String: writer.String(value.GetString()); break;
        default: writer.Raw(value.GetRawText()); break;
    }
}
static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
static void Reject<T>(Action action) where T : Exception
{
    try { action(); } catch (T) { return; }
    throw new InvalidOperationException("Expected " + typeof(T).Name);
}
