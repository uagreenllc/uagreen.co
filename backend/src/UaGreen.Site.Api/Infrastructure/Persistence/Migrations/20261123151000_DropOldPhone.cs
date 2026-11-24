using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UaGreen.Site.Api.Infrastructure.Persistence.Migrations
{
    /// The company has one number, +380 66 869 60 72 (commit 297b2e2 fixed migration/*.json).
    /// Databases imported before that, plus the seeded legal pages, still carry +380 95 717 37 72:
    /// swap it for 066 in page HTML and drop it from the phones setting. Data only, idempotent.
    [DbContext(typeof(AppDbContext))]
    [Migration("20261123151000_DropOldPhone")]
    public partial class DropOldPhone : Migration
    {
        protected override void Up(MigrationBuilder mb)
        {
            mb.Sql("""
                UPDATE content
                SET body_html = replace(replace(body_html, '+380957173772', '+380668696072'), '+38 (095) 717 37 72', '+38 (066) 869 60 72'),
                    updated_at = now()
                WHERE body_html LIKE '%+380957173772%' OR body_html LIKE '%+38 (095) 717 37 72%';

                UPDATE settings
                SET value = coalesce((
                    SELECT string_agg(btrim(p), ',' ORDER BY i)
                    FROM unnest(string_to_array(value, ',')) WITH ORDINALITY AS t(p, i)
                    WHERE regexp_replace(p, '\D', '', 'g') !~ '957173772$'), '')
                WHERE key = 'phones';
                """);
        }

        protected override void Down(MigrationBuilder mb) { }
    }
}
