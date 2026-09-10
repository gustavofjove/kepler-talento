## REMOVED Requirements

### Requirement: Reference vertical slice

**Reason**: The reference operation existed only to prove that a request could travel the
production HTTP → application → persistence boundaries with a template slice, before any real
business slice existed. The real candidate read operation in the `candidate-management`
capability now travels those same boundaries under per-operation authorization, so the reference
operation no longer proves anything the product does not already prove. Keeping it would leave a
second, less-guarded route to candidate personal data alive in the deployed surface — which
principle 1 does not permit for the aggregate this change is relocating.

**Migration**: Callers of the reference candidate operation use the candidate read operation
defined by `candidate-management`, which requires the `candidates.read` capability and returns
the same problem-details contract for not-found and refused requests. The seeded reference
candidate is no longer created. The boundary evidence the reference slice supplied is now
supplied by the candidate integration tests and the candidate browser flow; the remaining
platform requirements in this specification (validation failures, unhandled error contract,
correlation, architectural separation) are unaffected and continue to be exercised by the
candidate slices.
