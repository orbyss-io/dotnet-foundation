# Canonicalization implementation decision

User approved the scoped implementation after reviewing the failed dependency assessment on 2026-09-12.

## Assessment evidence

The published MackySoft.Json.Canonicalization 0.1.0 nupkg identifies source commit
697d0b45b3ce3bc42aa110d20c2204489a6e705f, includes MIT plus upstream license notices, targets netstandard2.1,
and depends on System.Text.Json >= 8.0.5. Executing its published DLL on this repository's .NET runtime
reproduced the upstream subnormal bug: input 1e-320 emitted a long zero-prefixed fraction ending in
1e-178, rather than 1e-320. Negative 1e-320 failed similarly. It is not selected.

Baqhub.Packages.JsonCanonicalization 1.0.1 has no dependencies and declares an Apache license URL, but
its published repository URL returned 404 and its nuspec supplied no source commit. Provenance was not
established. The jsoncanonicalizer package also did not establish published-source correspondence.
None of these packages is added to Foundation.

Sources:
- https://github.com/cyberphone/json-canonicalization/issues/34
- https://github.com/mackysoft/dotnet-foundations/tree/697d0b45b3ce3bc42aa110d20c2204489a6e705f
- https://api.nuget.org/v3-flatcontainer/mackysoft.json.canonicalization/0.1.0/mackysoft.json.canonicalization.0.1.0.nupkg
- https://api.nuget.org/v3-flatcontainer/baqhub.packages.jsoncanonicalization/1.0.1/baqhub.packages.jsoncanonicalization.1.0.1.nupkg

## Approved approach

Foundation's bounded RFC 8785 implementation uses System.Text.Json for syntax/token admission and .NET
shortest round-trip binary64 formatting for digits. It applies explicit ECMAScript decimal/exponent
placement and RFC string/key rules. No upstream algorithm source is copied or vendored.

The version is rfc8785-reject-negative-zero-v1, incorporating verified erratum 7920:
https://www.rfc-editor.org/errata/eid7920
Input that rounds to negative zero is rejected. Arrays and Unicode strings are never normalized.
Product wire formats and arbitrary-precision numeric identities are outside this algorithm.

The regression suite exercises hand-selected RFC-style thresholds, Unicode, malformed input, and the
subnormal defect. A deterministic 10,000-pattern binary64 corpus is independently formatted by Node/V8
and compared byte-for-byte, alongside Unicode/object vectors. This is regression evidence, not a formal
proof for every binary64 value. Numeric behavior is tied to the supported .NET runtime and rechecked in CI.
