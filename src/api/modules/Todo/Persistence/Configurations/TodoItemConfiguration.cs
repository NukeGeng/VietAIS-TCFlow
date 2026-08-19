using Finbuckle.MultiTenant;
using VietAIS.TCFlow.WebApi.Todo.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace VietAIS.TCFlow.WebApi.Todo.Persistence.Configurations;
internal sealed class TodoItemConfiguration : IEntityTypeConfiguration<TodoItem>
{
    public void Configure(EntityTypeBuilder<TodoItem> builder)
    {
        builder.IsMultiTenant();
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(100);
        builder.Property(x => x.Note).HasMaxLength(1000);
    }
}
