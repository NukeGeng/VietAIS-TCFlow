using FSH.Framework.Core.Domain.Events;

namespace VietAIS.TCFlow.WebApi.Catalog.Domain.Events;
public sealed record ProductUpdated : DomainEvent
{
    public Product? Product { get; set; }
}
