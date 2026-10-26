using Mariliz.Application;

if (args is ["version"])
{
    Console.WriteLine(BuildInfo.Version);
    return 0;
}

Console.Error.WriteLine("Uso: mariliz version");
return 2;
