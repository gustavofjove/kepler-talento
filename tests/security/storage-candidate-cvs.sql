-- Storage policy checks for private candidate CV bucket.

do $$
begin
	-- Bucket must remain private to deny public access.
	if not exists (
		select 1
		from storage.buckets
		where id = 'candidate-cvs'
			and public = false
	) then
		raise exception 'Bucket candidate-cvs must exist and be private.';
	end if;

	if not exists (
		select 1
		from pg_policies
		where schemaname = 'storage'
			and tablename = 'objects'
			and policyname = 'candidate_cvs_read'
	) then
		raise exception 'Policy candidate_cvs_read is missing.';
	end if;

	if not exists (
		select 1
		from pg_policies
		where schemaname = 'storage'
			and tablename = 'objects'
			and policyname = 'candidate_cvs_insert'
	) then
		raise exception 'Policy candidate_cvs_insert is missing.';
	end if;

	if not exists (
		select 1
		from pg_proc
		where pronamespace = 'public'::regnamespace
			and proname = 'can_access_candidate_cv_path'
	) then
		raise exception 'Function can_access_candidate_cv_path is missing.';
	end if;
end $$;

select 'storage-candidate-cvs checks completed' as note;
