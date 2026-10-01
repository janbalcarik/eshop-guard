using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Tenancy;
using EshopGuard.Data.Tests.Isolation;
using EshopGuard.Tests.Shared;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EshopGuard.Data.Tests.Catalog;

[Collection(DbCollection.Name)]
[Trait("Category", "Db")]
public sealed class EnumCheckConstraintTests(PostgresTestDatabase database, TwoTenantsFixture tenants)
{
    [Fact]
    public async Task EveryEnumProperty_HasACheckWithTheSameValues()
    {
        using var db = TestDatabase.CreateDb("App", new TenantContext());
        var problems = new List<string>();
        var count = 0;
        foreach (var entityType in db.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                var enumType = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
                if (!enumType.IsEnum)
                {
                    continue;
                }

                count++;
                var table = entityType.GetTableName()!;
                var schema = entityType.GetSchema()!;
                var column = property.GetColumnName();
                var expected = Enum.GetNames(enumType).Select(SnakeCase.Of).Order(StringComparer.Ordinal).ToList();
                var definition = await database.For("Owner").ScalarAsync<string>($"""
                    SELECT pg_get_constraintdef(co.oid) FROM pg_constraint co
                    JOIN pg_class c ON c.oid = co.conrelid JOIN pg_namespace n ON n.oid = c.relnamespace
                    WHERE n.nspname = '{schema}' AND c.relname = '{table}' AND co.conname = 'ck_{table}_{column}'
                    """);
                if (definition is null)
                {
                    problems.Add($"{schema}.{table}.{column}: no CHECK");
                    continue;
                }

                var actual = System.Text.RegularExpressions.Regex.Matches(definition, "'([a-z_]+)'::text").Select(m => m.Groups[1].Value).Order(StringComparer.Ordinal).ToList();
                if (!actual.SequenceEqual(expected))
                {
                    problems.Add($"{schema}.{table}.{column}: {string.Join(",", actual)} ≠ {string.Join(",", expected)}");
                }
            }
        }

        Assert.Empty(problems);
        Assert.True(count >= 60, $"only {count} enum properties found");
    }

    [Fact]
    public async Task UnknownValue_IsRefusedByTheDatabase()
    {
        var state = await IsolationSql.InTenantAsync("App", tenants.A.Tenant.TenantId, (conn, tx) =>
            IsolationSql.SqlStateAsync(conn, tx, "UPDATE checks.runs SET status = 'done' WHERE id = $1", tenants.A.RunId));
        Assert.Equal(PostgresErrorCodes.CheckViolation, state);
    }

    [Theory]
    [InlineData("KeptWithEvidence", "kept_with_evidence")]
    [InlineData("AwaitingPayment", "awaiting_payment")]
    [InlineData("Ok", "ok")]
    [InlineData("ReadWrite", "read_write")]
    public void SnakeCase_ConvertsEnumNames(string name, string text) => Assert.Equal(text, SnakeCase.Of(name));

    [Fact]
    public void Converter_RoundTrips()
    {
        Assert.Equal("kept_with_evidence", SnakeCaseEnumConverter<Entities.Checks.FindingStatus>.ToText(Entities.Checks.FindingStatus.KeptWithEvidence));
        Assert.Equal(Entities.Checks.FindingStatus.KeptWithEvidence, SnakeCaseEnumConverter<Entities.Checks.FindingStatus>.FromText("kept_with_evidence"));
        Assert.Equal("status IN ('open', 'answered')", EnumCheckExtensions.CheckSql<Entities.Checks.QuestionStatus>("status"));
        Assert.Throws<InvalidOperationException>(() => SnakeCaseEnumConverter<Entities.Checks.QuestionStatus>.FromText("done"));
    }
}
