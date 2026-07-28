# VoiceLab third-party dependency audit

Audited from the current project package references and resolved dependency graph.

| Dependency | Version | Scope and purpose | License/source | Native or network behavior |
| --- | --- | --- | --- | --- |
| NAudio | 2.2.1 | Runtime Windows audio capture, formats, and DSP helpers; also referenced by tests | MIT; https://github.com/naudio/NAudio | Uses Windows audio APIs. No application network feature. The package family includes managed Windows interop assemblies. |
| Microsoft.Extensions.DependencyInjection | 8.0.1 | Runtime composition root | MIT; https://github.com/dotnet/runtime | Managed code; no network feature used. |
| Microsoft.NET.Test.Sdk | 17.11.1 | Test discovery and execution only | MIT; https://github.com/microsoft/vstest | Development/test tooling, excluded from runtime output. |
| xunit | 2.9.2 | Unit-test framework only | Apache-2.0; https://github.com/xunit/xunit | Development/test tooling, excluded from runtime output. |
| xunit.runner.visualstudio | 2.8.2 | Visual Studio and `dotnet test` adapter only | Apache-2.0; https://github.com/xunit/visualstudio.xunit | Development/test tooling with `PrivateAssets=all`, excluded from runtime output. |

NAudio 2.2.1 resolves its platform assemblies (Core, Wasapi, WinMM, Midi, Asio, and WinForms) at the same version plus Microsoft Windows compatibility libraries. Dependency Injection resolves `Microsoft.Extensions.DependencyInjection.Abstractions`. Test SDK and runners resolve their own test-platform support libraries. These are transitive dependencies; they are not separately invoked as application services.

No analytics SDK, cloud SDK, model runtime, Python environment, or automatic-download component is referenced. This repository distributes source code only; downstream binary distributors are responsible for carrying all applicable dependency notices.

## Maintenance findings

- NAudio is active, but this project pins 2.2.1 while the stable NuGet line has advanced to 2.3.0. An upgrade requires a separate audio compatibility review.
- The xUnit 2 metapackage line is marked deprecated/legacy in favor of xUnit v3. It is test-only and does not enter the application runtime; migration should be evaluated separately.
- The .NET 8 dependency-injection line remains the supported target selected by the project. Keep all Microsoft 8.x components patched together during release maintenance.

## Current NuGet status

The package, transitive-package, vulnerability, deprecation, and outdated checks completed against only `https://api.nuget.org/v3/index.json`. NuGet reported no known vulnerable direct or transitive packages in any solution project.

NuGet reported xUnit 2.9.2 as the sole deprecated direct dependency (`Legacy`, alternative `xunit.v3`). It is test-only and is intentionally unchanged in this release. The outdated check reported NAudio 2.3.0, Microsoft.Extensions.DependencyInjection 10.0.10, Microsoft.NET.Test.Sdk 18.8.1, xUnit 2.9.3, and xunit.runner.visualstudio 3.1.5 as newer versions. No package version was updated automatically. Major-version changes require a separate compatibility review.
