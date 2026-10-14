# Static Analysis and Warning Policy

The solution uses the latest recommended .NET analyzers, nullable reference types, implicit usings, and warnings-as-errors. Code-style diagnostics configured in EditorConfig are also enforced during build. A warning is not considered harmless simply because the compilation otherwise succeeds.

## Required behavior

- Keep compiler, nullable, recommended analyzer, and enforced style warnings visible and actionable.
- Do not suppress warnings globally or add broad NoWarn entries to make CI pass.
- If a suppression is necessary, scope it to the smallest code element and explain why it is safe.
- Review new analyzer diagnostics for correctness and runtime behavior before choosing a fix.
- Run the Release build and test suite after changing analyzer settings or suppression rules.

## Local verification

~~~bash
dotnet restore BackendSafetyKit.slnx
dotnet build BackendSafetyKit.slnx --configuration Release --no-restore -warnaserror
dotnet test BackendSafetyKit.slnx --configuration Release --no-build
~~~

The dedicated static-analysis workflow runs the same warning-as-error build on pull requests and pushes to main. Formatting is checked separately by the Formatting Check workflow to keep diagnostics clear and changes reviewable.
