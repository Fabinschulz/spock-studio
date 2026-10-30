using System.Text.Json.Nodes;

namespace Mariliz.Application.Protocol;

internal sealed class ParamException(string message) : Exception(message);
