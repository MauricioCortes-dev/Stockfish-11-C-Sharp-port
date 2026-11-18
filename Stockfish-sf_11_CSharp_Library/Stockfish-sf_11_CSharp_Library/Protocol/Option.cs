using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Stockfish_sf_11_CSharp_Library.Protocol
{
    /// <summary>UCI option value, validation and change callback from Stockfish 11.</summary>
    public sealed class Option
    {
        private readonly Action<Option>? onChange;
        private readonly int minimum, maximum;
        private readonly string type;
        // std::stof accepts a numeric prefix and ignores a nonnumeric suffix.
        private static readonly Regex NumericPrefix = new Regex(@"^[ \t\r\n\f\v]*[+-]?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)(?:[eE][+-]?[0-9]+)?",RegexOptions.CultureInvariant);
        private static readonly Regex HexPrefix = new Regex(@"^[ \t\r\n\f\v]*([+-]?)0[xX]([0-9a-fA-F]+(?:\.[0-9a-fA-F]*)?|\.[0-9a-fA-F]+)(?:[pP]([+-]?[0-9]+))?",RegexOptions.CultureInvariant);
        public string DefaultValue { get; }
        public string CurrentValue { get; private set; }

        public Option(Action<Option>? onChange = null)
        { type="button";DefaultValue=CurrentValue="";this.onChange=onChange; }
        public Option(bool value, Action<Option>? onChange = null)
        { type="check";DefaultValue=CurrentValue=value?"true":"false";this.onChange=onChange; }
        public Option(string value, Action<Option>? onChange = null)
        { type="string";DefaultValue=CurrentValue=value ?? throw new ArgumentNullException(nameof(value));this.onChange=onChange; }
        public Option(double value, int minimum, int maximum, Action<Option>? onChange = null)
        {
            if(minimum>maximum)throw new ArgumentOutOfRangeException(nameof(minimum));
            type="spin";this.minimum=minimum;this.maximum=maximum;
            DefaultValue=CurrentValue=value.ToString("F6",CultureInfo.InvariantCulture);this.onChange=onChange;
        }
        public Option(string values, string current, Action<Option>? onChange = null)
        { type="combo";DefaultValue=values ?? throw new ArgumentNullException(nameof(values));CurrentValue=current ?? throw new ArgumentNullException(nameof(current));this.onChange=onChange; }

        /// <summary>Applies a value if permitted; buttons invoke their callback without changing a value.</summary>
        public bool Assign(string value)
        {
            if(value==null)throw new ArgumentNullException(nameof(value));
            if(type!="button" && value.Length==0)return false;
            if(type=="check" && value!="true" && value!="false")return false;
            if(type=="spin" && (!TryNumericValue(value,out float n)
                || float.IsNaN(n) || float.IsInfinity(n) || n<minimum || n>maximum))return false;
            if(type=="combo")
            {
                bool found=false;
                foreach(string token in DefaultValue.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries))
                    if(string.Equals(token,value,StringComparison.OrdinalIgnoreCase)){found=true;break;}
                if(!found || value=="var")return false;
            }
            if(type!="button")CurrentValue=value;
            onChange?.Invoke(this);return true;
        }
        public static implicit operator double(Option option)
        {
            if(ReferenceEquals(option,null))throw new ArgumentNullException(nameof(option));
            if(option.type=="check")return option.CurrentValue=="true"?1:0;
            if(option.type=="spin" && TryNumericValue(option.CurrentValue,out float value))return value;
            throw new InvalidOperationException("Option is not numeric.");
        }
        private static bool TryNumericValue(string text,out float value)
        {
            Match hex=HexPrefix.Match(text);
            if(hex.Success) { value=ParseHexFloat(hex); return true; }
            Match match=NumericPrefix.Match(text);
            return float.TryParse(match.Value,NumberStyles.Float,CultureInfo.InvariantCulture,out value);
        }
        // Round hexadecimal input directly to binary32, including subnormals.
        // Keeping guard/sticky bits avoids double rounding through a double.
        private static float ParseHexFloat(Match match)
        {
            string digits=match.Groups[2].Value;
            int dot=digits.IndexOf('.'), fraction=dot<0?0:digits.Length-dot-1;
            digits=digits.Replace(".","");
            int first=0;while(first<digits.Length && digits[first]=='0')++first;
            int sign=match.Groups[1].Value=="-"?int.MinValue:0;
            if(first==digits.Length)return BitConverter.Int32BitsToSingle(sign);
            string exponentText=match.Groups[3].Value;long exponent=0;int index=0;bool negative=false;
            if(exponentText.Length!=0 && (exponentText[0]=='-' || exponentText[0]=='+')) { negative=exponentText[0]=='-';index=1; }
            for(;index<exponentText.Length;++index)exponent=Math.Min(1L<<50,exponent*10+exponentText[index]-'0');
            if(negative)exponent=-exponent;
            int leading=HexDigit(digits[first]), highest=leading>=8?3:leading>=4?2:leading>=2?1:0;
            long power=(long)exponent-4L*fraction+4L*(digits.Length-first-1)+highest;
            if(power>127)return BitConverter.Int32BitsToSingle(sign|0x7f800000);
            if(power< -150)return BitConverter.Int32BitsToSingle(sign);
            int keep=power>=-126?24:(int)power+150, consumed=0;uint mantissa=0;bool guard=false,sticky=false;
            for(int i=first;i<digits.Length;++i)
                for(int bit=i==first?highest:3;bit>=0;--bit)
                {
                    bool set=(HexDigit(digits[i])&(1<<bit))!=0;
                    if(consumed<keep)mantissa=(mantissa<<1)|(set?1U:0U);
                    else if(consumed==keep)guard=set;
                    else sticky|=set;
                    ++consumed;
                }
            if(consumed<keep)mantissa<<=keep-consumed;
            if(guard && (sticky || (mantissa&1)!=0))++mantissa;
            if(power< -126)return BitConverter.Int32BitsToSingle(sign|(int)mantissa);
            if(mantissa==(1U<<24)) { mantissa>>=1;++power; }
            return BitConverter.Int32BitsToSingle(sign|((int)(power+127)<<23)|((int)mantissa&0x7fffff));
        }
        private static int HexDigit(char c) => c<='9'?c-'0':(c|32)-'a'+10;
        public static implicit operator string(Option option)
        {
            if(ReferenceEquals(option,null))throw new ArgumentNullException(nameof(option));
            if(option.type!="string")throw new InvalidOperationException("Option is not a string.");
            return option.CurrentValue;
        }
        public static bool operator ==(Option? option,string? value)
        {
            if(ReferenceEquals(option,null))return value is null;
            if(option.type!="combo")throw new InvalidOperationException("Option is not a combo.");
            return string.Equals(option.CurrentValue,value,StringComparison.OrdinalIgnoreCase);
        }
        public static bool operator !=(Option? option,string? value) => !(option==value);
        public static bool operator ==(string? value,Option? option) => option==value;
        public static bool operator !=(string? value,Option? option) => !(option==value);
        public override bool Equals(object? value) => ReferenceEquals(this,value);
        public override int GetHashCode() => base.GetHashCode();
    }
}
