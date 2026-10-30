using System.Text.RegularExpressions;

namespace Mariliz.Core.Policy;

public sealed record SecretFinding(string Rule, int Line);
