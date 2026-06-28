# Security Policy

This project handles personal data related to candidate CVs.

## Sensitive Data

Do not commit:

- `.accdb` or `.mdb` databases;
- CV files;
- exported candidate data;
- `.env` files;
- Supabase service-role keys;
- internal Storage paths intended to remain private.

## Reporting

For now, report security issues directly through private repository issues using
the `security` label.

## Security Principles

- RLS is the real authorization boundary.
- Frontend checks are only UX aids.
- CV Storage buckets must remain private.
- Privileged operations must run server-side.
- Unauthorized access must fail closed.
