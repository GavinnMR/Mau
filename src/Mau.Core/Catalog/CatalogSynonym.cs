using Mau.Core.Entities;

namespace Mau.Core.Catalog;

/// <summary>A synonym as exposed by the read model.</summary>
public sealed record CatalogSynonym(string Name, SynonymLanguage Language);
