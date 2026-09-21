namespace RolloutExtractor;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            return ClothingPuller.Run(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("EXTRACTOR_CRASH");
            Console.Error.WriteLine(ex);
            return 3;
        }
    }
}
