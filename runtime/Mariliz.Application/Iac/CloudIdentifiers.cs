using System.Text.RegularExpressions;

namespace Mariliz.Application.Iac;

// Os formatos de identificador de cada nuvem, usados ao conferir o blueprint e ao montar o bootstrap: o que um aceita o
// outro também aceita. Quando os dois divergiam, vale o mais estrito (o que a própria nuvem recusa não passa).
internal static partial class CloudIdentifiers
{
    [GeneratedRegex("^[a-z0-9][a-z0-9.-]{1,61}[a-z0-9]$")]
    public static partial Regex S3Bucket();

    [GeneratedRegex("^[a-z]{2}(-gov)?-[a-z]+-[0-9]$")]
    public static partial Regex AwsRegion();

    [GeneratedRegex("^[0-9]{12}$")]
    public static partial Regex AwsAccount();

    [GeneratedRegex("^[A-Za-z0-9_.-]{1,64}$")]
    public static partial Regex AwsProfile();

    [GeneratedRegex("^[a-z][a-z0-9]{2,39}$")]
    public static partial Regex AzureLocation();

    [GeneratedRegex("^[a-z0-9]{3,24}$")]
    public static partial Regex AzureStorage();

    [GeneratedRegex("^[a-z0-9](?!.*--)[a-z0-9-]{1,61}[a-z0-9]$")]
    public static partial Regex AzureContainer();

    [GeneratedRegex("^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$")]
    public static partial Regex Guid();

    [GeneratedRegex("^[a-z0-9][a-z0-9._-]{1,61}[a-z0-9]$")]
    public static partial Regex GcsBucket();

    [GeneratedRegex("^[a-z][a-z0-9-]{4,28}[a-z0-9]$")]
    public static partial Regex GoogleProject();
}
