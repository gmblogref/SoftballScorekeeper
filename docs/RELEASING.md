# Releasing Softball Scorekeeper

The repository's release entry point is the **Manual Release** workflow at
`.github/workflows/release.yml`. It runs only through `workflow_dispatch`; a
merge or pull request never starts a release automatically.

## Workflow inputs

| Input | Required | Default | Meaning |
| --- | --- | --- | --- |
| `release_version` | Yes | None | Strict `major.minor.patch`, without `v` or a prerelease suffix. It must equal committed `ApplicationDisplayVersion`. |
| `android_version_code` | Yes | None | Positive integer equal to committed `ApplicationVersion` and not previously used by an inspectable version tag. |
| `android_ui_validation` | Yes | `none` | `none`, `smoke`, or `full`. Compilation of the Appium project is always mandatory. |
| `create_github_release` | Yes | `false` | When false, build and verify Actions artifacts without creating a tag or Release. When true, authorize the separately gated publication job. |

Dispatch the workflow from `main`. Preflight checks out the exact selected SHA,
requires `refs/heads/main`, confirms that SHA is in fetched `origin/main`, and
passes the full SHA to every job. It rejects a mismatched version, a reused
Android version code where repository tags allow that to be established, an
existing `v<version>` tag, or an existing Release for that tag. Checks fail
closed when GitHub state cannot be established.

## Prepare a future release

Release preparation is a normal reviewed source change which must be merged to
`main` before dispatch:

1. Change `ApplicationDisplayVersion` and `ApplicationVersion` in
   `SoftballScorekeeper/SoftballScorekeeper.csproj`. Use the intended semantic
   version and a new, monotonically increasing positive Android version code.
2. Reconcile `licenses/distribution/production-license-manifest.json` against
   the actual Android and Windows production dependency/runtime graphs. Change
   `release_target` to `v<version>`, update component versions and evidence, add
   or replace required notice files, and update
   `licenses/distribution/SHA256SUMS.txt`. This is a deliberate licensing review,
   not a version-string-only edit. The Windows packager will reject a stale
   target, missing notice, unexpected inventory, or checksum drift. The Android
   production-license gate likewise rejects a stale target, checksum drift, or
   an APK whose packaged application/common/Android license inventory or bytes
   do not exactly match the reviewed sources.
3. Copy `release-notes/RELEASE_NOTES_TEMPLATE.md` to
   `release-notes/v<version>.md`. Replace every placeholder with concise,
   human-authored highlights, known limitations, and direct links to every
   applicable original Microsoft Windows agreement in the reconciled production
   inventory. Keep the prominent **Windows licensing information** section
   before **Windows usage**. Its links must be pinned to the release's exact
   `v<version>` tag, and its component names and versions must agree with the
   production manifest. Preserve the statements that the application license
   does not replace Microsoft's terms, that recipients should review the terms
   before downloading or using Windows, and that no Microsoft approval or legal
   clearance is claimed. The publisher appends the verified source SHA,
   artifact names and hashes, Android identity/version code, and certificate
   identity; do not guess those generated values.
4. Review and merge the complete release-preparation change to `main`. Do not
   dispatch from the preparation branch.

`v2.0.0` already exists and is intentionally rejected. A meaningful hosted dry
run therefore requires a real future release-preparation commit. Do not create
a dummy production version or weaken version/licensing validation merely to
exercise the workflow.

## Protect the release environment

The repository owner must create an environment named `production-release` and
restrict its deployment branch to `main`. Configure a required reviewer and
prevent self-review when the repository plan supports those controls. Keep
administrator bypass disabled when practical. Environment protection is
evaluated before its secrets become available.

Add these four environment secrets; never store their values in the repository,
logs, release notes, or ordinary repository secrets:

- `ANDROID_SIGNING_KEYSTORE_BASE64`
- `ANDROID_SIGNING_KEY_ALIAS`
- `ANDROID_SIGNING_STORE_PASSWORD`
- `ANDROID_SIGNING_KEY_PASSWORD`

The keystore must contain the established production key whose certificate
SHA-256 is
`D55EFDC1E25B341F4408BC3014ECFF31D94255113EDD6F9B5F3CE98E833E4E05`.
The workflow reconstructs credentials under runner temporary storage, passes
passwords through files, verifies the definitive APK independently, and runs
unconditional cleanup.

Keep repository Actions permissions restricted by default. The workflow grants
`contents: read` globally and `contents: write` only to the publication job.
Repository or organization policy must permit that explicit job-level grant and
the pinned official Actions used by the workflow; broad default write access and
pull-request approval permission are not required.

For tags matching `v*`, use a tag ruleset that prevents updates and deletions.
The current workflow uses `GITHUB_TOKEN` to create a new tag, so do not enable a
creation restriction that the token cannot satisfy. If policy requires creation
to be restricted to a bypass actor, use a separately reviewed GitHub App design
before publication; do not add a broad human or administrator bypass merely to
make this workflow pass.

## Hosted dry run

