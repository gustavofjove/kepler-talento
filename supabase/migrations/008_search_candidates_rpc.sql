create or replace function public.search_candidates(filters jsonb default '{}')
returns table (
  candidate_id uuid,
  first_name text,
  last_name text,
  phone text,
  email text,
  status_name text,
  primary_cv_document_id uuid,
  updated_at timestamptz
)
language sql
stable
security invoker
set search_path = public
as $$
  with params as (
    select
      nullif(filters->>'text','') as text_filter,
      nullif(filters->>'status','') as status_filter,
      coalesce(filters->>'language_mode', 'ANY') as language_mode,
      coalesce(filters->>'program_mode', 'ANY') as program_mode,
      coalesce(filters->'language_values', '[]'::jsonb) as language_values,
      coalesce(filters->'program_values', '[]'::jsonb) as program_values,
      nullif(filters->>'has_cv','') as has_cv
  )
  select distinct
    c.id,
    c.first_name,
    c.last_name,
    c.phone,
    c.email,
    c.status,
    d.id,
    c.updated_at
  from public.candidates c
  cross join params p
  left join public.candidate_documents d on d.candidate_id = c.id and d.is_primary
  where c.is_active
    and (p.text_filter is null or concat_ws(' ', c.first_name, c.last_name, c.email, c.phone, c.notes) ilike '%' || p.text_filter || '%')
    and (p.status_filter is null or c.status = p.status_filter)
    and (p.has_cv is null or (p.has_cv = 'yes' and d.id is not null) or (p.has_cv = 'no' and d.id is null))
    and (
      jsonb_array_length(p.language_values) = 0
      or (
        p.language_mode = 'ANY'
        and exists (
          select 1 from public.candidate_languages cl
          where cl.candidate_id = c.id and cl.language in (select jsonb_array_elements_text(p.language_values))
        )
      )
      or (
        p.language_mode = 'ALL'
        and not exists (
          select 1 from jsonb_array_elements_text(p.language_values) v
          where not exists (
            select 1 from public.candidate_languages cl
            where cl.candidate_id = c.id and cl.language = v
          )
        )
      )
    )
    and (
      jsonb_array_length(p.program_values) = 0
      or (
        p.program_mode = 'ANY'
        and exists (
          select 1 from public.candidate_programs cp
          where cp.candidate_id = c.id and cp.program in (select jsonb_array_elements_text(p.program_values))
        )
      )
      or (
        p.program_mode = 'ALL'
        and not exists (
          select 1 from jsonb_array_elements_text(p.program_values) v
          where not exists (
            select 1 from public.candidate_programs cp
            where cp.candidate_id = c.id and cp.program = v
          )
        )
      )
    );
$$;

grant execute on function public.search_candidates(jsonb) to authenticated;
