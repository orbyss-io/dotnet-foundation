# Native EF migration authoring

Use native `dotnet ef` with a private Design reference and an exactly matched project-local tool.
Design-time context creation, migration operations, designer/snapshot, SQL and deployment remain EF
responsibilities. Keep deployment credentials separate from read-only runtime schema admission.

Analyzer 0.3.8 supports the native declarative migration layout emitted by EF without a generated
header. Within the actual relational `Migration.Up`/`Down` override, ORB1007–ORB1009 accept native
MigrationBuilder, ColumnsBuilder and operation/table builder declarations, including column and
constraint lambdas and annotation chains. This uses exact semantic EF relational symbols, not
filenames or a namespace/package-prefix convention. Ordinary helpers, local functions and builder
declarations containing arbitrary calls retain their checks. Other source/lifecycle rules stay active.
Explicit object construction and non-builder computations still use ordinary prepared-value rules.

Do not add a generated header to editable migration code, disable analyzers globally or replace
native operations to accommodate layout. Review the migration and provider SQL for actual data
effects and deployment safety. A passing layout check supplies no schema approval or migration
correctness claim.

`python tests/validate_analyzer.py --package <exact-analyzer.nupkg>` tests the exact archive alongside
compiled positive/adverse controls. Its native fixture generates migrations with original EF/tool
10.0.13 and Npgsql 10.0.3, plus the publisher's current pins when different. It retains all three raw
files and proves unchanged compilation, model drift/script output and authored-code rejection.
This fixture performs no database deployment. Consumers qualify actual deployment/runtime boundaries
separately; Program Kit is not a dependency of this publisher.

Upgrade only the private analyzer package through the consumer's normal reviewed dependency update.
Existing runtime/Host versions do not change. After qualification, remove a temporary generated header
or layout-only adaptation only from the specifically reviewed migration, preserve authored migration
operations/designer/snapshot and rerun compilation, drift/script and affected database checks.
Never rewrite already-applied operations or historical migration identity to perform this cleanup.
