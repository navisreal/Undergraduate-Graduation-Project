using System.Collections.Generic;
using System.Text;

/// <summary>
/// Static Morse code decoder.
/// Supports A-Z and 0-9 (36 characters). Invalid sequences are decoded as '?'.
/// Input format: characters separated by single space, words by three spaces.
///   Example: "... --- ...   -.-"  →  "SOS K"
/// </summary>
public static class MorseDecoder
{
    private static readonly Dictionary<string, char> morseToChar = new Dictionary<string, char>()
    {
        // Letters A-Z
        {".-",    'A'}, {"-...",  'B'}, {"-.-.",  'C'}, {"-..",   'D'},
        {".",     'E'}, {"..-.",  'F'}, {"--.",   'G'}, {"....",  'H'},
        {"..",    'I'}, {".---",  'J'}, {"-.-",   'K'}, {".-..",  'L'},
        {"--",    'M'}, {"-.",    'N'}, {"---",   'O'}, {".--.",  'P'},
        {"--.-",  'Q'}, {".-.",   'R'}, {"...",   'S'}, {"-",     'T'},
        {"..-",   'U'}, {"...-",  'V'}, {".--",   'W'}, {"-..-",  'X'},
        {"-.--",  'Y'}, {"--..",  'Z'},
        // Digits 0-9
        {"-----", '0'}, {".----", '1'}, {"..---", '2'}, {"...--", '3'},
        {"....-", '4'}, {".....", '5'}, {"-....", '6'}, {"--...", '7'},
        {"---..", '8'}, {"----.", '9'},
    };

    /// <summary>
    /// Decode a morse sequence string into plain text.
    /// Characters are separated by single space, words by three spaces.
    /// Unknown sequences become '?'.
    /// </summary>
    public static string Decode(string morseSequence)
    {
        if (string.IsNullOrEmpty(morseSequence))
            return "";

        StringBuilder result = new StringBuilder();

        // Split by three spaces first to get words
        string[] words = morseSequence.Split(new string[] { "   " }, System.StringSplitOptions.None);

        for (int w = 0; w < words.Length; w++)
        {
            if (w > 0) result.Append(' '); // single space between decoded words

            // Split each word by single space to get characters
            string[] codes = words[w].Split(new char[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);

            foreach (string code in codes)
            {
                if (morseToChar.TryGetValue(code, out char c))
                    result.Append(c);
                else
                    result.Append('?');
            }
        }

        return result.ToString();
    }
}