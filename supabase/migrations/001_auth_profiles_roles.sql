create extension if not exists pgcrypto;

create table if not exists public.roles (
  id uuid primary key default gen_random_uuid(),
  name text unique not null,
  description text,
  permissions text[] not null default '{}',
  is_system boolean not null default false,
  is_technical boolean not null default false,
  is_manager boolean not null default false,
  all_companies boolean not null default false,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now()
);

create table if not exists public.profiles (
  id uuid primary key references auth.users(id) on delete cascade,
  display_name text,
  email text unique,
  role text not null default 'readonly' references public.roles(name),
  is_active boolean not null default true,
  mfa_required boolean not null default false,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now()
);

insert into public.roles (name, description, permissions, is_system, is_technical, all_companies)
values
  ('rrhh_admin', 'Gestion completa RRHH', array['view_candidates','create_candidates','edit_candidates','delete_candidates','view_all_candidates','download_candidate_documents','upload_candidate_documents','export_candidates','import_candidates','manage_catalogs','manage_users','manage_roles'], true, false, true),
  ('rrhh_user', 'Operacion RRHH', array['view_candidates','create_candidates','edit_candidates','download_candidate_documents','upload_candidate_documents','export_candidates'], true, false, false),
  ('manager_reader', 'Lectura limitada', array['view_candidates','download_candidate_documents'], true, false, false),
  ('readonly', 'Solo consulta basica', array['view_candidates'], true, false, false),
  ('system_admin', 'Administracion tecnica', array['view_candidates','view_all_candidates','manage_catalogs','manage_users','manage_roles'], true, true, true)
on conflict (name) do update set
  description = excluded.description,
  permissions = excluded.permissions,
  is_system = excluded.is_system,
  is_technical = excluded.is_technical,
  all_companies = excluded.all_companies;

create or replace function public.current_profile_id()
returns uuid language sql stable security definer set search_path = public as $$
  select id from public.profiles where id = auth.uid() and is_active limit 1;
$$;

create or replace function public.current_app_role()
returns text language sql stable security definer set search_path = public as $$
  select role from public.profiles where id = auth.uid() and is_active limit 1;
$$;

create or replace function public.has_role_permission(permission_name text)
returns boolean language sql stable security definer set search_path = public as $$
  select exists (
    select 1
    from public.profiles p
    join public.roles r on r.name = p.role
    where p.id = auth.uid()
      and p.is_active
      and permission_name = any(coalesce(r.permissions, '{}'))
  );
$$;

alter table public.roles enable row level security;
alter table public.profiles enable row level security;

drop policy if exists roles_select on public.roles;
create policy roles_select on public.roles for select to authenticated
  using (public.has_role_permission('manage_roles') or public.has_role_permission('view_candidates'));

drop policy if exists profiles_self_select on public.profiles;
create policy profiles_self_select on public.profiles for select to authenticated
  using (id = auth.uid() or public.has_role_permission('manage_users'));

grant select on public.roles to authenticated;
grant select, insert, update on public.profiles to authenticated;
