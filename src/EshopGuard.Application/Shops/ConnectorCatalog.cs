using EshopGuard.Data.Entities.Shops;

namespace EshopGuard.Application.Shops;

/// <summary>Whether a connector exists for a platform (change 15 registers its catalog).</summary>
public interface IConnectorCatalog
{
    bool IsAvailable(ShopPlatform platform);
}

/// <summary>Until change 15: no connector, the recommended way is the web.</summary>
public sealed class NoConnectorCatalog : IConnectorCatalog
{
    public bool IsAvailable(ShopPlatform platform) => false;
}
