insert into storage.buckets (id, name, public, file_size_limit, allowed_mime_types)
values ('candidate-cvs', 'candidate-cvs', false, 10485760, array['application/pdf'])
on conflict (id) do update set
  public = false,
  file_size_limit = excluded.file_size_limit,
  allowed_mime_types = excluded.allowed_mime_types;

create or replace function public.can_access_candidate_cv_path(object_name text)
returns boolean language sql stable security definer set search_path = public as $$
  select public.has_role_permission('download_candidate_documents')
    or public.has_role_permission('upload_candidate_documents');
$$;

drop policy if exists candidate_cvs_read on storage.objects;
create policy candidate_cvs_read on storage.objects for select to authenticated
  using (bucket_id = 'candidate-cvs' and public.can_access_candidate_cv_path(name));

drop policy if exists candidate_cvs_insert on storage.objects;
create policy candidate_cvs_insert on storage.objects for insert to authenticated
  with check (bucket_id = 'candidate-cvs' and public.has_role_permission('upload_candidate_documents'));
