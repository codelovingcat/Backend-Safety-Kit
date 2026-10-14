# XML Documentation Contract

The public API of each shipping package is documented in compiler-generated XML. The three package projects set GenerateDocumentationFile to true; CI enforces the setting for every packable project that declares a PackageId.

Missing public-member documentation (compiler warning CS1591) is treated as an error. New public types, members, parameters, options and enum values therefore need consumer-oriented XML documentation before they can pass CI. Suppressing the warning globally is not an accepted workaround.

The release workflow additionally checks that every primary NuGet package contains a non-empty lib/net10.0/<PackageId>.xml file alongside its assembly, README and license. This catches a packaging regression even if the project still builds.

## Local validation

~~~bash
dotnet build BackendSafetyKit.slnx --configuration Release
dotnet pack BackendSafetyKit.slnx --configuration Release --output ./artifacts
python scripts/validate_nuget_packages.py ./artifacts
~~~

The generated XML file is part of the package contract and should remain available to IDEs and documentation tooling. See [public API review](public-api-review.md) for the broader compatibility checklist.
