# Optional MDI policy fields and source-generated JSON

The initial individual-policy test run passed 11 of 12 new specifications. Removing the new fields from JSON loaded false rather than the intended true defaults. On the project's .NET 10 source-generation path, init-only setters can receive CLR defaults when their property is absent, overwriting a property initializer. Ordinary in-memory construction and full JSON round-trips passed, which is why the explicit earlier-schema test is required.

DockWindow now has a public JsonConstructor overload whose two optional policy arguments default to true. The source-generated constructor metadata carries those defaults without a migration pass, reflection fallback or per-window JSON rewriting. Its original three-argument primary constructor and three-value deconstruction remain available. Explicit false values are still false; nine omitted/false/true combinations are checked with both flags. All existing serializer, immutable-model and package-consumer tests remain required.

The change does not relax structural validation, add a new schema version, or restore application content. It addresses only these additive metadata fields. Microsoft's immutable-type serialization documentation explains constructor selection and notes that preserving missing init-only property initializers in source generation changes in .NET 11. DockingScene remains on its pinned .NET 10 target.

Primary reference: https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/immutability . Exact-head CI establishes whether the correction passes; an earlier subset is not a successful release.
