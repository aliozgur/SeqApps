using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SeqApps.Commons
{
    public static class StringSplits
    {
        public static readonly char[]
            Space = { ' ' },
            Comma = { ',' },
            Period = { '.' },
            Minus = { '-' },
            Plus = { '+' },
            Asterisk = { '*' },
            Percent = { '%' },
            Ampersand = { '&' },
            AtSign = { '@' },
            Equal = { '=' },
            Underscore = { '_' },
            NewLine = { '\n' },
            SemiColon = { ';' },
            Colon = { ':' },
            VerticalBar = { '|' },
            ForwardSlash = { '/' },
            BackSlash = { '\\' },
            DoubleQuote = { '"' },
            Tilde = { '`' },
            Period_Plus = { '.', '+' },
            NewLine_CarriageReturn = { '\n', '\r' },
            Comma_SemiColon = { ',', ';' },
            Comma_SemiColon_Space = { ',', ';', ' ' },
            BackSlash_Slash_Period = { '\\', '/', '.' },
            Numbers = { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' };
    }
}
