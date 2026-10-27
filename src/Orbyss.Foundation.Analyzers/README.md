# Orbyss.Foundation.Analyzers

- `ORB1001` prevents `AddHostedService` inside a CShells feature because the root Generic Host will not start it.
- `ORB1002` suggests named arguments for calls with four or more arguments. Consumers control its severity in `.editorconfig`.
- `ORB1003` requires each C# source file to declare exactly one named type, including nested and supporting types.
- `ORB1004` requires private helper methods to appear after the type's other members.
- `ORB1005` requires purposeful XML documentation on every declared type and member, including private members.
- `ORB1006` requires every packaged CShells feature to declare `[ShellFeature]` with the exact
  `FoundationFeatureIdentity`, preventing CLR, package-descriptor, and shell identity drift.
- `ORB1007` requires vertical layout for lists with more than two parameters/arguments and all already
  split nonempty lists, including primary constructors. The closing parenthesis aligns with the
  opening line's indentation, preserving tabs.
- `ORB1008` requires runtime calls, constructed values (including arrays, collection expressions,
  record `with` expressions and explicit/implicit `stackalloc`),
  and deferred lambda/anonymous-method bodies in arguments to be prepared separately. The actual
  compile-time `nameof` operator is exempt; a method named `@nameof` is not.
- `ORB1009` requires a fluent receiver and every chained invocation to occupy separate indented lines,
  including the first invocation. Conditional access, parentheses, and intervening properties retain
  this obligation. A bare factory/delegate call used as a chain receiver needs an explicit receiver
  or a separately prepared value. An ordinary single invocation stays concise.
  A prepared parenthesized receiver uses its complete wrapper span and outer indentation.
- `ORB1010` requires fields, constants and field-like events before constructors and other members.

ORB1007–ORB1010 are selected together with the compiler-visible MSBuild property
`<FoundationSourceReadability>true</FoundationSourceReadability>`. The package imports its own
`buildTransitive/Orbyss.Foundation.Analyzers.props`; consumers do not need to recreate compiler plumbing.
The default leaves existing independent Foundation profiles unchanged. A consumer adopting this contract
must explicitly enable it for all owned authored source and repair existing violations before feature work
continues. A baseline, hidden source exclusion, or severity suppression is not conformance.
`buildTransitive/source-readability-contract.json` advertises `source-readability-v1`; verify the actual
resolved package and a native negative control before claiming capability. An unpublished source candidate
does not establish availability of a public package version. Existing ORB1002 remains an Info recommendation.

Once selected, the nine mandatory errors ORB1001, ORB1003–ORB1010 are nonconfigurable: EditorConfig
severity, NoWarn and warning pragmas cannot waive them. The selected
native analyzer reuses the existing Foundation policy checks and chooses protected descriptors for
that compilation; the legacy producer is mutually exclusive. Without selection, existing diagnostic
configurability remains unchanged. ORB1002 stays a configurable Info recommendation in both modes.
The consuming profile must also require analyzer execution and delivery of compiler-visible properties;
a disabled analyzer or generated-config pipeline cannot evaluate the selected contract.

The package's incremental generator rejects typed SuppressMessage and UnconditionalSuppressMessage
attributes targeting mandatory IDs with a descriptive `FoundationSourceSuppressionPolicy` compiler
`#error` (CS1029). This also inspects generated GlobalSuppressions already present as Compile inputs; source attributes cannot
waive that compiler error. ORB1002 and unrelated suppression attributes remain allowed. The capability
marker describes this separately from the nine analyzer diagnostics. Like other Roslyn generators, it
cannot inspect suppression attributes emitted by sibling generators in the same generator run; those
outputs remain a source-review boundary. By default, selected builds treat native
generator failures CS8784/CS8785 as errors; a mandatory consuming profile must reject explicit
NoWarn or WarningsNotAsErrors overrides for those failures.

Native EF Core main migrations deliberately remain editable and may have no generated header.
Within the actual EF relational `Migration.Up`/`Down` override, ORB1007–ORB1009 accept declarative
calls to the native MigrationBuilder/column/operation/table builders, including generated column and
constraint lambdas and annotation chains. Recognition uses exact relational assembly/type symbols,
not a migrations folder, filename or package prefix. This does not label the whole file generated:
authored helpers, local functions and declarations containing arbitrary calls remain checked, as do
the other source/lifecycle rules. Review migration operations/SQL normally; no header, formatter,
rewritten operations, alternate generator or global analyzer suppression is needed for this layout.

Generated syntax is excluded from source-quality rules through Roslyn's native generated-code treatment. The analyzer reports one list
diagnostic, one prepared-value diagnostic per offending argument, one per offending chain, and one per
misplaced field. It supplies no automatic refactoring: moving calls can change evaluation order, deferred
activation, cancellation and disposal. It does not certify XML comment meaning, abstraction quality, async
ownership, cancellation propagation, absolute deadlines, cleanup or post-dispatch uncertainty. These require
focused adverse tests and independent source review.

The SDK's CS4014 catches common unobserved task calls and CA2012 covers ValueTask misuse. Neither proves task
ownership, and blanket syntax bans on async void would reject event handlers; blanket `.Result` or `.Wait()`
bans would confuse unrelated user types and intentional completed-task boundaries. No additional async rule
is claimed here without a separately specified and qualified semantic contract. No rule enforces the
120-column target or contextual named arguments as an error. Constructor-versus-member ordering and private
helper first-use order remain source review obligations; ORB1004 only certifies private helpers are last.

Run `python tests/validate_analyzer.py` for separately compiled controls and a real local packed-package
install/build/repaired-build journey. Compiler setup failures and analyzer crashes never count as rejection.

The consumer scaffold references this package centrally with `PrivateAssets=all`; application projects do not
need individual analyzer references.
