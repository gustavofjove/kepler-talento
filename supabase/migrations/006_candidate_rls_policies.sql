alter table public.candidates enable row level security;
alter table public.candidate_languages enable row level security;
alter table public.candidate_programs enable row level security;
alter table public.candidate_education enable row level security;
alter table public.candidate_experience enable row level security;
alter table public.candidate_skills enable row level security;
alter table public.candidate_documents enable row level security;
alter table public.candidate_audit_log enable row level security;
alter table public.import_batches enable row level security;
alter table public.import_errors enable row level security;
alter table public.export_events enable row level security;

drop policy if exists candidates_select on public.candidates;
create policy candidates_select on public.candidates for select to authenticated
  using (public.has_role_permission('view_candidates'));

drop policy if exists candidates_insert on public.candidates;
create policy candidates_insert on public.candidates for insert to authenticated
  with check (public.has_role_permission('create_candidates'));

drop policy if exists candidates_update on public.candidates;
create policy candidates_update on public.candidates for update to authenticated
  using (public.has_role_permission('edit_candidates'))
  with check (public.has_role_permission('edit_candidates'));

drop policy if exists candidate_documents_select on public.candidate_documents;
create policy candidate_documents_select on public.candidate_documents for select to authenticated
  using (public.has_role_permission('view_candidates'));

drop policy if exists candidate_documents_insert on public.candidate_documents;
create policy candidate_documents_insert on public.candidate_documents for insert to authenticated
  with check (public.has_role_permission('upload_candidate_documents'));

drop policy if exists export_events_insert on public.export_events;
create policy export_events_insert on public.export_events for insert to authenticated
  with check (public.has_role_permission('export_candidates'));

do $$
declare
  table_name text;
begin
  foreach table_name in array array['candidate_languages','candidate_programs','candidate_education','candidate_experience','candidate_skills','candidate_audit_log','import_batches','import_errors']
  loop
    execute format('drop policy if exists %I_read on public.%I', table_name, table_name);
    execute format('create policy %I_read on public.%I for select to authenticated using (public.has_role_permission(''view_candidates''))', table_name, table_name);
    execute format('drop policy if exists %I_write on public.%I', table_name, table_name);
    execute format('create policy %I_write on public.%I for all to authenticated using (public.has_role_permission(''edit_candidates'') or public.has_role_permission(''import_candidates'')) with check (public.has_role_permission(''edit_candidates'') or public.has_role_permission(''import_candidates''))', table_name, table_name);
  end loop;
end $$;
