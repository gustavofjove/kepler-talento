create or replace function public.prevent_last_active_admin_mutation()
returns trigger
language plpgsql
as $$
declare
  active_admin_count integer;
  target_is_admin boolean;
begin
  target_is_admin := old.role in ('rrhh_admin', 'system_admin');

  if tg_op = 'DELETE' and target_is_admin and old.is_active then
    select count(*)
      into active_admin_count
      from public.profiles
     where is_active = true
       and role in ('rrhh_admin', 'system_admin');

    if active_admin_count <= 1 then
      raise exception 'No se puede eliminar el ultimo admin activo.';
    end if;
  end if;

  if tg_op = 'UPDATE' and target_is_admin and old.is_active then
    if new.is_active = false or new.role not in ('rrhh_admin', 'system_admin') then
      select count(*)
        into active_admin_count
        from public.profiles
       where is_active = true
         and role in ('rrhh_admin', 'system_admin');

      if active_admin_count <= 1 then
        raise exception 'No se puede desactivar o cambiar el rol del ultimo admin activo.';
      end if;
    end if;
  end if;

  return coalesce(new, old);
end;
$$;

drop trigger if exists trg_profiles_last_admin_guardrail on public.profiles;
create trigger trg_profiles_last_admin_guardrail
before update or delete on public.profiles
for each row execute function public.prevent_last_active_admin_mutation();

create or replace function public.prevent_catalog_value_deactivation_when_in_use()
returns trigger
language plpgsql
as $$
begin
  if tg_table_name = 'catalog_languages' then
    if exists (select 1 from public.candidate_languages where lower(language) = lower(old.name_es)) then
      raise exception 'No se puede desactivar/eliminar catalogo en uso: %', old.name_es;
    end if;
  elsif tg_table_name = 'catalog_programs' then
    if exists (select 1 from public.candidate_programs where lower(program) = lower(old.name_es)) then
      raise exception 'No se puede desactivar/eliminar catalogo en uso: %', old.name_es;
    end if;
  elsif tg_table_name = 'catalog_skills' then
    if exists (select 1 from public.candidate_skills where lower(skill) = lower(old.name_es)) then
      raise exception 'No se puede desactivar/eliminar catalogo en uso: %', old.name_es;
    end if;
  end if;

  if tg_op = 'UPDATE' and old.is_active = true and new.is_active = false then
    return new;
  end if;

  return coalesce(new, old);
end;
$$;

drop trigger if exists trg_catalog_languages_guardrail on public.catalog_languages;
create trigger trg_catalog_languages_guardrail
before update or delete on public.catalog_languages
for each row execute function public.prevent_catalog_value_deactivation_when_in_use();

drop trigger if exists trg_catalog_programs_guardrail on public.catalog_programs;
create trigger trg_catalog_programs_guardrail
before update or delete on public.catalog_programs
for each row execute function public.prevent_catalog_value_deactivation_when_in_use();

drop trigger if exists trg_catalog_skills_guardrail on public.catalog_skills;
create trigger trg_catalog_skills_guardrail
before update or delete on public.catalog_skills
for each row execute function public.prevent_catalog_value_deactivation_when_in_use();
