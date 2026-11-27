---
status: accepted
---

# Assemblies come only from Features

An Application learns which assemblies to scan only from its Features (`IFeature.Assemblies`), mainly its Subdomain Features. Generic Infrastructure Features scan `IBackendFxApplication.Assemblies` while being enabled. We removed the `assemblies` parameter of the `BackendFxApplication` constructor, and Boot fails when no Feature brings any assembly. The aim is to make downstream applications think of the code they bring as subdomains or bounded contexts. Without assemblies, the Infrastructure Features would silently register nothing.

## Considered Options

- **Keep constructor assemblies, deprecated.** Rejected: a second way in keeps the technical framing alive. The only consumers are our own applications, so a clean break is cheap. Legacy code that is not sliced yet gets its own Feature, named after what it is (e.g. MEP's `MepLegacyFeature`).
- **Ship a generic `AssemblyFeature` helper** to ease migration of tests and small applications. Rejected: it would teach people that a Feature is a technical grouping of assemblies, the opposite of the intent. Each application or test suite names its own Feature instead.
- **Drop `IBackendFxApplication.Assemblies`.** Rejected: it is how Infrastructure Features (domain events, domain services, mediator, data seeding, configuration settings, …) find what to scan. It is also why Features are enabled during Boot, after all of them have been added.