After the future release-preparation commit is merged, open **Actions > Manual
Release > Run workflow**, select `main`, and enter the committed versions. Keep
`android_ui_validation` at `none` for the first run and keep
`create_github_release` false. Approve the protected signing job only after
confirming the run's source SHA and inputs.

This run executes normal emulator-independent tests, compiles the Appium project
without running it, compiles Android and Windows, builds and verifies the signed
APK and Windows portable ZIP, validates licensing, and uploads one 30-day Actions
artifact named `SoftballScorekeeper-<version>-release-artifacts`. It does not
create a tag or GitHub Release.

Download the Actions artifact and confirm it contains exactly:

- `SoftballScorekeeper-<version>-<shortSHA>.apk`
- `SoftballScorekeeper-<version>-windows-x64-portable.zip`
- `SHA256SUMS.txt`

From that directory, verify the two hashes in PowerShell:

```powershell
Get-Content .\SHA256SUMS.txt
Get-FileHash .\SoftballScorekeeper-<version>-<shortSHA>.apk -Algorithm SHA256
Get-FileHash .\SoftballScorekeeper-<version>-windows-x64-portable.zip -Algorithm SHA256
```

Treat packaged Windows runtime launch as advisory manual validation; the hosted
workflow validates build, ZIP structure, versions, and licensing but does not
claim an interactive desktop smoke test.

Open the Windows ZIP and confirm that root `LICENSE-INFO.txt` directs recipients
to `LICENSE.txt`, `THIRD-PARTY-NOTICES.txt`, `licenses/common/`, and
`licenses/windows/`. Confirm that every agreement linked from the tracked
release notes is present byte-for-byte at its manifest-mapped ZIP path. Any
change to a packaged licensing source after this dry run changes the release
artifacts and requires a new dry run from the resulting merged `main` SHA.

## Optional Android UI validation

Choose `smoke` only when the focused release smoke scenario is required, or
`full` for the complete Android UI suite. Both modes use the dedicated API 35
release AVD and are gated on hosted KVM acceleration. They never use the Visual
Studio/manual-development AVD. `none` skips runtime UI automation but still
requires Appium-project compilation. A selected UI mode must pass before final
artifact verification or publication can proceed.

## Publish a real release

After a successful dry run and release-owner review, dispatch **the same merged
`main` SHA and committed versions** again with the desired UI level and
`create_github_release` true. Review every `production-release` approval GitHub
presents. Any required validation or packaging failure prevents publication.
If `main` has advanced since the dry run, do not publish the earlier result;
repeat the dry run against the new `main` tip and review its new source SHA and
artifacts first.

The publication job rechecks the exact source and final three-file artifact set,
then checks again that neither the tag nor Release exists. It creates an
annotated `v<version>` tag object targeting the exact source commit, creates a
stable draft Release from the tracked notes plus generated verified metadata,
uploads only the APK, Windows ZIP, and checksum file, and verifies each remote
asset's name, state, size, and SHA-256 digest. Only then does it publish the
draft as a non-prerelease Release.

The publisher preserves the tracked release notes verbatim before appending the
verified metadata. Before approving publication, inspect the draft and confirm
that the complete Windows licensing section, tag-pinned agreement links, review-
before-use direction, and unresolved-status caveat are present. Do not approve a
draft whose licensing section is missing, stale, or inconsistent with the
manifest and Windows ZIP.

Publication is deliberately not atomic. If it fails after creating state, the
workflow never deletes or moves the tag, draft, or assets. Record the run URL and
reported `tag_reference_created`, `draft_release_created`, `release_id`,
`uploaded_assets`, and `published` values. Do not rerun: duplicate protection
will reject the preserved state. Verify that the annotated tag targets the
intended SHA, then either complete the preserved draft manually with the exact
verified artifacts or leave it intact while investigating. Never force-move the
tag or overwrite an asset; any destructive recovery requires a separate,
explicit owner decision and audit trail.

## Verify the GitHub Release

After publication, independently confirm:

1. The `v<version>` tag is annotated and ultimately targets the recorded full
   source SHA on `main`.
2. The Release is published, not a draft or prerelease, and contains exactly the
   three expected assets.
3. The Release body preserves the Windows licensing section before Windows
   usage; every agreement link resolves through the exact release tag to the
   reviewed original file, and the text makes no claim of Microsoft approval or
   legal clearance.
4. Downloaded APK and ZIP hashes match `SHA256SUMS.txt` and the metadata in the
   Release body.
5. The APK reports package `com.companyname.softballscorekeeper`, the requested
   version name/code, one signer, and the permanent certificate SHA-256 above.
6. The Windows ZIP has the requested version, expected portable inventory, and
   all reconciled licensing files at the paths described by `LICENSE-INFO.txt`.
7. The workflow's required test, compile, package, optional UI, artifact, and
   publication jobs all succeeded for the same source SHA.

Do not call the release accepted until these checks and any planned physical
Android-tablet or Windows runtime acceptance have been recorded.
