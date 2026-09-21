using Microsoft.EntityFrameworkCore;

namespace BiasAudit.Api.Data;

/// <summary>
/// Keeps installations that historically used EnsureCreated compatible when the
/// consent feature is added to an already populated database. Each statement is
/// idempotent and creates only the two new tables and their indexes.
/// </summary>
public static class PhotoSchemaInitializer
{
    public static async Task EnsureCreatedAsync(AuditDbContext db, CancellationToken cancellationToken = default)
    {
        // E-mail used to be mandatory although authentication has always used a
        // username and password. Existing installations need this one-time,
        // idempotent compatibility update.
        await db.Database.ExecuteSqlRawAsync(
            "ALTER TABLE \"Users\" ALTER COLUMN \"Email\" DROP NOT NULL;",
            cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS photo_posts (
                "Id" uuid NOT NULL,
                "OwnerId" uuid NOT NULL,
                "ObjectKey" character varying(512) NOT NULL,
                "OriginalFileName" character varying(256) NOT NULL,
                "ContentType" character varying(128) NOT NULL,
                "SizeBytes" bigint NOT NULL,
                "PublicationStatus" character varying(32) NOT NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                "PublishedAt" timestamp with time zone NULL,
                CONSTRAINT "PK_photo_posts" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_photo_posts_Users_OwnerId"
                    FOREIGN KEY ("OwnerId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
            );
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS photo_tag_consents (
                "Id" uuid NOT NULL,
                "PhotoPostId" uuid NOT NULL,
                "TaggedUserId" uuid NOT NULL,
                "Status" character varying(32) NOT NULL,
                "RespondedAt" timestamp with time zone NULL,
                CONSTRAINT "PK_photo_tag_consents" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_photo_tag_consents_photo_posts_PhotoPostId"
                    FOREIGN KEY ("PhotoPostId") REFERENCES photo_posts ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_photo_tag_consents_Users_TaggedUserId"
                    FOREIGN KEY ("TaggedUserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
            );
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_photo_posts_PublicationStatus\" ON photo_posts (\"PublicationStatus\");",
            cancellationToken);
        await db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_photo_posts_CreatedAt\" ON photo_posts (\"CreatedAt\");",
            cancellationToken);
        await db.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_photo_tag_consents_PhotoPostId_TaggedUserId\" ON photo_tag_consents (\"PhotoPostId\", \"TaggedUserId\");",
            cancellationToken);
        await db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_photo_tag_consents_TaggedUserId_Status\" ON photo_tag_consents (\"TaggedUserId\", \"Status\");",
            cancellationToken);
    }
}
