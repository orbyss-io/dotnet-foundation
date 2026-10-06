using System.Text;
using Orbyss.Foundation.Json;

var bounded = new JsonProfile(new() { MaxBytes = 32 });
var enumerationCount = 0;
try
{
    _ = bounded.Serialize(Values());
    throw new InvalidOperationException("oversized server output accepted");
}
catch (JsonOutputLimitException) { }
Require(enumerationCount < 5000, "retained whole oversized output before rejecting");
var exact = new JsonProfile(new() { MaxBytes = 32 });
Require(exact.Serialize(new string('a', 30)).Length == 32, "exact-cap output rejected");
Reject<JsonOutputLimitException>(() => exact.Serialize(new string('a', 31)));
var nearCap = new JsonProfile(new() { MaxBytes = 1_000_000 });
var nearScalar = new string('a', 999_999);
var nearBefore = GC.GetAllocatedBytesForCurrentThread();
Reject<JsonOutputLimitException>(() => nearCap.Serialize(nearScalar));
Require(GC.GetAllocatedBytesForCurrentThread() - nearBefore < 100_000, "near-cap oversized scalar encoded before rejection");
var unicode = "😀é\n\"\\";
var encoded = new JsonProfile(new()).Serialize(unicode);
Require(new JsonProfile(new() { MaxBytes = encoded.Length }).Serialize(unicode).SequenceEqual(encoded), "Unicode exact-cap output changed");
Reject<JsonOutputLimitException>(() => new JsonProfile(new() { MaxBytes = encoded.Length - 1 }).Serialize(unicode));
var huge = new string('a', 10_000_000);
var before = GC.GetAllocatedBytesForCurrentThread();
Reject<JsonOutputLimitException>(() => bounded.Serialize(huge));
Require(GC.GetAllocatedBytesForCurrentThread() - before < 100_000, "huge scalar allocated whole output");
Reject<JsonProfileException>(() => bounded.Deserialize<string>(Encoding.UTF8.GetBytes("\"" + new string('a', 31) + "\"")));
Console.WriteLine("Exact-cap, cap-plus-one, Unicode, early enumerable/scalar rejection and request/output direction passed.");
IEnumerable<int> Values() { for (var value = 0; value < 100_000; value++) { enumerationCount++; yield return value; } }
static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
static void Reject<T>(Action action) where T : Exception
{
    try { action(); } catch (T) { return; }
    throw new InvalidOperationException("Expected " + typeof(T).Name);
}
