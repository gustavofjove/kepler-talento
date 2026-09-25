import { Navigate, useParams } from 'react-router';

/**
 * The former candidate edit page (KTL-22). Since KTL-29 every panel is edited on the
 * candidate page itself; the old address stays valid for bookmarks and replaces itself in
 * the history.
 */
export function CandidateEditRedirect() {
  const { id = '' } = useParams<{ id: string }>();
  return <Navigate to={`/app/candidates/${id}`} replace />;
}
