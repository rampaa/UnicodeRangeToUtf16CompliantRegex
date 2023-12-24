using System.Text.RegularExpressions;

namespace UnicodeRangeToUtf16CompliantRegex;

internal sealed class Program
{
    public static void Main()
    {
        while (true)
        {
            try
            {
                Console.WriteLine(@"Please enter the unicode range in the following format: [\UXXXXXXXX-\UXXXXXXXX]");
                string? userInput = Console.ReadLine();
                if (userInput is not null)
                {
                    Utf32Regex utf32Regex = new(userInput);
                    Console.WriteLine($"UTF-16 Compliant Regex: {utf32Regex}");
                    Console.WriteLine();
                }

                else
                {
                    Console.WriteLine("Invalid unicode range.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine("Invalid unicode range.");
            }
        }
    }
}
