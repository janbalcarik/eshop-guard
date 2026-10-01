using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EshopGuard.Data.Configurations.Conventions;

/// <summary>Enum columns as <c>text</c> with a <c>CHECK</c> generated from the enum, so the two cannot drift apart.</summary>
public static class EnumCheckExtensions
{
    /// <summary>Maps an enum property as snake_case text with <c>CHECK (column IN (…))</c> named <c>ck_&lt;table&gt;_&lt;column&gt;</c>.</summary>
    public static PropertyBuilder<TEnum> HasEnum<TEntity, TEnum>(this EntityTypeBuilder<TEntity> builder, Expression<Func<TEntity, TEnum>> property)
        where TEntity : class
        where TEnum : struct, Enum
    {
        var propertyBuilder = builder.Property(property).HasConversion(new SnakeCaseEnumConverter<TEnum>());
        AddCheck<TEntity, TEnum>(builder, propertyBuilder.Metadata.Name);
        return propertyBuilder;
    }

    /// <summary>Nullable variant of <see cref="HasEnum{TEntity,TEnum}(EntityTypeBuilder{TEntity},Expression{Func{TEntity,TEnum}})"/>; <c>NULL</c> passes the check.</summary>
    public static PropertyBuilder<TEnum?> HasEnum<TEntity, TEnum>(this EntityTypeBuilder<TEntity> builder, Expression<Func<TEntity, TEnum?>> property)
        where TEntity : class
        where TEnum : struct, Enum
    {
        var propertyBuilder = builder.Property(property).HasConversion(new SnakeCaseEnumConverter<TEnum>());
        AddCheck<TEntity, TEnum>(builder, propertyBuilder.Metadata.Name);
        return propertyBuilder;
    }

    /// <summary>Text of the check: <c>column IN ('a', 'b')</c>.</summary>
    public static string CheckSql<TEnum>(string column)
        where TEnum : struct, Enum =>
        $"{column} IN ({string.Join(", ", SnakeCaseEnumConverter<TEnum>.AllTexts.Select(t => $"'{t}'"))})";

    private static void AddCheck<TEntity, TEnum>(EntityTypeBuilder<TEntity> builder, string propertyName)
        where TEntity : class
        where TEnum : struct, Enum
    {
        var column = SnakeCase.Of(propertyName);
        var table = builder.Metadata.GetTableName() ?? throw new InvalidOperationException("Call ToTable before HasEnum.");
        builder.ToTable(t => t.HasCheckConstraint($"ck_{table}_{column}", CheckSql<TEnum>(column)));
    }
}
