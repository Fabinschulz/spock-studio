using System.Security.Cryptography;
using System.Text;

namespace Mariliz.Application.Workspace;

public sealed record AuthorizedProject(string ProjectId, string DisplayName, string DisplayPath);
