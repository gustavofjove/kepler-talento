create or replace function public.protect_system_roles()
returns trigger language plpgsql as $$
begin
  if tg_op = 'DELETE' and old.is_system then
    raise exception 'El rol de sistema "%" no puede eliminarse.', old.name;
  end if;
  if tg_op = 'UPDATE' and old.is_system and new.name <> old.name then
    raise exception 'El rol de sistema "%" no puede renombrarse.', old.name;
  end if;
  if tg_op = 'UPDATE' and old.is_system and not new.is_system then
    raise exception 'No se puede quitar el flag de sistema.';
  end if;
  return coalesce(new, old);
end;
$$;

drop trigger if exists trg_protect_system_roles on public.roles;
create trigger trg_protect_system_roles
before update or delete on public.roles
for each row execute function public.protect_system_roles();
