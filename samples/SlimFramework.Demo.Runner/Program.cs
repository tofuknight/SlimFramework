using SlimFramework.Demo;

try
{
    DemoSession.Run();
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    return 1;
}
