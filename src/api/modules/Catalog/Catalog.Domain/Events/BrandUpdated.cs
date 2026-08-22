using FSH.Framework.Core.Domain.Events;

namespace VietAIS.TCFlow.WebApi.Catalog.Domain.Events;
public sealed record BrandUpdated : DomainEvent
{
    public Brand? Brand { get; set; }
}
