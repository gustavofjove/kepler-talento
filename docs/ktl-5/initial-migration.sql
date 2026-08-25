CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260825090844_InitialInfrastructure') THEN
    CREATE TABLE "AUD_Events" (
        "Id" uuid NOT NULL,
        "EventType" character varying(100) NOT NULL,
        "SubjectId" character varying(100) NOT NULL,
        "CorrelationId" character varying(100) NOT NULL,
        "OutcomeCode" character varying(100),
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_AUD_Events" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260825090844_InitialInfrastructure') THEN
    CREATE TABLE "CND_Candidates" (
        "Id" uuid NOT NULL,
        "FirstName" character varying(120) NOT NULL,
        "LastName" character varying(180) NOT NULL,
        "IsActive" boolean NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "UpdatedAtUtc" timestamp with time zone NOT NULL,
        "DeletedAtUtc" timestamp with time zone,
        CONSTRAINT "PK_CND_Candidates" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_CND_Candidates_Name" CHECK (char_length("FirstName") > 0 AND char_length("LastName") > 0)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260825090844_InitialInfrastructure') THEN
    CREATE TABLE "OPS_Operations" (
        "Id" uuid NOT NULL,
        "Type" character varying(80) NOT NULL,
        "Status" character varying(24) NOT NULL,
        "CorrelationId" character varying(100) NOT NULL,
        "IdempotencyKey" character varying(160) NOT NULL,
        "AttemptCount" integer NOT NULL,
        "MaxAttempts" integer NOT NULL,
        "Owner" character varying(100),
        "LeaseExpiresAtUtc" timestamp with time zone,
        "OutcomeCode" character varying(100),
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "UpdatedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_OPS_Operations" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_OPS_Operations_Attempts" CHECK ("AttemptCount" >= 0 AND "MaxAttempts" > 0)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260825090844_InitialInfrastructure') THEN
    CREATE TABLE "CND_Documents" (
        "Id" uuid NOT NULL,
        "CandidateId" uuid NOT NULL,
        "StorageKey" character varying(240) NOT NULL,
        "OriginalFileName" character varying(255) NOT NULL,
        "ContentType" character varying(120) NOT NULL,
        "Size" bigint NOT NULL,
        "Sha256" character varying(64) NOT NULL,
        "ScanState" character varying(32) NOT NULL,
        "ScanFailureCode" text,
        "ScannerSignature" text,
        "ScannedAtUtc" timestamp with time zone,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "UpdatedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_CND_Documents" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_CND_Documents_Size" CHECK ("Size" > 0 AND "Size" <= 20971520),
        CONSTRAINT "FK_CND_Documents_CND_Candidates_CandidateId" FOREIGN KEY ("CandidateId") REFERENCES "CND_Candidates" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260825090844_InitialInfrastructure') THEN
    CREATE INDEX "IX_AUD_Events_CorrelationId" ON "AUD_Events" ("CorrelationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260825090844_InitialInfrastructure') THEN
    CREATE INDEX "IX_CND_Candidates_LastName_FirstName" ON "CND_Candidates" ("LastName", "FirstName");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260825090844_InitialInfrastructure') THEN
    CREATE INDEX "IX_CND_Documents_CandidateId" ON "CND_Documents" ("CandidateId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260825090844_InitialInfrastructure') THEN
    CREATE UNIQUE INDEX "IX_CND_Documents_StorageKey" ON "CND_Documents" ("StorageKey");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260825090844_InitialInfrastructure') THEN
    CREATE UNIQUE INDEX "IX_OPS_Operations_IdempotencyKey" ON "OPS_Operations" ("IdempotencyKey");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260825090844_InitialInfrastructure') THEN
    CREATE INDEX "IX_OPS_Operations_Status_LeaseExpiresAtUtc" ON "OPS_Operations" ("Status", "LeaseExpiresAtUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260825090844_InitialInfrastructure') THEN
    DO $$
    BEGIN
        IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'ktl_runtime') THEN
            REVOKE CREATE ON SCHEMA public FROM ktl_runtime;
            GRANT USAGE ON SCHEMA public TO ktl_runtime;
            GRANT SELECT, INSERT, UPDATE, DELETE
                ON "CND_Candidates", "CND_Documents", "OPS_Operations", "AUD_Events"
                TO ktl_runtime;
            GRANT SELECT ON "__EFMigrationsHistory" TO ktl_runtime;
        END IF;
    END
    $$;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260825090844_InitialInfrastructure') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260825090844_InitialInfrastructure', '10.0.4');
    END IF;
END $EF$;
COMMIT;

