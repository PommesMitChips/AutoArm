using Microsoft.CodeAnalysis.CSharp;

// Generated names must survive Framework's older Unicode tables, not only the
// SDK runtime's tables. In particular Armenian U+0560/U+0588 are not portable.
internal static class PortableIdentifiers
{
    internal static bool IsLetter(char c) => char.IsLetter(c) && SyntaxFacts.IsIdentifierStartCharacter(c) &&
        (c <= 0x7F || c >= 0x0100 && c <= 0x02AF ||
         c >= 0x0391 && c <= 0x03C9 || c >= 0x0400 && c <= 0x052F ||
         c >= 0x0531 && c <= 0x0556 || c == 0x0559 || c >= 0x0561 && c <= 0x0587 ||
         c >= 0x05D0 && c <= 0x05EA || c >= 0x05F0 && c <= 0x05F2 ||
         c >= 0x0620 && c <= 0x06FF || c >= 0x1E00 && c <= 0x1EFF ||
         c >= 0x4E00 && c <= 0x9FA5);

    internal static void Check()
    {
        if (IsLetter('\u0560') || IsLetter('\u0588') || IsLetter('\u05EF') ||
            !IsLetter('\u0561') || !IsLetter('\u0587') || !IsLetter('\u1E00'))
            throw new Exception("Framework identifier pool regression.");
    }
}
