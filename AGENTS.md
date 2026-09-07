# Contributor instructions

- Foundation owns runtime source, tests, packaging, and publication; it must not depend on Program Kit.
- Keep provider-neutral contracts free of provider-specific implementation details.
- Keep `Orbyss.Foundation.Host` application-neutral; behavior belongs in independently selected features.
- Run locked restore, Release build, and the focused validators before tagging.
- A stable tag is only available after the complete Release workflow succeeds.
