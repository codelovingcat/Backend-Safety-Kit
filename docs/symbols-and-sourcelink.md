# Source Link and Debug Symbols

The shipping NuGet packages use the GitHub Source Link provider as a private build dependency. It is not exposed as a runtime dependency to package consumers.

CI builds deterministic assemblies with repository metadata, embeds source information for untracked files where supported, and creates a portable-PDB .snupkg alongside each primary .nupkg. The symbol package uses the same package ID and version as its primary package. Consumers can use a compatible debugger and the NuGet.org symbol server to step from a call site into the exact repository source for the package revision.

The package validation script verifies that each symbol archive matches its package ID/version and contains the corresponding PDB. Release artifacts must be built from a real repository commit; the primary package's nuspec records the repository URL and commit.

Source Link and symbols improve debugging but do not change runtime behavior. Publishing to NuGet.org remains a separate, explicitly authorized release operation. See Microsoft's [symbol package guidance](https://learn.microsoft.com/nuget/create-packages/symbol-packages-snupkg) and the [Source Link project](https://github.com/dotnet/sourcelink) for the supported formats and debugger requirements.
