# NetAcuity Client API — .NET

A .NET client library for querying the [NetAcuity](https://www.digitalelement.com/solutions/netacuity/) Server for IP geolocation and intelligence data. Supports the XML UDP query protocol.

## Requirements

- .NET Framework 4.8 **or** .NET 10.0 (or later)
- A running **NetAcuity Server** accessible on UDP port 5400
- An **API ID** (customer-provided integer, range 0–127; default 0)

## Installation / Build

```bash
git clone https://github.com/netacuity/netacuity-client-api-dotnet.git
cd netacuity-client-api-dotnet
dotnet build  # use 'dotnet build -f net10.0' on Linux/macOS
```

## Adding as a Dependency

This library isn't published as a NuGet package — reference the cloned project directly from your own .NET project:

```bash
dotnet add reference C:\absolute\path\to\netacuity-client-api-dotnet\src\NetAcuity\NetAcuity.csproj
# use '/absolute/path/to/.../NetAcuity.csproj' on Linux/macOS
```

If you don't already have a .NET project to add this to:

```bash
dotnet new console --name YourProject
cd YourProject
dotnet add reference C:\absolute\path\to\netacuity-client-api-dotnet\src\NetAcuity\NetAcuity.csproj
# use '/absolute/path/to/.../NetAcuity.csproj' on Linux/macOS
```

## Quick Start

### XML UDP Query (recommended)

The XML UDP protocol supports multiple feature codes in a single query.

```csharp
using NetAcuity;

string serverIP = "192.0.2.1";   // NetAcuity Server to query
string queryIP = "203.0.113.1";  // IP address to look up

var xml = new NetAcuityXML();
xml.Create(serverIP, apiID: 74, timeoutMicroseconds: 3_000_000);

try
{
    xml.QueryXML(queryIP, featureCodes: "3,4", transactionID: "txn-001");
    Console.WriteLine(xml.FieldValue("country"));
    Console.WriteLine(xml.FieldValue("region"));
}
catch (NetAcuityException e)
{
    Console.WriteLine("Error: " + e.Message);
}
```

## API Reference

### `NetAcuityXML` (XML UDP protocol)

`Create(string serverIP, int apiID = 0, int timeoutMicroseconds = 2_000_000)` — initializes the connection parameters for subsequent queries.

| Parameter | Type | Description |
|---|---|---|
| `serverIP` | `string` | IP address of the NetAcuity Server |
| `apiID` | `int` | API ID assigned by Digital Element (0–127); defaults to 0 |
| `timeoutMicroseconds` | `int` | Query timeout, in microseconds; defaults to 2,000,000 (2 seconds) |

`QueryXML(string queryIP, string featureCodes, string transactionID)` — queries one or more feature codes for an IP address; parsed fields become available via `ResponseFields()`/`FieldValue()`. Throws `NetAcuityException` if the query fails, a feature code is invalid, or the server reports an error.

| Parameter | Type | Description |
|---|---|---|
| `queryIP` | `string` | IP address to look up |
| `featureCodes` | `string` | Comma-separated feature code(s), e.g. `"3,4"` |
| `transactionID` | `string` | Caller-supplied ID echoed back in the response |

`FieldValue(string fieldName)` → `string` — retrieves a parsed response field (e.g. `"country"`, `"region"`) after a successful `QueryXML` call.

`ResponseFields()` → `IReadOnlyDictionary<string, string>` — all parsed response fields (including `trans-id` and `ip`) after a successful `QueryXML` call.

`FieldOrder()` → `IReadOnlyList<string>` — the response's field names in wire order. `ResponseFields()`'s dictionary has no documented iteration-order guarantee; use this when the original order matters.

## Feature Codes

For the complete, up-to-date list of feature codes and their response fields, see the [NetAcuity documentation](https://docs.netacuity.com/).

## Examples

Runnable examples are provided in the `examples/` directory:

```bash
dotnet run --project examples/XmlQuery -- <serverIP> <queryIP> <featureCodes>
```

## Running the Tests

```bash
dotnet test tests/NetAcuityAPI.Tests
```

Tests use a local mock UDP server and do not require a live NetAcuity Server.

## Changelog

See [CHANGELOG.md](CHANGELOG.md) for release history.

## Support

Technical Support is only available to those under active contract with Digital Element. To contact Support, use the contact information provided at contract initiation.

- Documentation: [docs.netacuity.com](https://docs.netacuity.com/)
- Issues: [GitHub Issues](https://github.com/netacuity/netacuity-client-api-dotnet/issues)

## License

Copyright 2026 Digital Envoy, Inc.

Licensed under the Apache License, Version 2.0. See [LICENSE](LICENSE) for the full license text.

This repository contains no third-party source code or binaries. Third-party packages resolved at build time by the test project (xunit, Microsoft.NET.Test.Sdk, coverlet) are supplied by NuGet and licensed under their own terms; the published library has no third-party dependencies.
