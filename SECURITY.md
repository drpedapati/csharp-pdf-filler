# Security Policy

## Reporting

For security-sensitive issues, do not open a public issue with exploit details or private documents.

Until a dedicated security contact exists, report issues privately to the maintainer through a non-public channel and include:
- affected version or commit
- reproduction steps
- whether the issue involves parsing, fill output integrity, or release artifacts

## Sensitive Documents

Do not share:
- real patient PDFs
- member or claims documents
- regulated health or financial forms containing live data
- secrets such as API keys or license keys

Use public forms or sanitized samples only.

## Scope Notes

This project handles PDF form workflows.
Security reports are especially useful for:
- malformed PDF handling
- unsafe temp-file behavior
- output corruption that could silently alter form content
- release artifact integrity

Viewer-specific XFA behavior remains experimental and should not be treated as production support.
