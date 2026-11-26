namespace EnglishPlatform.BuildingBlocks.Infrastructure.Modules;

/// <summary>
/// Подготовка модуля после миграций: справочники, роли, первый администратор.
/// Должна быть идемпотентной, потому что выполняется при каждом старте каждого экземпляра.
/// </summary>
public interface IModuleInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken);
}
