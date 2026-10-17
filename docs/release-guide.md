# Fully enabled portable distribution

Aster works locally without an account, payment, key, activation or feature restrictions. It retains the MIT license. Application projects have no third-party NuGet dependencies; the framework-dependent portable package uses a separately installed Microsoft .NET Desktop Runtime. It contains no bundled runtime. The local workflow below neither signs nor publishes a release.

## Prepare and verify a candidate

Use Windows x64, PowerShell 7 and the .NET 10 SDK. Start in the checkout root. First run the complete local verification:

```powershell
pwsh -NoProfile -File tools/Verify-Release.ps1
```

It restores and builds Release for `win-x64`, discovers the current engine test count, runs all engine tests and the unfiltered hidden WinForms harness, and compares source/binary hashes before and after. It stops for any failed, skipped, incomplete or changed evidence. Inspect its unique `artifacts/ReleaseVerification/<run>/` reports. A manifest is written only after success. It includes file hashes and the evidence limits; it is not a signed or tamper-proof attestation.

Pass the actual repository-relative manifest path to the publisher:

```powershell
pwsh -NoProfile -File tools/Publish-Portable.ps1 -VerificationManifest artifacts/ReleaseVerification/<run>/manifest.json
```

Publishing validates the manifest against the current source, reports and binaries, uses `dotnet publish --no-build --no-restore`, compares published application bytes with verified bytes, includes LICENSE/documentation/reports, and verifies every ZIP entry. Each run creates a new `artifacts/ReleaseCandidates/<run>/` directory without replacing an existing candidate. `package-manifest.json` records actual product version and file hashes; `SHA256SUMS.txt` covers the ZIP. The package manifest cannot list its own final hash; the ZIP checksum covers it. The old specialized audit helper remains for its existing prepared-output workflow; prefer this end-to-end workflow for new candidates.

## Install and first run

Use a currently supported Windows x64 edition from Microsoft's [.NET 10 supported-OS matrix](https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md). “Windows 10” alone is not a sufficient compatibility specification. Install the matching [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/10.0), extract the complete ZIP into a user-writable application folder and run `Aster.exe`. Keep every extracted file together. Standard defaults to a light theme; F1 opens local help. The portable package does not modify the registry, add services or require an installer.

Keep Windows and the installed runtime patched. Framework-dependent applications use the installed compatible runtime; a separately created self-contained publish would bundle its runtime and require a new application package to service that bundled runtime. See [Microsoft's deployment guidance](https://learn.microsoft.com/en-us/dotnet/core/deploying/). This portable tool deliberately produces one explicit framework-dependent x64 format.

## Data, privacy and support

The normal profile is `%LOCALAPPDATA%/AsterCalculator/state.json`, or the `ASTER_DATA_DIRECTORY` override. History persistence defaults to off; memory is session-only. Preferences are local. There is no calculation service, automatic telemetry or background update/activation connection. Keep profile backups private if you opt to retain history. A future unsupported schema is preserved and saving is refused, allowing a compatible newer app to reopen it.

F1's Support information exposes local version/build/runtime/culture/theme/DPI information for a user-initiated report. Review copied diagnostics before sharing; calculations, saved history, memory, usernames and profile paths are excluded. Use [the existing issue tracker](https://github.com/gcfernando/aster/issues), providing reproducible sample inputs. No report is sent automatically.

## Update, rollback and removal

1. Close Aster before replacing its application files. Download only a trusted complete package and verify its supplied SHA-256 checksum. A checksum detects differences from the supplied manifest; it does not authenticate the publisher.
2. Keep the previous application folder and, if needed, make a private backup of the profile while Aster is closed. Extract the new version into a separate folder. Run it using the same profile and verify the relevant workflows before retiring the previous folder. Never copy only the EXE between versions.
3. For rollback, close Aster and reopen the previous complete folder. If that version refuses a newer profile schema, retain the file and return to the compatible app; alternatively use a separate `ASTER_DATA_DIRECTORY`. Do not discard unknown profile data to bypass this protection.
4. To uninstall, close Aster, delete its extracted application folder and remove any shortcuts you created. This retains the separate profile. Remove the named profile directory only if you intentionally want to erase its settings and any saved history. Never delete a shared override directory wholesale.

## Required external release gates

This workflow prepares a local unsigned candidate. Engine/component passes alone do not justify unrestricted commercial-release claims. Before public distribution, record:

- Clean-machine extraction/startup on the chosen supported Windows editions, including runtime installation/detection, offline calculation and non-admin profile access.
- Upgrade and rollback with legacy/current/future profiles; interrupted/write-denied storage recovery; full-folder removal with intentional profile retention.
- Physical keyboard/mouse and clipboard workflows in all modes, Narrator/stateful control semantics, real high contrast, IME/RTL expectations and real monitor-DPI transitions.
- The owner's authentic publisher/signing and distribution policy, package provenance and reputation review. No certificate or release identity is fabricated by these tools.
- Version/release notes, numeric limitations, known issues, support ownership and runtime/security update responsibility.

The tested development host and actual results belong in the audit evidence. They do not substitute for a supported-OS matrix or clean-machine run. The checked-in CI workflow runs builds and tests and retains evidence; it never deploys or publishes packages, and its remote execution remains unverified until run by the repository owner.
