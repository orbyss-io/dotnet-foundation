# Host transport and JSON admission

`hostsettings.json` / environment / command-line `Foundation:Transport` configures validated
`MaxRequestBodyBytes` and `MaxRequestHeadersBytes` before Kestrel starts. Defaults preserve native
30,000,000-byte body and 32 KiB header limits. Invalid values fail startup. This is process-wide transport
admission; shell JSON profiles cannot change another shell's server limits. The transport ceiling also
counts HTTP framing; the typed parser independently counts actual decoded JSON bytes. Equating these
ceilings would reject valid exact-cap chunked JSON.

Reverse proxies need an independently reviewed ceiling compatible with the host and admitted shell
profiles. JSON response/page bytes and page item counts are separate from transport body limits.
Problem output uses the configured problem-response profile with a 512-byte minimum and a finite,
nonrecursive safe fallback. Successful output is admitted before headers; cancellation or a transport
failure after commitment cannot be replaced with a new problem or described as rollback.
