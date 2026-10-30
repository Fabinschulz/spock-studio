using Mariliz.Core.Plan;

namespace Mariliz.Application.Execution;

internal sealed record PlannedWrite(FileChange Change, string Op);
