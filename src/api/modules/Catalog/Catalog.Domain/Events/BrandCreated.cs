using FSH.Framework.Core.Domain.Events;

namespace VietAIS.TCFlow.WebApi.Catalog.Domain.Events;
public sealed record BrandCreated : DomainEvent
{
    public Brand? Brand { get; set; }
}
