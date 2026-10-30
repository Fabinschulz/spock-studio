using System.Reflection;
using Mariliz.Core.Plan;

namespace Mariliz.Application.Harness;

public sealed record CatalogComponent(string Id, ComponentKind Kind, string Version, string Title);
