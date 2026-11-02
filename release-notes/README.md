# Release notes

For each future release, copy `RELEASE_NOTES_TEMPLATE.md` to
`v<major>.<minor>.<patch>.md`, replace every placeholder, and commit that file with
the matching application-version change. Write deliberate release highlights and
known limitations for that release; do not copy an older release's feature claims.

The manual publication job rejects a missing version-specific file, unresolved
placeholders, or notes that do not identify the requested release version. At
publication time it appends the validated source SHA, artifact names and hashes,
Android identity and version code, and production certificate identity. Those
generated values must not be guessed or committed into the human-authored notes.
