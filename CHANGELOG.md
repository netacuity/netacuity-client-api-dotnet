# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
This changelog starts at the initial public release on GitHub; changes prior to that are not tracked here.

## [7.0.0]

### Added
- Initial public release of the NetAcuity .NET Client API on GitHub.
- XML UDP query protocol, taking feature codes as a comma-separated string, and using exceptions (`NetAcuityException`) rather than status-code returns.
- API ID, feature-code, IP-format, and transaction-ID validation; response-echo verification (transaction ID and IP) to reject spoofed or stale replies; a 2-second default timeout; and a `Connect()`-ed UDP socket so the OS itself rejects packets from any other source.
- Apache License 2.0 (see [LICENSE](LICENSE) and [NOTICE](NOTICE)).
