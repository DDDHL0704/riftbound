M-line Windows handoff archive, 2026-11-05. This tag is a recovery snapshot, not a product branch; never merge its tree into main.

The bundles are byte-for-byte copies of the original local archives and the extra pre-cleanup safety bundle. source-refs.json maps every contained original reference to its exact commit and bundle SHA256. The incremental safety bundle requires origin/main history through ececa8377e59cd178f940d866a6b36b1214d6c16.

Fetch the tag and export it with git archive --format=zip --output=M-source.zip archive/m-windows-handoff-20261105 . Extract the zip with a binary-safe ZIP tool. Avoid PowerShell text redirection for bundle bytes. Verify a bundle with git bundle verify <file>. To inspect an archived source, fetch its refs into a temporary non-branch namespace and use git show <sha>:<path>. Delete temporary inspection refs when done.

Git remote m/recovered-code, m/recovered-mechanics and m/recovered-regressions contain the product candidates. Continue from M.handoff.md. BACKLOG remains the only product status document. Historical failing/aborted logs do not prove passing validation. Mechanics final 50/50 TRX and cards 158/158 TRX are included. The final regression run was deliberately interrupted for migration.
