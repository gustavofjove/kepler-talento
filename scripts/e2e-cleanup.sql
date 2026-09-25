-- Development-only purge of rows created by the Playwright suite (tests/e2e).
--
-- Every e2e spec builds the names it creates from `Date.now()`, so test data is recognised
-- by a 13-digit timestamp in a name, title, code, e-mail or file name. Seeded demo data and
-- real records never carry one. The API offers no delete for most of these rows, so this runs
-- as the Compose superuser through `node scripts/e2e-cleanup.js`, never against a shared or
-- production database. It prints the storage keys of purged binaries for the caller to
-- remove. All foreign keys are RESTRICT, so a kept row that still references test data
-- aborts the whole transaction instead of leaving it half-deleted.
\set ON_ERROR_STOP on
\set QUIET on
begin;

create temp table e2e_candidates on commit drop as
  select "Id" from "CND_Candidates"
  where "FirstName" ~ '\d{13}' or "LastName" ~ '\d{13}' or coalesce("Email", '') ~ '\d{13}';

create temp table e2e_batches on commit drop as
  select "Id" from "ADM_ImportBatches"
  where "OriginalFileName" ~ '\d{13}'
     or "Id" in (select "BatchId" from "ADM_ImportRowOutcomes"
                 where "CandidateId" in (select "Id" from e2e_candidates));

select 'KEY ' || "StorageKey" from "CND_Documents"
  where "CandidateId" in (select "Id" from e2e_candidates)
union all
select 'KEY ' || "StorageKey" from "ADM_ImportBatches"
  where "Id" in (select "Id" from e2e_batches) and "StorageKey" is not null;

delete from "ADM_ImportRowOutcomes"
  where "BatchId" in (select "Id" from e2e_batches)
     or "CandidateId" in (select "Id" from e2e_candidates);
delete from "ADM_ImportBatches" where "Id" in (select "Id" from e2e_batches);
delete from "CND_Documents" where "CandidateId" in (select "Id" from e2e_candidates);
delete from "CND_CandidateEducation" where "CandidateId" in (select "Id" from e2e_candidates);
delete from "CND_CandidateExperience" where "CandidateId" in (select "Id" from e2e_candidates);
delete from "CND_CandidateLanguages" where "CandidateId" in (select "Id" from e2e_candidates);
delete from "CND_CandidatePrograms" where "CandidateId" in (select "Id" from e2e_candidates);
delete from "CND_CandidateSkills" where "CandidateId" in (select "Id" from e2e_candidates);
delete from "CND_CandidateTags" where "CandidateId" in (select "Id" from e2e_candidates);
delete from "CND_CandidateNotes" where "CandidateId" in (select "Id" from e2e_candidates);
-- KTL-30 links go when either side is test data, before both of the rows they reference.
delete from "OPS_PositionCandidates"
  where "CandidateId" in (select "Id" from e2e_candidates)
     or "PositionId" in (select "Id" from "OPS_Positions" where "Title" ~ '\d{13}');
delete from "CND_Candidates" where "Id" in (select "Id" from e2e_candidates);
delete from "OPS_Positions" where "Title" ~ '\d{13}';
delete from "CAT_CatalogItems" where "NameEs" ~ '\d{13}' or "Code" ~ '\d{13}';
delete from "ADM_SearchPresets" where "Name" ~ '\d{13}';
delete from "ADM_Users" where "DisplayName" ~ '\d{13}' or "Email" ~ '\d{13}';
delete from "ADM_Roles" where not "IsSystem" and ("Name" ~ '\d{13}' or "Label" ~ '\d{13}');

commit;
