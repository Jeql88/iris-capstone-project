using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IRIS.Core.Migrations
{
    /// <inheritdoc />
    public partial class FixAlertEnumColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1
                    FROM information_schema.columns
                    WHERE table_schema = 'public'
                      AND table_name = 'Alerts'
                      AND column_name = 'Severity'
                      AND data_type IN ('text', 'character varying')
                ) THEN
                    ALTER TABLE ""Alerts""
                    ALTER COLUMN ""Severity"" TYPE integer
                    USING (
                        CASE
                            WHEN ""Severity"" ~ '^[0-9]+$' THEN ""Severity""::integer
                            WHEN lower(""Severity"") = 'low' THEN 0
                            WHEN lower(""Severity"") = 'medium' THEN 1
                            WHEN lower(""Severity"") = 'high' THEN 2
                            WHEN lower(""Severity"") = 'critical' THEN 3
                            ELSE 1
                        END
                    );
                END IF;

                IF EXISTS (
                    SELECT 1
                    FROM information_schema.columns
                    WHERE table_schema = 'public'
                      AND table_name = 'Alerts'
                      AND column_name = 'Type'
                      AND data_type IN ('text', 'character varying')
                ) THEN
                    ALTER TABLE ""Alerts""
                    ALTER COLUMN ""Type"" TYPE integer
                    USING (
                        CASE
                            WHEN ""Type"" ~ '^[0-9]+$' THEN ""Type""::integer
                            WHEN lower(""Type"") = 'hardware' THEN 0
                            WHEN lower(""Type"") = 'network' THEN 1
                            WHEN lower(""Type"") = 'software' THEN 2
                            WHEN lower(""Type"") = 'security' THEN 3
                            WHEN lower(""Type"") = 'system' THEN 4
                            WHEN lower(""Type"") = 'thermal' THEN 0
                            ELSE 4
                        END
                    );
                END IF;
            END $$;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1
                    FROM information_schema.columns
                    WHERE table_schema = 'public'
                      AND table_name = 'Alerts'
                      AND column_name = 'Severity'
                      AND data_type = 'integer'
                ) THEN
                    ALTER TABLE ""Alerts""
                    ALTER COLUMN ""Severity"" TYPE text
                    USING (
                        CASE ""Severity""
                            WHEN 0 THEN 'Low'
                            WHEN 1 THEN 'Medium'
                            WHEN 2 THEN 'High'
                            WHEN 3 THEN 'Critical'
                            ELSE 'Medium'
                        END
                    );
                END IF;

                IF EXISTS (
                    SELECT 1
                    FROM information_schema.columns
                    WHERE table_schema = 'public'
                      AND table_name = 'Alerts'
                      AND column_name = 'Type'
                      AND data_type = 'integer'
                ) THEN
                    ALTER TABLE ""Alerts""
                    ALTER COLUMN ""Type"" TYPE text
                    USING (
                        CASE ""Type""
                            WHEN 0 THEN 'Hardware'
                            WHEN 1 THEN 'Network'
                            WHEN 2 THEN 'Software'
                            WHEN 3 THEN 'Security'
                            WHEN 4 THEN 'System'
                            ELSE 'System'
                        END
                    );
                END IF;
            END $$;");
        }
    }
}
