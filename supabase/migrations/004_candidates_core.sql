create table if not exists public.candidates (
  id uuid primary key default gen_random_uuid(),
  first_name text not null,
  last_name text not null,
  phone text,
  email text,
  location text,
  province text,
  country text not null default 'Espana',
  availability text,
  status text not null default 'new',
  source text,
  notes text,
  received_at date,
  consent_at date,
  review_due_at date,
  is_active boolean not null default true,
  deleted_at timestamptz,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now(),
  created_by uuid references auth.users(id),
  updated_by uuid references auth.users(id)
);

create table if not exists public.candidate_languages (
  id uuid primary key default gen_random_uuid(),
  candidate_id uuid not null references public.candidates(id) on delete cascade,
  language text not null,
  level text,
  certification text,
  notes text,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now(),
  created_by uuid references auth.users(id),
  updated_by uuid references auth.users(id),
  unique(candidate_id, language)
);

create table if not exists public.candidate_programs (
  id uuid primary key default gen_random_uuid(),
  candidate_id uuid not null references public.candidates(id) on delete cascade,
  program text not null,
  level text,
  years_experience numeric(4,1),
  notes text,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now(),
  created_by uuid references auth.users(id),
  updated_by uuid references auth.users(id),
  unique(candidate_id, program)
);

create table if not exists public.candidate_education (
  id uuid primary key default gen_random_uuid(),
  candidate_id uuid not null references public.candidates(id) on delete cascade,
  degree text not null,
  institution text,
  status text,
  end_year integer,
  notes text,
  created_at timestamptz not null default now()
);

create table if not exists public.candidate_experience (
  id uuid primary key default gen_random_uuid(),
  candidate_id uuid not null references public.candidates(id) on delete cascade,
  company text,
  position text,
  sector text,
  functions text,
  years_experience numeric(4,1),
  is_current boolean not null default false,
  created_at timestamptz not null default now()
);

create table if not exists public.candidate_skills (
  id uuid primary key default gen_random_uuid(),
  candidate_id uuid not null references public.candidates(id) on delete cascade,
  skill text not null,
  level text,
  notes text,
  created_at timestamptz not null default now()
);

grant select, insert, update on public.candidates to authenticated;
grant select, insert, update, delete on public.candidate_languages, public.candidate_programs, public.candidate_education, public.candidate_experience, public.candidate_skills to authenticated;
