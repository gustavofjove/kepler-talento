create table if not exists public.candidate_documents (
  id uuid primary key default gen_random_uuid(),
  candidate_id uuid not null references public.candidates(id) on delete cascade,
  document_type text not null default 'CV',
  storage_bucket text not null,
  storage_path text not null,
  original_filename text not null,
  mime_type text,
  size_bytes bigint,
  is_primary boolean not null default false,
  uploaded_at timestamptz not null default now(),
  uploaded_by uuid references auth.users(id),
  file_hash text,
  created_at timestamptz not null default now(),
  unique(storage_bucket, storage_path)
);

create unique index if not exists candidate_documents_one_primary_per_candidate
on public.candidate_documents(candidate_id)
where is_primary = true;

create table if not exists public.candidate_audit_log (
  id uuid primary key default gen_random_uuid(),
  candidate_id uuid references public.candidates(id),
  action text not null,
  entity_name text,
  entity_id uuid,
  old_data jsonb,
  new_data jsonb,
  created_at timestamptz not null default now(),
  created_by uuid references auth.users(id)
);

create table if not exists public.import_batches (
  id uuid primary key default gen_random_uuid(),
  source_name text not null,
  source_type text not null default 'csv',
  status text not null default 'pending',
  total_rows integer not null default 0,
  loaded_rows integer not null default 0,
  error_rows integer not null default 0,
  started_at timestamptz not null default now(),
  finished_at timestamptz,
  created_by uuid references auth.users(id),
  summary jsonb not null default '{}'
);

create table if not exists public.import_errors (
  id uuid primary key default gen_random_uuid(),
  batch_id uuid not null references public.import_batches(id) on delete cascade,
  row_number integer,
  entity_name text,
  raw_data jsonb,
  error_code text,
  message text,
  created_at timestamptz not null default now()
);

create table if not exists public.export_events (
  id uuid primary key default gen_random_uuid(),
  requested_by uuid references auth.users(id),
  requested_at timestamptz not null default now(),
  format text not null,
  filters jsonb not null default '{}',
  row_count integer not null default 0,
  field_set text not null,
  status text not null default 'created'
);

grant select, insert, update on public.candidate_documents, public.candidate_audit_log, public.import_batches, public.import_errors, public.export_events to authenticated;
