-- Validate candidate read/write RLS cases against a configured Supabase test database.
-- Expected cases once executed against a seeded Supabase instance:
--   1. rrhh_admin and rrhh_user can insert/update/select candidates.
--   2. manager_reader and readonly can select but not insert/update/delete candidates.
--   3. Inactive or unauthenticated sessions cannot select candidates at all.
--   4. Logical deactivation (is_active = false) is performed via UPDATE, never DELETE.
select 'rls-candidates checks must be executed in integration environment' as note;
