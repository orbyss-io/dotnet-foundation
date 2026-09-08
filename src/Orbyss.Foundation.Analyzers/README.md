# Orbyss.Foundation.Analyzers

- `ORB1001` prevents `AddHostedService` inside a CShells feature because the root Generic Host will not start it.
- `ORB1002` suggests named arguments for calls with four or more arguments. Consumers control its severity in `.editorconfig`.
- `ORB1003` requires each C# source file to declare exactly one named type, including nested and supporting types.
- `ORB1004` requires private helper methods to appear after the type's other members.
- `ORB1005` requires purposeful XML documentation on every declared type and member, including private members.
- `ORB1006` requires every packaged CShells feature to declare `[ShellFeature]` with the exact
  `FoundationFeatureIdentity`, preventing CLR, package-descriptor, and shell identity drift.

The consumer scaffold references this package centrally with `PrivateAssets=all`; application projects do not
need individual analyzer references.
