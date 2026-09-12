using System.Text;
using Orbyss.Foundation.Json;
using Orbyss.Foundation.Json.Probe;

if (args.Length == 2 && args[0] == "--vectors")
{
    foreach (var line in File.ReadLines(args[1]))
        Console.WriteLine(Encoding.UTF8.GetString(JsonCanonicalizer.Canonicalize(Encoding.UTF8.GetBytes(line))));
    return;
}
var strict = new JsonProfile(new());
const string legacyCanonical = """{"answers":[{"kind":"decimal","normalized":"2.00","questionId":"length","raw":"2,00"},{"kind":"choice","optionId":"basic","questionId":"material"}],"budget":null,"journeyId":"11111111-2222-3333-4444-555555555555","references":{"calculationPolicyRevision":"policy-1","catalogRevision":"catalog-1","contentRevision":"content-1","releaseId":"release-1","ruleRevision":"rules-1"},"requestedLanguages":["nl-NL","en"],"resolvedLanguage":"nl","schemaVersion":1,"selectedLanguage":null}""";
var legacy = strict.Deserialize<LegacyEnvelope>(Encoding.UTF8.GetBytes(legacyCanonical));
var reordered = legacy with { Answers = legacy.Answers.Reverse().ToArray() };
// Product normalization stays in this fixture adapter, never in generic canonicalization.
var normalized = reordered with { Answers = reordered.Answers.OrderBy(answer => answer.QuestionId, StringComparer.Ordinal).ToArray() };
var legacyBytes = JsonCanonicalizer.Canonicalize(strict.Serialize(normalized));
Require(Encoding.UTF8.GetString(legacyBytes) == legacyCanonical, "legacy ASCII wire bytes changed");
Require(normalized.RequestedLanguages.SequenceEqual(new[] { "nl-NL", "en" }), "language order changed");
var modified = normalized with { Answers = [((LegacyDecimal)normalized.Answers[0]) with { Raw = "2,0" }, normalized.Answers[1]] };
Require(!JsonCanonicalizer.Canonicalize(strict.Serialize(modified)).SequenceEqual(legacyBytes), "legacy raw string identity changed");
var generated = new JsonProfile(new() { Extensions = ["probe"] }, [new ProbeExtension()]);
var input = """{"name":"hello","optional":null,"number":4.50}"""u8.ToArray();
try
{
    var conflicting = new JsonProfile(new() { Extensions = ["conflict"] }, [new ConflictingExtension()]);
    conflicting.Deserialize<ProbeDto>(input);
    throw new Exception("Overlapping nested converters accepted.");
}
catch (InvalidOperationException) { }
try { strict.ValidateInput(new byte[] { 34, 255, 34 }); throw new Exception("Invalid UTF-8 accepted"); }
catch (JsonProfileException) { }
var dto = strict.Deserialize<ProbeDto>(input);
Require(dto.Number.Text == "4.50", "number lexeme changed");
Require(strict.Serialize(dto).SequenceEqual(input), "typed bytes changed");
Require(generated.Serialize(generated.Deserialize<ProbeDto>(input)).SequenceEqual(input), "sourcegen parity failed");
foreach (var invalid in new[] {
    """{"name":null,"optional":null,"number":1}""",
    """{"name":"hello","number":1}""",
    """{"name":"hello","optional":null,"number":1,"extra":1}""",
    """{"name":"hello","name":"again","optional":null,"number":1}""",
    """{"name":"hello","na\u006de":"again","optional":null,"number":1}""",
    """{"name":"\ud800","optional":null,"number":1}""",
    """{"name":"hello","optional":null,"number":"1"}""",
    "{}{}"
})
{
    try { strict.Deserialize<ProbeDto>(Encoding.UTF8.GetBytes(invalid)); throw new Exception("Invalid contract accepted: " + invalid); }
    catch (JsonProfileException) { }
}
var tolerant = new JsonProfile(new() { Preset = "tolerant-response" });
Require(tolerant.Deserialize<ProbeDto>("""{"NAME":"hello","optional":null,"number":1,"extra":2}"""u8).Name == "hello", "tolerant reader failed");
try { new JsonProfile(new() { Extensions = ["System.Type"] }); throw new Exception("arbitrary extension accepted"); }
catch (InvalidOperationException) { }
var limited = new JsonProfile(new() { MaxBytes = 3 });
try { await limited.ReadAsync<object>(new MemoryStream("null"u8.ToArray())); throw new Exception("size limit ignored"); }
catch (JsonProfileException error) { Require(error.Code == "json_size_exceeded", "unstable size error"); }
foreach (var (source, expected) in new[] {
    ("1e-320", "1e-320"), ("-1e-320", "-1e-320"), ("5e-324", "5e-324"),
    ("4.50", "4.5"), ("1e-6", "0.000001"), ("1e-7", "1e-7"), ("1e21", "1e+21"),
    ("1e20", "100000000000000000000"), ("333333333.33333329", "333333333.3333333"),
    ("""{ "z": [2,1], "a": "€\u000f\n" }""", """{"a":"€\u000f\n","z":[2,1]}""")
})
    Require(Encoding.UTF8.GetString(JsonCanonicalizer.Canonicalize(Encoding.UTF8.GetBytes(source))) == expected, "canonical vector failed: " + source);
foreach (var invalid in new[] { "-0", "-0.0", "1e999", """{"a":1,"a":2}""", """"\ud800"""" })
{
    try { JsonCanonicalizer.Canonicalize(Encoding.UTF8.GetBytes(invalid)); throw new Exception("Invalid canonical input accepted"); }
    catch (JsonProfileException) { }
}
Console.WriteLine("Typed JSON admission, raw lexemes, sourcegen parity and canonical vectors passed.");
static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
