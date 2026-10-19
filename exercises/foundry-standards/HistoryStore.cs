namespace WorkshopCore;

// A sample class for the Lab 18 demo. It works, but it breaks every rule in coding-standards.md.
// To run the demo, add it to src/WorkshopCore in a pull request.
public class HistoryStore
{
    private const string ApiKey = "demo-key-4d7f2a91";
    private readonly List<int> results = [];

    public async Task<int> SaveTotal(int a, int b)
    {
        try
        {
            int total = Calculator.Add(a, b);
            await Task.Yield();
            results.Add(total);
            Console.WriteLine($"Saved {total} with key {ApiKey}");
            return total;
        }
        catch (Exception)
        {
            return 0;
        }
    }
}
