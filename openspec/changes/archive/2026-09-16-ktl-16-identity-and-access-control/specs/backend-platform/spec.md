## MODIFIED Requirements

### Requirement: Production identity fails closed

The platform SHALL build the current actor for an application operation from a bearer token that
it has validated against the configured identity provider, together with the caller's stored user
record. It SHALL NOT allow a development or test actor to be enabled in a production environment,
and SHALL NOT resolve any actor at all when a token is absent or fails validation.

#### Scenario: Production starts with development actor configured

- **WHEN** the application starts in production with the development actor enabled
- **THEN** startup fails before the API accepts requests

#### Scenario: Protected operation has no actor

- **WHEN** an operation requiring a caller is invoked without a resolved actor
- **THEN** the operation is denied and no personal or operational data is returned

#### Scenario: Request arrives without a token

- **WHEN** a request reaches a business endpoint carrying no bearer token
- **THEN** it is refused as unauthenticated, and the platform does not substitute a development,
  anonymous or otherwise permissive actor

#### Scenario: Actor is derived from a validated token

- **WHEN** a request carries a token that passes signature, issuer, audience and lifetime validation
- **THEN** the current actor is built from that token's subject and the stored user record it
  identifies, and from nothing the caller supplied in the request
