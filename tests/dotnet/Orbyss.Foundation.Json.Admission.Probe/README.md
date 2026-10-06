# HTTP JSON admission acceptance

This executable probe uses actual CShells activation and Kestrel requests, including intentionally
incorrect endpoint adaptations. It checks precommit stream/pipe/start rejection, retained rejection
after a caught bypass, direct JSON/body-reader and ignored-input rejection, actual native DTO binding
rejection, chunked exact-cap/cap-plus-one input, observable request cancellation, a configured 512-byte
problem budget and actual OpenAPI extension export. Separate processes qualify invalid shell capacity,
strictness and resolver configuration without letting an invalid shell contaminate valid endpoints.

The provenance cases exercise externally implemented `IFoundationProblemResult` values returning
200 and 409 with oversized bodies, an ignored request paired with a forged problem, and a native
`Results.Problem` using status 200. All must fail before their result executes; a legitimate native
409 still executes through the common bounded writer. Run the executable with `forged` to print
each independent response status and byte count. The public interface is descriptive rather than
proof that an arbitrary implementation has passed admission.

Run `python tests/validate_json_admission.py` from Foundation with its pinned SDK. An explicitly
selected development substitute can use `--development-sdk-substitute` from the approved SDK's
working directory; that evidence records the actual SDK and does not satisfy pinned release validation.

The chunked 64/65-byte vectors count decoded JSON independently of transport framing. Kestrel also
counts chunk prefixes, suffixes and extensions against its transport ceiling; setting that ceiling
to the JSON profile's decoded byte limit incorrectly rejects an exact-cap chunked request. Keep
host transport limits independent and finite while the profile reader enforces actual decoded bytes.
The [ASP.NET v10.0.11 chunk parser](https://raw.githubusercontent.com/dotnet/aspnetcore/v10.0.11/src/Servers/Kestrel/Core/src/Internal/Http/Http1ChunkedEncodingMessageBody.cs)
is the primary source for this distinction.
