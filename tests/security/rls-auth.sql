-- Security checks for auth/profile guardrails.

do $$
begin
	-- Authenticated users can read roles metadata and self profile only.
	if not exists (
		select 1
		from pg_policies
		where schemaname = 'public'
			and tablename = 'roles'
			and policyname = 'roles_select'
	) then
		raise exception 'Policy roles_select is missing.';
	end if;

	-- No-profile and unauthenticated users must fail closed without profile row.
	if not exists (
		select 1
		from pg_policies
		where schemaname = 'public'
			and tablename = 'profiles'
			and policyname = 'profiles_self_select'
	) then
		raise exception 'Policy profiles_self_select is missing.';
	end if;

	-- Readonly and inactive users are constrained by permission checks on candidate tables.
	if not exists (
		select 1
		from pg_policies
		where schemaname = 'public'
			and tablename = 'candidates'
			and policyname = 'candidates_select'
	) then
		raise exception 'Policy candidates_select is missing.';
	end if;

	if not exists (
		select 1
		from pg_policies
		where schemaname = 'public'
			and tablename = 'candidates'
			and policyname = 'candidates_insert'
	) then
		raise exception 'Policy candidates_insert is missing.';
	end if;

	if not exists (
		select 1
		from pg_policies
		where schemaname = 'public'
			and tablename = 'candidates'
			and policyname = 'candidates_update'
	) then
		raise exception 'Policy candidates_update is missing.';
	end if;

	if not exists (
		select 1
		from pg_trigger
		where tgname = 'trg_profiles_last_admin_guardrail'
			and not tgisinternal
	) then
		raise exception 'Trigger trg_profiles_last_admin_guardrail is missing.';
	end if;
end $$;

select 'rls-auth checks completed' as note;
