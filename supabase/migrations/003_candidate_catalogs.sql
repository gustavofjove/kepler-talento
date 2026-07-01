create table if not exists public.catalog_candidate_statuses (
  id uuid primary key default gen_random_uuid(),
  code text unique not null,
  name_es text not null,
  name_en text,
  sort_order integer not null default 100,
  is_active boolean not null default true,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now()
);

create table if not exists public.catalog_languages (like public.catalog_candidate_statuses including all);
create table if not exists public.catalog_language_levels (like public.catalog_candidate_statuses including all);
create table if not exists public.catalog_programs (like public.catalog_candidate_statuses including all);
create table if not exists public.catalog_program_levels (like public.catalog_candidate_statuses including all);
create table if not exists public.catalog_skills (like public.catalog_candidate_statuses including all);
create table if not exists public.catalog_document_types (like public.catalog_candidate_statuses including all);

insert into public.catalog_candidate_statuses (code, name_es, sort_order)
values ('new','Nuevo',10),('available','Disponible',20),('in_process','En proceso',30),('hired','Contratado',40),('rejected','Descartado',50)
on conflict (code) do update set name_es = excluded.name_es, sort_order = excluded.sort_order;

insert into public.catalog_languages (code, name_es)
values ('en','Ingles'),('fr','Frances'),('de','Aleman'),('it','Italiano'),('pt','Portugues')
on conflict (code) do update set name_es = excluded.name_es;

insert into public.catalog_programs (code, name_es)
values ('excel','Excel'),('sap','SAP'),('autocad','AutoCAD'),('navision','Navision'),('power-bi','Power BI')
on conflict (code) do update set name_es = excluded.name_es;

insert into public.catalog_document_types (code, name_es)
values ('cv','CV'),('cover-letter','Carta de presentacion'),('certificate','Certificado')
on conflict (code) do update set name_es = excluded.name_es;

grant select on all tables in schema public to authenticated;
